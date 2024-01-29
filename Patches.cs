using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace MoreTerminalCommands
{
    [HarmonyPatch(typeof(TerminalAccessibleObject), "CallFunctionFromTerminal")]
    public class TerminalAccessibleObjectPatch
    {
        public static bool call { get; set; } = false;

        public static void Postfix(TerminalAccessibleObject __instance)
        {
            if (call)
            {
                return;
            }
            TerminalAccessibleObject[] array = UnityEngine.Object.FindObjectsOfType<TerminalAccessibleObject>();
            foreach (var item in array)
            {
                if (item.name.Contains("Turret") && GetDistance(item.transform.position, __instance.transform.position) < 10)
                {
                    call = true;
                    item.CallFunctionFromTerminal();
                    call = false;
                }
            }
        }

        public static float GetDistance(Vector3 pos1, Vector3 pos2)
        {
            return (float)Math.Round((double)Vector3.Distance(pos1, pos2));
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "DiscardHeldObject")]
    public class PlayerControllerBPatch
    {
        public static bool Prefix(PlayerControllerB __instance, bool placeObject, NetworkObject parentObjectTo, Vector3 placePosition, bool matchRotationOfParent)
        {
            MoreTerminalCommandsPlugin.ManualLog.LogInfo($"DiscardHeldObject|placeObject:{placeObject}|parentObjectTo:{parentObjectTo}|placePosition:{placePosition}|matchRotationOfParent:{matchRotationOfParent}");
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "SetObjectAsNoLongerHeld")]
    public class PlayerControllerBPatch3
    {
        public static bool Prefix(bool droppedInElevator, bool droppedInShipRoom, Vector3 targetFloorPosition, GrabbableObject dropObject, ref int floorYRot)
        {

            var id = dropObject.GetComponent<NetworkObject>().GetInstanceID();
            MoreTerminalCommandsPlugin.ManualLog.LogInfo("SetObjectAsNoLongerHeld:" + id);
            if (MoreTerminalCommandsPlugin.YRots.ContainsKey(id))
            {
                floorYRot = MoreTerminalCommandsPlugin.YRots[id];
                MoreTerminalCommandsPlugin.ManualLog.LogInfo("set floorYRot:" + floorYRot);
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "ThrowObjectServerRpc")]
    public class PlayerControllerBPatch2
    {
        public static bool Prefix(NetworkObjectReference grabbedObject, bool droppedInElevator, bool droppedInShipRoom, Vector3 targetFloorPosition, ref int floorYRot)
        {
            if (grabbedObject.TryGet(out var networkObject, null))
            {
                var id = networkObject.GetInstanceID();
                MoreTerminalCommandsPlugin.ManualLog.LogInfo("ThrowObjectServerRpc:" + id);
                if (MoreTerminalCommandsPlugin.YRots.ContainsKey(id))
                {
                    floorYRot = MoreTerminalCommandsPlugin.YRots[id];
                    MoreTerminalCommandsPlugin.ManualLog.LogInfo("set floorYRot:" + floorYRot);
                    MoreTerminalCommandsPlugin.YRots.Remove(id);
                }
            }
            MoreTerminalCommandsPlugin.ManualLog.LogInfo($"ThrowObjectServerRpc|grabbedObject:{grabbedObject}|droppedInElevator:{droppedInElevator}|droppedInShipRoom:{droppedInShipRoom}|targetFloorPosition:{targetFloorPosition}|floorYRot:{floorYRot}");
            return true;
        }

        public static void Postfix(PlayerControllerB __instance, NetworkObjectReference grabbedObject, bool droppedInElevator, bool droppedInShipRoom, Vector3 targetFloorPosition, int floorYRot)
        {
            if (MoreTerminalCommandsPlugin.Debug && __instance.playerClientId == StartOfRound.Instance.localPlayerController.playerClientId)
            {
                if (grabbedObject.TryGet(out var networkObject, null))
                {
                    typeof(HUDManager).GetMethod("AddChatMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(HUDManager.Instance, new object[]{
                        MoreTerminalCommandsPlugin.PositionToString(networkObject.transform.position)+"|"+floorYRot,
                        networkObject.gameObject.name
                    });
                }
            }
        }
    }


}
