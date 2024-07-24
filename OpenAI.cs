using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEngine;

#pragma warning disable SYSLIB0014

namespace Oxide.Plugins
{
    [Info("OpenAI", "Goo_", "1.0.0")]
    [Description("Plugin to interact with OpenAI API")]
    public class OpenAI : RustPlugin
    {
        private string ApiKey => _config.OpenAI_Api_Key.ApiKey;
        private PluginConfig _config { get; set; }
        private Dictionary<string, Uri> _uriCache = new Dictionary<string, Uri>();
        private bool isApiKeyValid = true;
        private string defaultCompletionsModel;
        private string defaultAssistantModel;

        private void Init()
        {
            LoadConfig();
            this.defaultCompletionsModel = _config.DefaultCompletionsModel.Model;
            this.defaultAssistantModel = _config.DefaultAssistantModel.Model;

            if (!string.IsNullOrEmpty(ApiKey) && ApiKey != "your-api-key-here")
            {
                VerifyApiKey();
            }

            cmd.AddChatCommand("openaitest", this, nameof(OpenAITestCommand));
            cmd.AddChatCommand("listmodels", this, nameof(ListModelsCommand));
        }

        private void VerifyApiKey()
        {
            var messages = new List<object>
            {
                new { role = "system", content = "This is a test message to verify the API key." },
                new { role = "user", content = "If you are there, reply by only saying 'Hello '." }
            };

            Completions_SimpleChat(messages, response =>
            {
                if (response != null && response["choices"] != null && response["choices"].HasValues)
                {
                    Puts("API key verified successfully.");
                    string reply = response["choices"][0]["message"]["content"].ToString().Trim();
                    Puts($"Assistant replied: {reply}");
                }
                else
                {
                    HandleInvalidApiKey("API key verification failed. Please check your API key.");
                }
            });
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
        }

        private void HandleWebClientError(Exception error)
        {
            if (error is WebException webException)
            {
                using (var response = webException.Response)
                {
                    if (response != null)
                    {
                        var httpResponse = (HttpWebResponse)response;
                        using (var dataStream = httpResponse.GetResponseStream())
                        using (var reader = new StreamReader(dataStream))
                        {
                            string responseText = reader.ReadToEnd();
                            PrintError($"HTTP Status: {httpResponse.StatusCode}");
                            PrintError($"Response: {responseText}");
                            HandleInvalidApiKey($"OpenAI API Error: {responseText}");
                        }
                    }
                    else
                    {
                        PrintError("WebException occurred, but no response received.");
                        HandleInvalidApiKey("WebException occurred, but no response received.");
                    }
                }
            }
            else
            {
                PrintError($"Error: {error.Message}");
                HandleInvalidApiKey($"Error: {error.Message}");
            }

            PrintError("There was an issue with the API request. Check your API key and API URL. If the problem persists, check your usage at OpenAI.");
        }

        private void HandleInvalidApiKey(string errorMessage)
        {
            isApiKeyValid = false;
            NotifyAdmins(errorMessage);
        }

        private void NotifyAdmins(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.net.connection.authLevel >= 2)
                {
                    player.ChatMessage($"<color=#ff0000>[ADMIN ONLY: OpenAI]</color> {message}");
                }
            }
        }

# region Chat Commands

        private void OpenAITestCommand(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 2)
            {
                player.ChatMessage("You must be an admin to use this command.");
                return;
            }

            var messages = new List<object>
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = "Hello!" }
            };

            player.ChatMessage("Sending test message to OpenAI...");

            Completions_SimpleChat(messages, response =>
            {
                if (response != null && response["choices"] != null && response["choices"].HasValues)
                {
                    string reply = response["choices"][0]["message"]["content"].ToString().Trim();
                    player.ChatMessage($"OpenAI replied: {reply}");
                }
                else
                {
                    player.ChatMessage("<color=#ff0000>Failed to get a valid response from OpenAI. Please check your API key and settings.</color>");
                }
            });
        }

        // An example command to list available models from OpenAI
        private void ListModelsCommand(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 2)
            {
                player.ChatMessage("You must be an admin to use this command.");
                return;
            }

            FetchModels(modelList =>
            {
                var modelListStr = string.Join(", ", modelList);
                player.ChatMessage($"Available models: {modelListStr}");
            });
        }


# endregion

# region Utility Methods

        [HookMethod("FetchModels")]
        public void FetchModels(Action<List<string>> callback)
        {
            var url = "https://api.openai.com/v1/models";

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");

                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback(new List<string>());
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        if (response != null && response["data"] != null)
                        {
                            var models = response["data"];
                            var modelNames = new List<string>();

                            foreach (var model in models)
                            {
                                modelNames.Add(model["id"].ToString());
                            }

                            callback(modelNames);
                        }
                        else
                        {
                            PrintError("Failed to fetch models from OpenAI. Please check your API key.");
                            callback(new List<string>());
                        }
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback(new List<string>());
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback(new List<string>());
            }
        }

