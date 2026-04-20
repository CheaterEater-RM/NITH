using HarmonyLib;
using RimWorld;
using Verse;

namespace NITH.Patches
{
    /// <summary>
    /// Patch 2 — PawnsArrivalModeWorker_CenterDrop.TryResolveRaidSpawnCenter (prefix)
    ///
    /// Replaces vanilla center-finding with NITH's 4-pass open-sky algorithm.
    ///
    /// Decision tree:
    ///   Found     → center drop in open sky (normal NITH behavior)
    ///   TooSmall  → EdgeWalkIn (open sky exists but too small for the full raid)
    ///   NoOpenSky → EdgeWalkIn; if that also fails → vanilla center drop with roof-punch
    ///               bypass (last-ditch: better a roof-punching raid than no raid at all)
    ///
    /// Pre-assigned centers (quest-scripted raids) are left untouched.
    /// BypassRoofCheck guard prevents re-entry during the vanilla fallback.
    /// </summary>
    [HarmonyPatch(typeof(PawnsArrivalModeWorker_CenterDrop),
                  nameof(PawnsArrivalModeWorker_CenterDrop.TryResolveRaidSpawnCenter))]
    public static class Patch_TryResolveRaidSpawnCenter
    {
        public static bool Prefix(ref bool __result, IncidentParms parms)
        {
            // During vanilla fallback, BypassRoofCheck is set — let vanilla run untouched.
            if (NITH_State.BypassRoofCheck)
                return true;

            // Pre-assigned centers (e.g. quest-scripted raids) are left untouched.
            if (parms.spawnCenter.IsValid)
                return true;

            Map map = parms.target as Map;
            if (map == null)
                return true;

            CenterFindResult findResult = NITHCenterFinder.FindCenter(map, parms.points, out IntVec3 center);

            switch (findResult)
            {
                case CenterFindResult.Found:
                    parms.spawnCenter   = center;
                    parms.spawnRotation = Rot4.Random;
                    if (!parms.raidArrivalModeForQuickMilitaryAid)
                        parms.podOpenDelay = PawnsArrivalModeWorker_CenterDrop.PodOpenDelay;
                    __result = true;
                    return false;

                case CenterFindResult.TooSmall:
                    __result = TryEdgeWalkIn(parms);
                    return false;

                case CenterFindResult.NoOpenSky:
                default:
                    if (TryEdgeWalkIn(parms))
                    {
                        __result = true;
                        return false;
                    }
                    VanillaFallback(ref __result, parms);
                    return false;
            }
        }

        /// <summary>
        /// Switches the raid to EdgeWalkIn and resolves its spawn center.
        /// Returns true if EdgeWalkIn found a valid spawn center.
        /// </summary>
        private static bool TryEdgeWalkIn(IncidentParms parms)
        {
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            return parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms);
        }

        /// <summary>
        /// Last-ditch fallback: runs vanilla TryResolveRaidSpawnCenter with roof checks
        /// bypassed. Raiders will punch through roofs as in unmodded RimWorld.
        /// BypassRoofCheck prevents our prefix from intercepting the re-entrant call.
        /// </summary>
        private static void VanillaFallback(ref bool __result, IncidentParms parms)
        {
            parms.raidArrivalMode = PawnsArrivalModeDefOf.CenterDrop;
            NITH_State.BypassRoofCheck = true;
            try
            {
                __result = parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms);
            }
            finally
            {
                NITH_State.BypassRoofCheck = false;
            }
        }
    }
}
