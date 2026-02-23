using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace TBCTackleboxMap;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    internal static Map Map;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;

        Harmony.CreateAndPatchAll(typeof(Plugin));
        
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    [HarmonyPatch(typeof(DebugMenu), nameof(DebugMenu.ManagedOnEnable))]
    [HarmonyPrefix]
    static void RegisterMap(ref DebugMenu __instance)
    {
        Map = __instance._gameObject.AddComponent<Map>();
        Map._imgui = __instance._imgui;
        Manager.GetBehaviourUpdater().MarkForRegister(Map);
    }
    
    [HarmonyPatch(typeof(DebugMenu), nameof(DebugMenu.ManagedUpdate))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> DontDisableImGui(IEnumerable<CodeInstruction> instructions)
    {
        // replace the second load of the flag with a true load
        var index = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.IsLdloc())
            {
                if (index == 1)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                }
                else
                {
                    yield return instruction;
                }

                index++;
            }
            else
            {
                yield return instruction;
            }
        }
    }
}
