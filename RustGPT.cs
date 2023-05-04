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

#pragma warning disable SYSLIB0014

#pragma warning disable SYSLIB0014

namespace Oxide.Plugins
{
    [Info("RustGPT", "GooGurt", "1.6.51")]
    [Description("Players can use OpenAI's ChatGPT from the game chat")]
    public class RustGPT : RustPlugin
    {
        #region Declarations
        private string ApiKey => _config.OpenAI_Api_Key.ApiKey;
        private string ApiUrl => _config.OutboundAPIUrl.ApiUrl;
        private Regex _questionRegex { get; set; }
        private PluginConfig _config { get; set; }
        private const string PluginVersion = "1.6.51";
        private readonly Version _version = new Version(PluginVersion);
        private Dictionary<string, float> _lastUsageTime = new Dictionary<string, float>();
        private Dictionary<string, Uri> _uriCache = new Dictionary<string, Uri>();


        [PluginReference]
        private Plugin DiscordMessages, DeathNotes;
        #endregion

        #region Overrides
        private void Init()
        {
            permission.RegisterPermission("RustGPT.chat", this);

            if (string.IsNullOrEmpty(ApiKey) || ApiKey == "your-api-key-here")
            {
                CheckOpenAIModel(ApiKey, _config.AIResponseParameters.Model, modelExists =>
                {
                    if (modelExists)
                    {
                        Puts($"Using OpenAI model: {_config.AIResponseParameters.Model}");
                        return true;
                    }
                    else
                    {
                        Puts($"Model {_config.AIResponseParameters.Model} does not exist or you do not have access. Check your config file.");
                        return false;
                    }
                });
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file.");
            _config = new PluginConfig { PluginVersion = PluginVersion };
            Config.WriteObject(_config, true);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();
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

            if (_config.PluginVersion != PluginVersion)
            {
                MigrateConfig(_config);
            }

            _questionRegex = new Regex(_config.QuestionPattern, RegexOptions.IgnoreCase);

        }
        #endregion

        #region User_Chat
        private void OnPlayerChat(BasePlayer player, string user_chat_question)
        {
            if (_questionRegex.IsMatch(user_chat_question) && permission.UserHasPermission(player.UserIDString, "RustGPT.chat"))
            {
                if (!HasCooldownElapsed(player))
                {
                    return;
                }
                string cleaned_chat_question = !string.IsNullOrEmpty(_config.QuestionPattern)
                    ? user_chat_question.Replace(_config.QuestionPattern, "").Trim()
                    : user_chat_question;
                string system_prompt = $"{_config.AIPromptParameters.UserServerDetails}";
                RustGPTHook(ApiKey,
                    new
                    {
                        model = _config.AIResponseParameters.Model,
                        messages = new[]
                        {
                            new {role = "system", content= system_prompt},
                            new {role = "user", content = cleaned_chat_question}
                        },
                        temperature = _config.AIResponseParameters.Temperature,
                        max_tokens = _config.AIResponseParameters.MaxTokens,
                        presence_penalty = _config.AIResponseParameters.PresencePenalty,
                        frequency_penalty = _config.AIResponseParameters.FrequencyPenalty
                    },
                    ApiUrl,
                    response =>
                    {
                        string customPrefix = $"<color={_config.ResponsePrefixColor}>{_config.ResponsePrefix}</color>";
                        string GPT_Chat_Reply = response["choices"][0]["message"]["content"].ToString().Trim();
                        string toChat = $"{customPrefix} {GPT_Chat_Reply}";

                        if (_config.OptionalPlugins.UseDiscordWebhookChat)
                        {
                            SendDiscordMessage(GPT_Chat_Reply);
                        }

                        if (_config.BroadcastResponse)
                        {
                            Server.Broadcast($" Responding to {player.displayName}:\n {toChat}");
                        }
                        else
                        {
                            player.ChatMessage(toChat);
                        }
                    });
            }
            else if (_questionRegex.IsMatch(user_chat_question))
            {
                PrintError($"{player.displayName} does not have permission to use RustGPT.");
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
                    JObject jsonResponse = JObject.Parse(response);
                    JToken models;
                    if (jsonResponse.TryGetValue("data", out models))
                    {
                        List<string> modelIds = new List<string>();
                        foreach (JObject model in models)
                        {
                            modelIds.Add(model["id"].ToString());
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
                        $"[OpenAI-RustGPT] If your API key is correctly entered in your configuration file you may have an invalid API key\n" +
                        $"[OpenAI-RustGPT] Make sure your API key is valid: https://platform.openai.com/account/api-keys \n" +
                        $"[OpenAI-RustGPT] config/RustGPT.json - OpenAI API Key: {ApiKey}\n");
                }
            }
        }

        // ListAvailableModels Usage
        // ListAvailableModels(ApiKey, modelIds =>
        // {
        //     Puts("Available models:");
        //     foreach (string modelId in modelIds)
        //     {
        //         Puts($"{modelId}");
        //     }
        // });

        [HookMethod("CheckOpenAIModel")]
        public void CheckOpenAIModel(string apiKey, string model, Func<bool, bool> callback)
        {
            ListAvailableModels(apiKey, modelIds =>
            {
                bool modelExists = modelIds.Contains(model);
                callback(modelExists);
            });
        }

        // CheckOpenAIModel Usage
        //CheckOpenAIModel(ApiKey, _config.AIResponseParameters.Model, modelExists =>
        //    {
        //    if (modelExists)
        //    {
        //        Puts($"Using OpenAI model: {_config.AIResponseParameters.Model}");
        //        return true;
        //    }
        //    else
        //    {
        //        Puts($"Model {_config.AIResponseParameters.Model} does not exist. Check your config file.");
        //        return false;
        //    }
        //});

        #endregion

        #region Helpers

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
                if (permission.UserHasPermission(player.UserIDString, "RustGPT.chat"))
                {
                    player.ChatMessage("The RustGPT API key is not set in the configuration file.");
                }
            }
        }

