using SmartCaptureThem;
using HarmonyLib;
using Verse;

namespace Capture_Them.HarmonyPatches;

[HarmonyPatch(typeof(Pawn_HealthTracker), "MakeUndowned")]
public class Pawn_HealthTracker_MakeUndowned
{
    public static void Prefix(Pawn ___pawn)
    {
        if (___pawn is { Map: not null })
        {
            ___pawn.Map.designationManager.TryRemoveDesignationOn(___pawn, CaptureThemDefOf.CaptureThemCapture);
            ___pawn.Map.designationManager.TryRemoveDesignationOn(___pawn, CaptureThemDefOf.CaptureThemCapture_CE);
            ___pawn.Map.designationManager.TryRemoveDesignationOn(___pawn, CaptureThemDefOf.CaptureThemCapture_FirstAid);
        }
    }
}