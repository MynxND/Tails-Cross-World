# 12 Tails Cross World - Development Handoff

Last updated: 2026-09-11 (Asia/Bangkok)  
Repository branch: `develop`  
Previous implementation commit at batch start: `9ad55c0 Add Wolf Blade Fang skill`

## 1. Objective and non-negotiable requirements

The user is rebuilding the offline 12 Tails client as a maintainable Unity 6 game with a new authoritative server. Development is divided into phases. The immediate work is Phase 4: migrate the original production assets and make the chapters playable.

The user explicitly requires the original character, monster, map, and environment assets wherever they exist. Do not replace them with newly invented lookalikes. Visual assets should match the installed offline game as closely as the extracted source permits. Runtime gameplay code is being reimplemented in C# because the original UnityScript and server are obsolete or unavailable.

Keep these boundaries:

- Treat the original installation and AssetRipper export as read-only evidence.
- Never execute legacy scripts as part of inspection or import.
- Never commit original binaries, extracted assets, saves, logs, reports, or private asset payloads.
- Store imported production assets only under `client/Assets/TwelveTails/LegacyPrivate/`; this directory is ignored by Git.
- Store generated evidence and machine-readable audit output under `artifacts/`; this directory is ignored by Git.
- Commit reusable code, tests, schemas, documentation, and editor tooling.
- Preserve original `.meta` files and GUID relationships when copying AssetRipper output.

Architecture decision record: `docs/architecture/0001-clean-room-boundary.md`.

## 2. Local environment

Workspace:

```text
C:\Users\nikza\Desktop\Co-Work\-=[ 12tailsoffline ]=-
```

Important local paths:

```text
Original offline game:
D:\CaseShop\12tails-legacy-extracted\-=[ 12tailsoffline ]=-

AssetRipper Unity export:
D:\CaseShop\12tails-legacy-unity-export\ExportedProject

Game specification PDF:
D:\CaseShop\12-tails-game-spec-roadmap.pdf

Unity Editor:
C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe

Unity project:
client\

Windows development build:
client\Builds\Windows\TwelveTailsPrototype.exe
```

The user has installed Unity Hub/Unity 6000.6.0f1 and Blender. The Unity project version is recorded in `client/ProjectSettings/ProjectVersion.txt`.

Codex's bundled Python is available at:

```text
C:\Users\nikza\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe
```

PowerShell scripts already locate this Python automatically where practical.

## 3. Current phase status

### Phase 0 - Preserve and inventory

Substantially complete:

- Public/private boundary established.
- Read-only reference inspector and SHA-256 inventory implemented.
- Engine, architecture, assemblies, level files, shared assets, and sensitive paths detected.
- Tests and public-tree policy checks added.

### Phase 1 - Recover knowledge

Partially complete:

- Assembly inventory tool exists.
- Initial system, protocol, persistence, and payload catalogs exist.
- Versioned domain contracts exist for Account, Character, Item, Equipment, Quest, Monster, and Map.
- Strict parsing and invalid-input rejection exist.
- NRBF/BinaryFormatter save probe exists and does not deserialize untrusted data.

Still incomplete:

- Review and sanitize the full assembly inventory into final type/system summaries.
- Complete protocol/RPC semantics beyond the eight priority payloads.
- Reconcile mission codes, scene parts, and the complete chapter structure.

### Phase 2 - Offline vertical slice

Complete for the prototype scope:

- Unity 6 URP project.
- Player movement, follow camera, melee attack, health, enemy, quest, reward, save/load, corrupted-save rejection, NPC placeholder, portal marker, HUD.
- Windows x64 development build.
- EditMode tests.

Current controls:

```text
WASD  Move
Space Attack
F5    Save
F9    Load
```

The current quest defeats one Carron, grants 25 EXP and one potion, and saves automatically.

### Phase 3 - LAN authoritative server

Complete for the planned LAN prototype:

- Session/account state.
- Two-player lobby authority and host migration.
- Versioned TCP transport.
- Authoritative map state, movement, combat, rewards, reconnect, and atomic persistence.
- Rejection of forged/replayed movement and invalid combat actions.
- Two-client TCP integration coverage.

Server source is under `src/server/`.

### Phase 4 - Content pipeline

Active.

Completed:

