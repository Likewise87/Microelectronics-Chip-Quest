using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace TSA.MicroQuest
{
    public enum SliderFormat { Fixed0, Fixed1, Fixed2, Multiplier, Percent, PercentDecimal }

    public class ModInit : Mod
    {
        public static Settings Settings = null!;

        public ModInit(ModContentPack content) : base(content)
        {
            Settings = GetSettings<Settings>();

            var harmony = new Harmony("tsa.microquest");
            harmony.PatchAll();
        }

        public override string SettingsCategory() => "Microelectronics Chip Quest";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var s = Settings ?? (Settings = new Settings());
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            DrawResetButton(listing, () => s.ResetDefaults());

            s.PawnCount = Mathf.Clamp(Mathf.RoundToInt(LabeledSlider(listing,
                "Pawns to spawn",
                s.PawnCount, 1f, 20f,
                "Number of pawns spawned for the microelectronics chip quest site.",
                1f, SliderFormat.Fixed0, 8f)), 1, 20);

            listing.End();
        }

        // ----- WD-style helpers (kept in ModInit; no separate library file) -----

        private static void DrawResetButton(Listing_Standard l, Action onReset)
        {
            if (onReset == null) return;
            l.Gap(4f);
            Rect row = l.GetRect(30f);
            float resetW = Mathf.Min(250f, row.width);
            if (resetW < 120f) resetW = Mathf.Max(100f, row.width * 0.35f);
            Rect resetRect = new Rect(row.x, row.y, resetW, row.height);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (Widgets.ButtonText(resetRect, "Restore defaults")) onReset();
            GUI.color = prev;
            l.Gap(6f);
        }

        private static float LabeledSlider(Listing_Standard l, string label, float val, float min, float max, string tooltip = null, float step = 0.1f, SliderFormat format = SliderFormat.Fixed1, float? defaultValue = null)
        {
            l.Gap(2f);
            Rect r = l.GetRect(24f);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(r, TooltipWithDefault(tooltip, defaultValue, format));
            Widgets.Label(r.LeftPart(0.5f), $"{label}: {FormatValue(val, format).Colorize(Color.cyan)}");
            return Widgets.HorizontalSlider(r.RightPart(0.5f), val, min, max, false, null, null, null, step);
        }

        private static string TooltipWithDefault(string tooltip, float? defaultValue, SliderFormat format = SliderFormat.Fixed1)
        {
            if (!defaultValue.HasValue) return tooltip;
            return AppendDefault(tooltip, FormatValue(defaultValue.Value, format));
        }

        private static string AppendDefault(string tooltip, string def)
        {
            string line = $"Default: {def}";
            return tooltip.NullOrEmpty() ? line : tooltip + "\n" + line;
        }

        private static string FormatValue(float val, SliderFormat format) => format switch
        {
            SliderFormat.Fixed0 => val.ToString("F0"),
            SliderFormat.Fixed1 => val.ToString("F1"),
            SliderFormat.Fixed2 => val.ToString("F2"),
            SliderFormat.Multiplier => val.ToString("F2") + "x",
            SliderFormat.Percent => (val * 100f).ToString("F0") + " %",
            SliderFormat.PercentDecimal => (val * 100f).ToString("F1") + " %",
            _ => val.ToString()
        };
    }

    public class Settings : ModSettings
    {
        public int PawnCount = 8;

        public void ResetDefaults()
        {
            PawnCount = 8;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref PawnCount, "PawnCount", 8);
        }
    }
}
