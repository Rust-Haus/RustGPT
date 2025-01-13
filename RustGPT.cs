using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Rust;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using System.ComponentModel;
using System.Runtime.Remoting.Channels;
using System.Linq;
using System.Security.Policy;
using System.Text;

#pragma warning disable SYSLIB0014

namespace Oxide.Plugins
{
    [Info("RustGPT", "Goo_", "1.7.7")]
    [Description("Players can use OpenAI's ChatGPT from the game chat, incudes ChatGPT color commentary for deaths.")]
    public class RustGPT : RustPlugin
    {
        #region Declarations

        private const string PluginVersion = "1.7.7";
        private readonly Version _version = new Version(PluginVersion);

        private string ApiKey => _config.OpenAI_Api_Key.ApiKey;
        private string ApiUrl => _config.OutboundAPIUrl.ApiUrl;
        private Regex _questionRegex { get; set; }
        private PluginConfig _config { get; set; }

        private Dictionary<string, float> _lastUsageTime = new Dictionary<string, float>();
        private Dictionary<string, Uri> _uriCache = new Dictionary<string, Uri>();

        #endregion

        #region Overrides

        private void Init()
        {
            // Register permissions
            permission.RegisterPermission("RustGPT.use", this);
            permission.RegisterPermission("RustGPT.admin", this);

            // Test OpenAI configuration
            TestOpenAIConfiguration();

            ShowPluginStatusToAdmins();
            cmd.AddChatCommand("models", this, nameof(ModelsCommand));
        }

        private void TestOpenAIConfiguration()
        {
            if (string.IsNullOrEmpty(ApiKey) || ApiKey == "your-api-key-here")
            {
                PrintError("API key is not configured. Please set a valid API key in the config file.");
                NotifyAdminsOfApiKeyRequirement();
                return;
            }

            // Test API connection with current model
            ListAvailableModels(ApiKey, modelIds =>
            {
                bool modelExists = modelIds.Contains(_config.AIResponseParameters.Model);
                if (!modelExists)
                {
                    PrintWarning($"Current model '{_config.AIResponseParameters.Model}' is not available.");
                    GetSuggestedModel(suggestedModel =>
                    {
                        if (!string.IsNullOrEmpty(suggestedModel))
                        {
                            string oldModel = _config.AIResponseParameters.Model;
                            _config.AIResponseParameters.Model = suggestedModel;
                            Config.WriteObject(_config, true);
                            Puts($"Updated to suggested model: {suggestedModel}");
                            NotifyAdminsOfModelUpdate(oldModel, suggestedModel);
                        }
                        else
                        {
                            PrintError("Failed to get suggested model. Please check your configuration.");
                        }
                    });
                }
                else
                {
                    Puts($"OpenAI configuration test successful. Using model: {_config.AIResponseParameters.Model}");
                }
            });
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file.");
            _config = new PluginConfig 
            { 
                PluginVersion = PluginVersion,
                ChatSettings = new ChatMessageConfig()
            };
            Config.WriteObject(_config, true);
        }


        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                
                // Initialize ChatSettings if it's null (for older configs)
                if (_config.ChatSettings == null)
                {
                    _config.ChatSettings = new ChatMessageConfig();
                }
            }
            catch (Exception ex)
            {
                Puts($"Error deserializing config: {ex.Message}");
                LoadDefaultConfig();
                return;
            }

            if (_config == null || _config.OpenAI_Api_Key.ApiKey == "your-api-key-here")
            {
                NotifyAdminsOfApiKeyRequirement();
            }

            // Migrate config if version mismatch
            if (_config.PluginVersion != PluginVersion)
            {
                MigrateConfig(_config);
            }

