using SmartCaptureThem;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Capture_Them.HarmonyPatches;

[HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
public class Pawn_GuestTracker_SetGuestStatus
{
    public static void Postfix(Pawn ___pawn, GuestStatus guestStatus)
    {
        if (guestStatus != GuestStatus.Prisoner)
        {
            return;
        }

        if (___pawn.Map != null)
        {
            ___pawn.Map.designationManager.TryRemoveDesignationOn(___pawn, CaptureThemDefOf.CaptureThemCapture);
            return;
        }

        ___pawn.MapHeld?.designationManager.TryRemoveDesignationOn(___pawn, CaptureThemDefOf.CaptureThemCapture);
    }
}