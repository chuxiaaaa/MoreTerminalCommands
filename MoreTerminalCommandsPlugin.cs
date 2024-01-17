using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalAPI.LibTerminal;
using LethalAPI.LibTerminal.Attributes;
using LethalAPI.LibTerminal.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MoreTerminalCommands
{
    [BepInPlugin("cn.chuxiaaaa.plugin.MoreTerminalCommands", "MoreTerminalCommands", "1.0.2")]
    public class MoreTerminalCommandsPlugin : BaseUnityPlugin
    {
        private TerminalModRegistry Commands;

        public static ManualLogSource ManualLog = null;

        ConfigEntry<string> LangugeConfig;

        void Start()
        {
            LangugeConfig = Config.Bind("config", "languge", "en_US", "mod languge");
            LocalizationManager.SetLanguage(LangugeConfig.Value);
            Commands = TerminalRegistry.CreateTerminalRegistry();
            Commands.RegisterFrom(this);
            ManualLog = Logger;
            Logger.LogInfo("More Terminal Commands Loaded!");
        }

        private static float CalculateLootValue()
        {
            List<GrabbableObject> list = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
                                          where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
                                          select obj).ToList<GrabbableObject>();
            return (float)list.Sum((GrabbableObject scrap) => scrap.scrapValue);
        }


        public void CollectObjectsOfType<T>(List<T> list, Predicate<T> predicate = null) where T : MonoBehaviour
        {
            foreach (var obj in UnityEngine.Object.FindObjectsOfType<T>())
            {
                if (predicate == null || predicate(obj))
                {
                    list.Add(obj);
                }
            }
        }

        public List<EnemyAI> enemies = new List<EnemyAI>();
        public DateTime cooldown { get; set; }

        [TerminalCommand("Lang"), CommandInfo("Set Plugin Languge")]
        public string lang(string languge)
        {
            languge = languge.ToLower();
            if (languge != "en_us" && languge != "zh_cn")
            {
                return "unknow languge:" + languge;
            }
            LangugeConfig.Value = languge;
            LocalizationManager.SetLanguage(LangugeConfig.Value);
            Config.Save();
            return $"{LocalizationManager.GetString("Lang")}:" + languge;
        }

        [TerminalCommand("Enemy"), CommandInfo("Detect enemies within the facility, with a cooldown period of 120 seconds")]
        public string Enemy()
        {
            if (cooldown.AddSeconds(120) > DateTime.Now)
            {
                return $"[{LocalizationManager.GetString("Cooldown")}: {(cooldown.AddSeconds(120) - DateTime.Now).TotalSeconds} {LocalizationManager.GetString("sec")}.]";
            }
            enemies.Clear();
            CollectObjectsOfType(enemies);
            StringBuilder sb = new StringBuilder();
            Dictionary<string, int> dics = new Dictionary<string, int>();
            foreach (var item in enemies)
            {
                if (!item.isOutside)
                {
                    if (dics.ContainsKey(item.enemyType.enemyName))
                    {
                        dics[item.enemyType.enemyName]++;
                    }
                    else
                    {
                        dics.Add(item.enemyType.enemyName, 1);
                    }
                }
            }
            if (dics.Count > 0)
            {
                sb.AppendLine($"[" + LocalizationManager.GetString("Enemies") + "]");
                foreach (var item in dics)
                {
                    sb.AppendLine(LocalizationManager.TryGetString("enemy_", item.Key) + ":" + item.Value);
                }
                sb.AppendLine();
                sb.Append($"{LocalizationManager.GetString("enemy_tip1")}");
            }
            else
            {
                sb.Append($"{LocalizationManager.GetString("enemy_tip2")}");
            }
            cooldown = DateTime.Now;
            return sb.ToString();
        }


        [TerminalCommand("LightOn"), CommandInfo("Turn On The Light")]
        public string LightOn()
        {
            UnityEngine.Object.FindObjectOfType<ShipLights>().SetShipLightsServerRpc(true);
            return $"[{LocalizationManager.GetString("ShipLight")}:On]";
        }

        [TerminalCommand("ShipDoor"), CommandInfo("Open or Close the Ship Door")]
        public string ShipDoor()
        {
            var shipdoor = UnityEngine.Object.FindObjectOfType<HangarShipDoor>().gameObject;
            string animation = "";
            if (StartOfRound.Instance.hangarDoorsClosed)
            {
                animation = "OpenDoor";
            }
            else
            {
                animation = "CloseDoor";
            }
            shipdoor.GetComponentsInChildren<AnimatedObjectTrigger>()
            .First(trigger => trigger.animationString == animation)?
            .GetComponentInParent<InteractTrigger>().onInteract.Invoke(GameNetworkManager.Instance?.localPlayerController);
            return $"[{LocalizationManager.GetString("ShipDoor")}:{(!StartOfRound.Instance.hangarDoorsClosed ? LocalizationManager.GetString("Close") : LocalizationManager.GetString("Open"))}]";
        }

        private string TeleportMethod(bool Inverse)
        {
            ShipTeleporter[] shipTeleporters = UnityEngine.Object.FindObjectsOfType<ShipTeleporter>();
            ShipTeleporter shipTeleporter = shipTeleporters.Where(x => x.isInverseTeleporter == Inverse).FirstOrDefault();
            if (shipTeleporter == null)
            {
                return $"[Has 0 {(Inverse ? LocalizationManager.GetString("Inverse") : "")}{LocalizationManager.GetString("Teleporter")}]";
            }
            var cooldownTime = (float)typeof(ShipTeleporter).GetField("cooldownTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(shipTeleporter);
            if (cooldownTime > 0)
            {
                return $"[{LocalizationManager.GetString("Cooldown")}: {cooldownTime} {LocalizationManager.GetString("sec")}.]";
            }
            shipTeleporter.PressTeleportButtonOnLocalClient();
            return $"[{LocalizationManager.GetString("Teleport")}]";
        }

        [TerminalCommand("ShipLoot"), CommandInfo("Calculate Loot Value In Ship")]
        public string ShipLoot()
        {
            return $"[{LocalizationManager.GetString("ShipLoot")}:{CalculateLootValue()}]";
        }

        [TerminalCommand("Teleport"), CommandInfo("Teleport the player displayed on the current monitor")]
        public string Teleport()
        {
            return TeleportMethod(false);
        }


        [TerminalCommand("tp"), CommandInfo("teleport the player displayed on the current monitor")]
        public string tp()
        {
            return Teleport();
        }

        [TerminalCommand("Teleport2"), CommandInfo("Inverse Teleporter")]
        public string Teleport2()
        {
            return TeleportMethod(true);
        }

        [TerminalCommand("tp2"), CommandInfo("Inverse Teleporter")]
        public string tp2()
        {
            return Teleport2();
        }
    }


}
