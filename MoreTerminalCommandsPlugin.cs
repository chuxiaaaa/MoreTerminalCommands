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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace MoreTerminalCommands
{
    [BepInDependency(LethalAPI.LibTerminal.PluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("cn.chuxiaaaa.plugin.MoreTerminalCommands", "MoreTerminalCommands", "1.0.8")]
    public class MoreTerminalCommandsPlugin : BaseUnityPlugin
    {
        private TerminalModRegistry Commands;

        public static ManualLogSource ManualLog = null;

        public static Dictionary<int, int> YRots = new Dictionary<int, int>();

        public static bool Debug { get; set; }

        ConfigEntry<string> LangugeConfig;

        void Awake()
        {
            try
            {
                LangugeConfig = Config.Bind("config", "languge", "en_US", "mod languge");
                Harmony.CreateAndPatchAll(typeof(TerminalAccessibleObjectPatch), "cn.chuxiaaaa.plugin.MoreTerminalCommands");
                Harmony.CreateAndPatchAll(typeof(PlayerControllerBPatch3), "cn.chuxiaaaa.plugin.MoreTerminalCommands");
                Harmony.CreateAndPatchAll(typeof(PlayerControllerBPatch2), "cn.chuxiaaaa.plugin.MoreTerminalCommands");
                Harmony.CreateAndPatchAll(typeof(PlayerControllerBPatch), "cn.chuxiaaaa.plugin.MoreTerminalCommands");
                LocalizationManager.SetLanguage(LangugeConfig.Value);
                Commands = TerminalRegistry.CreateTerminalRegistry();
                Commands.RegisterFrom(this);
                ManualLog = Logger;
                Logger.LogInfo("More Terminal Commands Loaded!");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
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

        /// <summary>
        /// 字符串转Vector3
        /// </summary>
        /// <param name="p_sVec3">需要转换的字符串</param>
        /// <returns></returns>
        public static Vector3 GetVec3ByString(string p_sVec3)
        {
            if (p_sVec3.Length <= 0)
                return Vector3.zero;

            string[] tmp_sValues = p_sVec3.Trim(' ').Split(',');
            if (tmp_sValues != null && tmp_sValues.Length == 3)
            {
                float tmp_fX = float.Parse(tmp_sValues[0]);
                float tmp_fY = float.Parse(tmp_sValues[1]);
                float tmp_fZ = float.Parse(tmp_sValues[2]);

                return new Vector3(tmp_fX, tmp_fY, tmp_fZ);
            }
            return Vector3.zero;
        }

        [TerminalCommand("Debug"), CommandInfo("Layout Debug")]
        public string DebugCommand()
        {
            Debug = !Debug;
            return Debug.ToString();
        }

        [TerminalCommand("Load"), CommandInfo("Load Layout From File")]
        public string Load(string name)
        {
            Dictionary<string, int> objs = new Dictionary<string, int>();
            var go = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
                      where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
                      select obj).ToList<GrabbableObject>();
            var lines = File.ReadAllLines($"{name}.txt").ToList();
            var lp = GameNetworkManager.Instance.localPlayerController;
            float carryWeight = lp.carryWeight;
            var localEulerAngles = lp.transform.localEulerAngles;
            Logger.LogInfo("carryWeight:" + carryWeight);
            if (lp.currentlyHeldObjectServer != null)
            {
                lp.DiscardHeldObject(false, null, new Vector3(0, 0, 0), true);
            }
            try
            {
                foreach (var grabbableObject in go)
                {
                    if (!grabbableObject.isHeld && !grabbableObject.isPocketed)
                    {
                        int index = 1;
                        if (!objs.ContainsKey(grabbableObject.itemProperties.itemName))
                        {
                            objs.Add(grabbableObject.itemProperties.itemName, 1);
                        }
                        index = objs[grabbableObject.itemProperties.itemName];
                        Logger.LogInfo("index:" + index);
                        var line = lines.FindIndex(x => x.StartsWith($"{grabbableObject.itemProperties.itemName}{index}="));
                        string[] v = null;
                        if (line > -1)
                        {
                            v = lines[line].Split('=')[1].Split('|');
                            objs[grabbableObject.itemProperties.itemName]++;
                        }
                        else
                        {
                            line = lines.FindIndex(x => x.StartsWith($"{grabbableObject.itemProperties.itemName}-1="));
                            if (line > -1)
                            {
                                v = lines[line].Split('=')[1].Split('|');
                            }
                            else
                            {
                                line = lines.FindIndex(x => x.StartsWith($"Other="));
                                if (line > -1)
                                {
                                    v = lines[line].Split('=')[1].Split('|');
                                }
                            }
                        }
                        if (v != null && v.Length > 0)
                        {
                            Logger.LogInfo("v[0]:" + v[0] + "|v[1]:" + v[1]);
                            Vector3 point = GetVec3ByString(v[0]);
                            if (grabbableObject.gameObject.transform.position == point)
                            {
                                continue;
                            }
                            grabbableObject.gameObject.transform.position = point;
                            var yrot = int.Parse(v[1]);
                            grabbableObject.floorYRot = (int)yrot;
                            grabbableObject.targetFloorPosition = GetVec3ByString(v[1]);
                            int key = grabbableObject.gameObject.GetComponent<NetworkObject>().GetInstanceID();
                            Logger.LogInfo("key:" + key + "," + yrot);
                            YRots.Add(key, (int)yrot);
                            grabbableObject.playerHeldBy = lp;
                            lp.currentlyHeldObjectServer = grabbableObject;
                            grabbableObject.EquipItem();
                            lp.DiscardHeldObject(false, null, new Vector3(0, 0, 0), true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                while (ex != null)
                {
                    Logger.LogInfo(ex.ToString());
                    ex = ex.InnerException;
                }
            }
            lp.transform.localEulerAngles = localEulerAngles;
            lp.carryWeight = carryWeight;
            return "ok!";
        }

        [TerminalCommand("Save"), CommandInfo("Save Layout To File")]
        public string Save(string name)
        {
            var go = (from obj in GameObject.Find("/Environment/HangarShip").GetComponentsInChildren<GrabbableObject>()
                      where obj.name != "ClipboardManual" && obj.name != "StickyNoteItem"
                      select obj).ToList<GrabbableObject>();
            StringBuilder sb = new StringBuilder();
            foreach (var item in go)
            {
                sb.AppendLine($"{item.itemProperties.itemName}={PositionToString(item.transform.position)}|{item.transform.localEulerAngles.y}");
            }
            var sbl = sb.ToString().Split('\n').ToList();
            sbl.Sort();
            File.WriteAllLines(name, sbl.ToArray());
            return "ok!";
        }

        public static string PositionToString(Vector3 postion)
        {
            return postion.ToString().Replace("(", "").Replace(")", "");
        }

        [TerminalCommand("dm"), CommandInfo("查看所有机关(机枪、地雷、机械门)代码")]
        public string dm()
        {
            return Code();
        }

        [TerminalCommand("Code"), CommandInfo("List the codes of turrets, mines, and mechanical doors")]
        public string Code()
        {
            StringBuilder sb = new StringBuilder();
            TerminalAccessibleObject[] array = UnityEngine.Object.FindObjectsOfType<TerminalAccessibleObject>();
            for (int i = 0; i < array.Length; i++)
            {
                sb.AppendLine($"{array[i].objectCode}|{LocalizationManager.GetString(array[i].name)}");
            }
            return sb.ToString();
        }

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

        [TerminalCommand("dr"), CommandInfo("探测设施内的敌人(冷却时间120秒)")]
        public string dr()
        {
            return Enemy();
        }

        [TerminalCommand("Enemy"), CommandInfo("Detect enemies within the facility, with a cooldown period of 120 seconds")]
        public string Enemy()
        {
            if (cooldown.AddSeconds(120) > DateTime.Now)
            {
                return $"[{LocalizationManager.GetString("Cooldown")}: {(cooldown.AddSeconds(120) - DateTime.Now).TotalSeconds} {LocalizationManager.GetString("sec")}.]";
            }
            List<EnemyAI> enemies = new List<EnemyAI>();
            CollectObjectsOfType(enemies);
            StringBuilder sb = new StringBuilder();
            Dictionary<string, int> dics = new Dictionary<string, int>();
            foreach (var item in enemies)
            {
                if (!item.isOutside && !item.isEnemyDead)
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

        [TerminalCommand("kd"), CommandInfo("打开飞船的灯(只能打开，因为我不想看到来自终端的灯光秀)")]
        public string kd()
        {
            return LightOn();
        }

        [TerminalCommand("km"), CommandInfo("打开或关闭飞船舱门(根据当前飞船舱门的状态决定)")]
        public string km()
        {
            return ShipDoor();
        }

        [TerminalCommand("wk", true), CommandInfo("将监视器切换到工作中的同事(在工厂内部的同事)")]
        public string wk()
        {
            return Work();
        }

        [TerminalCommand("Work",true), CommandInfo("Switch the monitor to colleagues who are working(colleagues in the factory)")]
        public string Work()
        {
            return WorkMethod(false);
        }

        private string WorkMethod(bool callFromSelf)
        {
            Logger.LogInfo(StartOfRound.Instance.mapScreen.radarTargets.Count);
            if (StartOfRound.Instance.mapScreen.radarTargets.Count > 1)
            {
                var @switch = false;
                for (int i = StartOfRound.Instance.mapScreen.targetTransformIndex + 1; i < StartOfRound.Instance.mapScreen.radarTargets.Count; i++)
                {
                    if (!StartOfRound.Instance.mapScreen.radarTargets[i].isNonPlayer)
                    {
                        var pl = StartOfRound.Instance.mapScreen.radarTargets[i].transform.gameObject.GetComponent<PlayerControllerB>();
                        if (pl != null && pl.isPlayerControlled && pl.isInsideFactory)
                        {
                            @switch = true;
                            StartOfRound.Instance.mapScreen.SwitchRadarTargetAndSync(i);
                            break;
                        }
                    }
                }
                if (!@switch)
                {
                    StartOfRound.Instance.mapScreen.targetTransformIndex = 0;
                    if (!callFromSelf)
                    {
                        WorkMethod(true);
                    }
                    else
                    {
                        StartOfRound.Instance.mapScreen.SwitchRadarTargetAndSync(StartOfRound.Instance.mapScreen.targetTransformIndex);
                    }
                }
            }
            else
            {
                StartOfRound.Instance.mapScreen.SwitchRadarTargetAndSync(0);
            }
            return "[" + LocalizationManager.GetString("Switch") + "]";
        }

        [TerminalCommand("LightOn"), CommandInfo("Turn On The Light")]
        public string LightOn()
        {
            UnityEngine.Object.FindObjectOfType<ShipLights>().SetShipLightsServerRpc(true);
            return $"[{LocalizationManager.GetString("ShipLight")}:{LocalizationManager.GetString("On")}]";
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

        [TerminalCommand("zj"), CommandInfo("计算当前飞船内的物品价值")]
        public string zj()
        {
            return ShipLoot();
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

        [TerminalCommand("pl"), CommandInfo("显示玩家列表(死亡、是否在飞船)")]
        public string pl()
        {
            return Player();
        }

        [TerminalCommand("Player"), CommandInfo("Show the players info(death or inship)")]
        public string Player()
        {
            List<PlayerControllerB> players = new List<PlayerControllerB>();
            CollectObjectsOfType(players);
            players = players.Where(x => !x.disconnectedMidGame &&
                    !x.playerUsername.Contains("Player #")).ToList();
            int inShip = 0;
            int death = 0;
            int num = players.Count;
            foreach (var item in players)
            {
                if (item.isPlayerDead)
                {
                    death++;
                }
                else if (item.isInHangarShipRoom)
                {
                    inShip++;
                }
            }
            return $"[{LocalizationManager.GetString("Players")}]{Environment.NewLine}[{LocalizationManager.GetString("inShip")}]: {inShip}/{num}{Environment.NewLine}[{LocalizationManager.GetString("death")}]: {death}/{num}";
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
