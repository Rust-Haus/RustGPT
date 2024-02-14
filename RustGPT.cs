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

#pragma warning disable SYSLIB0014

namespace Oxide.Plugins
{
    [Info("RustGPT", "Goo_", "1.7.6")]
    [Description("Players can use OpenAI's ChatGPT from the game chat, incudes ChatGPT color commentary for deaths.")]
    public class RustGPT : RustPlugin
    {
        #region Declarations
        private string ApiKey => _config.OpenAI_Api_Key.ApiKey;
        private string ApiUrl => _config.OutboundAPIUrl.ApiUrl;
        private Regex _questionRegex { get; set; }
        private PluginConfig _config { get; set; }
        private const string PluginVersion = "1.7.6";
        private readonly Version _version = new Version(PluginVersion);
        private Dictionary<string, float> _lastUsageTime = new Dictionary<string, float>();
        private Dictionary<string, Uri> _uriCache = new Dictionary<string, Uri>();

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

            ShowPluginStatusToAdmins();

            cmd.AddChatCommand("models", this, nameof(ModelsCommand));


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
                    });
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
        #endregion

        #region Death_Commentary

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!_config.OptionalPlugins.UseDeathComment || entity == null || info == null) return;

            BasePlayer victim = entity.ToPlayer();
            BasePlayer attacker = info.InitiatorPlayer;

            if (victim != null && !(victim is NPCPlayer) && attacker != null && !(attacker is NPCPlayer))
            {
                string attackerName = StripRichText(attacker.displayName);
                string victimName = StripRichText(victim.displayName);

                string weaponName = GetWeaponDisplayName(attacker);
                string hitBone = GetHitBone(info);

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

                SendDeathMessageToOpenAI(deathMessage);

            }
        }

        private string GetEntityName(BaseCombatEntity entity)
        {

            if (entity is BasePlayer player)
            {
                return StripRichText(player.displayName);
            }
            else if (entity is NPCPlayer)
            {
                if (entity.ShortPrefabName.Contains("scientist"))
                {
                    return "Scientist";
                }
            }
            else if (entity is BaseAnimalNPC)
            {
                return entity.ShortPrefabName;
            }
            else if (entity is BaseHelicopter)
            {
                return "Helicopter";
            }

            return "an unknown entity";
        }

        private string StripRichText(string text)
        {
            return Regex.Replace(text, "<.*?>", String.Empty);
        }

        private string GetHitBone(HitInfo info)
        {
            if (info.HitEntity == null || info.HitEntity.ToPlayer() == null) return "";

            string hitBone;

            BaseCombatEntity hitEntity = info.HitEntity as BaseCombatEntity;

            SkeletonProperties.BoneProperty boneProperty = hitEntity.skeletonProperties?.FindBone(info.HitBone);

            string bone = boneProperty?.name?.english ?? "";

            return bone;
        }

        private string CreateDeathMessage(BasePlayer victim, BasePlayer attacker, HitInfo info)
        {
            string weaponDisplayName = GetWeaponDisplayName(attacker);
            string bodyPart = GetHitBone(info);
            bool isHeadshot = info.isHeadshot;

            string deathMessage = $"{attacker.displayName} killed {victim.displayName} with a {weaponDisplayName}";

            if (!string.IsNullOrEmpty(bodyPart))
            {
                deathMessage += $" by hitting their {bodyPart}";
            }

            if (isHeadshot)
            {
                deathMessage += " (headshot)";
            }

            return deathMessage;
        }

        private string GetWeaponDisplayName(BasePlayer attacker)
        {
            if (attacker != null && attacker.GetActiveItem() != null)
            {
                Item activeItem = attacker.GetActiveItem();
                return activeItem.info.displayName.translated;
            }

            return "unknown";
        }

        private ItemDefinition GetItemDefinitionFromEntity(BaseEntity entity)
        {
            if (entity != null)
            {
                Item item = entity.GetComponent<Item>();

                if (item != null)
                {
                    return item.info;
                }
            }

            return null;
        }

        private void SendDeathMessageToOpenAI(string deathMessage)
        {
            RustGPTHook(ApiKey,
                new
                {
                    model = _config.AIResponseParameters.Model,
                    messages = new[]
                    {
                        new { role = "system", content = _config.OptionalPlugins.DeathCommentaryPrompt },
                        new { role = "user", content = deathMessage }
                    },
                    temperature = _config.AIResponseParameters.Temperature,
                    max_tokens = _config.AIResponseParameters.MaxTokens,
                    presence_penalty = _config.AIResponseParameters.PresencePenalty,
                    frequency_penalty = _config.AIResponseParameters.FrequencyPenalty
                },
                ApiUrl,
                response =>
                {
                    string GPT_Chat_Reply = response["choices"][0]["message"]["content"].ToString().Trim();

                    BroadcastDeathNote(GPT_Chat_Reply);
                });
        }

        private void BroadcastDeathNote(string message)
        {
            const int MaxMessageLength = 500; // I left this out of the config since this is more of a steam thing more than a rust thing. I doubt steam will change their message sizes in the near future. 
            string killMessageColor = _config.DeathNoteSettings.KillMessageColor;
            int killMessageFontSize = _config.DeathNoteSettings.KillMessageFontSize;

            var messageParts = SplitMessageIntoChunks(message, MaxMessageLength).ToList();

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

        private IEnumerable<string> SplitMessageIntoChunks(string message, int chunkSize)
        {
            for (int i = 0; i < message.Length; i += chunkSize)
            {
                yield return message.Substring(i, Math.Min(chunkSize, message.Length - i));
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
            if (player.net.connection.authLevel < 2)
            {
                player.ChatMessage("You must have auth level 2 to use this command.");
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
                callback(modelExists);
            });
        }

        #endregion

        #region Helpers

        private void ShowPluginStatusToAdmins()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, "RustGPT.chat") && player.net.connection.authLevel >= 2)
                {
                    string statusMessage = "RustGPT Plugin is active. Use /rustgptfeedback for suggestions and/or problems.\n";

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
                if (permission.UserHasPermission(player.UserIDString, "RustGPT.use"))
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

                var response = webClient.UploadString(_config.OptionalPlugins.DiscordWebhookChatUrl, "POST", serializedPayload);

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

        private class DeathNoteConfig
        {
            [JsonProperty("Kill Message Color")]
            public string KillMessageColor { get; set; } = "#ADD8E6";

            [JsonProperty("Kill Message Font Size")]
            public int KillMessageFontSize { get; set; } = 12;

            [JsonProperty("Show simple kill feed in chat")]
            public bool ShowSimpleKillFeed { get; set; } = false;
        }


        private class PluginConfig
        {
            public OpenAI_Api_KeyConfig OpenAI_Api_Key { get; set; }
            public OutboundAPIUrlConfig OutboundAPIUrl { get; set; }
            public AIResponseParametersConfig AIResponseParameters { get; set; }
            public AIPromptParametersConfig AIPromptParameters { get; set; }
            public OptionalPluginsConfig OptionalPlugins { get; set; }
            public DeathNoteConfig DeathNoteSettings { get; set; }


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
                UseDeathComment = oldConfig.OptionalPlugins.UseDeathComment,
                DeathCommentaryPrompt = oldConfig.OptionalPlugins.DeathCommentaryPrompt,
                SteamApiKey = oldConfig.OptionalPlugins.SteamApiKey,
            };

            _config.DeathNoteSettings = new DeathNoteConfig
            {
                KillMessageColor = oldConfig.DeathNoteSettings?.KillMessageColor ?? "#ADD8E6",
                KillMessageFontSize = oldConfig.DeathNoteSettings?.KillMessageFontSize ?? 12,
                ShowSimpleKillFeed = oldConfig.DeathNoteSettings?.ShowSimpleKillFeed ?? false,
            };

            _config.PluginVersion = PluginVersion;
            Config.WriteObject(_config, true);
        }

        #endregion

    }
}
