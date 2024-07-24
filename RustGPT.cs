using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RustGPT", "Goo_", "2.0.0")]
    [Description("Simple chat application using OpenAI for Rust")]
    public class RustGPT : RustPlugin
    {
        [PluginReference]
        private Plugin OpenAI;

        private PluginConfig _config;
        private Dictionary<string, float> _lastUsageTime = new Dictionary<string, float>();

        private void Init()
        {
            LoadConfig();
            cmd.AddChatCommand("askgpt", this, nameof(AskGptCommand));
            permission.RegisterPermission("RustGPT.chat", this);
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
            catch
            {
                Puts("Error reading configuration file! Creating a new configuration file.");
                LoadDefaultConfig();
            }

            if (_config == null)
            {
                Puts("Configuration file is null! Creating a new configuration file.");
                LoadDefaultConfig();
            }
        }

        private void AskGptCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "RustGPT.chat"))
            {
                player.ChatMessage("<color=#ff0000>You do not have permission to use this command.</color>");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Usage: /askgpt <your question>");
                return;
            }

            if (!HasCooldownElapsed(player))
            {
                return;
            }

            var userMessage = string.Join(" ", args);

            var messages = new List<object>
            {
                new { role = "system", content = _config.DefaultContent },
                new { role = "user", content = userMessage }
            };

            player.ChatMessage("Sending your message to OpenAI...");

            OpenAI?.Call("Completions_SimpleChat", messages, (System.Action<JObject>)((response) =>
            {
                if (response != null && response["choices"] != null && response["choices"].HasValues)
                {
                    string GPT_Chat_Reply = response["choices"][0]["message"]["content"].ToString().Trim();
                    if (GPT_Chat_Reply.Length > 1200)
                    {
                        CreateNotesForResponse(player, GPT_Chat_Reply);
                    }
                    else
                    {
                        SendChatMessageInChunks(player, $"<color={_config.ReplyPrefixColor}>{_config.ReplyPrefix}</color> {GPT_Chat_Reply}", 250);
                    }
                }
                else
                {
                    player.ChatMessage("<color=#ff0000>Failed to get a valid response from OpenAI. Please try again later.</color>");
                }
            }));
        }

        private void SendChatMessageInChunks(BasePlayer player, string message, int chunkSize)
        {
            for (int i = 0; i < message.Length; i += chunkSize)
            {
                string chunk = message.Substring(i, Math.Min(chunkSize, message.Length - i));
                player.ChatMessage(chunk);
            }
        }

        private void CreateNotesForResponse(BasePlayer player, string response)
        {
            int noteCharacterLimit = 1000; // Set character limit for each note
            int totalNotes = (int)Math.Ceiling((double)response.Length / noteCharacterLimit);

            for (int i = 0; i < totalNotes; i++)
            {
                string noteContent = response.Substring(i * noteCharacterLimit, Math.Min(noteCharacterLimit, response.Length - (i * noteCharacterLimit)));
                CreateNoteForPlayer(player, noteContent);
            }
        }

        private void CreateNoteForPlayer(BasePlayer player, string content)
        {
            Item note = ItemManager.CreateByName("note", 1);
            note.text = content;
            note.MarkDirty();
            player.GiveItem(note);
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

        private class PluginConfig
        {
            public string DefaultContent { get; set; }
            public string ReplyPrefix { get; set; }
            public string ReplyPrefixColor { get; set; }
            public int CooldownInSeconds { get; set; }

            public PluginConfig()
            {
                DefaultContent = "You are a helpful assistant.";
                ReplyPrefix = "OpenAI";
                ReplyPrefixColor = "#ADD8E6";
                CooldownInSeconds = 30;
            }
        }
    }
}
