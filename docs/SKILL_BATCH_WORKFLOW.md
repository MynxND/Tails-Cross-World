# Twelve-Character Skill Batch Workflow

This is the default workflow for implementing recovered character skills. Its purpose is to avoid repeating source searches, schema edits, tests, Unity generation, builds, handoff updates, and commits for every individual skill.

## Scope And Completion

Process the full 12-character roster in three batches:

1. **Evidence and catalog**: audit all characters in parallel and produce normalized records.
2. **Runtime primitives**: implement each distinct mechanic once and map every audited skill onto those primitives.
3. **Integration and validation**: generate bindings/assets/scenes, run table-driven tests, build once, update the handoff, and commit.

A batch is complete only when its artifacts are internally consistent and its listed checks pass. Do not claim full skill fidelity when required runtime primitives or source values remain unresolved.

## Batch A: Evidence And Catalog

Use parallel read-only subagents, ideally one character per task. Keep source excerpts out of the main conversation; each worker should return or write a compact structured record with:

```json
{
  "character_id": "wolf",
  "legacy_skill": "bladeFang",
  "skill_id": "skill.wolf_blade_fang",
  "level": 1,
  "evidence": {
    "animation_clips": ["bladeFang1", "bladeFang2", "bladeFang3"],
    "hit_delay_seconds": {"value": 0.7, "confidence": "proven"},
    "hit_count": {"value": 2, "confidence": "proven"},
    "hit_interval_seconds": {"value": 0.2, "confidence": "proven"}
  },
  "required_primitives": ["melee_multi"],
  "deferred": ["dash", "oriented_box", "knockback", "passive.403"]
}
```

Store machine-generated or private evidence under ignored `artifacts/skill-evidence/`. Promote only reviewed runtime values into tracked `content/v1/skills.json` and `content/v1/animation_profiles.json`.

Confidence meanings:

- `proven`: directly established by source code or a compatible asset.
- `conditional`: applies only with a passive, level, equipment, or state not always present.
- `unknown`: source evidence is insufficient.
- `prototype`: temporary reconstruction value, explicitly not source fidelity.

Audit callers as well as projectile/effect scripts. Callers often own lifetime, timing, level scaling, target selection, cooldown, and passive branches. Verify animation compatibility from curve paths and the target rig, not filename alone.

## Batch B: Runtime Primitives

Group skills by behavior and implement shared primitives instead of character-specific MonoBehaviours:

- `melee_single`
- `melee_multi`
- `projectile`
- `homing_projectile`
- `area_projectile`
- `oriented_area`
- `dash_attack`
- `channel`
- `buff_debuff`
- `summon_trap`

Extend the canonical schema only for reusable behavior. Typical fields include hit sequencing, target shape, movement phases, knockback, resource deltas, animation phases, effect/audio cues, and passive requirements.

Rules:

- Existing skills receive explicit neutral defaults when a schema field is added.
- Validators reject impossible combinations, such as a final hit outside action duration.
- Offline execution and server authority consume the same canonical values.
- The server applies authoritative total outcomes and validates character ownership.
- Conditional passives stay disabled until passive/loadout state exists.
- Missing effects use an explicit generic fallback or no effect; do not substitute unrelated assets.

## Batch C: Integration And Validation

Generate all affected private character prefabs once. Verify expected animation GUIDs in generated prefabs before building.

Prefer table-driven coverage:

- every skill ID is unique and schema-valid;
- every character-specific skill is accepted only for its owner;
- every animation binding resolves;
- each runtime primitive has focused timing, targeting, deduplication, and lifecycle tests;
- exceptional skills get dedicated tests only for behavior not covered by a primitive test.

Validation order:

1. Focused Python/content tests while changing schemas or authority.
2. Focused Unity EditMode tests while changing a runtime primitive.
3. C# diagnostics for touched Unity files.
4. `scripts/test.ps1` once after the batch is integrated.
5. Full `TwelveTails.Tests.GameplayTests` once.
6. Generate private prefabs once and verify required clip GUIDs.
7. Run `TwelveTails.EditorTools.VerticalSliceBuilder.BuildWindows` once.
8. Preserve the pre-build `EditorBuildSettings.asset` scene list and exclude unrelated generated file-ID churn.
9. Update `docs/HANDOFF.md`, create one coherent batch commit, and launch the resulting player.

## Resume Protocol

At the start of a new prompt:

1. Read `.github/copilot-instructions.md`, this file, and the Phase 4B section of `docs/HANDOFF.md`.
2. Inspect `git status` and the latest commits.
3. Read existing `artifacts/skill-evidence/` records before searching legacy source.
4. Resume the first incomplete batch; do not restart completed character audits.
5. Keep the main conversation to concise progress summaries. Delegate broad source inspection to parallel subagents and request compact structured results.

## Current Baseline

Implemented and committed:

- Sheep class-special homing projectile: `513d4ef`.
- Mole stun grenade base AoE, without conditional Smart Shell stun: `1b03c99`.
- Wolf Blade Fang level-one multi-hit timing: `9ad55c0`.

Current full validation baseline:

- Repository Python/content/protocol/policy checks: `45/45`.
- Unity gameplay EditMode tests: `39/39`.
- Combined Windows build: `client/Builds/Windows/TwelveTailsPrototype.exe`.

Next work should begin with Batch A for the remaining nine characters, then identify the smallest set of missing runtime primitives before editing individual skill records.