- Canonical roster of 12 characters and their base classes.
- Data-driven training chapter and character definitions.
- Cross-reference and 12-variant validation.
- Original 12 character models imported into the private asset tree.
- Character materials converted to URP.
- Cat default hair accessory restored.
- Original M101 Carron Harvest map imported.
- M101 terrain, colliders, renderers, materials, fences, gates, and Carron model validated.
- Original Carron extracted as a reusable monster prefab with three original animation clips.
- Carron connected to the offline combat/quest/reward/save loop.
- Original Mupo visual prefab extracted from the M102 source scene with converted URP material.
- M102 herd-rule state machine added with unique live-Mupo counting and fail-on-death tests.
- M102 playable prototype scene generated with six original Mupo actors, source-derived spawn positions, pen trigger, flee behavior, reward, save, and HUD objective.
- M102 herd progress now persists through save schema v2, with schema-v1 read compatibility and checksum coverage.
- M102 LAN map mode added with server-owned herd completion/failure, reward authority, and Unity client herd event dispatch.
- M102 lethal Mupo damage now fails the mission through the real Health and melee runtime path.
- M102 TCP integration test now covers map selection, herd events, and server-owned completion reward.
- Shared skill content foundation added: three validated skills, animation profiles for all 12 characters, and runtime cooldown/resource/damage execution.
- Unity scene builders now load skill definitions from `content/v1/skills.json` instead of duplicating combat values in editor code.
- Unity scene builders also load `content/v1/animation_profiles.json`; `SkillExecutor` resolves each skill clip from the selected character profile with a definition fallback.
- Canonical 12-chapter catalog added with eight-stage structure, evidence/status fields, and strict validation.
- M103 source inventory confirmed original `StingBug_green` actors, and reusable `OriginalMonsters/StingBug.prefab` was extracted with one renderer and one converted material.
- Quest objectives and enemy rewards are now configurable by target ID/count, providing the shared objective path needed by M103-M108 and later chapters.
- Reusable `MonsterChase` AI added with detection, movement, attack range, cooldown, and damage parameters for StingBug and later monsters.
- M103 original environment and GoatFarmer prefabs are extracted from separate legacy `SceneObjects` and `Team` hierarchies without executing legacy scripts.
- M103 Bug Trouble local playable scene generated with the original map, GoatFarmer, five StingBugs, shared chase/attack AI, protect-target failure, defeat objective, reward, save trigger, and HUD state.
- `DefeatAndProtectMission` owns M103 completion/failure ordering so a defeated protected target cannot later grant mission rewards; focused Unity EditMode tests pass (`15/15`).
- M104 original `StingNest` and red StingBug actors extracted with reusable fail-closed visual validation.
- M104 Stingbug Nest is the first manifest-driven mission: source positions define seven nests, five green StingBugs, four red StingBugs, and the player spawn; the validator requires objective count to equal matching actor positions.
- Source-confirmed StingNest behavior now spawns two green and one red StingBug on first damage, guarded against duplicate spawning. Full Unity EditMode tests pass (`16/16`) and Python/content/policy tests pass (`35/35`).
- M104 Windows development build completed, reached input-idle, remained responsive, and produced zero matched startup runtime issues.
- Shared quest runtime now supports both defeat and interaction progress without breaking the existing `RegisterDefeat` API; `QuestInteractionTarget` handles idempotent interaction rewards.
- M105 Needle Cave local slice is generated from the same mission manifest/builder with the original map, MiniCat, six extracted NeedleBug color prefabs, nine source-positioned enemies, and the source-confirmed TalkToMiniCat completion objective.
- M105 Windows development build completed, reached input-idle, remained responsive, and produced zero matched startup runtime issues. Full Unity EditMode tests pass (`17/17`) and Python/content/policy tests pass (`35/35`).
- Shared mission runtime now supports recurrent knockout objectives: `KnockoutObjectiveTarget` restores the same actor after each knockout, counts rounds, grants rewards once, and is reachable through the real melee attack path.
- M106 Boldas Recruitment local slice is generated from the mission manifest with the original map and extracted Boldas visual. Source-backed values include StartPoint1/Boldas transforms, KO gauge `30`, speed `5`, attack `32`, and the three-knockout objective from event `1061`.
- M106 Windows development build completed, reached input-idle, remained responsive, and produced zero matched startup runtime issues. Full Unity EditMode tests pass (`19/19`) and Python/content/policy tests pass (`36/36`).
- Mission manifests now allow actorless interaction missions while still rejecting actorless defeat/knockout missions.
- M107 Request From Alcacia local slice is generated from the mission manifest with the original Light Palace map, source-positioned player, and extracted LightGod/Alcacia visual. Source code sends the sole completion event `1071` only at the end of `TalkToLightGod`; optional NPC conversations and actor KO/death handlers do not complete the mission.
- M107 Windows development build completed, reached input-idle, remained responsive, and produced zero matched startup runtime issues. Full Unity EditMode tests pass (`19/19`) and Python/content/policy tests pass (`37/37`).
- M101 Carron Hunt is now a source-backed manifest mission with the original environment, StartPoint1 player spawn, six initial Carron positions, six replacement spawn positions, objective count 12, and source actor values (HP 12, attack 2, speed 3).
- The recreated combined Windows build now starts at `ChapterMenu.unity`; its Chapter 1 button loads `CarronHarvest.unity` instead of the prototype `TrainingGround.unity`. The real Windows UI click path was exercised successfully and emitted `Loading Chapter 1: CarronHarvest` with a responsive process and no matched runtime errors.
- Full Unity EditMode tests pass (`20/20`) and Python/content/policy tests pass (`37/37`) after the M101 routing fix.
- M108 One On One Bout local slice uses the original arena, source StartPoint1/StartPoint2 positions, an original Bison visual as the offline opponent, and duel semantics derived from team events `1081`/`1082`/`1083`. Defeating the opponent completes the objective; player defeat fails it.
- M108 scene generation and standalone Windows build completed successfully. The Windows build remained responsive and its fresh startup log contained no matched critical runtime errors. Full Unity tests remain `20/20` and repository checks remain `37/37`.
- Phase 4B animation-driven combat has started. Skill definitions now carry strict `hit_delay_seconds` timing; `SkillExecutor` starts the animation immediately, applies damage only at the configured impact time, blocks overlapping wind-ups, and supports normal, Mupo, and recurrent-knockout targets through the timed path.
- Phase 4B action recovery and combo windows are complete. Skills now declare strict action duration and combo timing/reference fields; movement remains locked through recovery, one valid follow-up may queue during its configured window, and normal melee input uses the same timed `skill.basic_slash` path instead of applying immediate damage. Unity gameplay tests pass (`25/25`) and repository checks pass (`39/39`).
- All 12 generated character Animator Controllers now expose the `nAttack1`, `nAttack2`, and `cAttack1` states declared by `animation_profiles.json`, and `AnimatorMotionDriver` resolves them with Unity full-path hashes instead of silently missing every mapped state. The generated FBX files currently contain only `CharacterRig|Attack`, `CharacterRig|Idle`, and `CharacterRig|Run`, so mapped skill states explicitly reuse `Attack` until original per-character clips are retargeted. Legacy base clips exist but target paths such as `Monkey_tri/root`, which are incompatible with the clean-room rigs without retargeting. Unity gameplay tests pass (`26/26`), repository checks pass (`39/39`), and the rebuilt combined Windows player passed a responsive startup smoke test with no matched critical runtime errors.
- The engineering boundary is now documented as controlled compatibility reconstruction rather than a claim of strict legal clean-room independence. Authorized original assets and static/decompiled behavior evidence may be used privately for fidelity, but extracted scripts still never execute, private payloads remain untracked, and the new server remains authoritative.
- Original character prefabs now receive a clean `LegacyAnimationDriver` and rig-matched legacy clips during private import. All 12 original characters play their source primary attack through Unity's legacy `Animation` component; Rabbit correctly maps its basic attack to `nAttack` rather than the generic `nAttack1`. Missing optional source clips currently use deterministic fallback: Chameleon `nAttack2`, Rabbit `nAttack2`, and Mole `cAttack1`. Full Unity gameplay tests pass (`27/27`), repository checks pass (`39/39`), and the rebuilt Windows player remained responsive with no matched critical runtime errors.
- All 12 original character rigs now import source `ko` and `getUp` clips. `KnockoutAnimationDriver` plays the knockout clip and starts recovery after that clip's real duration without delaying health restoration, objective progress, or rewards. M106 attaches this presentation layer to Boldas; Unity gameplay tests pass (`28/28`), repository checks pass (`39/39`), and the dedicated M106 Windows player remained responsive with no matched critical runtime errors. No compatible `hit` clip was found for any of the 12 character rigs, so nonlethal hit reactions remain intentionally unimplemented rather than using the wrong animation.
- Phase 4B.3 defeat presentation now covers normal enemies and players. `DefeatAnimationDriver` resolves the original legacy `ko` clip already attached to the visual (`ko_115` for Carron and `ko_93` for StingBug), disables movement/AI/collision immediately, preserves immediate mission/reward authority, and delays only visual deactivation for the real clip duration. Player KO keeps the root active for a later recovery flow. Missing enemy clips fail closed to immediate deactivation. Mupo still deactivates immediately because no compatible source-backed Mupo KO clip has been established.
- Phase 4B.4 skill runtime now includes deterministic resource regeneration, cooldown/action/combo lifecycle events, reusable projectile delivery, status duration/tick/slow/stun handling, movement integration, and strict schema validation. The Python server loads the shared skill catalog and validates actor ownership, skill ID, cooldown, resource, target, range, and aim before applying authoritative damage/rewards. LAN clients send intent-only `action_request` messages, consume authoritative resource snapshots, and disable local damage execution while online. Current content keeps projectile/status fields neutral until class-specific source evidence is mapped.
- Phase 4B.3/4B.4 validation passes: Unity gameplay EditMode tests `36/36`, repository Python/content/protocol/policy tests `41/41`, and C# diagnostics report no errors. Combined M101 and dedicated M103 Windows builds complete successfully; both executables reached responsive input-idle in a short smoke launch. The custom player log files were not emitted during that short launch, so runtime log scanning remains to be repeated during visual QA.
- Phase 4B.5 has its first evidence-backed class skill: Sheep `cAttack` resolves from the shared class-special input to `skill.sheep_class_special`. Legacy evidence establishes projectile speed `8`, life `5 * rangeMod` (baseline `5s`), and homing rotation `0.1` radians every `0.1s` (`1 rad/s`). The server persists the selected character and rejects class skills used by another character; LAN action intents now include character ID and support power/class-special inputs. Damage, cooldown, resource cost, and action timing remain prototype tuning until stronger source evidence is mapped. Unity gameplay tests pass (`37/37`), repository checks pass (`42/42`), and the combined Windows build completes successfully.
- Phase 4B.5 now includes Mole `stunGrenade` on input `4` as `skill.mole_stun_grenade`. Source-backed values are animation `grenade`, fire delay `0.6s`, recovery `0.8s`, speed `15`, lifetime `3 * rangeMod` (baseline `3s`), fallback range `12`, AoE radius `6 * rangeMod` (baseline `6`), and base damage `10 * level` (level-one baseline `10`). The reusable projectile runtime deduplicates multiple colliders per target and damages every valid target inside the impact radius. The base skill intentionally applies no stun because legacy source grants status `264` for `30s` only when the Smart Shell passive is active; passive/loadout state is not implemented yet. The private Mole prefab now includes the rig-compatible original `grenade.anim`; the projectile keeps the generic visual fallback because no imported `stunGrenade` effect prefab was found. Both client and authoritative server restrict the skill to Mole. Unity gameplay tests pass (`38/38`), repository checks pass (`43/43`), C# diagnostics report no errors, and the combined Windows build completes successfully.
- Phase 4B.5 now includes Wolf Blade Fang level 1 on input `5` as `skill.wolf_blade_fang`. The shared runtime supports validated multi-hit sequences; source timing produces two damage pulses at `0.7s` and `0.9s` with recovery ending at `1.1s`. The authoritative server owns both hits and rejects the skill for non-Wolf characters. The private Wolf prefab includes the rig-compatible original `bladeFang1`, `bladeFang2`, and `bladeFang3` clips, while current presentation starts with `bladeFang1`. Damage/resource/cooldown remain prototype values until the character ATK, talent adjustment, skill cost, and Double Art timeout models exist. The source dash, rectangular hit volume, alternating knockback, SP gain, later animation transitions, effects/audio, and Blood Fang passive 403 remain intentionally deferred rather than approximated as complete. Unity gameplay tests pass (`39/39`), repository checks pass (`45/45`), C# diagnostics report no errors, and the combined Windows build completes successfully.
- Batch A evidence records now cover Bison, Panda, Whale, Rabbit, Monkey, Penguin, Bat, Chameleon, and Cat under ignored `artifacts/skill-evidence/`. Direct caller/projectile source corrected several initial summaries: Bison is charge-count-driven, Panda has three non-uniform hit times, Monkey Fireball has a pre-cast phase before `cast3`, Whale Javelin persists and can hit repeatedly, and Cat Support Fire requires summon/controller behavior.
- Phase 4B.5 now includes Panda Three Steps level 1 through the shared character-skill slot on input `4`. The runtime adds reusable explicit hit schedules, oriented boxes, multi-target application, and collider deduplication. Source-backed behavior is three box hits at `0.4s`, `0.7s`, and `1.3s`, a `30s` base timeout before AGI adjustment, and the rig-compatible `threeSteps` animation. Damage `10` and resource cost `0` are prototype values because the current catalog cannot evaluate the source attack/focused-art/talent formula or recover an authoritative cost. The Nine Steps passive, movement phases, SP/combo side effects, VFX, and audio remain deferred. Both client and server enforce Panda ownership; the server owns all three damage applications. Repository checks pass (`48/48`), Unity gameplay tests pass (`45/45`), C# diagnostics report no errors, the private Panda prefab references the verified `threeSteps` clip GUID, and the combined Windows build reached responsive input-idle.
- Character-skill development uses the repository-wide batch process in `docs/SKILL_BATCH_WORKFLOW.md`. Batch A is complete for all 12 characters. Continue Batch B by implementing grouped shared primitives; do not re-audit source unless an evidence record is missing, contradictory, or `unknown` for a required field.
- All 211 exported legacy scenes audited without running legacy code.
- Chapter 1 dependency closure imported: Tutorial 1-3 and M101-M108.
- Chapter 1 Unity source-scene validation added.
- Clean Unity 6 environment prefabs generated for all 11 Chapter 1 scenes.

