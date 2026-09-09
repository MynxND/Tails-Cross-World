#!/usr/bin/env python3
"""Fail when tracked paths look like private legacy evidence."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

BLOCKED_PARTS = {"12tailsonline_data", "savegame", "screenshot", "decompiled", "artifacts"}
BLOCKED_SUFFIXES = {".exe", ".dll", ".assets", ".assetbundle", ".zrdata", ".ttodrop"}
BLOCKED_NAMES = {"maindata", "output_log.txt"}


def tracked_paths(root: Path) -> list[Path]:
    result = subprocess.run(
        ["git", "ls-files", "-z"], cwd=root, check=True, stdout=subprocess.PIPE
    )
    return [Path(raw.decode("utf-8")) for raw in result.stdout.split(b"\0") if raw]


def violation(path: Path, root: Path) -> str | None:
    lowered = {part.lower() for part in path.parts}
    if lowered & BLOCKED_PARTS or path.name.lower() in BLOCKED_NAMES or path.suffix.lower() in BLOCKED_SUFFIXES:
        return "blocked legacy/sensitive path"
    full = root / path
    if full.is_file():
        with full.open("rb") as stream:
            if stream.read(2) == b"MZ":
                return "PE executable signature"
    return None


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    failures = [(p, reason) for p in tracked_paths(root) if (reason := violation(p, root))]
    for path, reason in failures:
        print(f"ERROR: {path.as_posix()}: {reason}", file=sys.stderr)
    if failures:
        return 1
    print("Public-tree policy passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
