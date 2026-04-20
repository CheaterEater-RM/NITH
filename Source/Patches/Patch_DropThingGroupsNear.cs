using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace NITH.Patches
{
    /// <summary>
    /// Patch 3 — DropPodUtility.<>c__DisplayClass3_0.<DropThingGroupsNear>b__0 (postfix)
    ///
    /// DropThingGroupsNear contains a last-ditch fallback that scans the entire map for
    /// any cell passing: c.Walkable(map) && !(c.GetRoof(map)?.isThickRoof ?? false)
    /// This predicate permits thin roofs. We postfix the compiled lambda to additionally
    /// reject any roofed cell, keeping the fallback consistent with Patch 1.
    ///
    /// Bypassed when BypassPodPlacement is true (vanilla fallback raid on sealed map).
    ///
    /// The lambda is a method on a compiler-generated display class; map is a captured
    /// field accessed via Traverse.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_DropThingGroupsNear
    {
        static MethodBase TargetMethod()
        {
            var displayClass = AccessTools.Inner(typeof(DropPodUtility), "<>c__DisplayClass3_0");
            if (displayClass == null)
            {
                Log.Error("[NITH] Patch 3: could not find <>c__DisplayClass3_0 on DropPodUtility — " +
                          "thin-roof fallback is NOT patched.");
                return null;
            }

            var method = AccessTools.Method(displayClass, "<DropThingGroupsNear>b__0");
            if (method == null)
                Log.Error("[NITH] Patch 3: could not find <DropThingGroupsNear>b__0 — " +
                          "thin-roof fallback is NOT patched.");

            return method;
        }

        static void Postfix(object __instance, IntVec3 c, ref bool __result)
        {
            if (!__result) return;
            if (NITH_State.BypassPodPlacement) return;

            Map map = Traverse.Create(__instance).Field<Map>("map").Value;
            if (map == null) return;

            if (map.roofGrid.Roofed(c))
                __result = false;
        }
    }
}