Still incomplete:

- M108 LAN two-player authority, countdown presentation, exact character combat values, weapon trails, and visual QA. The local slice uses an AI opponent while preserving win/fail semantics.
- M102 Windows smoke test and visual QA for terrain, pen geometry, Mupo scale, animation, collision, and camera.
- M102 exact legacy pen geometry comparison.
- M102 Unity-client smoke test against the LAN server and lobby/map persistence across server restart.
- M103 LAN authority, protected-target failure persistence, and visual QA for actor scale, material, animation, collision, and camera. The dedicated M103 Windows development build completed, reached input-idle, remained responsive, and produced a startup `Player.log` with zero matched runtime exceptions/errors.
- M104 LAN authority and visual QA for nest/monster scale, animations, collision, camera, and first-hit spawn timing. Legacy reward values came from the unavailable server, so the manifest intentionally grants zero reward until authoritative evidence is recovered.
- M105 opening/ending dialogue, Warthog/MiniSheep interactions, replacement NeedleBug waves after kills, LAN authority, visual QA, and server-derived rewards. The local slice currently covers cave traversal, combat, and the source-confirmed MiniCat end interaction.
- M106 opening/ending dialogue, true KO-gauge behavior, exact recovery timing/state fidelity, Boldas combat AI fidelity, LAN authority, visual QA, and server-derived rewards. The local slice models the source KO gauge as resettable objective health, now plays source `ko`/`getUp` animations, and intentionally grants zero reward pending authoritative evidence.
- M107 full Alcacia dialogue/camera sequence, optional RedPanda/Falcon/Baboon/Walrus/Panther conversations, LAN authority, visual QA, and server-derived rewards. The local slice covers navigation and the source-confirmed Alcacia completion interaction.
- Mission objectives, NPC dialogue, cutscenes, portals, spawn waves, fail conditions, bosses, drops, and rewards.
- Monster extraction and runtime AI beyond Carron.
- Dynamic effects, trails, and particle conversion.
- Runtime NPC appearance/material assignment.
- Chapter 2-12 selector routes, campaign map graph, and inter-mission transitions. The combined build now has a working Chapter 1 route.
- Remaining chapters, towns, lobbies, guild scenes, arenas, events, and multi-part dungeons.
- Equipment, armor, weapons, animation profiles, VFX, and LOD validation at production scale.
- Full skill library mapping, character-specific skill animation fidelity, and VFX/audio timing.

