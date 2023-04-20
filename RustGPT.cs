using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RustGPT", "GooGurt", "1.6.0")]
    [Description("Ask questions to RustGPT from the game chat")]
    class RustGPT : CovalencePlugin
    {
        private string ApiKey => _config.OpenAI_Api_Key.ApiKey;
        private string ApiUrl => _config.OutboundAPIUrl.ApiUrl; 
        private Regex _questionRegex;
        private PluginConfig _config;

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file.");
            _config = new PluginConfig();
            Config.WriteObject(_config, true);
            _questionRegex = new Regex(_config.QuestionPattern, RegexOptions.IgnoreCase);

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
            
            _questionRegex = new Regex(_config.QuestionPattern, RegexOptions.IgnoreCase);

        }

        private void Init()
        {
            permission.RegisterPermission("RustGPT.use", this);
        }

        [Command("askgpt")]
        private void AskGPTCommand(IPlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, "RustGPT.use"))
            {
                player.Reply("You don't have permission to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                player.Reply("Usage: /askgpt <your question>");
                return;
            }

            string question = string.Join(" ", args);
            string coloredPrefix = $"<color={_config.ResponsePrefixColor}>{_config.ResponsePrefix}</color>";
            AskRustGPT(question, response => player.Reply($"{coloredPrefix} {response}"));
        }

        private void AskRustGPT(string question, Action<string> callback)
        {
            WebClient webClient = new WebClient();
            webClient.Headers.Add("Content-Type", "application/json");
            webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");

            string payload = JsonConvert.SerializeObject(new
            {
                model = _config.AIResponseParameters.ChatModel,
                prompt = $"Human: {question}\nAI:",
                temperature = _config.AIResponseParameters.Temperature,
                max_tokens = _config.AIResponseParameters.MaxTokens,
                top_p = 1,
                frequency_penalty = _config.AIResponseParameters.FrequencyPenalty,
                presence_penalty = _config.AIResponseParameters.PresencePenalty,
                stop = new[] { " Human:", " AI:" }
            });

            webClient.UploadStringCompleted += (sender, e) =>
            {
                if (e.Error != null)
                {
                    Puts($"Error: {e.Error.Message}");
                    return;
                }

                RustGPTResponse response = JsonConvert.DeserializeObject<RustGPTResponse>(e.Result);
                string answer = response.choices[0].text.Trim();
                callback(answer);
            };

            webClient.UploadStringAsync(new Uri(ApiUrl), "POST", payload);
        }

        private class RustGPTResponse
        {
            public List<Choice> choices { get; set; }
        }

        private class Choice
        {
            public string text { get; set; }
        }

        private object OnUserChat(IPlayer player, string message)
        {
            if (permission.UserHasPermission(player.Id, "RustGPT.use") && _questionRegex.IsMatch(message))
            {
                string coloredPrefix = $"<color={_config.ResponsePrefixColor}>{_config.ResponsePrefix}</color>";
                AskRustGPT(message, response => player.Reply($"{coloredPrefix} {response}"));
            }

            return null;
        }

        private void NotifyAdminsOfApiKeyRequirement()
        {
            foreach (IPlayer player in players.Connected) 
            {
                if (permission.UserHasPermission(player.Id, "RustGPT.use"))
                {
                    player.Message("The RustGPT API key is not set in the configuration file. Please update the 'your-api-key-here' value with a valid API key to use the RustGPT plugin.");
                }
            }
        }

        private class OpenAI_Api_KeyConfig
        {
            [JsonProperty("API Key")]
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
                ApiUrl = "https://api.openai.com/v1/completions";
            }
        }

        private class AIResponseParametersConfig
        {
            [JsonProperty("Model")]
            public string ChatModel { get; set; }

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
                ChatModel = "text-davinci-003";
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
            
            [JsonProperty("Response Prefix")]
            public string ResponsePrefix { get; set; }

            [JsonProperty("Question Pattern")]
            public string QuestionPattern { get; set; }

            [JsonProperty("Response Prefix Color")]
            public string ResponsePrefixColor { get; set; }

            public PluginConfig()
            {
                OpenAI_Api_Key = new OpenAI_Api_KeyConfig();
                AIResponseParameters = new AIResponseParametersConfig();
                OutboundAPIUrl = new OutboundAPIUrlConfig();
                ResponsePrefix = "[RustGPT]";
                QuestionPattern = @"!gpt";
                ResponsePrefixColor = "#55AAFF";

            }
        }
    }
}