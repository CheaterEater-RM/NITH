using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NITH
{
    public enum CenterFindResult
    {
        Found,      // valid open-sky center returned
        TooSmall,   // open sky exists but not enough for the raid — use EdgeWalkIn
        NoOpenSky,  // no valid landing cells anywhere on the map — use EdgeWalkIn, then vanilla
    }

    /// <summary>
    /// Finds a valid drop pod raid center restricted to open sky.
    ///
    /// Pass 1 — vanilla-style proximity: 300 random attempts near colony pawns.
    /// Pass 2 — broad map sample: requires enough open area for the estimated raid size.
    /// Pass 3 — broad map sample: no area requirement; any open-sky cell near the colony.
    /// Pass 4 — exhaustive: all map cells in random order.
    ///   If valid cells found but fewer than estimatedPawns → TooSmall.
    ///   If no valid cells at all → NoOpenSky.
    ///
    /// All cell validity checks use CanPhysicallyDropInto consistently.
    /// The 21x21 area count window uses roofGrid directly for performance since it is
    /// called many times per pass and roof status is the only meaningful criterion there.
    ///
    /// Passes 2/3 intentionally pre-filter out all roofed cells before calling
    /// CanPhysicallyDropInto. Although CanPhysicallyDropInto is called with
    /// canRoofPunch: true (to reuse vanilla's other validity checks), NITH only wants
    /// open-sky centers — thin-roof cells would be rejected by Patch 1 during pod
    /// placement anyway. The pre-filter also improves sampling efficiency on
    /// nearly-fully-roofed maps.
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
        /// Uses roofGrid directly for performance — called many times per pass.
        /// Intentionally an approximation: walls inside the window are not excluded,
        /// but this is acceptable for the purpose of selecting a landing area center.
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
        /// Counts all valid landing cells on the entire map using CanPhysicallyDropInto.
        /// Only called from the exhaustive pass — acceptable cost on a rare path.
        /// Uses the same criterion as FindAnyOpenCell for consistency.
        /// </summary>
        private static int CountAllValidCells(Map map)
        {
            int count = 0;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (DropCellFinder.CanPhysicallyDropInto(cell, map, canRoofPunch: true))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Main entry point. Returns the result type and sets <paramref name="center"/>
        /// to a valid cell on Found, or IntVec3.Invalid on TooSmall / NoOpenSky.
        /// </summary>
        public static CenterFindResult FindCenter(Map map, float raidPoints, out IntVec3 center)
        {
            int estimatedPawns = EstimatedPawnCount(raidPoints);

            center = TryPass1(map, estimatedPawns);
            if (center.IsValid) return CenterFindResult.Found;

            center = TryBroadSample(map, estimatedPawns, requireArea: true);
            if (center.IsValid) return CenterFindResult.Found;

            center = TryBroadSample(map, estimatedPawns, requireArea: false);
            if (center.IsValid) return CenterFindResult.Found;

            return TryExhaustive(map, estimatedPawns, out center);
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
            var roofGrid    = map.roofGrid;

            tmpCandidates.Clear();
            for (int i = 0; i < sampleCount; i++)
            {
                IntVec3 cell = CellFinder.RandomCell(map);

                // Intentionally skip all roofed cells — NITH only wants open-sky centers.
                // CanPhysicallyDropInto is called with canRoofPunch: true below only to
                // reuse vanilla's other validity checks (walkable, not water, not vacuum).
                if (roofGrid.Roofed(cell)) continue;

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

            int     earlyExitDistSq = NITHMod.Settings.earlyExitDistanceTiles * NITHMod.Settings.earlyExitDistanceTiles;
            int     limit           = NITHMod.Settings.pass2CandidateLimit;
            IntVec3 bestSpot        = IntVec3.Invalid;
            float   bestDistSq      = float.MaxValue;
            int     evaluated       = 0;

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

        // --- Pass 4 — exhaustive ---

        private static CenterFindResult TryExhaustive(Map map, int estimatedPawns, out IntVec3 center)
        {
            int totalValidCells = CountAllValidCells(map);

            if (totalValidCells == 0)
            {
                center = IntVec3.Invalid;
                return CenterFindResult.NoOpenSky;
            }

            center = FindAnyOpenCell(map);

            if (totalValidCells < estimatedPawns)
                return CenterFindResult.TooSmall;

            return center.IsValid ? CenterFindResult.Found : CenterFindResult.NoOpenSky;
        }

        /// <summary>
        /// Returns a random valid landing cell from a shuffled full-map scan.
        /// Uses CanPhysicallyDropInto — consistent with CountAllValidCells.
        /// </summary>
        private static IntVec3 FindAnyOpenCell(Map map)
        {
            foreach (IntVec3 cell in map.AllCells.InRandomOrder())
            {
                if (DropCellFinder.CanPhysicallyDropInto(cell, map, canRoofPunch: true))
                    return cell;
            }
            return IntVec3.Invalid;
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