### Phase 5 - Private online alpha

Not started beyond Phase 3 LAN foundations. Needs dev/staging/production environments, TLS, secrets, metrics, backups, restore drills, launcher version enforcement, moderation, load testing, latency testing, crash recovery, and hostile-request testing.

### Phase 6 - Public readiness

Not started. Needs provenance/license review, Git-history review, secret scanning, performance, accessibility, compatibility, reproducible release, deployment, incident response, and rollback procedures.

## 4. Repository layout

```text
client/                         Unity 6 client
  Assets/TwelveTails/
    Editor/                     Unity editor generators and validators
    Runtime/                    Gameplay runtime scripts
    Tests/EditMode/             Unity EditMode tests
    Resources/Characters/       Public/generated fallback character prefabs
    LegacyPrivate/              Original assets; ignored by Git
    Scenes/TrainingGround.unity Tracked prototype scene

content/v1/                     Versioned data-driven content
docs/                           Roadmap, architecture, analysis, this handoff
src/domain/                     Strict versioned domain contracts
src/protocol/                   Versioned network messages
src/server/                     LAN authoritative server
tests/                          Python unit/integration/policy tests
tools/                          Inspectors, validators, importers, Blender tools
scripts/                        Test, server, and asset-generation entry points
artifacts/                      Local reports/logs; ignored by Git
output/pdf/                     Generated development status PDF
```

## 5. Original asset migration

### Characters

Private generated character prefabs:

```text
client/Assets/TwelveTails/LegacyPrivate/Resources/OriginalCharacters/
```

All 12 characters have original visual assets. `LegacyAssetValidator.GenerateOriginalCharacterPrefabsIfAvailable()` regenerates them, applies default appearance, converts materials, and attaches Cat's default hair.

The generated public fallback characters under `client/Assets/TwelveTails/Resources/Characters/` remain useful when private assets are unavailable. `CharacterSelector` prefers the original private resource path when present.

### M101 and Carron

Source scene:

```text
client/Assets/TwelveTails/LegacyPrivate/Scene/M101_CarronHarvest.unity
```

Converted scene:

```text
client/Assets/TwelveTails/LegacyPrivate/Scenes/M101_CarronHarvest_URP.unity
```

Existing M101 environment resource used by `VerticalSliceBuilder`:

```text
client/Assets/TwelveTails/LegacyPrivate/Resources/OriginalMaps/M101_CarronHarvest.prefab
```

