using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System;
using System.Net;
using UnityEngine;
using System.ComponentModel;
using System.Runtime.Remoting.Channels;
using System.Linq;
using System.Security.Policy;

namespace Oxide.Plugins
{
    [Info("AIDeathMessages", "Goo_", "1.0")]
    [Description("Generates death messages using OpenAI")]
    public class AIDeathMessages : RustPlugin
    {
        [PluginReference]
        private Plugin OpenAI;

        private PluginConfig _config;

        private void Init()
        {
            LoadConfig();
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

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null)
            {
                Puts("Entity is null");
                return;
            }

            if (info == null)
            {
                Puts("HitInfo is null");
                return;
            }

            BasePlayer victim = entity.ToPlayer();
            if (victim == null)
            {
                Puts("Victim is null or not a BasePlayer");
                return;
            }

            BasePlayer attacker = info.InitiatorPlayer;
            if (attacker == null)
            {
                Puts("Attacker is null or not a BasePlayer");
                return;
            }

            if (_config == null)
            {
                Puts("Config is null");
                return;
            }

            if (OpenAI == null)
            {
                Puts("OpenAI plugin reference is null");
                return;
            }

            if (!(victim is NPCPlayer) && !(attacker is NPCPlayer))
            {
                string attackerName = StripRichText(attacker.displayName);
                string victimName = StripRichText(victim.displayName);

                string weaponName = GetWeaponDisplayName(attacker);
                string hitBone = GetHitBone(info);

                string deathMessage = $"{attackerName} killed {victimName} with a {weaponName} in the {hitBone}";

                var message = new List<object>
                {
                    new { role = "system", content = _config.SystemContent },
                    new { role = "user", content = deathMessage }
                };

                OpenAI.Call("Completions_SimpleChat", message, (System.Action<JObject>)((response) =>
                {
                    if (response != null && response["choices"] != null && response["choices"].HasValues)
                    {
                        string aiResponse = response["choices"][0]["message"]["content"].ToString().Trim();
                        PrintToChat(aiResponse);
                    }
                }));
            }
        }



        private string StripRichText(string text)
        {
            return Regex.Replace(text, "<.*?>", String.Empty);
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

        private string GetHitBone(HitInfo info)
        {
            if (info.HitEntity == null || info.HitEntity.ToPlayer() == null) return "";

            string hitBone;

            BaseCombatEntity hitEntity = info.HitEntity as BaseCombatEntity;

            SkeletonProperties.BoneProperty boneProperty = hitEntity.skeletonProperties?.FindBone(info.HitBone);

            string bone = boneProperty?.name?.english ?? "";

            return bone;
        }

        private class PluginConfig
        {
            public string SystemContent { get; set; }

            public PluginConfig()
            {
                SystemContent = "You are a color commentator for a TV show that where contestants kill each other. You are tasked with describing the deaths of players in the game. Be rude and use profanity.";
            }
        }
    }
}

