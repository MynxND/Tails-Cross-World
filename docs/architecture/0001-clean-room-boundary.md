# ADR 0001: Controlled compatibility boundary

Status: accepted

## Decision

The existing Windows build is an immutable, private reference. New runtime and server source code is maintained independently, but compatibility work may use static analysis of extracted scenes, assets, serialized values, decompiler output, and protocol behavior when the user has the rights required to use that material. Exact values and behavior may be translated into reviewed data or new source; legacy scripts are evidence, not executable dependencies.

This boundary does not promise legal clean-room independence or automatic permission to redistribute the original game. Provenance and applicable rights must be reviewed before publishing. It is an engineering and security boundary for private preservation work.

The public repository must not contain legacy executables, Unity data files, managed DLLs, saves, logs, screenshots, extracted assets, decompiler output, credentials, endpoints copied from runtime data, or generated reference reports.

The repository uses a deny-by-default `.gitignore`. CI fails if a tracked file has a known legacy filename, sensitive path segment, prohibited extension, or PE executable signature. The reference inspector is read-only and emits only metadata and checksums to a path outside the reference tree.

## Evidence handling

- Keep the original reference tree private and unchanged.
- Work from a copy when future analysis could mutate a file.
- Record claims with an evidence category and confidence level during Phase 1.
- Do not commit full logs, save data, binary serialization, network captures, or decompiler dumps.
- Review and sanitize small fixtures before committing them; synthetic fixtures are preferred.
- Do not publish assets or code unless provenance and redistribution rights are established.

## Fidelity workflow

- Original art, animation, audio, maps, and serialized configuration may be imported into the ignored `LegacyPrivate` tree and used by local builds.
- Extracted scripts must never execute inside the reconstructed Unity project. Reimplement their behavior in reviewed source and tests.
- The original executable may be observed only from a read-only copy in an isolated, network-restricted environment after its hash and provenance are recorded. Do not automate a reference run when the executable is untrusted or the user has not authorized it.
- Static decompilation and serialized-data inspection may establish behavior, formulas, timings, state names, and protocol semantics. Prefer source-backed values over invented approximations and record uncertainty where evidence conflicts.
- Preserve server authority: compatibility with a legacy client message does not make client-supplied damage, rewards, inventory, or progression authoritative.
- Keep a deterministic fallback for private assets that are absent or incompatible so the public project remains buildable and testable.
- Treat 100% parity as an evidence target, not a guarantee. Unavailable server logic, missing payloads, nondeterministic timing, and unknown external services must be identified explicitly.

## Verification

Run the unit tests and `tools/check_public_tree.py`. Before publishing, inspect the complete Git history and run a secret scanner in addition to the current-tree check.
