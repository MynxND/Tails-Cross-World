# 12 Tails Cross World

Clean-room development workspace for a new game inspired by observed behavior of a privately held legacy reference build.

The legacy executable, Unity data, saves, logs, extracted assets, and decompiler output are private evidence. They must never be committed. This repository starts with a deny-by-default `.gitignore`; only the clean-room documentation, inspector source, tests, and CI configuration are allowed.

## Phase 0 quick start

Python 3.10 or later is sufficient; the inspector uses only the standard library.

```powershell
python -m unittest discover -s tests -v
python tools/reference_inspector/inspect_reference.py --root "D:\path\to\legacy-build" --output artifacts/reference-inventory.json
```

The inspector opens reference files only for reading. The output contains file metadata and SHA-256 checksums, but no save contents, log contents, secrets, or extracted game assets. Keep generated reports private unless they have been reviewed and sanitized.

Run all current checks from PowerShell with:

```powershell
.\scripts\test.ps1
```

The tests cover the read-only reference inventory, public Git boundary, versioned domain contracts, and safe `.zrData` format classification. A playable clean-room game begins in Phase 2.

See [the roadmap](docs/ROADMAP.md) and [clean-room boundary](docs/architecture/0001-clean-room-boundary.md).

## Phase 1 assembly inventory

The .NET metadata inspector reads CLI metadata without loading or executing legacy assemblies:

```powershell
pwsh tools/assembly_inspector/Inspect-Assemblies.ps1 -ManagedDirectory "D:\path\to\Game_Data\Managed" -OutputPath "D:\private-reports\assembly-inventory.json"
```

## Phase 2 playable prototype

Open the `client` directory with Unity `6000.6.0f1`, load `Assets/TwelveTails/Scenes/TrainingGround.unity`, and press Play. Use WASD to move, Space to attack the Carron, F5 to save, and F9 to load. Completing the quest grants 25 EXP and one potion, then saves automatically. Run Unity tests with:

Use the character panel at the bottom of the game window to switch between all 12 procedural prototype characters. The selected character is retained for the next run.

## Legacy scene migration audit

Audit every AssetRipper-exported scene without executing legacy scripts:

```powershell
python tools/audit_legacy_scenes.py --export-project "D:\path\to\ExportedProject" --output-json artifacts/legacy-scene-audit.json --output-csv artifacts/legacy-scene-audit.csv --output-markdown artifacts/legacy-scene-migration-audit.md --level-file-count 271
```

The audit reports scene coverage, dependency integrity, legacy static batching, terrain, lightmaps, runtime scripts, animations, particles, audio, and structure-visibility risks. Use its per-scene CSV as the migration checklist before generating Unity 6 prefabs.

Use `--entry-glob` to plan or copy a complete scene group. `--include-static-batch-meshes` recovers likely individual visual meshes that old Unity scenes omitted from their direct GUID dependencies. Add `--manifest` to retain the exact entry, dependency, missing-GUID, and static-mesh plan before copying.

Regenerate all original Blender character sources and Unity FBX imports with:

```powershell
.\scripts\generate-character-art.ps1
```

```powershell
.\scripts\test-unity.ps1
```

After a Windows build has been generated locally, launch `client\Builds\Windows\TwelveTailsPrototype.exe` directly.

When the private original-asset import is present, the build uses the original 12 character models and the M101 Carron Harvest environment. Those assets remain under `client/Assets/TwelveTails/LegacyPrivate/` and are intentionally excluded from Git. The editor builder falls back to the generated prototype art when the private import is unavailable.

## Phase 3 LAN play

Start the authoritative server on the host computer:

```powershell
.\scripts\run-server.ps1
```

Open two Unity clients. In each LAN panel, enter a unique account name and click **Login**. On the first client click **Create Lobby**; copy its six-character lobby code into the second client and click **Join Lobby**. The server owns movement, monster damage, quest completion, EXP, and potion rewards. Port `12712/TCP` must be allowed through the host firewall for other computers on the LAN.
