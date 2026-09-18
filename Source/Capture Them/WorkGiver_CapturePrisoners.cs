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
        foreach (var designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(Designation).ToList())
        {
            yield return designation.target.Thing;
        }
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is not Pawn { Downed: true } pawn2 ||
            t.Map.designationManager.DesignationOn(t, this.Designation) == null)
        {
            return false;
        }

        // 目标已经躺在床上（例如已被抬进囚犯床）：抓捕目标已达成，移除标记
        if (pawn2.InBed())
        {
            pawn2.Map.designationManager.TryRemoveDesignationOn(pawn2, this.Designation);
            return false;
        }

        // 已经逮捕（目标已成为囚犯）：不再重复抓捕，改为就地治疗，避免反复弹出「无法抓捕」的提示
        if (pawn2.IsPrisoner)
        {
            return InPlaceTendEnabled(pawn) && IsBleedingOut(pawn2) &&
                   pawn.CanReserve(pawn2, 1, -1, null, forced) && !DangerIsNear(pawn, pawn2, 40f);
        }

        if (pawn2.Faction == pawn.Faction)
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

        // 没有可用囚犯床：原版抓捕必须把目标搬到囚犯床，这里退化为原地逮捕
        if (StartUp.ArrestHere && StartUp.settings.doArrestFirst && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Warden) && !pawn2.IsPrisoner)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] No prisoner bed for {pawn2.Name}, will arrest in place because Arrest Here is loaded");
            }
            return true;
        }

        // 只在玩家主动右键时提示，工作扫描会反复调用本方法，否则会不停弹出
        if (forced)
        {
            Messages.Message("CannotCapture".Translate() + ": " + "NoPrisonerBed".Translate(), pawn2,
                MessageTypeDefOf.RejectInput, false);
        }
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
            if (pawn2.health.hediffSet.BleedRateTotal > 0 && HealthUtility.TicksUntilDeathDueToBloodLoss(pawn2) / 2500f < StartUp.settings.maxBleedoutFirstAid)
            {
                if (StartUp.settings.debug)
                {
                    Log.Message("Doing arrest on " + pawn2.Name + " first");
                }
                return ArrestInPlace(pawn, pawn2);
            }
        }
        return null;
    }

    /// <summary>
    /// 原地逮捕。原版抓捕必须把目标搬到囚犯床，因此没有可用床位时只能依赖 [RH2] CPERS: Arrest Here! 提供的原地逮捕。
    /// </summary>
    protected Job ArrestInPlace(Pawn pawn, Pawn pawn2)
    {
        if (!StartUp.ArrestHere || !StartUp.settings.doArrestFirst || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Warden) || pawn2.IsPrisoner)
        {
            return null;
        }

        if (StartUp.CP_ImprisonInPlace == null)
        {
            StartUp.CP_ImprisonInPlace = DefDatabase<JobDef>.GetNamed("CP_ImprisonInPlace");
        }

        Job job = JobMaker.MakeJob(StartUp.CP_ImprisonInPlace, pawn2);
        job.count = 1;
        PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Capturing, KnowledgeAmount.Total);
        return job;
    }

    /// <summary>
    /// 查找可用于关押目标的囚犯床，第二次查找忽略其他人的预定。
    /// </summary>
    protected static Building_Bed FindPrisonerBed(Pawn pawn, Pawn pawn2)
    {
        var bed = RestUtility.FindBedFor(pawn2, pawn, false, false, GuestStatus.Prisoner);
        if (bed == null)
        {
            bed = RestUtility.FindBedFor(pawn2, pawn, false, true, GuestStatus.Prisoner);
        }
        return bed;
    }

    /// <summary>
    /// 目标是否因失血而需要在被捕后立刻处理。
    /// </summary>
    protected static bool IsBleedingOut(Pawn pawn2)
    {
        return pawn2.health.hediffSet.BleedRateTotal > 0 &&
               HealthUtility.TicksUntilDeathDueToBloodLoss(pawn2) / 2500f < StartUp.settings.maxBleedoutFirstAid;
    }

    /// <summary>
    /// 本变体是否启用了就地治疗。会被 HasJobOnHthing 频繁调用，必须无副作用。
    /// </summary>
    protected virtual bool InPlaceTendEnabled(Pawn pawn)
    {
        return StartUp.settings.doVanillaTend && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor);
    }

    /// <summary>
    /// 就地治疗任务（不依赖床位）。只有目标已经被逮捕时才会被派发。
    /// </summary>
    protected virtual Job InPlaceTendJob(Pawn pawn, Pawn pawn2)
    {
        if (!InPlaceTendEnabled(pawn) || !IsBleedingOut(pawn2))
        {
            return null;
        }

        Thing medicine = HealthAIUtility.FindBestMedicine(pawn, pawn2, onlyUseInventory: true);
        Job job;
        if (medicine != null)
        {
            job = JobMaker.MakeJob(JobDefOf.TendPatient, pawn2, medicine);
        }
        else
        {
            job = JobMaker.MakeJob(JobDefOf.TendPatient, pawn2);
        }
        job.count = 1;
        return job;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var pawn2 = t as Pawn;

        // 已经逮捕（目标已成为囚犯）：不再重复抓捕，改为就地治疗
        if (pawn2.IsPrisoner)
        {
            return InPlaceTendJob(pawn, pawn2);
        }

        // 没有可用囚犯床：原版抓捕必须把目标搬到囚犯床，这里退化为原地逮捕
        var t2 = FindPrisonerBed(pawn, pawn2);
        if (t2 == null)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] No prisoner bed for {pawn2.Name}, arresting in place");
            }
            return ArrestInPlace(pawn, pawn2);
        }

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
        var job = JobMaker.MakeJob(Job, pawn2, t2);
        job.count = 1;
        PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Capturing, KnowledgeAmount.Total);
        return job;
    }

    protected static bool DangerIsNear(Pawn pawn, Pawn p, float radius)
    {
#if v16
        if (VacuumUtility.VacuumConcernTo(p.Position, pawn))//Will not walk into vacuum unprotected
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] position of {p.Name} is danger vacuum to {pawn.Name}");
            }
            return true;
        }