            // Compile question regex
            _questionRegex = new Regex(_config.QuestionPattern, RegexOptions.IgnoreCase);
        }

        #endregion

        #region User_Chat

        private void OnPlayerChat(BasePlayer player, string user_chat_question)
        {
            // Check if user message matches our configured pattern and if they have permission
            if (_questionRegex.IsMatch(user_chat_question) && permission.UserHasPermission(player.UserIDString, "RustGPT.use"))
            {
                if (!HasCooldownElapsed(player))
                {
                    return;
                }

                // Validate API key
                if (string.IsNullOrEmpty(ApiKey) || ApiKey == "your-api-key-here" || !ApiKey.StartsWith("sk-"))
                {
                    player.ChatMessage("The API key is not properly configured. Please contact an administrator.");
                    return;
                }

                // Clean up user question
                string cleaned_chat_question = !string.IsNullOrEmpty(_config.QuestionPattern)
                    ? user_chat_question.Replace(_config.QuestionPattern, "").Trim()
                    : user_chat_question;

                // Combine system role + user server details
                string system_prompt = $"{_config.AIPromptParameters.SystemRole}\n{_config.AIPromptParameters.UserServerDetails}";



                try
                {
                    // Send to OpenAI
                    RustGPTHook(ApiKey,
                        new
                        {
                            model = _config.AIResponseParameters.Model,
                            messages = new[]
                            {
                                new { role = "developer", content = new[] { new { type = "text", text = system_prompt } } },
                                new { role = "user", content = new[] { new { type = "text", text = cleaned_chat_question } } }
                            },
                            temperature = _config.AIResponseParameters.Temperature,
                            max_tokens = _config.AIResponseParameters.MaxTokens,
                            presence_penalty = _config.AIResponseParameters.PresencePenalty,
                            frequency_penalty = _config.AIResponseParameters.FrequencyPenalty
                        },
                        ApiUrl,
                        response =>
                        {
                            try
                            {
                                if (response == null)
                                {
                                    player.ChatMessage("Received no response from AI. Please try again.");
                                    PrintError("Received null response from API");
                                    return;
                                }

                                if (response["error"] != null)
                                {
                                    player.ChatMessage("An error occurred. Please try again later.");
                                    PrintError($"OpenAI API Error: {response["error"]["message"]}");
                                    return;
                                }

                                if (response["choices"] == null || !response["choices"].HasValues)
                                {
                                    player.ChatMessage("Received invalid response. Please try again.");
                                    PrintError("No choices in response");
                                    return;
                                }

                                string customPrefix = $"<color={_config.ResponsePrefixColor}>{_config.ResponsePrefix}</color>";
                                string GPT_Chat_Reply = response["choices"][0]["message"]["content"].ToString().Trim();
                                string formattedReply = $"<color={_config.ChatSettings.ChatMessageColor}><size={_config.ChatSettings.ChatMessageFontSize}>{GPT_Chat_Reply}</size></color>";
                                string toChat = $"{customPrefix} {formattedReply}";

                                if (_config.OptionalPlugins.UseDiscordWebhookChat)
                                {
                                    var discordPayload = $"**{player}** \n> {cleaned_chat_question}.\n**{_config.ResponsePrefix}** \n> {GPT_Chat_Reply}";
                                    SendDiscordMessage(discordPayload);
                                }

                                if (_config.BroadcastResponse)
                                {
                                    Server.Broadcast(toChat);
                                }
                                else
                                {
                                    SendChatMessageInChunks(player, toChat, 450);
                                }
                            }
                            catch (Exception ex)
                            {
                                player.ChatMessage("Error processing AI response. Please try again.");
                                PrintError($"Error processing API response: {ex.Message}");
                            }
                        });
                }
                catch (Exception ex)
                {
                    player.ChatMessage("Error sending message to AI. Please try again.");
                    PrintError($"Error sending chat message to OpenAI: {ex.Message}");
                }
            }
            else if (_questionRegex.IsMatch(user_chat_question))
            {
                // They matched !gpt but don't have permission
                PrintError($"{player.displayName} does not have permission to use RustGPT.");
            }
        }


        private void SendChatMessageInChunks(BasePlayer player, string message, int chunkSize)
        {
            // Standardize chunk size to 450 for better reliability
            const int STANDARD_CHUNK_SIZE = 450;
            string formatStart = $"<color={_config.ChatSettings.ChatMessageColor}><size={_config.ChatSettings.ChatMessageFontSize}>";
            string formatEnd = "</size></color>";

            // Split message into chunks at sentence boundaries
            List<string> chunks = SplitIntoSmartChunks(message, STANDARD_CHUNK_SIZE);

            // Send first chunk immediately
            if (chunks.Count > 0)
            {
                player.ChatMessage(chunks[0]);
            }

            // Schedule remaining chunks with delay
            if (chunks.Count > 1)
            {
                timer.Once(0.5f, () => SendRemainingChunks(player, chunks.Skip(1).ToList(), formatStart, formatEnd, 0));
            }
        }

        private List<string> SplitIntoSmartChunks(string text, int maxChunkSize)
        {
            List<string> chunks = new List<string>();
            string[] sentences = text.Split(new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            StringBuilder currentChunk = new StringBuilder();
            
            foreach (string sentence in sentences)
            {
                string sentenceWithPunctuation = sentence.TrimEnd() + ". ";
                
                // If adding this sentence would exceed the chunk size
                if (currentChunk.Length + sentenceWithPunctuation.Length > maxChunkSize)
                {
                    // If the current chunk is not empty, add it to chunks
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().TrimEnd());
                        currentChunk.Clear();
                    }
                    
                    // If the sentence itself is longer than maxChunkSize, split it at word boundaries
                    if (sentenceWithPunctuation.Length > maxChunkSize)
                    {
                        string[] words = sentenceWithPunctuation.Split(' ');
                        StringBuilder wordChunk = new StringBuilder();
                        
                        foreach (string word in words)
                        {
                            if (wordChunk.Length + word.Length + 1 > maxChunkSize)
                            {
                                chunks.Add(wordChunk.ToString().TrimEnd());
                                wordChunk.Clear();
                            }
                            wordChunk.Append(word).Append(" ");
                        }
                        
                        if (wordChunk.Length > 0)
                        {
                            currentChunk.Append(wordChunk);
                        }
                    }
                    else
                    {
                        currentChunk.Append(sentenceWithPunctuation);
                    }
                }
                else
                {
                    currentChunk.Append(sentenceWithPunctuation);
                }
            }
            
            // Add any remaining text
            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().TrimEnd());
            }
            
            return chunks;
        }

        private void SendRemainingChunks(BasePlayer player, List<string> chunks, string formatStart, string formatEnd, int currentIndex)
        {
            if (currentIndex >= chunks.Count || !player.IsConnected)
                return;

            // Send current chunk with formatting
            player.ChatMessage($"{formatStart}{chunks[currentIndex]}{formatEnd}");

            // Schedule next chunk if there are more
            if (currentIndex + 1 < chunks.Count)
            {
                timer.Once(0.5f, () => SendRemainingChunks(player, chunks, formatStart, formatEnd, currentIndex + 1));
            }
        }

        #endregion

        #region Death_Commentary

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!_config.OptionalPlugins.UseDeathComment || entity == null || info == null) return;

            BasePlayer victim = entity.ToPlayer();
            BasePlayer attacker = info?.InitiatorPlayer;

            // Only proceed if we have both a valid victim and attacker, and neither is an NPC
            if (victim == null || victim is NPCPlayer || attacker == null || attacker is NPCPlayer) return;

            try
            {
                string attackerName = StripRichText(attacker.displayName ?? "Unknown");
                string victimName = StripRichText(victim.displayName ?? "Unknown");
                string weaponName = GetWeaponDisplayName(attacker) ?? "unknown weapon";
                string hitBone = GetHitBone(info) ?? "body";

                string deathMessage = $"{attackerName} killed {victimName} with a {weaponName} in the {hitBone}";

                if (_config.DeathNoteSettings.ShowSimpleKillFeed)
                {
                    string killMessageColor = _config.DeathNoteSettings.KillMessageColor;
                    int killMessageFontSize = _config.DeathNoteSettings.KillMessageFontSize;

                    string formattedMessagePart = $"<color={killMessageColor}><size={killMessageFontSize}>{deathMessage}</size></color>";
                    Server.Broadcast(formattedMessagePart);
                }

                if (_config.OptionalPlugins.UseDiscordWebhookChat)
                {
                    SendDiscordMessage(deathMessage);
                }

                // Send to OpenAI for color commentary
                SendDeathMessageToOpenAI(deathMessage);
            }
            catch (Exception ex)
            {
                PrintError($"Error processing death event: {ex.Message}");
            }
        }

        private string StripRichText(string text)
        {
            return Regex.Replace(text, "<.*?>", String.Empty);
        }

        private string GetHitBone(HitInfo info)
        {
            try
            {
                if (info?.HitEntity == null || !(info.HitEntity is BaseCombatEntity)) return "body";

                BaseCombatEntity hitEntity = info.HitEntity as BaseCombatEntity;
                if (hitEntity?.skeletonProperties == null) return "body";

                SkeletonProperties.BoneProperty boneProperty = hitEntity.skeletonProperties.FindBone(info.HitBone);
                return boneProperty?.name?.english ?? "body";
            }
            catch (Exception ex)
            {
                PrintError($"Error getting hit bone: {ex.Message}");
                return "body";
            }
        }

        private string GetWeaponDisplayName(BasePlayer attacker)
        {
            try
            {
                if (attacker?.GetActiveItem()?.info != null)
                {
                    return attacker.GetActiveItem().info.displayName.translated;
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error getting weapon name: {ex.Message}");
            }
            return "unknown weapon";
        }

        private void SendDeathMessageToOpenAI(string deathMessage)
        {
            // Skip if API key is invalid or empty
            if (string.IsNullOrEmpty(ApiKey) || ApiKey == "your-api-key-here" || !ApiKey.StartsWith("sk-"))
            {
                if (_config.OptionalPlugins.UseDeathComment)
                {
                    PrintWarning("Death commentary is enabled but OpenAI API key is invalid or not set. Death commentary will be disabled.");
                    _config.OptionalPlugins.UseDeathComment = false;
                    Config.WriteObject(_config, true);
                }
                return;
            }

            // Combine system role + rude kill commentary prompt
            string systemPromptForDeath = $"{_config.OptionalPlugins.DeathCommentaryPrompt}";

            try
            {
                RustGPTHook(ApiKey,
                    new
                    {
                        model = _config.AIResponseParameters.Model,
                        messages = new[]
                        {
                            new { role = "developer", content = new[] { new { type = "text", text = systemPromptForDeath } } },
                            new { role = "user", content = new[] { new { type = "text", text = deathMessage } } }
                        },
                        temperature = _config.AIResponseParameters.Temperature,
                        max_tokens = _config.AIResponseParameters.MaxTokens,
                        presence_penalty = _config.AIResponseParameters.PresencePenalty,
                        frequency_penalty = _config.AIResponseParameters.FrequencyPenalty
                    },
                    ApiUrl,
                    response =>
                    {
                        try
                        {
                            if (response == null)
                            {
                                PrintError("Received null response from API");
                                return;
                            }

                            if (response["error"] != null)
                            {
                                PrintError($"OpenAI API Error: {response["error"]["message"]}");
                                return;
                            }

                            if (response["choices"] == null || !response["choices"].HasValues)
                            {
                                PrintError("No choices in response");
                                return;
                            }

                            string GPT_Chat_Reply = response["choices"][0]["message"]["content"].ToString().Trim();
                            if (!string.IsNullOrEmpty(GPT_Chat_Reply))
                            {
                                BroadcastDeathNote(GPT_Chat_Reply);
                            }
                        }
                        catch (Exception ex)
                        {
                            PrintError($"Error processing API response: {ex.Message}");
                        }
                    });
            }
            catch (Exception ex)
            {
                PrintError($"Error sending death message to OpenAI: {ex.Message}");
            }
        }

        private void BroadcastDeathNote(string message)
        {
            string killMessageColor = _config.DeathNoteSettings.KillMessageColor;
            int killMessageFontSize = _config.DeathNoteSettings.KillMessageFontSize;

            var messageParts = SplitIntoSmartChunks(message, 450);
            if (!messageParts.Any())
            {
                return;
            }

            foreach (var messagePart in messageParts)
            {
                string formattedMessagePart = $"<color={killMessageColor}><size={killMessageFontSize}>{messagePart}</size></color>";
                Server.Broadcast(formattedMessagePart);
            }
        }

        #endregion

        #region Hook

        [HookMethod("RustGPTHook")]
        public void RustGPTHook(string apiKey, object payload, string endpoint, Action<JObject> callback)
        {
            if (payload == null)
            {
                PrintError($"Payload is empty!");
            }

            var webClient = new WebClient();
            webClient.Headers.Add("Content-Type", "application/json");
            webClient.Headers.Add("Authorization", $"Bearer {apiKey}");

            webClient.UploadStringCompleted += (sender, e) =>
            {
                if (e.Error != null)
                {
                    PrintError(e.Error.Message);
                    PrintError($"There was an issue with the API request. Check your API key and API Url. If problem persists, check your usage at OpenAI.", e.Error);
                    return;
                }
                callback(JObject.Parse(e.Result));
            };

            var url = string.IsNullOrEmpty(endpoint) ? ApiUrl : endpoint;
            var uri = (Uri)null;

            if (!_uriCache.TryGetValue(url, out uri))
            {
                _uriCache.Add(url, uri = new Uri(url));
            }

            webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
        }

        [HookMethod("GetSuggestedModel")]
        public void GetSuggestedModel(Action<string> callback)
        {
            var webClient = new WebClient();
            webClient.Headers.Add("Content-Type", "application/json");

            webClient.DownloadStringCompleted += (sender, e) =>
            {
                if (e.Error != null)
                {
                    PrintError($"Error fetching suggested model: {e.Error.Message}");
                    return;
                }
                try
                {
                    callback(e.Result.Trim());
                }
                catch (Exception ex)
                {
                    PrintError($"Error processing suggested model response: {ex.Message}");
                }
            };


            try
            {
                var uri = new Uri("https://www.rust.haus/api/openai-suggested-model");
                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating suggested model request: {ex.Message}");
            }
        }

        [HookMethod("ListAvailableModels")]
        public void ListAvailableModels(string apiKey, Action<List<string>> callback)
        {
            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {apiKey}");
                string url = "https://api.openai.com/v1/models";

                try
                {
                    string response = webClient.DownloadString(url);
                    if (response == null)
                    {
                        Puts("Something went wrong retrieving the model list.");
                        return;
                    }

                    JObject jsonResponse = JObject.Parse(response);
                    JToken models;
                    if (jsonResponse.TryGetValue("data", out models) && models is JArray modelsArray)
                    {
                        List<string> modelIds = new List<string>();
                        foreach (JObject model in modelsArray)
                        {
                            string modelId = model["id"].ToString();
                            if (modelId.StartsWith("gpt", StringComparison.OrdinalIgnoreCase))
                            {
                                modelIds.Add(modelId);
                            }
                        }

                        callback(modelIds);
                    }
                    else
                    {
                        PrintError($"Failed to fetch the list of available models.");
                    }
                }
                catch (WebException ex)
                {
                    PrintError($"Error: {ex.Status} - {ex.Message}\n" +
                        $"[RustGPT] If your API key is correctly entered in your configuration file you may have an invalid API key\n" +
                        $"[RustGPT] Make sure your API key is valid: https://platform.openai.com/account/api-keys \n" +
                        $"[RustGPT] config/RustGPT.json - OpenAI API Key: {apiKey}\n");
                }
            }
        }

        private void ModelsCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "RustGPT.admin"))
            {
                player.ChatMessage("You don't have permission to use this command.");
                return;
            }

            ListAvailableModels(ApiKey, (modelList) =>
            {
                if (modelList == null || modelList.Count == 0)
                {
                    player.ChatMessage("No models available or failed to retrieve models.");
                    return;
                }

                string models = string.Join("\n ", modelList.ToArray());
                player.ChatMessage($"Available Models:\n {models}");

                if (_config.OptionalPlugins.UseDiscordWebhookChat)
                {
                    SendDiscordMessage(models);
                }
            });
        }

        [HookMethod("CheckOpenAIModel")]
        public void CheckOpenAIModel(string apiKey, string model, Func<bool, bool> callback)
        {
            ListAvailableModels(apiKey, modelIds =>
            {
                bool modelExists = modelIds.Contains(model);
                if (!modelExists)
                {
                    // If model doesn't exist, get suggested model
                    GetSuggestedModel(suggestedModel =>
                    {
                        if (!string.IsNullOrEmpty(suggestedModel))
                        {
                            string oldModel = _config.AIResponseParameters.Model;
                            _config.AIResponseParameters.Model = suggestedModel;
                            Config.WriteObject(_config, true);
                            Puts($"Updated to suggested model: {suggestedModel}");
                            NotifyAdminsOfModelUpdate(oldModel, suggestedModel);
                        }
                    });
                }
                callback(modelExists);
            });
        }

        private void NotifyAdminsOfModelUpdate(string oldModel, string newModel)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, "RustGPT.admin"))
                {
                    player.ChatMessage($"[RustGPT] Model updated from '{oldModel}' to '{newModel}' based on API availability.");
                }
            }
        }


        #endregion

        #region Helpers

        private void ShowPluginStatusToAdmins()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, "RustGPT.admin"))
                {
                    string statusMessage = "RustGPT Plugin is active.\n";
                    statusMessage += "Using model: " + _config.AIResponseParameters.Model + "\n";

                    if (_config.OptionalPlugins.UseDiscordWebhookChat)
                    {
                        statusMessage += "Discord Messages: Enabled\n";
                    }
                    else
                    {
                        statusMessage += "Discord Messages: Disabled\n";
                    }

                    if (_config.OptionalPlugins.UseDeathComment)
                    {
                        statusMessage += "Death Notes: Enabled\n";
                    }
                    else
                    {
                        statusMessage += "Death Notes: Disabled\n";
                    }

                    player.ChatMessage(statusMessage);
                }
            }
        }


        private bool HasCooldownElapsed(BasePlayer player)
        {
            float lastUsageTime;
            if (_lastUsageTime.TryGetValue(player.UserIDString, out lastUsageTime))
            {
                float elapsedTime = Time.realtimeSinceStartup - lastUsageTime;
                if (elapsedTime < _config.CooldownInSeconds)
                {
                    float timeLeft = _config.CooldownInSeconds - elapsedTime;
                    player.ChatMessage($"<color=green>You must wait <color=orange>{timeLeft:F0}</color> seconds before asking another question.</color>");
                    return false;
                }
            }

            _lastUsageTime[player.UserIDString] = Time.realtimeSinceStartup;
            return true;
        }

        private void NotifyAdminsOfApiKeyRequirement()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, "RustGPT.admin"))
                {
                    player.ChatMessage("The RustGPT API key is not set in the configuration file.");
                }
            }
        }

        private void SendDiscordMessage(string message)
        {
            string goMessage = $"`{ConVar.Server.hostname}`\n{message}\n";
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers[HttpRequestHeader.ContentType] = "application/json";
                var payload = new { content = goMessage };
                var serializedPayload = JsonConvert.SerializeObject(payload);
                webClient.UploadString(_config.OptionalPlugins.DiscordWebhookChatUrl, "POST", serializedPayload);
            }
        }

        #endregion

        #region Config

        private class OpenAI_Api_KeyConfig
        {
            [JsonProperty("OpenAI API Key")]
            public string ApiKey { get; set; }

            public OpenAI_Api_KeyConfig()
            {
                ApiKey = "your-api-key-here";
            }
        }

        private class OutboundAPIUrlConfig
        {
            [JsonProperty("API URL")]
            public string ApiUrl { get; set; }

            public OutboundAPIUrlConfig()
            {
                ApiUrl = "https://api.openai.com/v1/chat/completions";
            }
        }

        private class AIPromptParametersConfig
        {
            [JsonProperty("System role")]
            public string SystemRole { get; set; }

            [JsonProperty("User Server Details")]
            public string UserServerDetails { get; set; }

            public AIPromptParametersConfig()
            {
                SystemRole = "You are a helpful assistant on Rust game server called Rust.Haus Testing Server.";
                UserServerDetails = "Server wipes Thursdays at 2pm CST. Blueprints are wiped on forced wipes only. Gather rate is 5X. Available commands with /info. Admin is Goo. Discord: https://discord.gg/EQNPBxdjRu";
            }
        }

        private class AIResponseParametersConfig
        {
            [JsonProperty("Model")]
            public string Model { get; set; }

            [JsonProperty("Temperature")]
            public double Temperature { get; set; }

            [JsonProperty("Max Tokens")]
            public int MaxTokens { get; set; }

            [JsonProperty("Presence Penalty")]
            public double PresencePenalty { get; set; }

            [JsonProperty("Frequency Penalty")]
            public double FrequencyPenalty { get; set; }

            public AIResponseParametersConfig()
            {
                Model = "gpt-4o-mini";
                Temperature = 0.9;
                MaxTokens = 1000;  // Increased from 200 to allow longer responses
                PresencePenalty = 0.6;
                FrequencyPenalty = 0.2;
            }
        }

        private class OptionalPluginsConfig
        {
            [JsonProperty("Turn on GPT Powered Death Notes")]
            public bool UseDeathComment { get; set; }

            [JsonProperty("Kill GPT Prompt")]
            public string DeathCommentaryPrompt { get; set; }

            [JsonProperty("DiscordMessages Webhook URL")]
            public string DiscordWebhookChatUrl { get; set; }

            [JsonProperty("Broadcast RustGPT Messages to Discord?")]
            public bool UseDiscordWebhookChat { get; set; }

            [JsonProperty("Steam API Key")]
            public string SteamApiKey { get; set; }

            public OptionalPluginsConfig()
            {
                UseDiscordWebhookChat = false;
                DiscordWebhookChatUrl = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
                UseDeathComment = true;
                DeathCommentaryPrompt = "You are an extremely rude color commentator on the hottest new deathmatch show of the future. You use profanity and make fun of the people involved.";
                SteamApiKey = "get-your-steam-api-key-here(https://steamcommunity.com/dev/apikey)";
            }
        }

        private class DeathNoteConfig
        {
            [JsonProperty("Kill Message Color")]
            public string KillMessageColor { get; set; } = "#ADD8E6";

            [JsonProperty("Kill Message Font Size")]
            public int KillMessageFontSize { get; set; } = 12;

            [JsonProperty("Show simple kill feed in chat")]
            public bool ShowSimpleKillFeed { get; set; } = false;
        }

        private class ChatMessageConfig
        {
            [JsonProperty("Chat Message Color")]
            public string ChatMessageColor { get; set; }

            [JsonProperty("Chat Message Font Size")]
            public int ChatMessageFontSize { get; set; }

            public ChatMessageConfig()
            {
                ChatMessageColor = "#FFFFFF";
                ChatMessageFontSize = 12;
            }
        }
        

        private class PluginConfig
        {
            public OpenAI_Api_KeyConfig OpenAI_Api_Key { get; set; }
            public OutboundAPIUrlConfig OutboundAPIUrl { get; set; }
            public AIResponseParametersConfig AIResponseParameters { get; set; }
            public AIPromptParametersConfig AIPromptParameters { get; set; }
            public OptionalPluginsConfig OptionalPlugins { get; set; }
            public DeathNoteConfig DeathNoteSettings { get; set; }
            public ChatMessageConfig ChatSettings { get; set; }

            [JsonProperty("Response Prefix")]
            public string ResponsePrefix { get; set; }

            [JsonProperty("Question Pattern")]
            public string QuestionPattern { get; set; }

            [JsonProperty("Response Prefix Color")]
            public string ResponsePrefixColor { get; set; }

            [JsonProperty("Broadcast Response to the server")]
            public bool BroadcastResponse { get; set; }

            [JsonProperty("Plugin Version")]
            public string PluginVersion { get; set; }

            [JsonProperty("Chat cool down in seconds")]
            public int CooldownInSeconds { get; set; }

            public PluginConfig()
            {
                OpenAI_Api_Key = new OpenAI_Api_KeyConfig();
                AIResponseParameters = new AIResponseParametersConfig();
                AIPromptParameters = new AIPromptParametersConfig();
                OutboundAPIUrl = new OutboundAPIUrlConfig();
                OptionalPlugins = new OptionalPluginsConfig();
                ResponsePrefix = "[RustGPT]";
                QuestionPattern = @"!gpt";
                ResponsePrefixColor = "#55AAFF";
                BroadcastResponse = false;
                CooldownInSeconds = 10;
                DeathNoteSettings = new DeathNoteConfig();
                ChatSettings = new ChatMessageConfig();
            }
        }

        private void MigrateConfig(PluginConfig oldConfig)
        {
            Puts($"Updating configuration file to version {PluginVersion}");

            var newConfig = new PluginConfig();

            // Copy existing settings
            newConfig.OpenAI_Api_Key = oldConfig.OpenAI_Api_Key;
            newConfig.OutboundAPIUrl = oldConfig.OutboundAPIUrl;
            newConfig.ResponsePrefix = oldConfig.ResponsePrefix;
            newConfig.ResponsePrefixColor = oldConfig.ResponsePrefixColor;
            newConfig.BroadcastResponse = oldConfig.BroadcastResponse;
            newConfig.QuestionPattern = oldConfig.QuestionPattern;
            newConfig.CooldownInSeconds = oldConfig.CooldownInSeconds;

            newConfig.AIResponseParameters = new AIResponseParametersConfig
            {
                Model = oldConfig.AIResponseParameters?.Model ?? "gpt-4o-mini",
                Temperature = oldConfig.AIResponseParameters?.Temperature ?? 0.9,
                MaxTokens = oldConfig.AIResponseParameters?.MaxTokens ?? 200,
                PresencePenalty = oldConfig.AIResponseParameters?.PresencePenalty ?? 0.6,
                FrequencyPenalty = oldConfig.AIResponseParameters?.FrequencyPenalty ?? 0.2
            };

            newConfig.AIPromptParameters = new AIPromptParametersConfig
            {
                SystemRole = oldConfig.AIPromptParameters?.SystemRole ?? "You are a helpful assistant.",
                UserServerDetails = oldConfig.AIPromptParameters?.UserServerDetails ?? string.Empty
            };

            newConfig.OptionalPlugins = new OptionalPluginsConfig
            {
                DiscordWebhookChatUrl = oldConfig.OptionalPlugins?.DiscordWebhookChatUrl ?? "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                UseDiscordWebhookChat = oldConfig.OptionalPlugins?.UseDiscordWebhookChat ?? false,
                UseDeathComment = oldConfig.OptionalPlugins?.UseDeathComment ?? false,
                DeathCommentaryPrompt = oldConfig.OptionalPlugins?.DeathCommentaryPrompt ?? "You are an extremely rude color commentator on the hottest new deathmatch show of the future. You use profanity and make fun of the people involved.",
                SteamApiKey = oldConfig.OptionalPlugins?.SteamApiKey ?? "get-your-steAM-api-key-here(https://steamcommunity.com/dev/apikey)"
            };

            newConfig.DeathNoteSettings = new DeathNoteConfig
            {
                KillMessageColor = oldConfig.DeathNoteSettings?.KillMessageColor ?? "#ADD8E6",
                KillMessageFontSize = oldConfig.DeathNoteSettings?.KillMessageFontSize ?? 12,
                ShowSimpleKillFeed = oldConfig.DeathNoteSettings?.ShowSimpleKillFeed ?? false
            };

            // Preserve existing ChatSettings or use defaults if not present
            newConfig.ChatSettings = new ChatMessageConfig
            {
                ChatMessageColor = oldConfig.ChatSettings?.ChatMessageColor ?? "#FFFFFF",
                ChatMessageFontSize = oldConfig.ChatSettings?.ChatMessageFontSize ?? 12
            };

            newConfig.PluginVersion = PluginVersion;
            _config = newConfig;
            Config.WriteObject(_config, true);
        }

        #endregion
    }
}
