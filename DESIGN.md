# NITH — Not in the Hospital!
## Design Document

---

## Concept

Drop pod raiders in vanilla RimWorld can punch through constructed roofs and thin rock roofs,
frequently landing inside buildings — hospitals, nurseries, anywhere pawns cluster. NITH
removes this behavior: drop pods can only land under open sky. Raiders must land outside and
walk in.

---

## Scope

- **In scope**: All hostile drop pod raids (human and mechanoid)
- **Out of scope**: Friendly drops, trade drops, player-controlled drops, shuttle landings,
  mech cluster building placement. These all use separate code paths and are unaffected.
- **Mech cluster penalty**: `GetClusterPositionScore` already heavily penalizes roofed cells.
  No additional patch needed.

---

## Key Source Findings

### `DropCellFinder.CanPhysicallyDropInto`
The single chokepoint for cell validity. Currently allows thin roofs when `canRoofPunch=true`.
Roof types:
- Constructed (`RoofGenerated`): `isThickRoof = false` — currently punchable, NITH blocks
- Thin rock (`RoofRockThin`): `isThickRoof = false` — currently punchable, NITH blocks
- Overhead mountain (`RoofRockThick`): `isThickRoof = true` — already blocked by vanilla

### `PawnsArrivalModeWorker_CenterDrop.TryResolveRaidSpawnCenter`
Calls `TryFindRaidDropCenterClose` to find a center near colony pawns. If that fails, falls
back to `EdgeDrop` (walk-in raid). `parms.points` is available here; `parms.pawnCount` is
NOT — it is set after pawns are generated, after arrival.

### `DropCellFinder.TryFindRaidDropCenterClose`
- Picks a random colony pawn position as anchor
- Finds `spot` via `RandomClosewalkCellNear(anchor, map, radius=10)` — **21×21 square**
- Validates via `CanPhysicallyDropInto`
- Retries up to **300 iterations** with a different random pawn each time
- If all 300 fail: `CellFinderLoose.RandomCellWith(CanPhysicallyDropInto)` — full map scan,
  no proximity
- If that fails: returns `false` → caller falls back to EdgeDrop

### `DropPodUtility.DropThingGroupsNear`
Called once for the whole raid, iterates per pawn. For each pawn:
1. `TryFindDropSpotNear(center, canRoofPunch=true, maxRadius=16)`
2. If fails, second attempt with `canRoofPunch=true` (hardcoded)
3. If still fails: `CellFinderLoose.RandomCellWith(c => c.Walkable && !c.GetRoof().isThickRoof)`
   — whole map scan, thin roofs permitted ← **NITH must patch this**

### `TryFindDropSpotNear` per-pawn search geometry
- Starts at radius 5 (**11×11 square**), steps by 1 up to radius 16 (**33×33 square**)
- Each step picks a random valid cell via `CellFinder.TryFindRandomCellNear`
- Validator → `IsGoodDropSpot` → `CanPhysicallyDropInto`
- Already-landed pods block cells (`Skyfaller`/`IActiveTransporter` check) — contention is
  real on large raids in small open areas

### Raid Size Estimation
- `parms.points` is set before center-finding
- Benchmark: 10,000 points → 81 pirates ≈ 123 points/pawn
- Conservative divisor of **100** used (slight overestimate — requires more space than strictly
  needed, never dangerously underestimates)
- Formula: `estimatedPawns = Ceil(parms.points / 100f)`
- Mis-estimation is acceptable — contention scatter is a vanilla problem too; fallback handles it

### Pod Cluster Geometry
- Center chosen within **21×21 square** (radius 10) of a colony pawn
- Pods land within **21×21 square** of center (first pass radius 5, typical tight cluster)
- Area count check window: **21×21** — large enough that big raids can satisfy it in Pass 1,
  small enough to be a meaningful quality gate

---

## Center-Finding Algorithm (4-Pass)

Implemented as a prefix on `TryResolveRaidSpawnCenter` that sets `parms.spawnCenter` and
returns false (skip vanilla) when a center is found. Falls through to vanilla EdgeDrop only
for Pass 4.

### estimatedPawns
```
estimatedPawns = Ceil(parms.points / 100f)
```

### Area Count Check
Count open-sky cells within the **21×21 square** centered on a candidate cell.
Uses `map.roofGrid.Roofed(cell)` directly — NOT `CanPhysicallyDropInto` — for performance.
Valid center requires: `openSkyCount >= estimatedPawns`.

### Pass 1 — Vanilla-style proximity (fast path)
- Up to **300 iterations**
- Each iteration:
  1. Pick random colony pawn position as anchor
  2. Pick random cell within radius 10 of anchor (`RandomClosewalkCellNear`)
  3. Check `CanPhysicallyDropInto` (open sky, walkable, etc.)
  4. Count open-sky cells in 21×21 square via `roofGrid` directly
  5. If count ≥ estimatedPawns → **center confirmed, done**
- Covers the common case (normal base with open areas near pawns) cheaply
- Colony pawn list fetched fresh each iteration (vanilla behavior)

### Pass 2 — Broad map sample (uncommon path)
- Sample `max(mapArea / 60, 1000)` random map cells
- Filter: must pass `CanPhysicallyDropInto`
- Filter: 21×21 open-sky count ≥ estimatedPawns
- Cache colony pawn list **once** before distance checks
- Iterate remaining candidates:
  - If distance to nearest pawn ≤ **7 tiles** (XML-configurable): fire immediately
  - Otherwise: track closest, stop after **100 candidates**
  - Take closest of those 100