# endregion

#region Chat Completions API

        [HookMethod("Completions_CreateChat")]
        public void Completions_CreateChat(List<object> messages, Action<JObject> callback = null, int? n = null, double? temperature = null, double? topP = null, Dictionary<string, int> logitBias = null, bool? logprobs = null, int? topLogprobs = null, double? frequencyPenalty = null, double? presencePenalty = null, string responseFormat = null, bool? stream = null, object streamOptions = null, string user = null, object tools = null, string toolChoice = null, bool? parallelToolCalls = null, List<string> stop = null, object functionCall = null)
        {
            var url = "https://api.openai.com/v1/chat/completions";
            var payload = new
            {
                model = _config.DefaultCompletionsModel.Model,
                messages = messages,
                max_tokens = _config.DefaultCompletionsModel.MaxTokens,
                n = n,
                temperature = temperature,
                top_p = topP,
                logit_bias = logitBias,
                logprobs = logprobs,
                top_logprobs = topLogprobs,
                frequency_penalty = frequencyPenalty,
                presence_penalty = presencePenalty,
                response_format = responseFormat,
                stream = stream,
                stream_options = streamOptions,
                user = user,
                tools = tools,
                tool_choice = toolChoice,
                parallel_tool_calls = parallelToolCalls,
                stop = stop,
                function_call = functionCall
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Completions_SimpleChat")]
        public void Completions_SimpleChat(List<object> messages, Action<JObject> callback = null)
        {
            var url = "https://api.openai.com/v1/chat/completions";
            var payload = new
            {
                model = _config.DefaultCompletionsModel.Model,
                messages = messages,
                max_tokens = _config.DefaultCompletionsModel.MaxTokens,
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }


# endregion

# region Assistant API

        [HookMethod("Assistant_CreateAssistant")]
        public void Assistant_CreateAssistant(string name = null, string description = null, string instructions = null, List<object> tools = null, object toolResources = null, Dictionary<string, string> metadata = null, double? temperature = null, double? topP = null, object responseFormat = null, Action<JObject> callback = null)
        {
            var url = "https://api.openai.com/v1/assistants";
            var payload = new
            {
                model = _config.DefaultAssistantModel.Model,
                name = name,
                description = description,
                instructions = instructions,
                tools = tools,
                tool_resources = toolResources,
                metadata = metadata,
                temperature = temperature,
                top_p = topP,
                response_format = responseFormat
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("CreateThread")]
        public void CreateThread(List<ChatMessage> messages = null, object toolResources = null, Dictionary<string, string> metadata = null, Action<JObject> callback = null)
        {
            var url = "https://api.openai.com/v1/threads";
            var payload = new
            {
                messages = messages ?? new List<ChatMessage>(),
                tool_resources = toolResources,
                metadata = metadata
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("RetrieveThread")]
        public void RetrieveThread(string threadId, Action<JObject> callback)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}";

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("ModifyThread")]
        public void ModifyThread(string threadId, Dictionary<string, string> metadata, object toolResources = null, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}";
            var payload = new
            {
                metadata = metadata,
                tool_resources = toolResources
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("DeleteThread")]
        public void DeleteThread(string threadId, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}";

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "DELETE", string.Empty);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_CreateMessage")]
        public void Assistant_CreateMessage(string threadId, string role, object content, List<object> attachments = null, Dictionary<string, string> metadata = null, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/messages";
            var payload = new
            {
                role = role,
                content = content,
                attachments = attachments,
                metadata = metadata
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_ListMessages")]
        public void Assistant_ListMessages(string threadId, int limit = 20, string order = "desc", string after = null, string before = null, string runId = null, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/messages";
            var queryParameters = new List<string>();

            if (limit != 20) queryParameters.Add($"limit={limit}");
            if (order != "desc") queryParameters.Add($"order={order}");
            if (!string.IsNullOrEmpty(after)) queryParameters.Add($"after={after}");
            if (!string.IsNullOrEmpty(before)) queryParameters.Add($"before={before}");
            if (!string.IsNullOrEmpty(runId)) queryParameters.Add($"run_id={runId}");

            if (queryParameters.Count > 0)
            {
                url += "?" + string.Join("&", queryParameters);
            }

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_RetrieveMessage")]
        public void Assistant_RetrieveMessage(string threadId, string messageId, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/messages/{messageId}";

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_ModifyMessage")]
        public void Assistant_ModifyMessage(string threadId, string messageId, Dictionary<string, string> metadata, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/messages/{messageId}";
            var payload = new
            {
                metadata = metadata
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_DeleteMessage")]
        public void Assistant_DeleteMessage(string threadId, string messageId, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/messages/{messageId}";

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "DELETE", string.Empty);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_CreateRun")]
        public void Assistant_CreateRun(string threadId, string assistantId, string instructions = null, string additionalInstructions = null, List<ChatMessage> additionalMessages = null, List<object> tools = null, Dictionary<string, string> metadata = null, double? temperature = null, double? topP = null, bool? stream = null, int? maxPromptTokens = null, int? maxCompletionTokens = null, object truncationStrategy = null, object toolChoice = null, bool? parallelToolCalls = null, object responseFormat = null, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/runs";
            var payload = new
            {
                assistant_id = assistantId,
                model = _config.DefaultAssistantModel.Model,
                instructions = instructions,
                additional_instructions = additionalInstructions,
                additional_messages = additionalMessages,
                tools = tools,
                metadata = metadata,
                temperature = temperature,
                top_p = topP,
                stream = stream,
                max_prompt_tokens = _config.DefaultAssistantModel.max_prompt_tokens,
                max_completion_tokens = _config.DefaultAssistantModel.max_completion_tokens,
                truncation_strategy = truncationStrategy,
                tool_choice = toolChoice,
                parallel_tool_calls = parallelToolCalls,
                response_format = responseFormat
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_CreateThreadAndRun")]
        public void Assistant_CreateThreadAndRun(string assistantId, object thread = null, string instructions = null, List<object> tools = null, object toolResources = null, Dictionary<string, string> metadata = null, double? temperature = null, double? topP = null, bool? stream = null, int? maxPromptTokens = null, int? maxCompletionTokens = null, object truncationStrategy = null, object toolChoice = null, bool? parallelToolCalls = null, object responseFormat = null, Action<JObject> callback = null)
        {
            var url = "https://api.openai.com/v1/threads/runs";
            var payload = new
            {
                assistant_id = assistantId,
                thread = thread,
                model = _config.DefaultAssistantModel.Model,
                instructions = instructions,
                tools = tools,
                tool_resources = toolResources,
                metadata = metadata,
                temperature = temperature,
                top_p = topP,
                stream = stream,
                max_prompt_tokens = _config.DefaultAssistantModel.max_prompt_tokens,
                max_completion_tokens = _config.DefaultAssistantModel.max_completion_tokens,
                truncation_strategy = truncationStrategy,
                tool_choice = toolChoice,
                parallel_tool_calls = parallelToolCalls,
                response_format = responseFormat
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_ListRuns")]
        public void Assistant_ListRuns(string threadId, int limit = 20, string order = "desc", string after = null, string before = null, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/runs";
            var queryParameters = new List<string>();

            if (limit != 20) queryParameters.Add($"limit={limit}");
            if (order != "desc") queryParameters.Add($"order={order}");
            if (!string.IsNullOrEmpty(after)) queryParameters.Add($"after={after}");
            if (!string.IsNullOrEmpty(before)) queryParameters.Add($"before={before}");

            if (queryParameters.Count > 0)
            {
                url += "?" + string.Join("&", queryParameters);
            }

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_RetrieveRun")]
        public void Assistant_RetrieveRun(string threadId, string runId, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/runs/{runId}";

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_ModifyRun")]
        public void Assistant_ModifyRun(string threadId, string runId, Dictionary<string, string> metadata, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/runs/{runId}";
            var payload = new
            {
                metadata = metadata
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_SubmitToolOutput")]
        public void Assistant_SubmitToolOutput(string threadId, string runId, List<object> toolOutputs, bool? stream = null, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/runs/{runId}/submit_tool_outputs";
            var payload = new
            {
                tool_outputs = toolOutputs,
                stream = stream
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_CancelRun")]
        public void Assistant_CancelRun(string threadId, string runId, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/runs/{runId}/cancel";

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", string.Empty);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_ListRunSteps")]
        public void Assistant_ListRunSteps(string threadId, string runId, int limit = 20, string order = "desc", string after = null, string before = null, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/runs/{runId}/steps";
            var queryParameters = new List<string>();

            if (limit != 20) queryParameters.Add($"limit={limit}");
            if (order != "desc") queryParameters.Add($"order={order}");
            if (!string.IsNullOrEmpty(after)) queryParameters.Add($"after={after}");
            if (!string.IsNullOrEmpty(before)) queryParameters.Add($"before={before}");

            if (queryParameters.Count > 0)
            {
                url += "?" + string.Join("&", queryParameters);
            }

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_RetrieveRunStep")]
        public void Assistant_RetrieveRunStep(string threadId, string runId, string stepId, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/threads/{threadId}/runs/{runId}/steps/{stepId}";

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_CreateVectorStore")]
        public void Assistant_CreateVectorStore(List<string> fileIds = null, string name = null, object expiresAfter = null, object chunkingStrategy = null, Dictionary<string, string> metadata = null, Action<JObject> callback = null)
        {
            var url = "https://api.openai.com/v1/vector_stores";
            var payload = new
            {
                file_ids = fileIds,
                name = name,
                expires_after = expiresAfter,
                chunking_strategy = chunkingStrategy,
                metadata = metadata
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }

        [HookMethod("Assistant_ListVectorStores")]
        public void Assistant_ListVectorStores(int limit = 20, string order = "desc", string after = null, string before = null, Action<JObject> callback = null)
        {
            var url = "https://api.openai.com/v1/vector_stores";
            var queryParameters = new List<string>();

            if (limit != 20) queryParameters.Add($"limit={limit}");
            if (order != "desc") queryParameters.Add($"order={order}");
            if (!string.IsNullOrEmpty(after)) queryParameters.Add($"after={after}");
            if (!string.IsNullOrEmpty(before)) queryParameters.Add($"before={before}");

            if (queryParameters.Count > 0)
            {
                url += "?" + string.Join("&", queryParameters);
            }

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }
        
        [HookMethod("Assistant_RetrieveVectorStore")]
        public void Assistant_RetrieveVectorStore(string vectorStoreId, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/vector_stores/{vectorStoreId}";

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.DownloadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }
        
        [HookMethod("Assistant_ModifyVectorStore")]
        public void Assistant_ModifyVectorStore(string vectorStoreId, string name = null, object expiresAfter = null, Dictionary<string, string> metadata = null, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/vector_stores/{vectorStoreId}";
            var payload = new
            {
                name = name,
                expires_after = expiresAfter,
                metadata = metadata
            };

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "POST", JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }
        
        [HookMethod("Assistant_DeleteVectorStore")]
        public void Assistant_DeleteVectorStore(string vectorStoreId, Action<JObject> callback = null)
        {
            var url = $"https://api.openai.com/v1/vector_stores/{vectorStoreId}";

            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("Content-Type", "application/json");
                webClient.Headers.Add("Authorization", $"Bearer {ApiKey}");
                webClient.Headers.Add("OpenAI-Beta", "assistants=v2");

                webClient.UploadStringCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        HandleWebClientError(e.Error);
                        callback?.Invoke(null);
                        return;
                    }

                    try
                    {
                        var response = JObject.Parse(e.Result);
                        callback?.Invoke(response);
                    }
                    catch (Exception parseEx)
                    {
                        PrintError($"Error parsing API response: {parseEx.Message}");
                        callback?.Invoke(null);
                    }
                };

                if (!_uriCache.TryGetValue(url, out Uri uri))
                {
                    uri = new Uri(url);
                    _uriCache.Add(url, uri);
                }

                webClient.UploadStringAsync(uri, "DELETE", string.Empty);
            }
            catch (Exception ex)
            {
                PrintError($"Error initiating API request: {ex.Message}");
                callback?.Invoke(null);
            }
        }


# endregion

# region ImageGeneration API



#endregion

        private class PluginConfig
        {
            public OpenAI_Api_KeyConfig OpenAI_Api_Key { get; set; }
            public CompletionsModelConfig DefaultCompletionsModel { get; set; }
            public AssistantModelConfig DefaultAssistantModel { get; set; }

            public PluginConfig()
            {
                OpenAI_Api_Key = new OpenAI_Api_KeyConfig();
                DefaultCompletionsModel = new CompletionsModelConfig();
                DefaultAssistantModel = new AssistantModelConfig();
            }
        }

        private class OpenAI_Api_KeyConfig
        {
            [JsonProperty("OpenAI API Key")]
            public string ApiKey { get; set; }

            public OpenAI_Api_KeyConfig()
            {
                ApiKey = "your-api-key-here";
            }
        }

        private class CompletionsModelConfig
        {
            public string Model { get; set; }
            public int MaxTokens { get; set; }

            public CompletionsModelConfig()
            {
                Model = "gpt-4o";
                MaxTokens = 150;
            }
        }

        private class AssistantModelConfig
        {
            public string Model { get; set; }
            public int max_prompt_tokens { get; set; }
            public int max_completion_tokens { get; set; }

            public AssistantModelConfig()
            {
                Model = "gpt-4o";
                max_prompt_tokens = 150;
                max_completion_tokens = 150;
            }
        }


        private class OpenAIAgent
        {
            public string AgentName { get; set; }
            public object Payload { get; set; }
            public string AssistantId { get; set; }
        }

        private class AssistantPayload
        {
            public string model { get; set; }
            public ChatMessage[] messages { get; set; }
        }

        private class CompletionPayload
        {
            public string model { get; set; }
            public ChatMessage[] messages { get; set; }
            public int max_tokens { get; set; }
        }

        public class ChatMessage
        {
            public string role { get; set; }
            public string content { get; set; }
        }
    }
}