#endif
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
        foreach (var designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(Designation).ToList())
        {
            yield return designation.target.Thing;
        }
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is not Pawn { Downed: true } pawn2 ||
            t.Map.designationManager.DesignationOn(t, this.Designation) == null)
        {
            return false;
        }

        // 目标已经躺在床上（例如已被抬进囚犯床）：抓捕目标已达成，移除标记
        if (pawn2.InBed())
        {
            pawn2.Map.designationManager.TryRemoveDesignationOn(pawn2, this.Designation);
            return false;
        }

        // 已经逮捕（目标已成为囚犯）：不再重复抓捕，改为就地治疗，避免反复弹出「无法抓捕」的提示
        if (pawn2.IsPrisoner)
        {
            return InPlaceTendEnabled(pawn) && IsBleedingOut(pawn2) &&
                   pawn.CanReserve(pawn2, 1, -1, null, forced) && !DangerIsNear(pawn, pawn2, 40f);
        }

        if (pawn2.Faction == pawn.Faction)
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

        // 没有可用囚犯床：原版抓捕必须把目标搬到囚犯床，这里退化为原地逮捕
        if (StartUp.ArrestHere && StartUp.settings.doArrestFirst && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Warden) && !pawn2.IsPrisoner)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] No prisoner bed for {pawn2.Name}, will arrest in place because Arrest Here is loaded");
            }
            return true;
        }

        // 只在玩家主动右键时提示，工作扫描会反复调用本方法，否则会不停弹出
        if (forced)
        {
            Messages.Message("CannotCapture".Translate() + ": " + "NoPrisonerBed".Translate(), pawn2,
                MessageTypeDefOf.RejectInput, false);
        }
        if (StartUp.settings.debug)
        {
            Log.Message($"[Smarter Capture] Trying to find a bed for {pawn2.Name} with ignoreOtherReservations and failed again");

        }
        return false;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var pawn2 = t as Pawn;

        // 已经逮捕（目标已成为囚犯）：不再重复抓捕，改为就地治疗
        if (pawn2.IsPrisoner)
        {
            return InPlaceTendJob(pawn, pawn2);
        }

        // 没有可用囚犯床：原版抓捕必须把目标搬到囚犯床，这里退化为原地逮捕
        var t2 = FindPrisonerBed(pawn, pawn2);
        if (t2 == null)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] No prisoner bed for {pawn2.Name}, arresting in place");
            }
            return ArrestInPlace(pawn, pawn2);
        }

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

    protected override bool InPlaceTendEnabled(Pawn pawn)
    {
        return StartUp.FirstAid && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor);
    }

    protected override Job InPlaceTendJob(Pawn pawn, Pawn pawn2)
    {
        if (!InPlaceTendEnabled(pawn) || !IsBleedingOut(pawn2))
        {
            return null;
        }

        if (StartUp.CP_FirstAid == null)
        {
            StartUp.CP_FirstAid = DefDatabase<JobDef>.GetNamed("CP_FirstAid");
        }

        return JobMaker.MakeJob(StartUp.CP_FirstAid, pawn2);
    }
}