Carron resource:

```text
client/Assets/TwelveTails/LegacyPrivate/Resources/OriginalMonsters/Carron.prefab
```

Carron validation:

- One renderer.
- One supported URP material.
- One legacy `Animation` component.
- Three animation clips.
- Default clip name reported as `root`.
- No embedded collider in the visual prefab; gameplay root owns the capsule collider, `Health`, and `EnemyTarget`.

M101 validation after fence/gate repair:

- Renderers: 68 in the full converted source scene.
- Meshes: 14 distinct.
- Materials: 26.
- Colliders: 54.
- Terrains: 1.
- Original Carrons in source: 6.
- Carrons retained in the reusable M101 environment: 0.
- Gates: 3.
- Short fences: 14.
- Long fences: 6.
- Invalid structure meshes: 0.
- Unsupported materials: 0.

### Chapter 1 imported source set

Imported scenes:

```text
M100_GameTutorial1.unity
M100_GameTutorial2.unity
M100_GameTutorial3.unity
M101_CarronHarvest.unity
M102_MupoRoundUp.unity
M103_BugTrouble.unity
M104_StingbugNest.unity
M105_NeedleCave.unity
M106_BoldasRecruitment.unity
M107_RequestFromAlcacia.unity
M108_OneOnOneBout.unity
```

Import result:

- 11 scenes.
- 1,020 dependency assets.
- Approximately 180.9 MiB.
- 30 extra individual meshes recovered for old static-batched objects.
- Zero unresolved GUIDs.
- Zero static-batch names left unresolved by the import planner.

Import plan:

```text
artifacts/chapter-1-import-plan.json
```

Unity source-scene validation:

```text
artifacts/legacy-chapter-1-unity-validation.txt
```

### Chapter 1 environment prefabs

Generated prefabs:

```text
client/Assets/TwelveTails/LegacyPrivate/Resources/OriginalChapter1Maps/
```

Generated converted scenes:

```text
client/Assets/TwelveTails/LegacyPrivate/Scenes/Chapter1/
```

Generation report:

```text
artifacts/legacy-chapter-1-environment-generation.txt
```

Latest successful results:

| Scene | Renderers | Static mesh repairs | Dynamic containers removed | Dynamic mesh placeholders removed |
|---|---:|---:|---:|---:|
| M100_GameTutorial1 | 8 | 0 | 2 | 0 |
| M100_GameTutorial2 | 49 | 0 | 2 | 0 |
| M100_GameTutorial3 | 47 | 13 | 2 | 0 |
| M101_CarronHarvest | 45 | 39 | 1 | 0 |
| M102_MupoRoundUp | 34 | 0 | 2 | 0 |
| M103_BugTrouble | 134 | 0 | 1 | 0 |
| M104_StingbugNest | 39 | 12 | 2 | 0 |
| M105_NeedleCave | 66 | 0 | 2 | 0 |
| M106_BoldasRecruitment | 39 | 0 | 2 | 0 |
| M107_RequestFromAlcacia | 42 | 0 | 2 | 2 |
| M108_OneOnOneBout | 39 | 0 | 3 | 0 |

Every generated environment prefab currently reports:

```text
missingMeshes=0
missingMaterials=0
unsupportedMaterials=0
missingScripts=0
```

The converter removes the `NPC`, `Icons`, and `TestControl` containers from environment prefabs. It also removes known runtime-generated missing-mesh placeholders such as `LineEmitter`, `ImageEmitter`, `TrailEmitter`, `ImageEffect`, and `ZodiacRing`. These must be recreated later through runtime spawn/VFX systems.

Important: `VerticalSliceBuilder` still loads `Resources/OriginalMaps/M101_CarronHarvest`. It has not yet been switched to the newer `Resources/OriginalChapter1Maps/M101_CarronHarvest` path. Compare both prefabs visually before changing the runtime path, then update the builder once the newer prefab is confirmed.

## 6. Full legacy scene audit

Original player files contain 271 `level*` binaries. AssetRipper exported 211 readable Unity scenes. The 60-file difference is not yet reconciled.

Exported scene categories:

| Category | Count |
|---|---:|
| Mission | 186 |
| Town | 8 |
| Lobby | 7 |
| Guild | 9 |
| Account/login | 1 |
| Total | 211 |

Inventory across the 211 scenes:

| Item | Count |
|---|---:|
| GameObjects | 66,727 |
| Renderers | 12,282 |
| MeshFilters | 9,903 |
| Skinned meshes | 2,442 |
| Colliders | 6,721 |
| Terrain instances | 132 across 130 scenes |
| MonoBehaviours | 10,118 |
| Animation/Animator components | 3,233 |
| AudioSources | 1,394 |
| Legacy particle components | 1,047 |
| Lightmapped renderers | 250 across 76 scenes |
| Legacy subset/static renderers | 231 across 25 scenes |
| Fence/gate/door/wall/barrier objects | 1,561 |
| Unresolved non-built-in GUIDs | 0 |

Affected-scene counts:

| Migration area | Scenes |
|---|---:|
| Runtime script reconstruction | 211 |
| Audio migration | 211 |
| Animation validation | 201 |
| Terrain upgrade | 130 |
| Legacy particles/trails | 97 |
| Lightmap rebuild | 76 |
| Legacy static batching | 25 |
| Structure visibility exposed to static-batch loss | 23 |

Static-batch scenes that need individual-mesh repair:

```text
G36_ForestCamp
L16_LobbyForest
M100_GameTutorial3
M101_CarronHarvest
M104_StingbugNest
M405_WindValleyEntrance1
M501_ThroughTheSwamp1
M504_WaterTemple
M707_MachineFromThePast
M902_MadVegetables
M916_CityUnderSiege
M922_DancingHippos
M946_GoldenKingBug
M965_UltimateQuiz
M971_MaohsTomb2
M971_MaohsTomb3
M971_MaohsTomb4
M971_MaohsTomb5
M971_MaohsTomb6
M971_MaohsTomb7
M971_MaohsTomb9
M971_MaohsTomb10
M983_CrystalDefense
M984_SteelChaos
T51_MainStreet
```

