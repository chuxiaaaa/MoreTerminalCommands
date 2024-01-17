using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                if (item.name.Contains("Turret") && GetDistance(item.transform.position,__instance.transform.position) < 30)
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
}
