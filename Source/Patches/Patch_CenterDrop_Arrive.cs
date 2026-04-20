using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace NITH.Patches
{
    /// <summary>
    /// Patch 4 — PawnsArrivalModeWorker_CenterDrop.Arrive (finalizer)
    ///
    /// Clears BypassPodPlacement after pod placement completes.
    /// BypassPodPlacement is set by VanillaFallback in Patch 2 when the map is fully
    /// sealed — it allows pods to land on thin roofs as a last resort.
    ///
    /// Uses a Finalizer rather than a Postfix to guarantee the flag is cleared even
    /// if Arrive throws an unhandled exception, preventing leakage into subsequent raids.
    /// </summary>
    [HarmonyPatch(typeof(PawnsArrivalModeWorker_CenterDrop),
                  nameof(PawnsArrivalModeWorker_CenterDrop.Arrive))]
    public static class Patch_CenterDrop_Arrive
    {
        public static Exception Finalizer(Exception __exception)
        {
            NITH_State.BypassPodPlacement = false;
            return __exception; // rethrow if any
        }
    }
}
