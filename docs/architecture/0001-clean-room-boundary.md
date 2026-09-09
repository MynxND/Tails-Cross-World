# ADR 0001: Clean-room boundary

Status: accepted

## Decision

The existing Windows build is an immutable, private reference. New source code is written from documented behavior and independently defined contracts. The public repository must not contain legacy executables, Unity data files, managed DLLs, saves, logs, screenshots, extracted assets, decompiler output, credentials, endpoints copied from runtime data, or generated reference reports.

The repository uses a deny-by-default `.gitignore`. CI fails if a tracked file has a known legacy filename, sensitive path segment, prohibited extension, or PE executable signature. The reference inspector is read-only and emits only metadata and checksums to a path outside the reference tree.

## Evidence handling

- Keep the original reference tree private and unchanged.
- Work from a copy when future analysis could mutate a file.
- Record claims with an evidence category and confidence level during Phase 1.
- Do not commit full logs, save data, binary serialization, network captures, or decompiler dumps.
- Review and sanitize small fixtures before committing them; synthetic fixtures are preferred.
- Do not publish assets or code unless provenance and redistribution rights are established.

## Verification

Run the unit tests and `tools/check_public_tree.py`. Before publishing, inspect the complete Git history and run a secret scanner in addition to the current-tree check.
