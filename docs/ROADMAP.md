# Development roadmap

This roadmap records project goals. The legacy build is evidence, not source code for the new implementation.

## Phase 0 - Preserve and inventory

- [x] Establish a deny-by-default public Git boundary.
- [x] Add a read-only reference inspector and deterministic SHA-256 inventory.
- [x] Detect engine evidence, Windows executable architecture, managed assemblies, level files, shared assets, and sensitive runtime paths.
- [x] Add automated tests and CI policy checks.
- [x] Keep legacy binaries, saves, logs, reports, extracted assets, and decompiler output outside public Git history.

Exit criteria: the inspector works without changing the reference tree, tests pass, and Git tracks no legacy binary or runtime evidence.

## Phase 1 - Recover knowledge

Create evidence-backed assembly/type, system, protocol, RPC, and persistence catalogs. Define schemas for character, item, equipment, quest, monster, map, and account data. Parsers must reject malformed input.

Progress:

- [x] Add a metadata-only assembly inventory tool that does not load or execute legacy DLLs.
- [x] Establish the initial system catalog and evidence/confidence format.
- [ ] Review and sanitize the assembly inventory into evidence-backed type and system summaries.
- [ ] Complete protocol/RPC payload semantics; eight priority payloads now have IL-derived key maps.
- [x] Define versioned client-intent contracts that exclude server-owned damage, healing, rewards, and progression.
- [x] Classify `.zrData` as BinaryFormatter/NRBF using a non-deserializing probe and synthetic tests.
- [x] Define strict version-1 contracts for Account, Character, Item, Equipment, Quest, Monster, and Map.
- [x] Reject unknown fields, invalid versions, invalid numbers, duplicate IDs, and incomplete 12-rig equipment variants.

## Phase 2 - Offline vertical slice

Build one complete quest, combat, loot, reward, save, and reload loop in a new Unity 6 URP project without legacy DLL dependencies.

Progress:

- [x] Create a Unity 6000.6 URP project from the installed official blank template.
- [x] Add the first controllable player, follow camera, training enemy, guide NPC, combat health, quest progress, and HUD scripts.
- [x] Generate the Training Ground scene and pass Unity EditMode tests.
- [x] Produce and smoke-test a Windows x64 development build.
- [x] Add loot, one-time quest reward, versioned checksummed save/load, atomic writes, and corrupted-save rejection.

## Phase 3 - LAN authoritative server

Implement account/session, lobby, map instances, two-player synchronization, reconnects, persistence, and server validation of combat and rewards.

Progress:

- [x] Add the first server-owned session and two-player lobby authority with host migration.
- [x] Expose the authority through a versioned TCP LAN transport and connect the Unity client.
- [x] Add authoritative map state, movement, combat, rewards, reconnects, and atomic persistence.
- [x] Pass a two-client TCP integration test and reject forged/replayed movement and invalid combat requests.

## Phase 4 - Content pipeline

Add data-driven maps, portals, spawns, NPCs, chapters, quests, armor, and weapons with editor validation and character-specific visual variants.

Progress:

- [x] Define the canonical 12-character roster and base classes with prefab addresses.
- [x] Add the first data-driven chapter, map, quest, item, and 12-variant equipment definitions.
- [x] Add automated cross-reference and 12-character variant validation.
- [x] Generate 12 distinct, addressable Unity prefabs and a persistent character-selection screen.
- [x] Drive player/monster spawns, NPC placement, and portal markers from validated chapter data.
- [x] Add an original Unity-native low-poly rig, idle/run/attack motion, weapon socket, armor shell, and class prop to every character prefab.
- [x] Generate reproducible Blender source and FBX exports for all 12 characters, with armatures and Idle/Run/Attack actions wired to Unity Animator Controllers.
- [ ] Replace low-poly geometry with final art-directed meshes, authored skin weights, animation clips, textures, VFX, and LODs.

## Phase 5 - Private online alpha

Operate separate environments with TLS, secrets management, metrics, backups, restore drills, launcher version checks, moderation, load tests, and hostile-request tests.

## Phase 6 - Public readiness

Complete provenance, license, Git-history, secret, performance, accessibility, compatibility, deployment, incident-response, and rollback reviews.
