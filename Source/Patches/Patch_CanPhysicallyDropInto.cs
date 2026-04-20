using HarmonyLib;
using RimWorld;
using Verse;

namespace NITH.Patches
{
    /// <summary>
    /// Patch 1 — DropCellFinder.CanPhysicallyDropInto (postfix)
    ///
    /// Blocks drop pod landing on any roofed cell. Vanilla already blocks thick rock;
    /// this extends that to constructed roofs and thin rock roofs.
    /// Covers all per-pawn landing cell searches automatically since every search
    /// path calls CanPhysicallyDropInto.
    ///
    /// Bypassed when NITH_State.BypassRoofCheck is true — set only during the
    /// last-ditch vanilla fallback when every NITH path has failed.
    /// </summary>
    [HarmonyPatch(typeof(DropCellFinder), nameof(DropCellFinder.CanPhysicallyDropInto))]
    public static class Patch_CanPhysicallyDropInto
    {
        public static void Postfix(ref bool __result, IntVec3 c, Map map)
        {
            if (!__result) return;
            if (NITH_State.BypassRoofCheck) return;

            if (c.GetRoof(map) != null)
                __result = false;
        }
    }
}
