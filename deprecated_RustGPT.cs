using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RustGPT", "Goo_", "1.8")]
    [Description("Players can use OpenAI's ChatGPT from the game chat.")]
    public class RustGPT : RustPlugin
    {
        [PluginReference]
        private Plugin OpenAI;

        private Regex _questionRegex;
        private PluginConfig _config;
        private Dictionary<string, float> _lastUsageTime = new Dictionary<string, float>();

        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission("RustGPT.chat", this);
            cmd.AddChatCommand("models", this, nameof(ModelsCommand));
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file.");
            _config = new PluginConfig();
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
                Puts("Please set your OpenAI API key in the configuration file.");
            }

            _questionRegex = new Regex(_config.QuestionPattern, RegexOptions.IgnoreCase);
        }

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

                var payload = new
                {
                    model = _config.AIResponseParameters.Model,
                    messages = new[]
                    {
                        new { role = "system", content = system_prompt },
                        new { role = "user", content = cleaned_chat_question }
                    },
                    temperature = _config.AIResponseParameters.Temperature,
                    max_tokens = _config.AIResponseParameters.MaxTokens,
                    presence_penalty = _config.AIResponseParameters.PresencePenalty,
                    frequency_penalty = _config.AIResponseParameters.FrequencyPenalty
                };

                OpenAI.Call("SendRequest", payload, new Action<JObject>(response =>
                {
                    string customPrefix = $"<color={_config.ResponsePrefixColor}>{_config.ResponsePrefix}</color>";
                    string GPT_Chat_Reply = response["choices"][0]["message"]["content"].ToString().Trim();
                    string toChat = $"{customPrefix} {GPT_Chat_Reply}";

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
                        SendChatMessageInChunks(player, toChat, 250);
                    }
                }));
            }
            else if (_questionRegex.IsMatch(user_chat_question))
            {
                PrintError($"{player.displayName} does not have permission to use RustGPT.");
            }
        }

        private void SendChatMessageInChunks(BasePlayer player, string message, int chunkSize)
        {
            for (int i = 0; i < message.Length; i += chunkSize)
            {
                string chunk = message.Substring(i, Math.Min(chunkSize, message.Length - i));
                player.ChatMessage(chunk);
            }
        }

        private void ModelsCommand(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 2)
            {
                player.ChatMessage("You must have auth level 2 to use this command.");
                return;
            }

            OpenAI.Call("ListAvailableModels", _config.OpenAI_Api_Key.ApiKey, new Action<List<string>>(modelList =>
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
            }));
        }

        private bool HasCooldownElapsed(BasePlayer player)
        {
            if (_lastUsageTime.TryGetValue(player.UserIDString, out float lastUsageTime))
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

        private void SendDiscordMessage(string message)
        {
            string goMessage = $"`{ConVar.Server.hostname}`\n{message}\n";

            using (var webClient = new WebClient())
            {
                webClient.Headers[HttpRequestHeader.ContentType] = "application/json";

                var payload = new { content = goMessage };
                var serializedPayload = JsonConvert.SerializeObject(payload);

                webClient.UploadString(_config.OptionalPlugins.DiscordWebhookChatUrl, "POST", serializedPayload);
            }
        }

        private class PluginConfig
        {
            public AIResponseParametersConfig AIResponseParameters { get; set; }
            public AIPromptParametersConfig AIPromptParameters { get; set; }
            public OptionalPluginsConfig OptionalPlugins { get; set; }
            public string ResponsePrefix { get; set; }
            public string QuestionPattern { get; set; }
            public string ResponsePrefixColor { get; set; }
            public bool BroadcastResponse { get; set; }
            public int CooldownInSeconds { get; set; }

            public PluginConfig()
            {
                AIResponseParameters = new AIResponseParametersConfig();
                AIPromptParameters = new AIPromptParametersConfig();
                OptionalPlugins = new OptionalPluginsConfig();
                ResponsePrefix = "[RustGPT]";
                QuestionPattern = @"!gpt";
                ResponsePrefixColor = "#55AAFF";
                BroadcastResponse = false;
                CooldownInSeconds = 10;
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
                UseDeathComment = false;
                DeathCommentaryPrompt = "You are an extremely rude color commentator on the hottest new deathmatch show of the future. You use profanity and make fun of the people involved.";
                SteamApiKey = "get-your-steam-api-key-here(https://steamcommunity.com/dev/apikey)";
            }
        }
    }
}
