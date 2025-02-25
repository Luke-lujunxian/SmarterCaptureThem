using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Capture_Them.HarmonyPatches;
using HarmonyLib;
using SmartCaptureThem.HarmonyPatches;
using Verse;

namespace SmartCaptureThem;

[StaticConstructorOnStartup]
public static class StartUp
{
    public static bool CE = false;
    public static bool FirstAid = false;
    public static bool DeathRattle = false;
    public static JobDef CP_FirstAid;
    public static JobDef CEStablize;
    public static HashSet<String> deathrattleHediffs;

    static StartUp()
    {
        var harmony = new Harmony("SmartCaptureThem.patch");
        harmony.Patch(AccessTools.Method(typeof(ReverseDesignatorDatabase), "InitDesignators"), postfix: new HarmonyMethod(typeof(ReverseDesignatorDatabase_InitDesignators), nameof(ReverseDesignatorDatabase_InitDesignators.Postfix)));

        try
        {
            ((Action)(() =>
            {
                foreach (var x in LoadedModManager.RunningModsListForReading)
                {
                    if (x.PackageId == ("rh2.bcds.first.aid"))
                    {
                        FirstAid = true;
                        var info1 = harmony.Patch(AccessTools.Method(typeof(ReverseDesignatorDatabase), "InitDesignators"), postfix: new HarmonyMethod(typeof(ReverseDesignatorDatabase_InitDesignators_FirstAid), nameof(ReverseDesignatorDatabase_InitDesignators_FirstAid.Postfix)));

#if DEBUG
                        
                        Log.Message("First Aid mod detected");
                        Log.Message(info1.ToString());
#endif
                    }
                    else if (x.PackageId == ("ceteam.combatextended"))
                    {
                        CE = true;
                        var info2 = harmony.Patch(AccessTools.Method(typeof(ReverseDesignatorDatabase), "InitDesignators"), postfix: new HarmonyMethod(typeof(ReverseDesignatorDatabase_InitDesignators_CE), nameof(ReverseDesignatorDatabase_InitDesignators_CE.Postfix)));

#if DEBUG
                        Log.Message("CE mod detected");
                        Log.Message(info2.ToString());

#endif
                    }
                    else if(x.PackageId == ("troopersmith1.deathrattle"))
                    {
                        DeathRattle = true;
                        deathrattleHediffs = ["IntestinalFailure", "LiverFailure", "KidneyFailure", "ClinicalDeathNoHeartbeat", "ClinicalDeathAsphyxiation"];
#if DEBUG
    Log.Message("Death Rattle mod detected");
#endif
                    }
                }

            }))();
        }
        catch (TypeLoadException ex) { Log.Error("[WOD] Error when patching VanillaTradingExpanded" + ex); }
    }
}