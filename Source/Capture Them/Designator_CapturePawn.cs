using RimWorld;
using UnityEngine;
using Verse;

namespace SmartCaptureThem;

public class Designator_CapturePawn : Designator
{
    public Designator_CapturePawn()
    {
        defaultLabel = "DesignatorCapturePawn".Translate();
        defaultDesc = "DesignatorCapturePawnDesc".Translate();
        icon = ContentFinder<Texture2D>.Get("CapturePawnGizmo");
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_Haul;
        hotKey = KeyBindingDefOf.Misc1;
    }

    protected override DesignationDef Designation => CaptureThemDefOf.CaptureThemCapture;

    public override int DraggableDimensions => 2;

    public override AcceptanceReport CanDesignateCell(IntVec3 loc)
    {
        if (!loc.InBounds(Map) || loc.Fogged(Map))
        {
            return false;
        }

        var firstPawn = loc.GetFirstPawn(Map);
        if (firstPawn == null || !firstPawn.RaceProps.Humanlike)
        {
            return "MessageMustDesignateDownedForeignPawn".Translate();
        }

        var result = CanDesignateThing(firstPawn);
        if (!result.Accepted)
        {
            return result;
        }

        return true;
    }

    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        if (Map.designationManager.DesignationOn(t, Designation) != null)
        {
            return false;
        }

        return t is Pawn { Downed: true } pawn && pawn.Faction != Faction.OfPlayer && !pawn.InBed() &&
               !pawn.IsPrisonerOfColony && pawn.RaceProps.Humanlike &&
               !IsBeyoundSaving(pawn); //Check if bleed out in less than certain time or is dieing from certain hediff
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        var thingList = c.GetThingList(Map);
        foreach (var thing in thingList)
        {
            if (thing is Pawn pawn)
            {
                DesignateThing(pawn);
            }
        }
    }

    public override void DesignateThing(Thing t)
    {
        var pawn = t as Pawn;
        if (pawn?.Faction != null && pawn.Faction != Faction.OfPlayer && !pawn.Faction.Hidden &&
            !pawn.Faction.HostileTo(Faction.OfPlayer) && !pawn.IsPrisonerOfColony && pawn.RaceProps.Humanlike)
        {
            Messages.Message("MessageCapturingWillAngerFaction".Translate(pawn.Named("PAWN")).AdjustedFor(pawn), pawn,
                MessageTypeDefOf.CautionInput, false);
        }

        Map.designationManager.RemoveAllDesignationsOn(t);
        Map.designationManager.AddDesignation(new Designation(t, Designation));
    }

    public bool IsBeyoundSaving(Pawn pawn)
    {
        bool canRevive = pawn.health.hediffSet.HasPreventsDeath;
        if (canRevive)
        {
#if DEBUG
            Log.Message(pawn.Name + "will revive, capture it regardless of hediff");
#endif
            return false;

        }
        bool willBleadingOut =  HealthUtility.TicksUntilDeathDueToBloodLoss(pawn) / 2500 < StartUp.settings.bleedoutMinHours;


        if (willBleadingOut)
        {
#if DEBUG
            Log.Message(pawn.Name + "will BleedOut in " + HealthUtility.TicksUntilDeathDueToBloodLoss(pawn) / 2500 + " hour(s), dont capture it because it is under the set threadhold " + minHours + ". Bleed rate: " + pawn.health.hediffSet.BleedRateTotal);
#endif
            return willBleadingOut;
        }
        else
        {
#if DEBUG
            Log.Message(pawn.Name + "will BleedOut in " + HealthUtility.TicksUntilDeathDueToBloodLoss(pawn) / 2500 + " hour(s), capture it. Bleed rate: " + pawn.health.hediffSet.BleedRateTotal);
#endif

        }

        if (StartUp.DeathRattle && StartUp.settings.giveUpMissingOrgan)
        {
            foreach(var hediff in pawn.health.hediffSet.hediffs)
            {
                if (StartUp.deathrattleHediffs.Contains(hediff.def.defName))
                {
                    return true;
                }
            };
        }

        return false;

    }
}

public class Designator_CapturePawn_FirstAid : Designator_CapturePawn
{
    public Designator_CapturePawn_FirstAid()
    {
        defaultLabel = "DesignatorCapturePawn_FirstAid".Translate();
        defaultDesc = "DesignatorCapturePawnDesc_FirstAid".Translate();
        icon = ContentFinder<Texture2D>.Get("CapturePawnGizmo_FA");
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_Haul;
        hotKey = KeyBindingDefOf.Misc1;
    }

    protected override DesignationDef Designation => CaptureThemDefOf.CaptureThemCapture_FirstAid;

}

public class Designator_CapturePawn_CE : Designator_CapturePawn
{
    public Designator_CapturePawn_CE()
    {
        defaultLabel = "DesignatorCapturePawn_CE".Translate();
        defaultDesc = "DesignatorCapturePawnDesc_CE".Translate();
        icon = ContentFinder<Texture2D>.Get("CapturePawnGizmo_CE");
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_Haul;
        hotKey = KeyBindingDefOf.Misc1;
    }

    protected override DesignationDef Designation => CaptureThemDefOf.CaptureThemCapture_CE;

}