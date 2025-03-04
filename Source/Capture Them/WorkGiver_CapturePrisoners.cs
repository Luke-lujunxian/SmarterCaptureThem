using RimWorld;
using System.Collections.Generic;
using System.Linq;
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
            t.Map.designationManager.DesignationOn(t, this.Designation) == null)
        {
            return false;
        }

        if (pawn2.InBed() || !pawn.CanReserve(pawn2, 1, -1, null, forced) || DangerIsNear(pawn, pawn2, 40f))
        {
            if (StartUp.settings.debug)
            {
                if (!pawn.CanReserve(pawn2, 1, -1, null, forced))
                {
                    Log.Message($"[Smarter Capture]{pawn.Name} is not assigned to rescue {pawn2.Name} because it has been or it cant be reserve (Maybe someone is already on the way?) \n");
                }
                else
                {
                    Log.Message($"[Smarter Capture]{pawn2.Name} is not a valid target for capture because on of the following is true:\n" +
                    $"pawn2.InBed(): {pawn2.InBed()},\n " +
                    $"DangerIsNear(): {DangerIsNear(pawn, pawn2, 40f)}");
                }

            }
            if (pawn2.InBed())
            {
                pawn2.Map.designationManager.TryRemoveDesignationOn(pawn2, CaptureThemDefOf.CaptureThemCapture);
            }
            return false;
        }

        var building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);
        if (building_Bed == null)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] Trying to find a bed for {pawn2.Name} but failed. Will try ignoreOtherReservations");

            }
            building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, true, GuestStatus.Prisoner);
        }

        if (building_Bed != null)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] Found a bed, trying to it for {pawn2.Name} and the result is {pawn.CanReserve(building_Bed, 1, -1, null, forced)}");
            }
            return pawn.CanReserve(building_Bed, 1, -1, null, forced);
        }

        Messages.Message("CannotCapture".Translate() + ": " + "NoPrisonerBed".Translate(), pawn2,
            MessageTypeDefOf.RejectInput, false);
        if (StartUp.settings.debug)
        {
            Log.Message($"[Smarter Capture] Trying to find a bed for {pawn2.Name} with ignoreOtherReservations and failed again");

        }
        return false;
    }

    public Job ArrestFirst(Pawn pawn, Pawn pawn2)
    {
        if (StartUp.ArrestHere && StartUp.settings.doArrestFirst && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Warden) && !pawn2.IsPrisoner)
        {
            if (StartUp.CP_ImprisonInPlace == null)
            {
                StartUp.CP_ImprisonInPlace = DefDatabase<JobDef>.GetNamed("CP_ImprisonInPlace");
            }

            if (pawn2.health.hediffSet.BleedRateTotal > 0 && HealthUtility.TicksUntilDeathDueToBloodLoss(pawn2) / 2500f < StartUp.settings.maxBleedoutFirstAid)
            {
                if (StartUp.settings.debug)
                {
                    Log.Message("Doing arrest on " + pawn2.Name + " first");
                }
                Job job = JobMaker.MakeJob(StartUp.CP_ImprisonInPlace, pawn2);
                job.count = 1;
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Capturing, KnowledgeAmount.Total);
                return job;
            }
        }
        return null;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var pawn2 = t as Pawn;

        if (ArrestFirst(pawn, pawn2) is Job job3 && job3 != null)
        {
            return job3;
        }

        if (StartUp.settings.doVanillaTend && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) )
        {

            if (pawn2.health.hediffSet.BleedRateTotal > 0 && HealthUtility.TicksUntilDeathDueToBloodLoss(pawn2) / 2500f < StartUp.settings.maxBleedoutFirstAid)
            {
                if (StartUp.settings.debug)
                {
                    Log.Message("Doing vanilla tend on " + pawn2.Name + " first");
                }
                Thing medicine2 = HealthAIUtility.FindBestMedicine(pawn, pawn2, onlyUseInventory: true);
                Job job2;
                if (medicine2 != null)
                {
                    job2 = JobMaker.MakeJob(JobDefOf.TendPatient, pawn2, medicine2);
                }
                else
                {
                    job2 = JobMaker.MakeJob(JobDefOf.TendPatient, pawn2);
                }
                job2.count = 1;
                //job2.draftedTend = true;

                /*                if ((pawn.CurJob != null && (pawn.CurJob.JobIsSameAs(pawn, job2))))
                                {
                                    return null;
                                }
                                pawn.stances.CancelBusyStanceSoft();
                                pawn.jobs.ClearQueuedJobs();
                                if (job2.TryMakePreToilReservations(pawn, errorOnFailed: true))
                                {
                                    pawn.jobs.jobQueue.EnqueueLast(job2, JobTag.Misc);
                                    return null;
                                }*/

                // I don't know why, but this make it works
                if (StartUp.ArrestHere && StartUp.settings.doArrestFirst)
                {
                    //If assigned a job first, this will make tending job done immediately
                    return job2;
                }
                else
                {
                    pawn.stances.CancelBusyStanceSoft();
                    pawn.jobs.ClearQueuedJobs();
                    //But if you don't have a job first, will get the 10 jobs in 10 ticks error if directly return
                    pawn.jobs.TryTakeOrderedJob(job2);
                    return null;
                }
            }
        }
        if (StartUp.settings.debug)
        {
            Log.Message("Carrying " + pawn2.Name + " to bed");
        }
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

