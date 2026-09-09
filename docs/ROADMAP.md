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
- [ ] Build protocol/opcode/RPC and persistence catalogs with synthetic fixtures.
- [ ] Define the initial domain schemas and malformed-input tests.

## Phase 2 - Offline vertical slice

Build one complete quest, combat, loot, reward, save, and reload loop in a new Unity 6 URP project without legacy DLL dependencies.

## Phase 3 - LAN authoritative server

Implement account/session, lobby, map instances, two-player synchronization, reconnects, persistence, and server validation of combat and rewards.

## Phase 4 - Content pipeline

Add data-driven maps, portals, spawns, NPCs, chapters, quests, armor, and weapons with editor validation and character-specific visual variants.

## Phase 5 - Private online alpha

Operate separate environments with TLS, secrets management, metrics, backups, restore drills, launcher version checks, moderation, load tests, and hostile-request tests.

## Phase 6 - Public readiness

Complete provenance, license, Git-history, secret, performance, accessibility, compatibility, deployment, incident-response, and rollback reviews.
