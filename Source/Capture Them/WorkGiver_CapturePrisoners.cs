using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SmartCaptureThem;

public class WorkGiver_CapturePrisoners : WorkGiver_RescueDowned
{
    protected JobDef Job => JobDefOf.Capture;

    protected DesignationDef Designation => CaptureThemDefOf.CaptureThemCapture;

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(Designation);
    }

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        foreach (var designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(Designation))
        {
            yield return designation.target.Thing;
        }
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is not Pawn { Downed: true } pawn2 || pawn2.Faction == pawn.Faction ||
            t.Map.designationManager.DesignationOn(t, Designation) == null)
        {
            return false;
        }

        if (pawn2.InBed() || !pawn.CanReserve(pawn2, 1, -1, null, forced) || DangerIsNear(pawn, pawn2, 40f))
        {
            return false;
        }

        var building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);
        if (building_Bed == null)
        {
            building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, true, GuestStatus.Prisoner);
        }

        if (building_Bed != null)
        {
            return pawn.CanReserve(building_Bed, 1, -1, null, forced);
        }

        Messages.Message("CannotCapture".Translate() + ": " + "NoPrisonerBed".Translate(), pawn2,
            MessageTypeDefOf.RejectInput, false);
        return false;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var pawn2 = t as Pawn;
        var t2 = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);
        var job = JobMaker.MakeJob(Job, pawn2, t2);
        job.count = 1;
        PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Capturing, KnowledgeAmount.Total);
        return job;
    }

    protected static bool DangerIsNear(Pawn pawn, Pawn p, float radius)
    {
        if (!p.Spawned || !StartUp.settings.checkForDanger)
        {
            return false;
        }

        var fogged = p.Position.Fogged(p.Map);
        var potentialTargetsFor = p.Map.attackTargetsCache.GetPotentialTargetsFor(pawn);
        foreach (var attackTarget in potentialTargetsFor)
        {
            if (!attackTarget.ThreatDisabled(pawn) &&
                (fogged || !attackTarget.Thing.Position.Fogged(attackTarget.Thing.Map)) &&
                p.Position.InHorDistOf(((Thing)attackTarget).Position, radius))
            {
                return true;
            }
        }

        return false;
    }
}

public class WorkGiver_CapturePrisoners_FirstAid: WorkGiver_CapturePrisoners
{
    protected new DesignationDef Designation => CaptureThemDefOf.CaptureThemCapture_FirstAid;

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(Designation);
    }

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        foreach (var designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(Designation))
        {
            yield return designation.target.Thing;
        }
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is not Pawn { Downed: true } pawn2 || pawn2.Faction == pawn.Faction ||
            t.Map.designationManager.DesignationOn(t, Designation) == null)
        {
            return false;
        }

        if (pawn2.InBed() || !pawn.CanReserve(pawn2, 1, -1, null, forced) || DangerIsNear(pawn, pawn2, 40f))
        {
            return false;
        }

        var building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);
        if (building_Bed == null)
        {
            building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, true, GuestStatus.Prisoner);
        }

        if (building_Bed != null)
        {
            return pawn.CanReserve(building_Bed, 1, -1, null, forced);
        }

        Messages.Message("CannotCapture".Translate() + ": " + "NoPrisonerBed".Translate(), pawn2,
            MessageTypeDefOf.RejectInput, false);
        return false;
    }
    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var pawn2 = t as Pawn;
        var t2 = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);

#if DEBUG
        Log.Message("Assigned "+pawn.Name+" to rescue" + pawn2.Name);
#endif

        if (StartUp.FirstAid && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
        {
            if (StartUp.CP_FirstAid == null)
            {
                StartUp.CP_FirstAid = DefDatabase<JobDef>.GetNamed("CP_FirstAid");
            }

            if (pawn2.health.hediffSet.BleedRateTotal > 0 && HealthUtility.TicksUntilDeathDueToBloodLoss(pawn2) / 2500f < StartUp.settings.maxBleedoutFirstAid)
            {
#if DEBUG
                Log.Message("Doing FirstAid on " + pawn2.Name + " first");
#endif
                return JobMaker.MakeJob(StartUp.CP_FirstAid, pawn2);
            }
        }

        var job = JobMaker.MakeJob(Job, pawn2, t2);
        job.count = 1;
        PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Capturing, KnowledgeAmount.Total);
        return job;
    }
}

public class WorkGiver_CapturePrisoners_CE : WorkGiver_CapturePrisoners
{
    protected new DesignationDef Designation => CaptureThemDefOf.CaptureThemCapture_CE;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        foreach (var designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(Designation))
        {
            yield return designation.target.Thing;
        }
    }
    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is not Pawn { Downed: true } pawn2 || pawn2.Faction == pawn.Faction ||
            t.Map.designationManager.DesignationOn(t, Designation) == null)
        {
            return false;
        }

        if (pawn2.InBed() || !pawn.CanReserve(pawn2, 1, -1, null, forced) || DangerIsNear(pawn, pawn2, 40f))
        {
            return false;
        }

        var building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);
        if (building_Bed == null)
        {
            building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, true, GuestStatus.Prisoner);
        }

        if (building_Bed != null)
        {
            return pawn.CanReserve(building_Bed, 1, -1, null, forced);
        }

        Messages.Message("CannotCapture".Translate() + ": " + "NoPrisonerBed".Translate(), pawn2,
            MessageTypeDefOf.RejectInput, false);
        return false;
    }

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(Designation);
    }
    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var pawn2 = t as Pawn;
        var t2 = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);

        if (StartUp.CE && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
        {
            if (StartUp.CEStablize == null)
            {
                StartUp.CEStablize = DefDatabase<JobDef>.GetNamed("Stabilize");
            }

            if (pawn2.health.hediffSet.BleedRateTotal>0 && HealthUtility.TicksUntilDeathDueToBloodLoss(pawn2) / 2500f < StartUp.settings.maxBleedoutFirstAid)
            {
#if DEBUG
                Log.Message("Doing CE Stabilize on " + pawn.Name + " first");
#endif
                // Take from CE https://github.com/CombatExtended-Continued/CombatExtended/blob/ba83aaf2d94c95c3ce1f10af0500e3aed21e19bc/Source/CombatExtended/Harmony/Harmony_FloatMenuMakerMap.cs#L165
                if (pawn.inventory == null || pawn.inventory.innerContainer == null || !pawn.inventory.innerContainer.Any(t => t.def.IsMedicine))
                {

                }
                else
                {
                    // Drop medicine from inventory
                    Medicine medicine = (Medicine)pawn.inventory.innerContainer.OrderByDescending(t => t.GetStatValue(StatDefOf.MedicalPotency)).FirstOrDefault();
                    Thing medThing;
                    if (medicine != null && pawn.inventory.innerContainer.TryDrop(medicine, pawn.Position, pawn.Map, ThingPlaceMode.Direct, 1, out medThing))
                    {
                        Job job2 = JobMaker.MakeJob(StartUp.CEStablize, pawn2, medThing);
                        job2.count = 1;
                        return job2;
                    }
                }

            }
        }

        var job = JobMaker.MakeJob(Job, pawn2, t2);
        job.count = 1;
        PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Capturing, KnowledgeAmount.Total);
        return job;
    }
}