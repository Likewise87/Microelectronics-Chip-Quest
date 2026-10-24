using HarmonyLib;
using RimWorld;
using Verse;

namespace TSA.MicroQuest
{
    [HarmonyPatch(typeof(CompAnalyzable), nameof(CompAnalyzable.OnAnalyzed))]
    public static class Patch_CompAnalyzable_OnAnalyzed
    {
        private static ThingDef? cachedChipDef;

        public static void Postfix(CompAnalyzable __instance)
        {
            if (__instance?.parent == null) return;

            cachedChipDef ??= DefDatabase<ThingDef>.GetNamedSilentFail(QuestHelper.ChipDefName);
            if (cachedChipDef == null) return;

            if (__instance.parent.def == cachedChipDef)
            {
                var comp = Current.Game?.GetComponent<GameComponent_MicroQuest>();
                if (comp != null)
                    comp.MarkPermanentlyComplete();
                else
                    QuestHelper.CompleteMicroQuestIfActive();
            }
        }
    }
}
