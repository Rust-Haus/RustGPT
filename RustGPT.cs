using CompanionServer;
using ConVar;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("RustGPT", "Goo_", "1.8.0")]
    [Description("AI chat integration for Rust with support for OpenAI, Anthropic, and XAI. Players can interact with AI from game chat.")]
    public class RustGPT : RustPlugin
    {
        #region The good stuff.
        private const string PluginVersion = "1.8.0";

        private Dictionary<string, IAIProvider> _providers;
        private IAIProvider _activeProvider;
        private Regex _questionRegex { get; set; }
        private PluginConfig _config { get; set; }

        private Dictionary<string, float> _lastUsageTime = new Dictionary<string, float>();
        private Dictionary<string, Uri> _uriCache = new Dictionary<string, Uri>();
        #endregion

        #region Configuration Classes
        private class AIPromptParametersConfig
        {
            [JsonProperty("System role")]
            public string SystemRole { get; set; }

            [JsonProperty("User Custom Prompt")]
            public string UserCustomPrompt { get; set; }

            [JsonProperty("Share Server Name")]
            public bool ShareServerName { get; set; }

            [JsonProperty("Share Server Description")]
            public bool ShareServerDescription { get; set; }

            [JsonProperty("Share Player Names")]
            public bool SharePlayerNames { get; set; }

            [JsonProperty("AI Rules")]
            public List<string> AIRules { get; set; }

            public AIPromptParametersConfig()
            {
                SystemRole = "You are a helpful assistant on a Rust game server.";
                UserCustomPrompt = "Server wipes Thursdays at 2pm CST. Blueprints are wiped on forced wipes only.";
                ShareServerName = true;
                ShareServerDescription = true;
                SharePlayerNames = true;
                AIRules = new List<string>();
            }
        }

        private class DiscordSettingsConfig
        {
            [JsonProperty("Discord Messages Webhook URL for Admin logging")]
            public string DiscordWebhookChatUrl { get; set; }

            [JsonProperty("Broadcast RustGPT Messages to Discord?")]
            public bool UseDiscordWebhookChat { get; set; }

            public DiscordSettingsConfig()
            {
                UseDiscordWebhookChat = false;
                DiscordWebhookChatUrl = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
            }
        }

        private class ChatMessageConfig
        {
            [JsonProperty("Chat Message Color")]
            public string ChatMessageColor { get; set; }

            [JsonProperty("Chat Message Font Size")]
            public int ChatMessageFontSize { get; set; }

            [JsonProperty("Response Prefix")]
            public string ResponsePrefix { get; set; }

            [JsonProperty("Response Prefix Color")]
            public string ResponsePrefixColor { get; set; }

            [JsonProperty("Question Pattern")]
            public string QuestionPattern { get; set; }

            [JsonProperty("Chat cool down in seconds")]
            public int CooldownInSeconds { get; set; }

            [JsonProperty("Broadcast Response to the server")]
            public bool BroadcastResponse { get; set; }

            public ChatMessageConfig()
            {
                ChatMessageColor = "#FFFFFF";
                ChatMessageFontSize = 12;
                ResponsePrefix = "[RustGPT]";
                ResponsePrefixColor = "#55AAFF";
                QuestionPattern = @"!gpt";
                CooldownInSeconds = 10;
                BroadcastResponse = false;
            }
        }

        public class AIProviderConfig
        {
            [JsonProperty("API Key")]
            public string ApiKey { get; set; }

            [JsonProperty("url")]
            public string ApiUrl { get; set; }

            [JsonProperty("model")]
            public string Model { get; set; }

            [JsonProperty("Max Tokens")]
            public int MaxTokens { get; set; }

            public AIProviderConfig()
            {
                ApiKey = "your-api-key-here";
                MaxTokens = 1500;
            }
        }

        private class AIProvidersConfig
        {
            [JsonProperty("openai")]
            public AIProviderConfig OpenAI { get; set; }

            [JsonProperty("xai")]
            public AIProviderConfig XAI { get; set; }

            [JsonProperty("anthropic")]
            public AIProviderConfig Anthropic { get; set; }

            [JsonProperty("Active Provider")]
            public string ActiveProvider { get; set; }

            public AIProvidersConfig()
            {
                OpenAI = new AIProviderConfig
                {
                    ApiUrl = "",
                    Model = ""
                };

                XAI = new AIProviderConfig
                {
                    ApiUrl = "",
                    Model = ""
                };

                Anthropic = new AIProviderConfig
                {
                    ApiUrl = "",
                    Model = ""
                };

                ActiveProvider = "OpenAI";
            }
        }

        private class PluginConfig
        {
            public AIProvidersConfig AIProviders { get; set; }
            public AIPromptParametersConfig AIPromptParameters { get; set; }
            public DiscordSettingsConfig DiscordSettings { get; set; }
            public ChatMessageConfig ChatSettings { get; set; }

            [JsonProperty("Plugin Version")]
            public string PluginVersion { get; set; }

            public PluginConfig()
            {
                AIProviders = new AIProvidersConfig();
                AIPromptParameters = new AIPromptParametersConfig();
                DiscordSettings = new DiscordSettingsConfig();
                ChatSettings = new ChatMessageConfig();
            }
        }
        #endregion

        #region AI Provider Interface and Implementations
        public interface IAIProvider
        {
            void SendMessage(string prompt, string systemPrompt, Action<JObject> callback);
            string GetApiKey();
            string GetApiUrl();
            string GetModel();
            int GetMaxTokens();
            bool IsEnabled();
        }

        public class OpenAIProvider : IAIProvider
        {
            private readonly AIProviderConfig _config;
            private readonly RustGPT _plugin;

            public OpenAIProvider(AIProviderConfig config, RustGPT plugin)
            {
                _config = config;
                _plugin = plugin;
            }

            public void SendMessage(string prompt, string systemPrompt, Action<JObject> callback)
            {
                var payload = new
                {
                    model = _config.Model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = _config.MaxTokens
                };

                _plugin.RustGPTHook(_config.ApiKey, payload, _config.ApiUrl, callback);
            }

            public string GetApiKey() => _config.ApiKey;
            public string GetApiUrl() => _config.ApiUrl;
            public string GetModel() => _config.Model;
            public int GetMaxTokens() => _config.MaxTokens;
            public bool IsEnabled() => !string.IsNullOrEmpty(_config.ApiKey) && _config.ApiKey != "your-api-key-here";
        }

        public class XAIProvider : IAIProvider
        {
            private readonly AIProviderConfig _config;
            private readonly RustGPT _plugin;

            public XAIProvider(AIProviderConfig config, RustGPT plugin)
            {
                _config = config;
                _plugin = plugin;
            }

            public void SendMessage(string prompt, string systemPrompt, Action<JObject> callback)
            {
                var payload = new
                {
                    model = _config.Model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = _config.MaxTokens
                };

                _plugin.RustGPTHook(_config.ApiKey, payload, _config.ApiUrl, callback);
            }

            public string GetApiKey() => _config.ApiKey;
            public string GetApiUrl() => _config.ApiUrl;
            public string GetModel() => _config.Model;
            public int GetMaxTokens() => _config.MaxTokens;
            public bool IsEnabled() => !string.IsNullOrEmpty(_config.ApiKey) && _config.ApiKey != "your-api-key-here";
        }

        public class AnthropicProvider : IAIProvider
        {
            private readonly AIProviderConfig _config;
            private readonly RustGPT _plugin;

            public AnthropicProvider(AIProviderConfig config, RustGPT plugin)
            {
                _config = config;
                _plugin = plugin;
            }

            public void SendMessage(string prompt, string systemPrompt, Action<JObject> callback)
            {
                var payload = new
                {
                    model = _config.Model,
                    max_tokens = _config.MaxTokens,
                    messages = new[]
                    {
                        new { role = "user", content = $"{systemPrompt}\n\n{prompt}" }
                    }
                };

                var headers = new Dictionary<string, string>
                {
                    { "x-api-key", _config.ApiKey },
                    { "anthropic-version", "2023-06-01" },
                    { "content-type", "application/json" }
                };

                _plugin.RustGPTHook(_config.ApiKey, payload, _config.ApiUrl, callback, headers);
            }

            public string GetApiKey() => _config.ApiKey;
            public string GetApiUrl() => _config.ApiUrl;
            public string GetModel() => _config.Model;
            public int GetMaxTokens() => _config.MaxTokens;
            public bool IsEnabled() => !string.IsNullOrEmpty(_config.ApiKey) && _config.ApiKey != "your-api-key-here";
        }
        #endregion

        #region Initialization and Configuration
        private void Init()
        {
            permission.RegisterPermission("RustGPT.use", this);
            permission.RegisterPermission("RustGPT.admin", this);

            InitializeProviders();

            GetSuggestedModel(suggestedModel =>
            {
                if (!string.IsNullOrEmpty(suggestedModel))
                {
                    Config.WriteObject(_config, true);
                }
            });

            cmd.AddChatCommand("provider", this, nameof(ProviderCommand));
        }

        private void InitializeProviders()
        {
            _providers = new Dictionary<string, IAIProvider>
            {
                { "openai", new OpenAIProvider(_config.AIProviders.OpenAI, this) },
                { "xai", new XAIProvider(_config.AIProviders.XAI, this) },
                { "anthropic", new AnthropicProvider(_config.AIProviders.Anthropic, this) }
            };

            var availableProviders = _providers.Where(p => p.Value.IsEnabled()).ToList();

            if (availableProviders.Count == 0)
            {
                PrintError("No providers have valid API keys configured");
                NotifyAdmins("No AI providers are configured. Please add an API key in the configuration.");
                _activeProvider = _providers.First().Value;
            }
            else if (availableProviders.Count == 1 || !_providers[_config.AIProviders.ActiveProvider.ToLower()].IsEnabled())
            {
                var provider = availableProviders[0];
                UpdateProvider(provider.Key);
                Puts($"Selected {provider.Key} as active provider");
            }
            else
            {
                _activeProvider = _providers[_config.AIProviders.ActiveProvider.ToLower()];
            }

            ShowPluginStatusToAdmins();
        }

        private void TestProviderConfiguration()
        {
            Puts("TestProviderConfiguration called");
            if (_activeProvider == null || !_activeProvider.IsEnabled())
            {
                PrintError("Active provider is null or not enabled");
                return;
            }

            var providerName = _config.AIProviders.ActiveProvider.ToLower();
            Puts($"Getting provider config for: {providerName}");
            var providerConfig = _config.AIProviders.GetType().GetProperty(providerName.ToUpper())?.GetValue(_config.AIProviders);
            if (providerConfig == null)
            {
                PrintError($"Could not get provider config for {providerName}");
                return;
            }

            var model = providerConfig.GetType().GetProperty("model")?.GetValue(providerConfig) as string;
            var apiUrl = providerConfig.GetType().GetProperty("url")?.GetValue(providerConfig) as string;

            Puts($"Current configuration - Model: {model}, API URL: {apiUrl}");

            if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(apiUrl))
            {
                Puts($"Empty configuration detected - Model: {model}, API URL: {apiUrl}");
                Puts("Calling GetSuggestedModel...");
                GetSuggestedModel(suggestedModel =>
                {
                    Puts("GetSuggestedModel callback received");
                    if (!string.IsNullOrEmpty(suggestedModel))
                    {
                        Puts($"Received suggested model: {suggestedModel}");
                        Config.WriteObject(_config, true);
                    }
                    else
                    {
                        PrintError("Failed to get suggested model from config defaults");
                    }
                });
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file.");
            _config = new PluginConfig
            {
                PluginVersion = PluginVersion,
                ChatSettings = new ChatMessageConfig(),
                AIPromptParameters = new AIPromptParametersConfig
                {
                    AIRules = new List<string>
                    {
                        "Only respond in plain text. Do not try to stylize responses.",
                        "Keep responses brief and helpful"
                    }
                }
            };
            Config.WriteObject(_config, true);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    LoadDefaultConfig();
                    return;
                }

                if (_config.AIPromptParameters == null)
                {
                    _config.AIPromptParameters = new AIPromptParametersConfig();
                }

                if (_config.AIPromptParameters.AIRules == null || !_config.AIPromptParameters.AIRules.Any())
                {
                    _config.AIPromptParameters.AIRules = new List<string>
                    {
                        "Only respond in plain text. Do not try to stylize responses.",
                        "Keep responses brief and helpful"
                    };
                }

                if (_config.PluginVersion != PluginVersion)
                {
                    MigrateConfig(_config);
                }

                Config.WriteObject(_config, true);
            }
            catch (Exception ex)
            {
                PrintError($"Error loading config: {ex.Message}");
                LoadDefaultConfig();
                return;
            }

            if (string.IsNullOrEmpty(_config.ChatSettings.QuestionPattern))
            {
                _config.ChatSettings.QuestionPattern = "!gpt";
                Config.WriteObject(_config, true);
            }

            _questionRegex = new Regex(_config.ChatSettings.QuestionPattern, RegexOptions.IgnoreCase);
        }
        #endregion

        #region Chat and Message Handling
        private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (channel != Chat.ChatChannel.Global || !_questionRegex.IsMatch(message))
                return null;

            if (!permission.UserHasPermission(player.UserIDString, "RustGPT.use"))
            {
                PrintError($"{player.displayName} does not have permission to use RustGPT.");
                return null;
            }

            if (!HasCooldownElapsed(player))
            {
                return null;
            }

            if (!_activeProvider.IsEnabled())
            {
                player.ChatMessage("The AI provider is not properly configured. Please contact an administrator.");
                PrintError($"Provider {_config.AIProviders.ActiveProvider} is not enabled");
                return null;
            }

            if (string.IsNullOrEmpty(_activeProvider.GetApiKey()) || _activeProvider.GetApiKey() == "your-api-key-here")
            {
                player.ChatMessage("The API key is not properly configured. Please contact an administrator.");
                PrintError($"API key not configured for provider {_config.AIProviders.ActiveProvider}");
                return null;
            }

            string cleaned_chat_question = !string.IsNullOrEmpty(_config.ChatSettings.QuestionPattern)
                ? message.Replace(_config.ChatSettings.QuestionPattern, "").Trim()
                : message;

            if (_config.AIPromptParameters.SharePlayerNames)
            {
                cleaned_chat_question = $"Player {player.displayName} is asking: {cleaned_chat_question}";
            }

            string system_prompt = BuildSystemPrompt();

            try
            {
                Puts($"Sending message to {_config.AIProviders.ActiveProvider} provider...");
                _activeProvider.SendMessage(cleaned_chat_question, system_prompt, response =>
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
                            string provider = _config.AIProviders.ActiveProvider;
                            string errorMessage;
                            switch (provider)
                            {
                                case "OpenAI":
                                    errorMessage = "OpenAI API error occurred. Please try again later.";
                                    break;
                                case "Anthropic":
                                    errorMessage = "Anthropic API error occurred. Please try again later.";
                                    break;
                                case "XAI":
                                    errorMessage = "XAI API error occurred. Please try again later.";
                                    break;
                                default:
                                    errorMessage = "API error occurred. Please try again later.";
                                    break;
                            }
                            player.ChatMessage(errorMessage);
                            PrintError($"API Error for {provider}: {response["error"]["message"]}");
                            return;
                        }

                        string aiResponse;
                        try
                        {
                            switch (_config.AIProviders.ActiveProvider.ToLower())
                            {
                                case "anthropic":
                                    aiResponse = response["content"][0]["text"].ToString().Trim();
                                    break;
                                case "openai":
                                case "xai":
                                    aiResponse = response["choices"][0]["message"]["content"].ToString().Trim();
                                    break;
                                default:
                                    PrintError($"Unknown provider: {_config.AIProviders.ActiveProvider}");
                                    player.ChatMessage("Error: Unknown AI provider");
                                    return;
                            }
                        }
                        catch (Exception ex)
                        {
                            string provider = _config.AIProviders.ActiveProvider.ToLower();
                            PrintError($"Error parsing response for {provider}: {ex.Message}");
                            PrintError($"Raw response: {response}");
                            player.ChatMessage($"Error processing {provider} response. Please try again.");
                            return;
                        }

                        if (string.IsNullOrEmpty(aiResponse))
                        {
                            PrintError("Received empty response from AI");
                            player.ChatMessage("Received empty response from AI. Please try again.");
                            return;
                        }

                        string customPrefix = $"<color={_config.ChatSettings.ResponsePrefixColor}><size={_config.ChatSettings.ChatMessageFontSize}>{_config.ChatSettings.ResponsePrefix}</size></color>";
                        string formattedReply = $"<color={_config.ChatSettings.ChatMessageColor}><size={_config.ChatSettings.ChatMessageFontSize}>{aiResponse}</size></color>";
                        string toChat = $"{customPrefix} {formattedReply}";

                        if (_config.DiscordSettings.UseDiscordWebhookChat)
                        {
                            var discordPayload = $"**{player}** \n> {cleaned_chat_question}.\n**{_config.ChatSettings.ResponsePrefix}** \n> {aiResponse}";
                            SendDiscordMessage(discordPayload);
                        }

                        if (_config.ChatSettings.BroadcastResponse)
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
                        PrintError($"Error processing AI response: {ex.Message}");
                        PrintError($"Stack trace: {ex.StackTrace}");
                        player.ChatMessage("Error processing AI response. Please try again.");
                    }
                });
            }
            catch (Exception ex)
            {
                PrintError($"Error sending message to AI: {ex.Message}");
                PrintError($"Stack trace: {ex.StackTrace}");
                player.ChatMessage("Error sending message to AI. Please try again.");
            }

            return null;
        }

        private void SendChatMessageInChunks(BasePlayer player, string message, int chunkSize)
        {
            const int STANDARD_CHUNK_SIZE = 450;
            string formatStart = $"<color={_config.ChatSettings.ChatMessageColor}><size={_config.ChatSettings.ChatMessageFontSize}>";
            string formatEnd = "</size></color>";

            List<string> chunks = SplitIntoSmartChunks(message, STANDARD_CHUNK_SIZE);

            if (chunks.Count > 0)
            {
                player.ChatMessage(chunks[0]);
            }

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

                if (currentChunk.Length + sentenceWithPunctuation.Length > maxChunkSize)
                {
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().TrimEnd());
                        currentChunk.Clear();
                    }

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

            player.ChatMessage($"{formatStart}{chunks[currentIndex]}{formatEnd}");

            if (currentIndex + 1 < chunks.Count)
            {
                timer.Once(0.5f, () => SendRemainingChunks(player, chunks, formatStart, formatEnd, currentIndex + 1));
            }
        }
        #endregion

        #region API and Web Hooks
        [HookMethod("RustGPTHook")]
        public void RustGPTHook(string apiKey, object payload, string endpoint, Action<JObject> callback, Dictionary<string, string> customHeaders = null)
        {
            if (!_activeProvider.IsEnabled())
            {
                PrintError("AI provider is not enabled");
                callback(null);
                return;
            }

            if (payload == null)
            {
                PrintError($"Payload is empty!");
                callback(null);
                return;
            }

            var webClient = new WebClient();
            webClient.Headers.Add("Content-Type", "application/json");

            if (customHeaders != null)
            {
                foreach (var header in customHeaders)
                {
                    webClient.Headers.Add(header.Key, header.Value);
                }
            }
            else
            {
                webClient.Headers.Add("Authorization", $"Bearer {apiKey}");
            }

            webClient.UploadStringCompleted += (sender, e) =>
            {
                if (e.Error != null)
                {
                    PrintError(e.Error.Message);
                    string provider = _config.AIProviders.ActiveProvider;
                    string errorMessage;
                    switch (provider)
                    {
                        case "OpenAI":
                            errorMessage = "Check your API key and API URL. If problem persists, check your usage at OpenAI.";
                            break;
                        case "Anthropic":
                            errorMessage = "Check your API key and API URL. If problem persists, check your usage at Anthropic.";
                            break;
                        case "XAI":
                            errorMessage = "Check your API key and API URL. If problem persists, check your usage at XAI.";
                            break;
                        default:
                            errorMessage = "Check your API key and API URL configuration.";
                            break;
                    }
                    PrintError($"[RustGPT] {errorMessage}");
                    PrintError($"[RustGPT] Provider: {provider}");
                    PrintError($"[RustGPT] URL: {endpoint}");
                    PrintError($"[RustGPT] Error: {e.Error.Message}");
                    callback(null);
                    return;
                }
                callback(JObject.Parse(e.Result));
            };

            var url = endpoint;
            var uri = (Uri)null;

            if (!_uriCache.TryGetValue(url, out uri))
            {
                _uriCache.Add(url, uri = new Uri(url));
            }

            webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
        }

        [HookMethod("SendMessage")]
        public void SendMessage(string prompt, string systemPrompt, Action<JObject> callback, Dictionary<string, string> customHeaders = null)
        {
            if (!_activeProvider.IsEnabled())
            {
                PrintError("AI provider is not enabled");
                callback(null);
                return;
            }

            var payload = new
            {
                model = _activeProvider.GetModel(),
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = prompt }
                },
                max_tokens = _activeProvider.GetMaxTokens()
            };

            RustGPTHook(_activeProvider.GetApiKey(), payload, _activeProvider.GetApiUrl(), callback, customHeaders);
        }

        [HookMethod("IsEnabled")]
        public bool IsEnabled()
        {
            return _activeProvider?.IsEnabled() ?? false;
        }

        [HookMethod("GetActiveProvider")]
        public string GetActiveProvider()
        {
            return _config.AIProviders.ActiveProvider;
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
                    callback(null);
                    return;
                }
                try
                {
                    var configDefaults = JObject.Parse(e.Result);

                    foreach (var provider in configDefaults.Properties())
                    {
                        var providerName = provider.Name;
                        var providerConfig = provider.Value as JObject;

                        if (providerConfig != null)
                        {
                            var model = providerConfig["model"]?.ToString();
                            var apiUrl = providerConfig["url"]?.ToString();

                            AIProviderConfig configProvider = null;
                            switch (providerName.ToLower())
                            {
                                case "openai":
                                    configProvider = _config.AIProviders.OpenAI;
                                    break;
                                case "xai":
                                    configProvider = _config.AIProviders.XAI;
                                    break;
                                case "anthropic":
                                    configProvider = _config.AIProviders.Anthropic;
                                    break;
                            }

                            if (configProvider != null)
                            {
                                if (string.IsNullOrEmpty(configProvider.Model) && !string.IsNullOrEmpty(model))
                                {
                                    configProvider.Model = model;
                                }

                                if (string.IsNullOrEmpty(configProvider.ApiUrl) && !string.IsNullOrEmpty(apiUrl))
                                {
                                    configProvider.ApiUrl = apiUrl;
                                }
                            }
                        }
                    }

                    Config.WriteObject(_config, true);
                    callback(configDefaults[_config.AIProviders.ActiveProvider.ToLower()]?["model"]?.ToString());
                }
                catch (Exception ex)
                {
                    PrintError($"Error processing suggested model response: {ex.Message}");
                    callback(null);
                }
            };

            try
            {
                var uri = new Uri("https://raw.githubusercontent.com/Rust-Haus/RustGPT/refs/heads/main/config-defaults.json");
                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating suggested model request: {ex.Message}");
                callback(null);
            }
        }
        #endregion

        #region Utility Methods
        private void NotifyAdmins(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, "RustGPT.admin"))
                {
                    player.ChatMessage($"[RustGPT] {message}");
                }
            }
        }

        private void ProviderCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "RustGPT.admin"))
            {
                player.ChatMessage("<color=red>You don't have permission to use this command.</color>");
                return;
            }

            var availableProviders = _providers.Where(p => p.Value.IsEnabled()).ToList();

            if (args.Length == 0)
            {
                string currentProvider = _config.AIProviders.ActiveProvider;
                string status = _activeProvider.IsEnabled() ? "<color=green>Configured</color>" : "<color=red>Not Configured</color>";
                string model = _activeProvider.GetModel();

                player.ChatMessage($"<color=#55AAFF>=== RustGPT Provider Status ===</color>");
                player.ChatMessage($"Current Provider: <color=yellow>{currentProvider}</color> ({status})");
                player.ChatMessage($"Current Model: <color=yellow>{model}</color>");
                player.ChatMessage($"\nAvailable Providers ({availableProviders.Count}):");
                foreach (var p in availableProviders)
                {
                    player.ChatMessage($"• <color=yellow>{p.Key}</color> - {GetProviderDescription(p.Key)}");
                }
                player.ChatMessage($"\nUsage: <color=yellow>/provider [name]</color>");
                return;
            }

            string provider = args[0].ToLower();

            if (!_providers.ContainsKey(provider))
            {
                player.ChatMessage($"<color=red>Invalid provider. Available: {string.Join(", ", availableProviders.Select(p => p.Key))}</color>");
                return;
            }

            if (!_providers[provider].IsEnabled())
            {
                player.ChatMessage($"<color=red>Provider {provider} is not configured. Please set up the API key in the configuration.</color>");
                return;
            }

            UpdateProvider(provider);
            player.ChatMessage($"<color=green>Successfully switched to {provider} provider.</color>");
        }

        private string GetProviderDescription(string provider)
        {
            switch (provider.ToLower())
            {
                case "openai": return "OpenAI GPT Models";
                case "xai": return "XAI Grok Models";
                case "anthropic": return "Anthropic Claude Models";
                default: return "Unknown Provider";
            }
        }

        private void UpdateProvider(string providerName)
        {
            providerName = providerName.ToLower();
            if (_providers.TryGetValue(providerName, out var provider))
            {
                if (!provider.IsEnabled())
                {
                    PrintError($"Provider {providerName} is not enabled in the configuration.");
                    NotifyAdmins($"Provider {providerName} is not properly configured");
                    return;
                }

                _activeProvider = provider;
                _config.AIProviders.ActiveProvider = providerName;
                Config.WriteObject(_config, true);
                NotifyAdmins($"Provider switched to {providerName}");
            }
        }

        private void NotifyAdminsOfProviderRequirement()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, "RustGPT.admin"))
                {
                    player.ChatMessage($"The RustGPT provider {_config.AIProviders.ActiveProvider} is not enabled in the configuration file.");
                }
            }
        }

        private string BuildSystemPrompt()
        {
            var prompt = new StringBuilder();

            prompt.AppendLine(_config.AIPromptParameters.SystemRole);

            if (_config.AIPromptParameters.ShareServerName)
            {
                prompt.AppendLine($"Server Name: {ConVar.Server.hostname}");
            }

            if (_config.AIPromptParameters.ShareServerDescription)
            {
                prompt.AppendLine($"Server Description: {ConVar.Server.description}");
                prompt.AppendLine(_config.AIPromptParameters.UserCustomPrompt);
            }

            if (_config.AIPromptParameters.AIRules?.Count > 0)
            {
                prompt.AppendLine("\nRules to follow:");
                foreach (var rule in _config.AIPromptParameters.AIRules)
                {
                    prompt.AppendLine($"- {rule}");
                }
            }

            return prompt.ToString();
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

        private void ShowPluginStatusToAdmins()
        {
            var status = new StringBuilder();
            status.AppendLine("RustGPT Plugin Status:");
            status.AppendLine($"Active Provider: {_config.AIProviders.ActiveProvider}");
            if (_activeProvider != null)
            {
                status.AppendLine($"Model: {_activeProvider.GetModel()}");
                status.AppendLine($"API URL: {_activeProvider.GetApiUrl()}");
            }
            status.AppendLine($"Discord Messages: {(_config.DiscordSettings.UseDiscordWebhookChat ? "Enabled" : "Disabled")}");

            NotifyAdmins(status.ToString());
        }

        private bool HasCooldownElapsed(BasePlayer player)
        {
            float lastUsageTime;
            if (_lastUsageTime.TryGetValue(player.UserIDString, out lastUsageTime))
            {
                float elapsedTime = UnityEngine.Time.realtimeSinceStartup - lastUsageTime;
                if (elapsedTime < _config.ChatSettings.CooldownInSeconds)
                {
                    float timeLeft = _config.ChatSettings.CooldownInSeconds - elapsedTime;
                    player.ChatMessage($"<color=green>You must wait <color=orange>{timeLeft:F0}</color> seconds before asking another question.</color>");
                    return false;
                }
            }

            _lastUsageTime[player.UserIDString] = UnityEngine.Time.realtimeSinceStartup;
            return true;
        }

        private void NotifyAdminsOfApiKeyRequirement()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, "RustGPT.admin"))
                {
                    player.ChatMessage("An API key is required to use RustGPT. Please set one in the configuration file.");
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
                webClient.UploadString(_config.DiscordSettings.DiscordWebhookChatUrl, "POST", serializedPayload);
            }
        }

        private void MigrateConfig(PluginConfig oldConfig)
        {
            var newConfig = new PluginConfig();

            newConfig.AIProviders.OpenAI = new AIProviderConfig
            {
                ApiKey = oldConfig.AIProviders?.OpenAI?.ApiKey ?? "your-api-key-here",
                ApiUrl = oldConfig.AIProviders?.OpenAI?.ApiUrl ?? "https://api.openai.com/v1/chat/completions",
                Model = oldConfig.AIProviders?.OpenAI?.Model ?? "gpt-4o-mini",
                MaxTokens = oldConfig.AIProviders?.OpenAI?.MaxTokens ?? 1000
            };

            if (oldConfig.AIProviders?.XAI != null)
            {
                newConfig.AIProviders.XAI = oldConfig.AIProviders.XAI;
            }
            if (oldConfig.AIProviders?.Anthropic != null)
            {
                newConfig.AIProviders.Anthropic = oldConfig.AIProviders.Anthropic;
            }

            newConfig.AIProviders.ActiveProvider = oldConfig.AIProviders?.ActiveProvider ?? "OpenAI";

            newConfig.AIPromptParameters = new AIPromptParametersConfig
            {
                SystemRole = oldConfig.AIPromptParameters?.SystemRole ?? "You are a helpful assistant on a Rust game server.",
                UserCustomPrompt = oldConfig.AIPromptParameters?.UserCustomPrompt ?? "Server wipes Thursdays at 2pm CST. Blueprints are wiped on forced wipes only.",
                ShareServerName = true,
                ShareServerDescription = true,
                SharePlayerNames = false
            };

            if (oldConfig.AIPromptParameters?.AIRules != null && oldConfig.AIPromptParameters.AIRules.Any())
            {
                newConfig.AIPromptParameters.AIRules = oldConfig.AIPromptParameters.AIRules.Distinct().ToList();
            }

            newConfig.DiscordSettings = oldConfig.DiscordSettings ?? new DiscordSettingsConfig();

            var chatSettings = oldConfig.ChatSettings ?? new ChatMessageConfig();
            chatSettings.CooldownInSeconds = oldConfig.ChatSettings?.CooldownInSeconds ?? 10;

            if (oldConfig.ChatSettings != null && oldConfig.ChatSettings.GetType().GetProperty("BroadcastResponse") != null)
            {
                chatSettings.BroadcastResponse = oldConfig.ChatSettings.BroadcastResponse;
            }
            else
            {
                chatSettings.BroadcastResponse = false;
            }

            newConfig.ChatSettings = chatSettings;

            newConfig.PluginVersion = PluginVersion;

            _config = newConfig;
            Config.WriteObject(_config, true);

            Puts("Configuration migration completed successfully");
        }
        #endregion
    }
}
