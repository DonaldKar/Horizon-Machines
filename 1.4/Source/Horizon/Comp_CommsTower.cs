﻿using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Horizon
{
	public class CompProperties_CommsTower : CompProperties
	{
		public CompProperties_CommsTower()
		{
			compClass = typeof(Comp_CommsTower);
		}
	}
	public class Comp_CommsTower: ThingComp
	{
		public CompPowerTrader powerComp;

		public bool CanUseCommsNow
		{
			get
			{
				if (parent.Spawned && parent.Map.gameConditionManager.ElectricityDisabled)
				{
					return false;
				}
				if (powerComp != null)
				{
					return powerComp.PowerOn;
				}
				return true;
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			powerComp = parent.GetComp<CompPowerTrader>();
			LessonAutoActivator.TeachOpportunity(ConceptDefOf.BuildOrbitalTradeBeacon, OpportunityType.GoodToKnow);
			LessonAutoActivator.TeachOpportunity(ConceptDefOf.OpeningComms, OpportunityType.GoodToKnow);
		}

		private void UseAct(Pawn myPawn, ICommunicable commTarget)
		{
			Job job = JobMaker.MakeJob(JobDefOf.UseCommsConsole, parent);
			job.commTarget = commTarget;
			myPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.OpeningComms, KnowledgeAmount.Total);
		}

		private FloatMenuOption GetFailureReason(Pawn myPawn)
		{
			if (!myPawn.CanReach(parent, PathEndMode.InteractionCell, Danger.Some))
			{
				return new FloatMenuOption("CannotUseNoPath".Translate(), null);
			}
			if (parent.Spawned && parent.Map.gameConditionManager.ElectricityDisabled)
			{
				return new FloatMenuOption("CannotUseSolarFlare".Translate(), null);
			}
			if (powerComp != null && !powerComp.PowerOn)
			{
				return new FloatMenuOption("CannotUseNoPower".Translate(), null);
			}
			if (!myPawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
			{
				return new FloatMenuOption("CannotUseReason".Translate("IncapableOfCapacity".Translate(PawnCapacityDefOf.Talking.label, myPawn.Named("PAWN"))), null);
			}
			if (!GetCommTargets(myPawn).Any())
			{
				return new FloatMenuOption("CannotUseReason".Translate("NoCommsTarget".Translate()), null);
			}
			if (!CanUseCommsNow)
			{
				Log.Error(string.Concat(myPawn, " could not use comm console for unknown reason."));
				return new FloatMenuOption("Cannot use now", null);
			}
			return null;
		}

		public IEnumerable<ICommunicable> GetCommTargets(Pawn myPawn)
		{
			return myPawn.Map.passingShipManager.passingShips.Cast<ICommunicable>().Concat(Find.FactionManager.AllFactionsVisibleInViewOrder.Where((Faction f) => !f.temporary && !f.IsPlayer).Cast<ICommunicable>());
		}

		public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn myPawn)
		{
			FloatMenuOption failureReason = GetFailureReason(myPawn);
			if (failureReason != null)
			{
				yield return failureReason;
				yield break;
			}
			foreach (ICommunicable commTarget in GetCommTargets(myPawn))
			{
				FloatMenuOption floatMenuOption = commTarget.CommFloatMenuOption((Building_CommsConsole)parent, myPawn);
				if (floatMenuOption != null)
				{
					yield return floatMenuOption;
				}
			}
			foreach (FloatMenuOption floatMenuOption2 in base.CompFloatMenuOptions(myPawn))
			{
				yield return floatMenuOption2;
			}
		}

		public void GiveUseCommsJob(Pawn negotiator, ICommunicable target)
		{
			Job job = JobMaker.MakeJob(JobDefOf.UseCommsConsole, parent);
			job.commTarget = target;
			negotiator.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.OpeningComms, KnowledgeAmount.Total);
		}
	}
	[HarmonyPatch(typeof(CommsConsoleUtility), "PlayerHasPoweredCommsConsole", new Type[] { })]
	public static class CommsConsoleUtility_PlayerHasPoweredCommsConsole_Patch
	{
		public static void Postfix(ref bool __result, Map map)
		{
			IEnumerable<Pawn> item = (IEnumerable<Pawn>)(from t in map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn)
														 where t is Pawn x && x.TryGetComp<Comp_CommsTower>() != null select t);
			foreach (Pawn t in item)
			{
				if (t.Faction == Faction.OfPlayer)
				{
					CompPowerTrader compPowerTrader = t.TryGetComp<CompPowerTrader>();
					if (compPowerTrader == null || compPowerTrader.PowerOn)
					{
						__result=true;
						return;
					}
				}
			}
			return;
		}
	}
}