        private void SendDiscordMessage(string message)
        {
            string Message = $"{_config.ResponsePrefix} {message}";
            DiscordMessages?.Call("API_SendTextMessage", _config.OptionalPlugins.DiscordWebhookChatUrl, string.Format(Message));
        }

        private object OnDeathNotice(Dictionary<string, object> data, string message)
        {
            if (!_config.OptionalPlugins.UseDeathNotes) return null;
            string someoneDead = data["VictimEntity"]?.ToString();
            string someMurderer = data["KillerEntity"]?.ToString();
            string murderWeapon = data["DamageType"]?.ToString();

            int dead = someoneDead.IndexOf('[');
            int murderer = someMurderer.IndexOf('[');
            if (dead != -1)
            {
                someoneDead = someoneDead.Substring(0, dead);
            }
            if (murderer != -1)
            {
                someMurderer = someMurderer.Substring(0, murderer);
            }

            var deathToGpt = $"{someMurderer} killed {someoneDead} with {murderWeapon} ";

            RustGPTHook(ApiKey, new
            {
                model = _config.AIResponseParameters.Model,
                messages = new[]
                        {
                            new {role = "system", content= $"{_config.OptionalPlugins.DeathNotesPrompt}"},
                            new {role = "user", content = deathToGpt}
                        },
                temperature = _config.AIResponseParameters.Temperature,
                max_tokens = _config.AIResponseParameters.MaxTokens,
                presence_penalty = _config.AIResponseParameters.PresencePenalty,
                frequency_penalty = _config.AIResponseParameters.FrequencyPenalty
            }, ApiUrl,
                    response =>
                    {
                        string customPrefix = $"<color={_config.ResponsePrefixColor}>{_config.ResponsePrefix}</color>";
                        string GPT_Chat_Reply = response["choices"][0]["message"]["content"].ToString().Trim();
                        string toChat = $"{customPrefix} {GPT_Chat_Reply}";

                        Server.Broadcast(toChat);

                        if (!_config.OptionalPlugins.UseDiscordWebhookChat) return;
                        SendDiscordMessage(toChat);
                    });