public class WorkGiver_CapturePrisoners_CE : WorkGiver_CapturePrisoners
{
    protected new DesignationDef Designation => CaptureThemDefOf.CaptureThemCapture_CE;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        foreach (var designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(Designation).ToList())
        {
            yield return designation.target.Thing;
        }
    }
    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is not Pawn { Downed: true } pawn2 ||
            t.Map.designationManager.DesignationOn(t, this.Designation) == null)
        {
            return false;
        }

        // 目标已经躺在床上（例如已被抬进囚犯床）：抓捕目标已达成，移除标记
        if (pawn2.InBed())
        {
            pawn2.Map.designationManager.TryRemoveDesignationOn(pawn2, this.Designation);
            return false;
        }

        // 已经逮捕（目标已成为囚犯）：不再重复抓捕，改为就地治疗，避免反复弹出「无法抓捕」的提示
        if (pawn2.IsPrisoner)
        {
            return InPlaceTendEnabled(pawn) && IsBleedingOut(pawn2) &&
                   pawn.CanReserve(pawn2, 1, -1, null, forced) && !DangerIsNear(pawn, pawn2, 40f);
        }

        if (pawn2.Faction == pawn.Faction)
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

        // 没有可用囚犯床：原版抓捕必须把目标搬到囚犯床，这里退化为原地逮捕
        if (StartUp.ArrestHere && StartUp.settings.doArrestFirst && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Warden) && !pawn2.IsPrisoner)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] No prisoner bed for {pawn2.Name}, will arrest in place because Arrest Here is loaded");
            }
            return true;
        }

        // 只在玩家主动右键时提示，工作扫描会反复调用本方法，否则会不停弹出
        if (forced)
        {
            Messages.Message("CannotCapture".Translate() + ": " + "NoPrisonerBed".Translate(), pawn2,
                MessageTypeDefOf.RejectInput, false);
        }
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

        // 已经逮捕（目标已成为囚犯）：不再重复抓捕，改为就地治疗
        if (pawn2.IsPrisoner)
        {
            return InPlaceTendJob(pawn, pawn2);
        }

        // 没有可用囚犯床：原版抓捕必须把目标搬到囚犯床，这里退化为原地逮捕
        var t2 = FindPrisonerBed(pawn, pawn2);
        if (t2 == null)
        {
            if (StartUp.settings.debug)
            {
                Log.Message($"[Smarter Capture] No prisoner bed for {pawn2.Name}, arresting in place");
            }
            return ArrestInPlace(pawn, pawn2);
        }

        if (ArrestFirst(pawn, pawn2) is Job job3 && job3 != null)
        {
            return job3;
        }

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

    protected override bool InPlaceTendEnabled(Pawn pawn)
    {
        // 就地稳定需要随身携带药品，没有药品就无法执行
        return StartUp.CE && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) &&
               pawn.inventory != null && pawn.inventory.innerContainer != null &&
               pawn.inventory.innerContainer.Any(t => t.def.IsMedicine);
    }

    protected override Job InPlaceTendJob(Pawn pawn, Pawn pawn2)
    {
        if (!InPlaceTendEnabled(pawn) || !IsBleedingOut(pawn2))
        {
            return null;
        }

        if (StartUp.CEStablize == null)
        {
            StartUp.CEStablize = DefDatabase<JobDef>.GetNamed("Stabilize");
        }

        // Take from CE https://github.com/CombatExtended-Continued/CombatExtended/blob/ba83aaf2d94c95c3ce1f10af0500e3aed21e19bc/Source/CombatExtended/Harmony/Harmony_FloatMenuMakerMap.cs#L165
        // Drop medicine from inventory
        Medicine medicine = (Medicine)pawn.inventory.innerContainer.OrderByDescending(t => t.GetStatValue(StatDefOf.MedicalPotency)).FirstOrDefault();
        Thing medThing;
        if (medicine != null && pawn.inventory.innerContainer.TryDrop(medicine, pawn.Position, pawn.Map, ThingPlaceMode.Direct, 1, out medThing))
        {
            Job job = JobMaker.MakeJob(StartUp.CEStablize, pawn2, medThing);
            job.count = 1;
            return job;
        }

        return null;
    }
}