using SmartCaptureThem;
using HarmonyLib;
using Verse;

namespace SmartCaptureThem.HarmonyPatches;

[HarmonyPatch(typeof(ReverseDesignatorDatabase), "InitDesignators")]
public class ReverseDesignatorDatabase_InitDesignators
{
    public static void Postfix(ref ReverseDesignatorDatabase __instance)
    {
        __instance.AllDesignators.Add(new Designator_CapturePawn());

    }
}


[HarmonyPatch(typeof(ReverseDesignatorDatabase), "InitDesignators")]
public class ReverseDesignatorDatabase_InitDesignators_CE
{
    public static void Postfix(ref ReverseDesignatorDatabase __instance)
    {
        __instance.AllDesignators.Add(new Designator_CapturePawn_CE());
    }
}

[HarmonyPatch(typeof(ReverseDesignatorDatabase), "InitDesignators")]
public class ReverseDesignatorDatabase_InitDesignators_FirstAid
{
    public static void Postfix(ref ReverseDesignatorDatabase __instance)
    {

        __instance.AllDesignators.Add(new Designator_CapturePawn_FirstAid());

    }
}