public class WorkGiver_CapturePrisoners_FirstAid : WorkGiver_CapturePrisoners
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
            t.Map.designationManager.DesignationOn(t, this.Designation) == null)
        {
            return false;
        }

        if (pawn2.InBed() || !pawn.CanReserve(pawn2, 1, -1, null, forced) || DangerIsNear(pawn, pawn2, 40f))
        {
            if (StartUp.settings.debug)
            {
                if (!pawn.CanReserve(pawn2, 1, -1, null, forced))
                {
                    Log.Message($"[Smarter Capture]{pawn.Name} is not assigned to rescue {pawn2.Name} because it has been or it cant be reserve (Maybe someone is already on the way?) \n");
                }
                else
                {
                    Log.Message($"[Smarter Capture]{pawn2.Name} is not a valid target for capture because on of the following is true:\n" +
                    $"pawn2.InBed(): {pawn2.InBed()},\n " +
                    $"DangerIsNear(): {DangerIsNear(pawn, pawn2, 40f)}");
                }

            }
            if (pawn2.InBed())
            {
                pawn2.Map.designationManager.TryRemoveDesignationOn(pawn2, CaptureThemDefOf.CaptureThemCapture_FirstAid);
            }
            return false;
        }

        var building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);
        if (building_Bed == null)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] Trying to find a bed for {pawn2.Name} but failed. Will try ignoreOtherReservations");

            }
            building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, true, GuestStatus.Prisoner);
        }

        if (building_Bed != null)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] Found a bed, trying to it for {pawn2.Name} and the result is {pawn.CanReserve(building_Bed, 1, -1, null, forced)}");
            }
            return pawn.CanReserve(building_Bed, 1, -1, null, forced);
        }

        Messages.Message("CannotCapture".Translate() + ": " + "NoPrisonerBed".Translate(), pawn2,
            MessageTypeDefOf.RejectInput, false);
        if (StartUp.settings.debug)
        {
            Log.Message($"[Smarter Capture] Trying to find a bed for {pawn2.Name} with ignoreOtherReservations and failed again");

        }
        return false;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var pawn2 = t as Pawn;
        var t2 = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);

        if (StartUp.settings.debug)
        {
            Log.Message("Assigned " + pawn.Name + " to rescue " + pawn2.Name);
        }
        if (ArrestFirst(pawn, pawn2) is Job job3 && job3 != null)
        {
            return job3;
        }

        if (StartUp.FirstAid && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
        {
            if (StartUp.CP_FirstAid == null)
            {
                StartUp.CP_FirstAid = DefDatabase<JobDef>.GetNamed("CP_FirstAid");
            }

            if (pawn2.health.hediffSet.BleedRateTotal > 0 && HealthUtility.TicksUntilDeathDueToBloodLoss(pawn2) / 2500f < StartUp.settings.maxBleedoutFirstAid)
            {
                if (StartUp.settings.debug)
                {
                    Log.Message("Doing FirstAid on " + pawn2.Name + " first");
                }
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
            t.Map.designationManager.DesignationOn(t, this.Designation) == null)
        {
            return false;
        }

        if (pawn2.InBed() || !pawn.CanReserve(pawn2, 1, -1, null, forced) || DangerIsNear(pawn, pawn2, 40f))
        {
            if (StartUp.settings.debug)
            {
                if (!pawn.CanReserve(pawn2, 1, -1, null, forced))
                {
                    Log.Message($"[Smarter Capture]{pawn.Name} is not assigned to rescue {pawn2.Name} because it has been or it cant be reserve (Maybe someone is already on the way?) \n");
                }
                else
                {
                    Log.Message($"[Smarter Capture]{pawn2.Name} is not a valid target for capture because on of the following is true:\n" +
                    $"pawn2.InBed(): {pawn2.InBed()},\n " +
                    $"DangerIsNear(): {DangerIsNear(pawn, pawn2, 40f)}");
                }

            }
            if (pawn2.InBed())
            {
                pawn2.Map.designationManager.TryRemoveDesignationOn(pawn2, CaptureThemDefOf.CaptureThemCapture_CE);
            }
            return false;
        }

        var building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);
        if (building_Bed == null)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] Trying to find a bed for {pawn2.Name} but failed. Will try ignoreOtherReservations");

            }
            building_Bed = RestUtility.FindBedFor(pawn2, pawn, false, true, GuestStatus.Prisoner);
        }

        if (building_Bed != null)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] Found a bed, trying to it for {pawn2.Name} and the result is {pawn.CanReserve(building_Bed, 1, -1, null, forced)}");
            }
            return pawn.CanReserve(building_Bed, 1, -1, null, forced);
        }

        Messages.Message("CannotCapture".Translate() + ": " + "NoPrisonerBed".Translate(), pawn2,
            MessageTypeDefOf.RejectInput, false);
        if (StartUp.settings.debug)
        {
            Log.Message($"[Smarter Capture] Trying to find a bed for {pawn2.Name} with ignoreOtherReservations and failed again");

        }
        return false;
    }

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(Designation);
    }
    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var pawn2 = t as Pawn;

        if (ArrestFirst(pawn, pawn2) is Job job3 && job3 != null)
        {
            return job3;
        }

        var t2 = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);

        if (StartUp.CE && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
        {
            if (StartUp.CEStablize == null)
            {
                StartUp.CEStablize = DefDatabase<JobDef>.GetNamed("Stabilize");
            }

            if (pawn2.health.hediffSet.BleedRateTotal > 0 && HealthUtility.TicksUntilDeathDueToBloodLoss(pawn2) / 2500f < StartUp.settings.maxBleedoutFirstAid)
            {
                if (StartUp.settings.debug)
                {
                    Log.Message("Doing CE Stabilize on " + pawn.Name + " first");
                }
                // Take from CE https://github.com/CombatExtended-Continued/CombatExtended/blob/ba83aaf2d94c95c3ce1f10af0500e3aed21e19bc/Source/CombatExtended/Harmony/Harmony_FloatMenuMakerMap.cs#L165
                if (pawn.inventory == null || pawn.inventory.innerContainer == null || !pawn.inventory.innerContainer.Any(t => t.def.IsMedicine))
                {
                    if (StartUp.settings.debug)
                    {
                        Log.Message($"{pawn.Name} has no medicine on inventory");
                    }
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