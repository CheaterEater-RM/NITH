# NITH — Milestones

## M1 — Core functionality ✓ (design complete, implementation in progress)
- [ ] Project scaffold
- [ ] Patch 1: `CanPhysicallyDropInto` postfix — any roof blocks landing
- [ ] Patch 2: `TryResolveRaidSpawnCenter` prefix — 4-pass center finder
- [ ] Patch 3: `DropThingGroupsNear` transpiler — fallback lambda fix
- [ ] Settings UI (4 tunable parameters)
- [ ] Basic in-game test: normal base, raiders land outside

## M2 — Edge case testing
- [ ] Large roofed base with small courtyard — raiders land in courtyard
- [ ] Mountain base all pawns inside — EdgeDrop fallback fires
- [ ] Single vent tile only — Pass 3 fires, pods scatter to open sky
- [ ] Very large raid (10,000 points) — area count check works correctly
- [ ] No pawns on map (building-only base) — no crash, graceful fallback
- [ ] Mechanoid raid — unaffected or gracefully handled

## M3 — Polish and publish
- [ ] Remove startup Log.Message (or gate behind dev mode)
- [ ] README polish
- [ ] Steam Workshop description
- [ ] Preview image
