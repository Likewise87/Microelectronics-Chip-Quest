using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace TSA.MicroQuest
{
    public static class QuestHelper
    {
        public const string QuestDefName = "TSA_MicroelectronicsCache_Quest";
        public const string ChipDefName = "Microelectronics_Chip";
        public const string ResearchDefName = "MicroelectronicsBasics";

        public static bool GenerateQuest()
        {
            var def = DefDatabase<QuestScriptDef>.GetNamedSilentFail(QuestDefName);
            if (def == null)
            {
                Log.Warning("[TSA] Quest def missing: " + QuestDefName);
                return false;
            }

            var homeMap = Find.AnyPlayerHomeMap;
            if (homeMap == null)
                return false;

            const int minDistance = 6;
            const int maxDistance = 14;

            if (!Util.TryFindQuestSiteTile(homeMap.Tile, minDistance, maxDistance, out PlanetTile foundTile))
            {
                Log.Warning("[TSA] Could not find a valid tile for the microelectronics cache quest.");
                return false;
            }

            var slate = new Slate();
            slate.Set("siteTile", foundTile);
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(homeMap));

            Quest quest;
            try
            {
                quest = QuestGen.Generate(def, slate);
            }
            catch (System.Exception e)
            {
                Log.Error("[TSA] QuestGen.Generate failed: " + e);
                return false;
            }

            if (quest == null)
                return false;

            Find.QuestManager.Add(quest);

            Find.LetterStack.ReceiveLetter(
                "TSA_MicroQuest_LetterLabel".Translate(),
                "TSA_MicroQuest_LetterText".Translate(),
                LetterDefOf.NeutralEvent,
                new GlobalTargetInfo(foundTile)
            );

            return true;
        }

        public static void CompleteMicroQuestIfActive()
        {
            var def = DefDatabase<QuestScriptDef>.GetNamedSilentFail(QuestDefName);
            if (def == null) return;

            foreach (var q in Find.QuestManager.QuestsListForReading)
            {
                if (q.root == def && q.State == QuestState.Ongoing)
                {
                    q.End(QuestEndOutcome.Success);
                }
            }
        }

        public static void FailMicroQuestIfActive()
        {
            var def = DefDatabase<QuestScriptDef>.GetNamedSilentFail(QuestDefName);
            if (def == null) return;

            foreach (var q in Find.QuestManager.QuestsListForReading)
            {
                if (q.root == def && q.State == QuestState.Ongoing)
                {
                    q.End(QuestEndOutcome.Fail);
                }
            }
        }

        public static bool AnyActiveMicroQuest()
        {
            var def = DefDatabase<QuestScriptDef>.GetNamedSilentFail(QuestDefName);
            if (def == null) return false;
            return Find.QuestManager.QuestsListForReading.Any(q => q.root == def && q.State == QuestState.Ongoing);
        }

        public static void DestroyAllChips()
        {
            var chipDef = DefDatabase<ThingDef>.GetNamedSilentFail(ChipDefName);
            if (chipDef == null) return;

            // Maps (ground / buildings)
            foreach (var map in Find.Maps)
            {
                var things = map.listerThings.ThingsOfDef(chipDef);
                for (int i = things.Count - 1; i >= 0; i--)
                    things[i].Destroy(DestroyMode.Vanish);

                foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                    DestroyChipsInContainer(p.inventory?.innerContainer, chipDef);
            }

            // Caravans (includes pack animals / pawn inventories via AllThings)
            foreach (Caravan caravan in Find.WorldObjects.Caravans)
            {
                List<Thing>? toDestroy = null;
                foreach (Thing t in caravan.AllThings)
                {
                    if (t.def != chipDef) continue;
                    toDestroy ??= new List<Thing>();
                    toDestroy.Add(t);
                }
                if (toDestroy == null) continue;
                for (int i = 0; i < toDestroy.Count; i++)
                {
                    if (!toDestroy[i].Destroyed)
                        toDestroy[i].Destroy(DestroyMode.Vanish);
                }
            }
        }

        public static bool AnyChipExists()
        {
            var chipDef = DefDatabase<ThingDef>.GetNamedSilentFail(ChipDefName);
            if (chipDef == null) return false;

            foreach (var map in Find.Maps)
            {
                if (map.listerThings.ThingsOfDef(chipDef).Count > 0)
                    return true;
                foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                {
                    if (ContainerHasChip(p.inventory?.innerContainer, chipDef))
                        return true;
                }
            }

            foreach (Caravan caravan in Find.WorldObjects.Caravans)
            {
                foreach (Thing t in caravan.AllThings)
                {
                    if (t.def == chipDef)
                        return true;
                }
            }

            return false;
        }

        private static void DestroyChipsInContainer(ThingOwner? container, ThingDef chipDef)
        {
            if (container == null) return;
            for (int i = container.Count - 1; i >= 0; i--)
            {
                if (container[i].def == chipDef)
                    container[i].Destroy(DestroyMode.Vanish);
            }
        }

        private static bool ContainerHasChip(ThingOwner? container, ThingDef chipDef)
        {
            if (container == null) return false;
            for (int i = 0; i < container.Count; i++)
            {
                if (container[i].def == chipDef)
                    return true;
            }
            return false;
        }
    }
}