            return null;
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
                UserServerDetails = "Server wipes Thursdays at 2pm CST. Blueprints are wiped on forced wipes only. Gather rate is 5X. Available commands available by using /info. Server admin is Goo. The discord link is https://discord.gg/EQNPBxdjRu";
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
                Model = "gpt-3.5-turbo";
                Temperature = 0.9;
                MaxTokens = 150;
                PresencePenalty = 0.6;
                FrequencyPenalty = 0;
            }
        }

        private class OptionalPluginsConfig
        {
            [JsonProperty("Turn on GPT Powered Death Notes")]
            public bool UseDeathNotes { get; set; }

            [JsonProperty("Death Notes GPT Prompt")]
            public string DeathNotesPrompt { get; set; }

            [JsonProperty("DiscordMessages Webhook URL")]
            public string DiscordWebhookChatUrl { get; set; }

            [JsonProperty("Broadcast RustGPT Messages to Discord?")]
            public bool UseDiscordWebhookChat { get; set; }

            public OptionalPluginsConfig()
            {
                UseDiscordWebhookChat = false;
                DiscordWebhookChatUrl = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
                UseDeathNotes = false;
                DeathNotesPrompt = "You are a color commentator on the hottest new deathmatch show of the future. You can use Markdown in your responses.";
            }

        }

        private class PluginConfig
        {
            public OpenAI_Api_KeyConfig OpenAI_Api_Key { get; set; }
            public OutboundAPIUrlConfig OutboundAPIUrl { get; set; }
            public AIResponseParametersConfig AIResponseParameters { get; set; }
            public AIPromptParametersConfig AIPromptParameters { get; set; }
            public OptionalPluginsConfig OptionalPlugins { get; set; }


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
            }
        }

        private void MigrateConfig(PluginConfig oldConfig)
        {
            Puts($"Updating configuration file to version {PluginVersion}");

            _config.OpenAI_Api_Key = oldConfig.OpenAI_Api_Key;
            _config.OutboundAPIUrl = oldConfig.OutboundAPIUrl;
            _config.ResponsePrefix = oldConfig.ResponsePrefix;
            _config.ResponsePrefixColor = oldConfig.ResponsePrefixColor;
            _config.BroadcastResponse = oldConfig.BroadcastResponse;
            _config.QuestionPattern = oldConfig.QuestionPattern;
            _config.BroadcastResponse = oldConfig.BroadcastResponse;
            _config.CooldownInSeconds = oldConfig.CooldownInSeconds;

            _config.AIResponseParameters = new AIResponseParametersConfig
            {
                Model = oldConfig.AIResponseParameters.Model,
                Temperature = oldConfig.AIResponseParameters.Temperature,
                MaxTokens = oldConfig.AIResponseParameters.MaxTokens,
                PresencePenalty = oldConfig.AIResponseParameters.PresencePenalty,
                FrequencyPenalty = oldConfig.AIResponseParameters.FrequencyPenalty
            };

            _config.AIPromptParameters = new AIPromptParametersConfig
            {
                SystemRole = oldConfig.AIPromptParameters.SystemRole,
                UserServerDetails = oldConfig.AIPromptParameters.UserServerDetails
            };

            _config.OptionalPlugins = new OptionalPluginsConfig
            {
                DiscordWebhookChatUrl = oldConfig.OptionalPlugins.DiscordWebhookChatUrl,
                UseDiscordWebhookChat = oldConfig.OptionalPlugins.UseDiscordWebhookChat,
                UseDeathNotes = oldConfig.OptionalPlugins.UseDeathNotes,
                DeathNotesPrompt = oldConfig.OptionalPlugins.DeathNotesPrompt,
            };

            _config.PluginVersion = PluginVersion;
            Config.WriteObject(_config, true);
        }

        #endregion

    }
}