Audit outputs:

```text
artifacts/legacy-scene-migration-audit.md
artifacts/legacy-scene-audit.csv
artifacts/legacy-scene-audit.json
artifacts/all-scenes-import-plan.json
```

The JSON/CSV/Markdown reports stay ignored because they are derived from private evidence.

### Chapter-count reconciliation

The user states that the main game has 12 chapters with 8 stages each, or 96 main stages. The exported data does not map one scene file to one stage:

- 186 exported mission scene files.
- 120 unique mission codes in exported scene filenames.
- 140 `case` entries in legacy `MissionData`.
- 90 entries explicitly marked `eMissionType.story` by the recovered script.
- Several missions have multiple scene parts, such as `M205_...1` and `M205_...2`.
- The `M9xx` range includes later story content, arenas, events, and multi-part dungeons.

Do not infer chapter completion from raw `.unity` file counts. Build a canonical 96-stage manifest by correlating:

1. The user's 12-by-8 chapter structure.
2. `MissionData` mission metadata.
3. Exported scene filename codes and numbered parts.
4. The original 271 level binaries/build settings.
5. Portal destinations, lobby entries, and observed offline-game behavior.

Any ambiguity should be recorded with evidence and confidence instead of silently guessed.

## 7. Important migration findings and fixes

### Old Unity static batching

Unity 3.5 scenes can reference a large `Combined Mesh` and store per-renderer subsets in `m_SubsetIndices`. Unity 6 does not reliably reconstruct the old subset selection, leaving GameObjects, colliders, and materials present while the visible fence/gate/structure disappears.

M101 originally exhibited this problem. The fix assigns original individual visual meshes:

```text
Plain_Gate_tri
PlainFence_short
PlainFence_long
```

The generalized importer now searches for likely individual meshes whenever a scene contains non-empty `m_SubsetIndices`. It normalizes `_tri`, `_model_`, `_collision_`, `_n`, separators, and numeric suffixes. Collision/collider meshes are excluded from visual candidates.

The Unity Chapter 1 converter performs a second repair stage by replacing loaded `Combined Mesh` references with the uniquely highest-scoring imported individual mesh. It throws on no match or an ambiguous best match. Keep this fail-closed behavior.

### Legacy dynamic meshes

The following missing `MeshFilter` objects are expected runtime-generated effects rather than lost static assets:

```text
LineEmitter
ImageEmitter
TrailEmitter
ImageEffect
ZodiacRing
```

They occur in LifeAltar effects, weapon trails, CosmoClock effects, and the zodiac ring. Rebuild them with Unity 6 `ParticleSystem`, `TrailRenderer`, meshes generated by new C# code, or VFX Graph as appropriate. Preserve original textures/materials/audio as references.

### Runtime character materials

Some legacy NPC `SkinnedMeshRenderer` components have an empty material slot because the old runtime selected skin/armor material dynamically. This appears in M106-M108, especially all 12 character NPCs in M107. The source-scene validator classifies these as `RUNTIME_CHARACTER_MATERIAL`, not unexplained loss.

When adding NPCs back to playable scenes, use the existing original-character material/appearance pipeline. Do not put arbitrary fallback materials into these slots.

### Materials and shaders

`LegacyAssetValidator.ConvertMaterial` converts legacy materials to `Universal Render Pipeline/Lit`, copies the main texture/color/scale/offset, sets smoothness and metallic to zero, disables culling, and derives opaque/cutout/transparent settings from the old shader name and render queue.

This removes the pink error shader, but visual QA is still required for:

- Alpha cutout thresholds.
- Additive particles.
- Emission.
- Normal maps.
- Water.
- Two-sided foliage.
- Terrain layers.
- Material animation and UV scrolling.

### Terrain and lighting

Terrain positions and height data load in Unity 6, but each of the 130 terrain scenes still needs checks for splat maps, detail meshes, trees, terrain shader, water, bounds, player grounding, and collision.

Legacy lightmap indices are evidence only. Re-bake lighting in Unity 6 after environment extraction. Do not assume old lightmaps are correct simply because renderers retain an index.

### Legacy scripts

Imported `.cs` source is renamed to `.cs.legacy-source` so Unity 6 cannot compile or execute it. Environment prefabs remove Missing MonoBehaviours after their presence is recorded.

Gameplay-critical behaviors must be written anew in C# and validated against observed behavior. Examples include mission state, spawn triggers, AI, escort/herding rules, timers, fail conditions, cutscenes, doors, portals, drops, and boss phases.

## 8. Key code and tools

### Unity editor tooling

`client/Assets/TwelveTails/Editor/LegacyAssetValidator.cs`

Menu commands and callable methods include:

- Validate original character assets.
- Generate original character prefabs.
- Generate original M101 map pilot.
- Validate original M101 map pilot.
- Validate imported Chapter 1 scenes.
- Generate Chapter 1 environment prefabs.

`client/Assets/TwelveTails/Editor/VerticalSliceBuilder.cs`

- Builds `TrainingGround.unity`.
- Loads the private M101 environment when available.
- Creates the fallback ground otherwise.
- Creates player, Carron target, NPC placeholder, portal, camera, light, HUD, save system, and LAN client.
- Builds the Windows development player through `BuildWindows()`.

### Private importer

`tools/import_legacy_assets.py`

Capabilities:

- One or more `--entry` paths.
- One or more `--entry-glob` patterns.
- GUID dependency closure.
- Preserves original relative paths and `.meta` files.
- Renames `.cs` to `.cs.legacy-source`.
- Filters Unity built-in GUIDs.
- Optional static-batch individual mesh discovery.
- Optional JSON manifest.
- Dry run unless `--copy` is passed.

Chapter 1 import command:

