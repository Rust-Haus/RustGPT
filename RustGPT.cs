using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Rust;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("RustGPT", "GooGurt", "1.6.3")]
    [Description("Players can use OpenAI's ChatGPT from the game chat")]
    class RustGPT : RustPlugin
    {
        #region Declarations

        private string ApiKey => _config.OpenAI_Api_Key.ApiKey;
        private string ApiUrl => _config.OutboundAPIUrl.ApiUrl;
        private Regex _questionRegex { get; set; }
        private PluginConfig _config { get; set; }
        private const string PluginVersion = "1.6.3";
        private readonly Version _version = new Version(PluginVersion);
        private Dictionary<string, float> _lastUsageTime = new Dictionary<string, float>();


        #endregion

        #region Overrides

        private void Init()
        {
            permission.RegisterPermission("RustGPT.use", this);
            cmd.AddChatCommand("askgpt", this, "AskRustCommand");
            Puts($"RustGPT {_version} initialized");
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
            else
            {
                Puts("Configuration file loaded.");
            }

            if (_config.PluginVersion != PluginVersion)
            {
                MigrateConfig(_config);
            }

            _questionRegex = new Regex(_config.QuestionPattern, RegexOptions.IgnoreCase);

        }

        #endregion

        #region Commands

        [Command("askgpt")]
        private void AskRustCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "RustGPT.use"))
            {
                player.ChatMessage("You don't have permission to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Usage: /askgpt <your question>");
                return;
            }

            string question = string.Join(" ", args);

            string coloredPrefix = $"<color={_config.ResponsePrefixColor}>{_config.ResponsePrefix}</color>";

            AskRustGPT(player, question, response =>
            {
                string formattedResponse = $"{coloredPrefix} {response}";
                Puts($"This is the response: {response}");
                if (_config.BroadcastResponse)
                {
                    Server.Broadcast($"{player.displayName} asked: {question}");
                    Server.Broadcast(formattedResponse);
                }
                else
                {
                    player.ChatMessage($"You asked: {question}");
                    player.ChatMessage(formattedResponse);
                }
            });

        }

        #endregion

        #region API

        private object OnPlayerChat(BasePlayer player, string chatQuestion)
        {
            if (permission.UserHasPermission(player.UserIDString, "RustGPT.use") &&
                _questionRegex.IsMatch(chatQuestion) &&
                HasCooldownElapsed(player))
            {
                string coloredPrefix = $"<color={_config.ResponsePrefixColor}>{_config.ResponsePrefix}</color>";
                timer.Once(0.1f, () =>
                {
                    AskRustGPT(player, chatQuestion, response =>
                    {
                        string formattedResponse = $"{coloredPrefix} {response}";
                        if (_config.BroadcastResponse)
                        {
                            Server.Broadcast($"{player.displayName} asked: {chatQuestion}");
                            Server.Broadcast(formattedResponse);
                        }
                        else
                        {
                            player.ChatMessage(formattedResponse);
                        }
                    });
                });
            }
            return null;
        }


        private void AskRustGPT(BasePlayer player, string question, Action<string> callback)
        {
            var webClient = new WebClient();
            webClient.Headers.Add("Content-Type", "application/json");
            webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");

            var payload = JsonConvert.SerializeObject(new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = $"{_config.AIPromptParameters.SystemRole}" },
                    new { role = "user", content = $"My name is {player.displayName} Tell me about this server." },
                    new { role = "assistant", content = $"{_config.AIPromptParameters.UserServerDetails}" },
                    new { role = "user", content = $"{question}" },
                },
                temperature = _config.AIResponseParameters.Temperature,
                max_tokens = _config.AIResponseParameters.MaxTokens,
            });

            webClient.UploadStringCompleted += (sender, e) =>
            {
                if (e.Error != null)
                {
                    PrintError(e.Error.Message);
                    player.ChatMessage($"{e.Error.Message} - There was an issue with the API request. Check your API key and API Url. If problem persists, check your usage at OpenAI.");
                    return;
                }

                JObject jsonResponse = JObject.Parse(e.Result);
                string answer = jsonResponse["choices"][0]["message"]["content"].ToString().Trim();
                callback(answer);
            };

            webClient.UploadStringAsync(new Uri(ApiUrl), "POST", payload);
        }

        #endregion

        #region Hook
        [HookMethod("RustGPTHook")]
        public void RustGPTHook(BasePlayer player, string question, string apiKey, float temperatureAI, int maxTokens, string systemRole, string userServerDetails, Action<string> callback)
        {
            var webClient = new WebClient();
            webClient.Headers.Add("Content-Type", "application/json");
            webClient.Headers.Add("Authorization", $"Bearer {apiKey}");

            var payload = JsonConvert.SerializeObject(new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = $"{systemRole}" },
                    new { role = "user", content = $"My name is {player.displayName} Tell me about this server." },
                    new { role = "assistant", content = $"{userServerDetails}" },
                    new { role = "user", content = $"{question}" },
                },
                temperature = temperatureAI,
                max_tokens = maxTokens,
            });

            webClient.UploadStringCompleted += (sender, e) =>
            {
                if (e.Error != null)
                {
                    PrintError(e.Error.Message);
                    player.ChatMessage($"{e.Error.Message} - There was an issue with the API request. Check your API key and API Url. If problem persists, check your usage at OpenAI.");
                    return;
                }

                JObject jsonResponse = JObject.Parse(e.Result);
                string answer = jsonResponse["choices"][0]["message"]["content"].ToString().Trim();
                callback(answer);
            };

            webClient.UploadStringAsync(new Uri("https://api.openai.com/v1/chat/completions"), "POST", payload);
        }

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
                if (permission.UserHasPermission(player.UserIDString, "RustGPT.use"))
                {
                    player.ChatMessage("The RustGPT API key is not set in the configuration file. Please update the 'your-api-key-here' value with a valid API key to use the RustGPT plugin.");
                }
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
                UserServerDetails = "Server wipes Thursdays at 2pm CST. Blueprints are wiped on forced wipes only. Gather rate is 5X. Available commands available by using /info. Server admin is Goo. The discord link is https://discord.gg/EQNPBxdjRu";
            }
        }

        private class AIResponseParametersConfig
        {
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
                Temperature = 0.9;
                MaxTokens = 150;
                PresencePenalty = 0.6;
                FrequencyPenalty = 0;
            }
        }

        private class PluginConfig
        {
            public OpenAI_Api_KeyConfig OpenAI_Api_Key { get; set; }
            public OutboundAPIUrlConfig OutboundAPIUrl { get; set; }
            public AIResponseParametersConfig AIResponseParameters { get; set; }
            public AIPromptParametersConfig AIPromptParameters { get; set; }


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

            _config.PluginVersion = PluginVersion;
            Config.WriteObject(_config, true);
        }

        #endregion
    }
}
