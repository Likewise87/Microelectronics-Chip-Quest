using RimWorld;
using Verse;

namespace TSA.MicroQuest
{
    public class GameComponent_MicroQuest : GameComponent
    {
        public bool Triggered;
        public bool ChipsCleared;
        public bool CacheMapGenerated;

        /// <summary>Game tick after which a failed/expired quest may be offered again.</summary>
        public int NextOfferTicks;

        private const int RetryDelayTicks = 60000; // 1 day

        private static ResearchProjectDef? cachedMicroProj;

        public GameComponent_MicroQuest(Game game) : base() { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Triggered, "Triggered", false);
            Scribe_Values.Look(ref ChipsCleared, "ChipsCleared", false);
            Scribe_Values.Look(ref CacheMapGenerated, "CacheMapGenerated", false);
            Scribe_Values.Look(ref NextOfferTicks, "NextOfferTicks", 0);
        }

        /// <summary>Chip analyzed or MicroelectronicsBasics finished — quest must never return.</summary>
        public void MarkPermanentlyComplete(bool destroyRemainingChips = false)
        {
            ChipsCleared = true;
            Triggered = true;
            QuestHelper.CompleteMicroQuestIfActive();
            if (destroyRemainingChips)
                QuestHelper.DestroyAllChips();
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;

            // Fully settled — nothing left to do.
            if (ChipsCleared) return;

            var microProj = cachedMicroProj ??= DefDatabase<ResearchProjectDef>.GetNamedSilentFail(QuestHelper.ResearchDefName);

            // Already researched (incl. mid-game install): never spawn / never respawn.
            if (microProj != null && microProj.IsFinished)
            {
                MarkPermanentlyComplete(destroyRemainingChips: true);
                return;
            }

            // Softlock escape: player entered the cache but the chip is gone without unlocking research.
            if (Triggered && CacheMapGenerated && !QuestHelper.AnyChipExists() && QuestHelper.AnyActiveMicroQuest())
            {
                Log.Warning("[TSA] Microelectronics chip missing after cache visit; failing quest.");
                QuestHelper.FailMicroQuestIfActive();
            }

            // Expired/failed only — re-offer after a day. Never if research done or quest succeeded.
            if (Triggered && !QuestHelper.AnyActiveMicroQuest() && !QuestHelper.AnyChipExists())
            {
                Triggered = false;
                CacheMapGenerated = false;
                if (NextOfferTicks < Find.TickManager.TicksGame)
                    NextOfferTicks = Find.TickManager.TicksGame + RetryDelayTicks;
            }

            if (!Triggered && Find.AnyPlayerHomeMap != null && Find.TickManager.TicksGame >= NextOfferTicks)
            {
                if (QuestHelper.GenerateQuest())
                    Triggered = true;
                // else: retry next pulse
            }
        }
    }
}