```powershell
$python = 'C:\Users\nikza\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $python tools/import_legacy_assets.py `
  --export-project 'D:\CaseShop\12tails-legacy-unity-export\ExportedProject' `
  --entry-glob 'Scene/M1*.unity' `
  --destination-assets 'client/Assets/TwelveTails/LegacyPrivate' `
  --include-static-batch-meshes `
  --manifest 'artifacts/chapter-1-import-plan.json' `
  --copy
```

Complete 211-scene dry-run plan:

```powershell
& $python tools/import_legacy_assets.py `
  --export-project 'D:\CaseShop\12tails-legacy-unity-export\ExportedProject' `
  --entry-glob 'Scene/*.unity' `
  --destination-assets 'client/Assets/TwelveTails/LegacyPrivate' `
  --include-static-batch-meshes `
  --manifest 'artifacts/all-scenes-import-plan.json'
```

Latest complete plan:

- 211 scene entries.
- 7,350 dependency assets.
- Approximately 1,843.7 MiB before subsequent matcher improvements.
- 25 static-batch scenes.

Run the dry plan again before a full copy because the matcher has since been improved.

### Scene audit

`tools/audit_legacy_scenes.py`

Example:

```powershell
& $python tools/audit_legacy_scenes.py `
  --export-project 'D:\CaseShop\12tails-legacy-unity-export\ExportedProject' `
  --output-json 'artifacts/legacy-scene-audit.json' `
  --output-csv 'artifacts/legacy-scene-audit.csv' `
  --output-markdown 'artifacts/legacy-scene-migration-audit.md' `
  --level-file-count 271 `
  --expected-chapters 12 `
  --expected-stages-per-chapter 8
```

### Runtime gameplay files

```text
client/Assets/TwelveTails/Runtime/CharacterRoster.cs
client/Assets/TwelveTails/Runtime/CharacterSelector.cs
client/Assets/TwelveTails/Runtime/PlayerMotor.cs
client/Assets/TwelveTails/Runtime/MeleeAttack.cs
client/Assets/TwelveTails/Runtime/Health.cs
client/Assets/TwelveTails/Runtime/EnemyTarget.cs
client/Assets/TwelveTails/Runtime/QuestProgress.cs
client/Assets/TwelveTails/Runtime/PlayerProgress.cs
client/Assets/TwelveTails/Runtime/ProgressSave.cs
client/Assets/TwelveTails/Runtime/SaveCoordinator.cs
client/Assets/TwelveTails/Runtime/LanGameClient.cs
client/Assets/TwelveTails/Runtime/PrototypeHud.cs
```

### Content and validation

```text
content/v1/characters.json
content/v1/training_chapter.json
tools/content_validator/validate_content.py
src/domain/contracts.py
```

The current content schema is sufficient for the prototype but not for the full chapter campaign. Extend it in a versioned, backwards-compatible way with explicit validation.

## 9. Tests and build commands

Run Python tests, public-tree policy, and content validation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
```

Latest repository validation result during this handoff: 45 tests passed, followed by public-tree and content validation.

Run Unity EditMode tests:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1
```

Latest Unity gameplay result during this handoff: 39 EditMode tests passed. Re-run after any runtime/editor code change when the Unity Editor is closed.

Run the LAN server:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-server.ps1
```

Generate the current training scene through Unity:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath (Resolve-Path 'client') `
  -executeMethod TwelveTails.EditorTools.VerticalSliceBuilder.Build `
  -logFile (Join-Path (Resolve-Path 'artifacts') 'training-ground-build.log')
```

Build Windows:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath (Resolve-Path 'client') `
  -executeMethod TwelveTails.EditorTools.VerticalSliceBuilder.BuildWindows `
  -logFile (Join-Path (Resolve-Path 'artifacts') 'windows-build.log')