- Covers unusual bases (large roofed areas, mountain bases with open perimeter)

### Pass 3 — Best available, no size requirement (rare path)
- Same sampling as Pass 2 (`max(mapArea / 60, 1000)` random cells)
- Filter: must pass `CanPhysicallyDropInto` only — **no area count check**
- Same proximity logic: early exit at 7 tiles, closest of 100 otherwise
- Some pods will scatter via per-pawn fallback, but raid still targets the colony
- Covers: base with only tiny open areas (single cooler vent, narrow gaps)

### Pass 4 — EdgeDrop
- Only reached if no open-sky cell exists on the map at all (fully sealed mountain base)
- Return true, allow vanilla to proceed → vanilla falls back to EdgeDrop naturally

---

## Per-Pawn Landing Cell

### Coverage by Patch 1
`CanPhysicallyDropInto` postfix rejects any roofed cell. This automatically covers the entire
`TryFindDropSpotNear` radius 5→16 search for every pod. No additional per-pawn logic needed
for the main search path.

### Fallback Problem
`DropThingGroupsNear` has a last-ditch fallback:
```csharp
result = CellFinderLoose.RandomCellWith(
    (IntVec3 c) => c.Walkable(map) && !(c.GetRoof(map)?.isThickRoof ?? false), map);
```
This predicate permits thin roofs — directly violating NITH's core guarantee. Reached when
all pods from a large raid have exhausted the 33×33 search (contention from already-landed
pods, or area genuinely too small for the full raid).

Skipping Patch 3 is not acceptable — it would allow the last few pods of large raids to land
inside buildings, undermining the mod's core promise.

### Patch 3 Fix
Transpiler on `DropThingGroupsNear`: replace `ldfld RoofDef.isThickRoof` in the fallback
lambda with a call to `map.roofGrid.Roofed(c)`. One instruction replacement.
Use `.ThrowIfInvalid(...)` so any version break is loud, not silent.

---

## Complete Patch Plan

### Patch 1 — `DropCellFinder.CanPhysicallyDropInto` (postfix)
```
if (__result && c.GetRoof(map) != null)
    __result = false;
```
Covers: all `TryFindDropSpotNear` calls (per-pawn search radius 5→16), all other callers
that pass `canRoofPunch=true`. Simple, single postfix.

### Patch 2 — `PawnsArrivalModeWorker_CenterDrop.TryResolveRaidSpawnCenter` (prefix)
Implements the 4-pass center-finding algorithm. Sets `parms.spawnCenter` directly and returns
false to skip vanilla when a center is found. Returns true (run vanilla → EdgeDrop) for Pass 4.

### Patch 3 — `DropPodUtility.DropThingGroupsNear` (transpiler)
Changes fallback `RandomCellWith` lambda from `!isThickRoof` to `!map.roofGrid.Roofed(c)`.
One `ldfld` replacement. `.ThrowIfInvalid` to catch version breaks loudly.

---

## Excluded Call Sites

| Caller | Reason excluded |
|---|---|
| `TradeDropSpot` | Already uses `canRoofPunch=false` — Patch 1 is moot |
| `TryFindSafeLandingSpotCloseToColony` | Friendly/shuttle landings, `canRoofPunch=false` |
| `FindSafeLandingSpot` | Allied drops, safe landings — `canRoofPunch=false` |
| `MechClusterUtility.SpawnCluster` | Individual pod placement bypasses cell finder; cluster position scoring already penalizes roofed cells heavily |
| `RoyalTitlePermitWorker_CallAid` | Friendly call-in, not hostile |
| `RoyalTitlePermitWorker_DropResources` | Resource drop, not raiders |

---

## Configuration (XML-exposed settings)

| Setting | Default | Notes |
|---|---|---|
| `pointsPerPawnDivisor` | `100` | Lower = more conservative (requires more space) |
| `earlyExitDistanceTiles` | `7` | Pass 2/3: fire early if candidate within this distance of a pawn |
| `pass2SampleDivisor` | `60` | mapArea / this = sample count (min 1000) |
| `pass2CandidateLimit` | `100` | Max candidates evaluated for proximity in Pass 2/3 |

---

## Known Limitations

- **Contention scatter**: On very large raids landing in a small-but-valid open area, the last
  few pods may scatter to a random open-sky cell anywhere on the map (via Patch 3 fallback).
  This is acceptable — it is the same class of problem as vanilla contention scatter, just
  redirected to open sky instead of roofed cells.
- **Estimation error**: `estimatedPawns` is approximate. A raid with unusually cheap pawns
  (many low-point units) may bring more bodies than estimated; a raid with expensive pawns
  (mechs, heavy fighters) brings fewer. Conservative divisor (100) biases toward
  overestimating space requirements, which is the safe direction.
- **Mech cluster individual pods**: `MechClusterUtility.SpawnCluster` places individual mech
  pods via `MakeDropPodAt` on pre-scored cells, bypassing the cell finder. Not patched.
  Cluster scoring already penalizes roofed cells; residual risk is low and accepted.

---

## Save Compatibility

No saved state. Pure behavioral patch. Safe to add or remove mid-save.

---

## Mod Compatibility Notes

- Any mod patching `CanPhysicallyDropInto` should be postfix-compatible with Patch 1
- Any mod patching `TryResolveRaidSpawnCenter` may conflict with Patch 2 depending on
  priority — document in README
- Any mod patching `DropThingGroupsNear` with a transpiler may conflict with Patch 3 —
  document in README
- Mech cluster mods: unaffected (see excluded call sites above)
