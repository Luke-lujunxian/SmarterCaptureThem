using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Capture_Them.HarmonyPatches;
using HarmonyLib;
using RimWorld;
using SmartCaptureThem.HarmonyPatches;
using UnityEngine;
using Verse;

namespace SmartCaptureThem;

[StaticConstructorOnStartup]
public class StartUp : Mod
{
    public static bool CE = false;
    public static bool FirstAid = false;
    public static bool DeathRattle = false;
    public static JobDef CP_FirstAid;
    public static JobDef CEStablize;
    public static HashSet<String> deathrattleHediffs;
    public static SmartCaptureThemSettings settings;

    public StartUp(ModContentPack content) : base(content)
    {
        var harmony = new Harmony("SmartCaptureThem.patch");
        harmony.Patch(AccessTools.Method(typeof(ReverseDesignatorDatabase), "InitDesignators"), postfix: new HarmonyMethod(typeof(ReverseDesignatorDatabase_InitDesignators), nameof(ReverseDesignatorDatabase_InitDesignators.Postfix)));
        harmony.Patch(AccessTools.Method(typeof(Pawn_HealthTracker), "MakeUndowned"), postfix: new HarmonyMethod(typeof(Pawn_HealthTracker_MakeUndowned), nameof(Pawn_HealthTracker_MakeUndowned.Prefix)));
        harmony.Patch(AccessTools.Method(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus)), postfix: new HarmonyMethod(typeof(Pawn_GuestTracker_SetGuestStatus), nameof(Pawn_GuestTracker_SetGuestStatus.Postfix)));

        settings = GetSettings<SmartCaptureThemSettings>();

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
if (StartUp.settings.debug) { 
                        
                        Log.Message("First Aid mod detected");
                        Log.Message(info1.ToString());
}
                    }
                    else if (x.PackageId == ("ceteam.combatextended"))
                    {
                        CE = true;
                        var info2 = harmony.Patch(AccessTools.Method(typeof(ReverseDesignatorDatabase), "InitDesignators"), postfix: new HarmonyMethod(typeof(ReverseDesignatorDatabase_InitDesignators_CE), nameof(ReverseDesignatorDatabase_InitDesignators_CE.Postfix)));

if (StartUp.settings.debug) { 
                        Log.Message("CE mod detected");
                        Log.Message(info2.ToString());

}
                    }
                    else if(x.PackageId == ("troopersmith1.deathrattle"))
                    {
                        DeathRattle = true;
                        deathrattleHediffs = ["IntestinalFailure", "LiverFailure", "KidneyFailure", "ClinicalDeathNoHeartbeat", "ClinicalDeathAsphyxiation"];
if (StartUp.settings.debug) { 
    Log.Message("Death Rattle mod detected");
}
                    }
                }

            }))();
        }
        catch (TypeLoadException ex) { Log.Error("[SmarterCaptureThem] Error when patching: " + ex); }
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);
        listingStandard.Label("bleedoutMinHoursSetting".Translate());
        settings.bleedoutMinHours = listingStandard.SliderLabeled($"{settings.bleedoutMinHours:F1} h", settings.bleedoutMinHours, 0, 24, 0.2f);
        listingStandard.Label("maxBleedoutFirstAid".Translate());
        settings.maxBleedoutFirstAid = listingStandard.SliderLabeled($"{settings.maxBleedoutFirstAid:F1} h", settings.maxBleedoutFirstAid, 1, 24, 0.2f);
        listingStandard.CheckboxLabeled("checkForDangerSetting".Translate(), ref settings.checkForDanger, "checkForDangerSettingDesc".Translate());
        listingStandard.CheckboxLabeled("doVanillaTendSetting".Translate(), ref settings.doVanillaTend, "doVanillaTendSettingDesc".Translate());
        if (DeathRattle)
            listingStandard.CheckboxLabeled("giveUpMissingOrganSetting".Translate(), ref settings.giveUpMissingOrgan, "giveUpMissingOrganSettingDesc".Translate());
        listingStandard.CheckboxLabeled("Debug", ref settings.debug, "Log will spam!");

        listingStandard.End();

        base.DoSettingsWindowContents(inRect);
    }

    /// <summary>
    /// Override SettingsCategory to show up in the list of settings.
    /// Using .Translate() is optional, but does allow for localisation.
    /// </summary>
    /// <returns>The (translated) mod name.</returns>
    public override string SettingsCategory()
    {
        return "SmarterCaptureThem".Translate();
    }
}

public class SmartCaptureThemSettings : ModSettings
{
    /// <summary>
    /// The three settings our mod has.
    /// </summary>
    public float bleedoutMinHours = 1f;
    public float maxBleedoutFirstAid = 6f;
    public bool giveUpMissingOrgan = true;
    public bool checkForDanger = true;
    public bool doVanillaTend = false;
    public bool debug = false;

    /// <summary>
    /// The part that writes our settings to file. Note that saving is by ref.
    /// </summary>
    public override void ExposeData()
    {
        Scribe_Values.Look(ref bleedoutMinHours, "bleedoutMinHours", 1f);
        Scribe_Values.Look(ref maxBleedoutFirstAid, "maxBleedoutFirstAid", 6f);
        Scribe_Values.Look(ref giveUpMissingOrgan, "giveUpMissingOrgan", true);
        Scribe_Values.Look(ref checkForDanger, "checkForDanger", true);
        Scribe_Values.Look(ref doVanillaTend, "doVanillaTent", false);
        Scribe_Values.Look(ref debug, "debug", false);

        base.ExposeData();
    }
}