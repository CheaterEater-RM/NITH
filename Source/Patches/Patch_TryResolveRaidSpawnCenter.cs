using HarmonyLib;
using RimWorld;
using Verse;

namespace NITH.Patches
{
    /// <summary>
    /// Patch 2 — PawnsArrivalModeWorker_CenterDrop.TryResolveRaidSpawnCenter (prefix)
    ///
    /// Replaces vanilla center-finding with NITH's 4-pass open-sky algorithm.
    /// Sets parms.spawnCenter directly and skips vanilla when a valid center is found.
    /// Falls through to vanilla (which triggers EdgeDrop) only when no open-sky cell
    /// exists on the map at all, or when the center was pre-assigned externally.
    /// </summary>
    [HarmonyPatch(typeof(PawnsArrivalModeWorker_CenterDrop),
                  nameof(PawnsArrivalModeWorker_CenterDrop.TryResolveRaidSpawnCenter))]
    public static class Patch_TryResolveRaidSpawnCenter
    {
        public static bool Prefix(ref bool __result, IncidentParms parms)
        {
            // Pre-assigned centers (e.g. quest-scripted raids) are left untouched.
            if (parms.spawnCenter.IsValid)
                return true;

            Map map = parms.target as Map;
            if (map == null)
                return true;

            IntVec3 center = NITHCenterFinder.FindCenter(map, parms.points);

            if (!center.IsValid)
                return true; // no open sky anywhere — let vanilla fall back to EdgeDrop

            parms.spawnCenter   = center;
            parms.spawnRotation = Rot4.Random;

            if (!parms.raidArrivalModeForQuickMilitaryAid)
                parms.podOpenDelay = PawnsArrivalModeWorker_CenterDrop.PodOpenDelay;

            __result = true;
            return false;
        }
    }
}
