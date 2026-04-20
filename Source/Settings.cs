using Verse;

namespace NITH
{
    public class NITHSettings : ModSettings
    {
        public float pointsPerPawnDivisor  = 100f;
        public int   earlyExitDistanceTiles = 7;
        public int   pass2SampleDivisor    = 60;
        public int   pass2CandidateLimit   = 100;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref pointsPerPawnDivisor,   "pointsPerPawnDivisor",   100f);
            Scribe_Values.Look(ref earlyExitDistanceTiles, "earlyExitDistanceTiles", 7);
            Scribe_Values.Look(ref pass2SampleDivisor,     "pass2SampleDivisor",     60);
            Scribe_Values.Look(ref pass2CandidateLimit,    "pass2CandidateLimit",    100);
            base.ExposeData();
        }
    }

    public class NITHMod : Verse.Mod
    {
        public static NITHSettings Settings;

        public NITHMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<NITHSettings>();
        }

        public override string SettingsCategory() => "Not in the Hospital!";

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label($"Points per pawn (divisor): {Settings.pointsPerPawnDivisor:F0}");
            listing.Label("Lower values require a larger open landing area for the same raid size.");
            Settings.pointsPerPawnDivisor = listing.Slider(Settings.pointsPerPawnDivisor, 50f, 300f);

            listing.Gap();
            listing.Label($"Landing zone search distance: {Settings.earlyExitDistanceTiles} tiles");
            listing.Label("How close to your colonists a landing zone must be before the search stops early.");
            Settings.earlyExitDistanceTiles = (int)listing.Slider(Settings.earlyExitDistanceTiles, 1f, 30f);

            listing.Gap();
            listing.Label($"Map sampling rate: 1 in {Settings.pass2SampleDivisor} tiles (min 1000 samples)");
            listing.Label("Higher values sample more tiles when searching the full map. Slight performance cost.");
            Settings.pass2SampleDivisor = (int)listing.Slider(Settings.pass2SampleDivisor, 20f, 200f);

            listing.Gap();
            listing.Label($"Landing zone candidates evaluated: {Settings.pass2CandidateLimit}");
            listing.Label("How many candidate cells are checked for proximity when searching the full map.");
            Settings.pass2CandidateLimit = (int)listing.Slider(Settings.pass2CandidateLimit, 10f, 500f);

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}
