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