```

Unity 6000.6 may launch a worker process and return control to PowerShell before the worker exits. Check `Get-Process Unity`, wait for it to finish, and inspect the requested log for `Exiting batchmode successfully now!` or `Build Finished, Result: Success.` Do not start another batch operation against the same project while the GUI Editor is open.

The character generator recreates tracked Animator Controller files and `TrainingGround.unity`, producing noisy file-ID/line-ending changes. After validation/build, restore generated noise unless the scene/controller change is intentional:

```powershell
git restore -- 'client/Assets/TwelveTails/Generated/Controllers' 'client/Assets/TwelveTails/Scenes/TrainingGround.unity'
```

Never restore intentional source edits indiscriminately.

## 10. Immediate next implementation plan

For character skills, `docs/SKILL_BATCH_WORKFLOW.md` overrides the older one-skill-at-a-time sequencing below. Batch A is complete for all 12 characters. Resume Batch B from the normalized evidence records and direct-source corrections; do not repeat audits unless a required field is missing, contradictory, or `unknown`.

### Priority 1 - Finish M102 verification

M102 is `Mupo Round Up`. The recovered mission text says the player must herd six Mupo into a pen without killing them.

Remaining verification:

1. Compare the generated pen trigger against the original M102 gates/fences and record the exact geometry.
2. Connect lethal player damage to Mupo failure and reject client-side damage authority.
3. Add a Unity-client-to-LAN-server integration test for M102 completion/failure and reconnect.
4. Build Windows and visually test terrain, pen, fences, Mupo scale, animation, collision, and camera.

After these checks, move to M103 rather than expanding M102 with optional VFX/cutscene parity.

### Priority 2 - Shared combat and animation runtime

The reusable skill runtime and defeat presentation foundation now exist. Continue with evidence-backed content and presentation fidelity:

- Keep original per-character clips on their matching original rigs through `LegacyAnimationDriver`; retarget only when a clip must run on a generated rig, and never attach clips whose transform paths target a different character rig.
- Add a PlayMode-validated animation-event or normalized-time impact adapter while preserving deterministic timing fallback for clips without events. EditMode does not reliably advance crossfaded Animator state progress.
- Implement the remaining grouped primitives before promoting dependent records: Bison charge hold/release; Monkey cast phases; Whale persistent projectile contact ticks; Rabbit trap/sticky status; Penguin defensive block; Bat/Chameleon spawned or channelled area behavior; Cat summon/controller behavior.
- Continue mapping recovered class skills to projectile/status values only where source evidence supports them; Sheep, Mole, Wolf, and Panda now have initial records while unresolved values remain explicitly prototype or deferred.
- Bind `SkillStarted`, `SkillReleased`, and `SkillEnded` to source-backed VFX/audio assets and timing.
- Add evidence-backed nonlethal hit reactions where rig-compatible clips exist, and complete player recovery/game-over presentation after KO.
- Add LAN tests for client resource reconciliation and multiple skill types beyond `skill.basic_slash`.

### Priority 3 - General map runtime

Replace the hard-coded training map selection with data-driven definitions containing:

- Map ID and original resource path.
- Spawn locations and rotations.
- Player/team spawn groups.
- NPC definitions.
- Monster groups and respawn rules.
- Trigger volumes.
- Portals and destinations.
- Mission objective graph.
- Lighting/music/weather profiles.
- Safe zones and instance rules.

Keep environment assets separate from runtime entities. This separation is already established by the Chapter 1 converter.

### Priority 4 - Chapter 1 missions M103-M108

Work in order after M102 proves the reusable runtime:

- M103 Bug Trouble: protect Carrons/Goat NPC while defeating Stingbugs. The original StingBug visual and configurable defeat objective path are ready; actor AI/protection/failure logic remains.
- M104 Stingbug Nest: enemy spawn/combat mission and nest environment.
- M105 Needle Cave: cave navigation, enemies, lighting, and objective triggers.
- M106 Boldas Recruitment: Boldas actor, weapon/trail reconstruction, dialogue/combat logic.
- M107 Request From Alcacia: 12 NPC appearances, CosmoClock/ImageEffect/ZodiacRing reconstruction, dialogue/cutscene.
- M108 One On One Bout: duel rules, Boldas weapon trail, win/fail state.

For every mission, first inventory actors, missing scripts, triggers, animations, audio, and runtime-created effects. Then implement one complete objective/reward/save/server loop before moving to the next.

### Priority 4 - Scale migration to all scenes

After Chapter 1 gameplay/runtime patterns stabilize:

1. Re-run the complete 211-scene import dry plan with the latest matcher.
2. Review total size and every ambiguous/unresolved mesh name.
3. Copy in batches by chapter or scene family, not one uncontrolled 1.8+ GiB import.
4. Generalize `GenerateChapterOneEnvironmentPrefabs` into a manifest-driven converter instead of duplicating methods per chapter.
5. Validate every generated prefab for missing mesh/material/script, supported shaders, renderer count, collider count, terrain presence, bounds, and expected dynamic-container separation.
6. Add screenshot/reference comparison checkpoints for every playable scene.
7. Handle the remaining 25 static-batch scenes explicitly and retain the mapping decisions in a private manifest.

### Priority 5 - Reconcile all content

- Build the canonical 12-chapter/96-stage manifest.
- Account for every one of the 271 original level binaries.
- Map multi-part scenes to one logical mission.
- Classify extra content: town, guild, lobby, arena, event, dungeon, tutorial, PvP.
- Extract and validate all monsters, bosses, NPCs, structures, weapons, armor, accessories, mounts, VFX, audio, and animation clips.
- Record provenance and private/public status for every production asset.

## 11. Definition of done per map

A map is not complete merely because its scene opens. Require all of the following:

- Original environment meshes visible and correctly positioned.
- No Combined Mesh/subset loss.
- No pink/error materials.
- Correct opaque, cutout, transparent, emission, and two-sided rendering.
- Terrain height, layers, trees, details, water, collider, and bounds validated.
- Lighting rebuilt or deliberately configured.
- Expected colliders and trigger volumes present.
- Dynamic actors removed from the environment prefab and spawned by runtime data.
- Original actor model, default appearance, scale, orientation, skeleton, and required animations validated.
- Runtime scripts reimplemented in C#.
- Objective, completion, failure, reward, save/load, reconnect, and server authority implemented.
- Portals and destination maps validated.
- Audio and VFX triggered correctly.
- Automated validators/tests pass.
- Windows build smoke test passes.
- Visual comparison against the user's offline game is reviewed.

## 12. Known risks and limitations

- The private AssetRipper output is large and not a complete editable reconstruction of original Unity source.
- 60 original player level files have not yet been reconciled with exported scenes.
- Some visual data was only recoverable by undoing legacy static batching.
- Some meshes/materials were generated or assigned at runtime and cannot be recovered by scene dependency closure alone.
- Removing Missing Scripts is safe for environment extraction only after their names/counts are recorded; it does not recreate gameplay.
- Original animation clips can load while still having incorrect wrap mode, binding, events, scale, or root motion.
- A supported URP shader does not prove visual fidelity.
- Environment prefab generation intentionally excludes NPC/effect containers; those assets must return through data-driven runtime spawning.
- M101 currently functions as a prototype map with one stationary Carron target. Carron AI, attacks, hit/death reactions, sound, drops, and respawn remain incomplete.
- The current HUD and map selection are developer prototypes.
- The generated development-status PDF predates the latest all-scene audit and Chapter 1 environment conversion and should be regenerated before external review.

## 13. Recent commits

```text
eee9585 Generate Chapter 1 environment prefabs
26696c5 Add batch legacy chapter import validation
6e11304 Audit all legacy scene migration risks
5eeb944 Restore M101 gate and fence meshes
7b1d761 Integrate original Carron combat target
8121a87 Integrate original M101 map pilot
4df57bc Add development status PDF generator
66997fd Restore Cat default hair accessory
b59c456 Convert original character materials for URP
1ad1d3c Integrate private original character assets
dc38cc2 Fix Blender armature deformation on Unity import
8f30622 Add Blender character production pipeline
```

Before starting new work, run `git status --short`, read `docs/ROADMAP.md`, this file, and the relevant source/report files. Keep the branch clean after each validated commit.

