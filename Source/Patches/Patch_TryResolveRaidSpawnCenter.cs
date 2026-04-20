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
    ///   Found             → center drop in open sky (normal NITH behavior)
    ///   TooSmall/NoOpenSky → EdgeWalkIn; if that fails → VanillaFallback
    ///
    /// VanillaFallback sets BypassPodPlacement so the subsequent Arrive call also
    /// bypasses roof checks, allowing pods to land on thin roofs as a last resort.
    /// BypassPodPlacement is cleared by Patch 4 (Arrive postfix) after pods drop.
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

        private static bool TryEdgeWalkIn(IncidentParms parms)
        {
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            return parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms);
        }

        /// <summary>
        /// Last-ditch fallback: finds a center with roof checks bypassed, then sets
        /// BypassPodPlacement so the subsequent Arrive call can also land pods anywhere.
        /// BypassPodPlacement is cleared by Patch 4 (Arrive postfix) after pods drop.
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

            // If center-finding succeeded, set BypassPodPlacement so Arrive can also
            // land pods on roofed cells. Cleared by Patch 4 after Arrive completes.
            if (__result)
                NITH_State.BypassPodPlacement = true;
        }
    }
}
