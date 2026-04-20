using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NITH
{
    /// <summary>
    /// Finds a valid drop pod raid center restricted to open sky.
    ///
    /// Pass 1 — vanilla-style proximity: 300 random attempts near colony pawns.
    /// Pass 2 — broad map sample: requires enough open area for the estimated raid size.
    /// Pass 3 — broad map sample: no area requirement; any open-sky cell near the colony.
    /// Pass 4 — no open sky exists anywhere; caller falls back to EdgeDrop.
    /// </summary>
    public static class NITHCenterFinder
    {
        // Width of the area-count window: 21x21 (matches vanilla's radius-10 drop cluster).
        private const int AreaWindowSize = 21;

        // Reused across calls to avoid per-raid allocation.
        private static readonly List<IntVec3> tmpCandidates = new List<IntVec3>();
        private static readonly List<Pawn>    tmpPawns      = new List<Pawn>();

        public static int EstimatedPawnCount(float points)
        {
            return Mathf.CeilToInt(points / NITHMod.Settings.pointsPerPawnDivisor);
        }

        /// <summary>
        /// Counts open-sky cells in the 21x21 square centered on <paramref name="center"/>.
        /// Uses roofGrid directly rather than CanPhysicallyDropInto for performance.
        /// </summary>
        public static int CountOpenSkyCells(IntVec3 center, Map map)
        {
            int count = 0;
            var roofGrid = map.roofGrid;
            foreach (IntVec3 cell in CellRect.CenteredOn(center, AreaWindowSize, AreaWindowSize).ClipInsideMap(map))
            {
                if (!roofGrid.Roofed(cell))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Returns a valid open-sky center cell, or IntVec3.Invalid if none exists (Pass 4).
        /// </summary>
        public static IntVec3 FindCenter(Map map, float raidPoints)
        {
            int estimatedPawns = EstimatedPawnCount(raidPoints);

            IntVec3 result = TryPass1(map, estimatedPawns);
            if (result.IsValid) return result;

            result = TryBroadSample(map, estimatedPawns, requireArea: true);
            if (result.IsValid) return result;

            result = TryBroadSample(map, estimatedPawns, requireArea: false);
            if (result.IsValid) return result;

            return IntVec3.Invalid; // Pass 4
        }

        // --- Pass 1 ---

        private static IntVec3 TryPass1(Map map, int estimatedPawns)
        {
            var pawns = map.mapPawns.FreeHumanlikesSpawnedOfFaction(map.ParentFaction ?? Faction.OfPlayer);
            if (!pawns.Any())
                return IntVec3.Invalid;

            int radius = AreaWindowSize / 2; // 10
            for (int i = 0; i < 300; i++)
            {
                IntVec3 anchor = pawns.RandomElement().Position;
                IntVec3 spot   = CellFinder.RandomClosewalkCellNear(anchor, map, radius);

                if (!spot.IsValid) continue;
                if (!DropCellFinder.CanPhysicallyDropInto(spot, map, canRoofPunch: true)) continue;
                if (spot.Fogged(map)) continue;
                if (CountOpenSkyCells(spot, map) >= estimatedPawns)
                    return spot;
            }

            return IntVec3.Invalid;
        }

        // --- Pass 2 / Pass 3 ---

        private static IntVec3 TryBroadSample(Map map, int estimatedPawns, bool requireArea)
        {
            int sampleCount = Math.Max(map.Size.x * map.Size.z / NITHMod.Settings.pass2SampleDivisor, 1000);

            tmpCandidates.Clear();
            for (int i = 0; i < sampleCount; i++)
            {
                IntVec3 cell = CellFinder.RandomCell(map);
                if (!DropCellFinder.CanPhysicallyDropInto(cell, map, canRoofPunch: true)) continue;
                if (cell.Fogged(map)) continue;
                if (requireArea && CountOpenSkyCells(cell, map) < estimatedPawns) continue;
                tmpCandidates.Add(cell);
            }

            if (tmpCandidates.Count == 0)
                return IntVec3.Invalid;

            tmpPawns.Clear();
            tmpPawns.AddRange(
                map.mapPawns.FreeHumanlikesSpawnedOfFaction(map.ParentFaction ?? Faction.OfPlayer));

            // No pawns on map — return any valid candidate.
            if (tmpPawns.Count == 0)
            {
                IntVec3 fallback = tmpCandidates[0];
                tmpCandidates.Clear();
                return fallback;
            }

            int   earlyExitDistSq = NITHMod.Settings.earlyExitDistanceTiles * NITHMod.Settings.earlyExitDistanceTiles;
            int   limit           = NITHMod.Settings.pass2CandidateLimit;
            IntVec3 bestSpot      = IntVec3.Invalid;
            float   bestDistSq    = float.MaxValue;
            int     evaluated     = 0;

            foreach (IntVec3 candidate in tmpCandidates)
            {
                if (evaluated >= limit) break;
                evaluated++;

                float distSq = NearestPawnDistSq(candidate, tmpPawns);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestSpot   = candidate;
                }

                if (distSq <= earlyExitDistSq) break;
            }

            tmpCandidates.Clear();
            tmpPawns.Clear();
            return bestSpot;
        }

        // --- Helpers ---

        private static float NearestPawnDistSq(IntVec3 cell, List<Pawn> pawns)
        {
            float best = float.MaxValue;
            foreach (Pawn pawn in pawns)
            {
                float d = cell.DistanceToSquared(pawn.Position);
                if (d < best) best = d;
            }
            return best;
        }
    }
}
