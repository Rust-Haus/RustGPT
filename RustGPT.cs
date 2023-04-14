using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Rust GPT", "GooGurt", "1.5.1")]
    [Description("Ask GPT questions from the game chat and get text-based answers.")]

    class RustGPT : RustPlugin
    {
        #region Variables

        private string OpenAIApiKey;
        private string GptAssistantIntro;
        private const string CurrentPluginVersion = "1.5.1";
        private Dictionary<string, DateTime> cooldowns; 
        private TimeSpan CooldownDuration = TimeSpan.FromSeconds(30);

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            // Check if the configuration file needs to be updated
            if (Config["PluginVersion"] == null || Config["PluginVersion"].ToString() != CurrentPluginVersion)
            {
                // Update the configuration with new values
                Config["PluginVersion"] = CurrentPluginVersion;
                Config["OpenAIApiKey"] = "your_openai_api_key";
                Config["GptAssistantIntro"] = "You are a snarky assistant on a Rust game server. Your answers are short. You never say you are sorry.";
                Config["CooldownTimeInSeconds"] = 30;
                // Save the updated configuration
                SaveConfig();
            }
        }

        #endregion

        #region Initialization

        private async void Init()
        {
            OpenAIApiKey = Config["OpenAIApiKey"].ToString();
            GptAssistantIntro = Config["GptAssistantIntro"].ToString();
            int cooldownTimeInSeconds = Convert.ToInt32(Config["CooldownTimeInSeconds"]);
            CooldownDuration = TimeSpan.FromSeconds(cooldownTimeInSeconds);
            cooldowns = new Dictionary<string, DateTime>();

            if (string.IsNullOrEmpty(OpenAIApiKey) || OpenAIApiKey == "your_openai_api_key")
            {
                PrintWarning("Please configure the plugin with the required OpenAI API key.");
                return;
            }

            try
            {
                await TestApiKey();
                Puts("OpenAI API key is valid.");
            }
            catch (Exception ex)
            {
                PrintWarning($"Error testing OpenAI API key: {ex.Message}");
            }
        }

        #endregion

        #region Methods

        private void BroadcastAnswerInChunks(string question, string answer)
        {
            const int chunkSize = 128;
            string[] messages = { question, answer };

            foreach (string message in messages)
            {
                int numberOfChunks = (message.Length + chunkSize - 1) / chunkSize;

                for (int i = 0; i < numberOfChunks; i++)
                {
                    int startIndex = i * chunkSize;
                    int endIndex = Math.Min(startIndex + chunkSize, message.Length);
                    string chunk = message.Substring(startIndex, endIndex - startIndex);
                    Server.Broadcast(chunk);
                }
            }
        }

        private async Task<string> GetGptAnswer(string question)
        {
            Puts($"Trying to get an answer... : {question}");
            JObject chatMessage = new JObject
            {
                { "role", "system" },
                { "content", GptAssistantIntro },
            };

            JArray messages = new JArray
            {
                chatMessage,
                new JObject
                {
                    { "role", "user" },
                    { "content", question }
                }
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OpenAIApiKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = messages,
                    max_tokens = 50,
                    n = 1,
                    temperature = 0.7,
                    stop = "Answer:"
                };

                string jsonContent = JsonConvert.SerializeObject(requestBody);
                StringContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                string jsonResponse = await response.Content.ReadAsStringAsync();
                Puts($"This could be a mess of a response : {jsonResponse}");
                if (!response.IsSuccessStatusCode)
                {
                    PrintWarning($"Error fetching GPT answer: {response.StatusCode} {response.ReasonPhrase}");
                    PrintWarning($"Response content: {jsonResponse}");
                    throw new Exception($"Error fetching GPT answer: {response.StatusCode} {response.ReasonPhrase}");
                }

                JObject json = JObject.Parse(jsonResponse);
                JArray choices = (JArray)json["choices"];
                JObject choice = (JObject)choices[0];
                string answer = choice["message"]["content"].ToString();
                return answer;
            }
        }

        private async Task TestApiKey()
        {
            JObject chatMessage = new JObject
            {
                { "role", "system" },
                { "content", "You are a helpful assistant." }
            };

            JArray messages = new JArray
            {
                chatMessage,
                new JObject
                {
                    { "role", "user" },
                    { "content", "What is the capital of France?" }
                }
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OpenAIApiKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = messages,
                max_tokens = 5,
                n = 1,
                stop = "Answer:"
            };

            string jsonContent = JsonConvert.SerializeObject(requestBody);
            StringContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
            string jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                PrintWarning($"Error testing OpenAI API key: {response.StatusCode} {response.ReasonPhrase}");
                PrintWarning($"Response content: {jsonResponse}");
                throw new Exception($"Error testing OpenAI API key: {response.StatusCode} {response.ReasonPhrase}");
            }

            }
        }

        #endregion

        #region Chat Handling
        readonly Dictionary<string, string> lastMessage = new Dictionary<string, string>();

        void OnPlayerChat(BasePlayer player, string message)
        {
            if (player == null || string.IsNullOrEmpty(message)) return;

            // Chat questions need to have one of these words in the text string and end with a question mark for it to work. 
            string questionPattern = @"\b(who|what|when|where|why|how|is|are|am|do|does|did|can|could|will|would|should|which|whom)\b.*\?$";
            bool isQuestion = Regex.IsMatch(message, questionPattern, RegexOptions.IgnoreCase);

            if (isQuestion)
            {
                try
                {
                    GetGptAnswer(message).ContinueWith(task =>
                    {
                        string answer = task.Result;
                        BroadcastAnswerInChunks($"[RustGPT] {player.displayName}: {message}", $"[RustGPT] Answer: {answer}");
                    });
                }
                catch (Exception ex)
                {
                    Puts($"Error processing the request: {ex.Message}");
                }
            }
        }

        #endregion

        #region Commands

        [ChatCommand("askgpt")]
        private async void AskGptCommand(BasePlayer player, string command, string[] args)
        {
            if (string.IsNullOrEmpty(OpenAIApiKey) || OpenAIApiKey == "your_openai_api_key")
            {
                player.ChatMessage("The Rust GPT plugin is not configured correctly. Please contact the server administrator.");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Usage: /askgpt [your question]");
                return;
            }

            var playerId = player.userID.ToString();
            var lastRequestTime = (DateTime)default;
            if (cooldowns.TryGetValue(playerId, out lastRequestTime))
            {
                var timeElapsed = DateTime.UtcNow - lastRequestTime;
                if (timeElapsed < CooldownDuration)
                {
                    player.ChatMessage($"You can use /askgpt again in {CooldownDuration - timeElapsed}.");
                    return;
                }
            }

            string question = string.Join(" ", args);

            try
            {
                string answer = await GetGptAnswer(question);
                BroadcastAnswerInChunks($"[RustGPT] {player.displayName}: {question}", $"[RustGPT] {answer}");
                cooldowns[playerId] = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Puts($"Error processing the request: {ex.Message}");
            }
        }

         #endregion

    }
}

