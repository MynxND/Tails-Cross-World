#!/usr/bin/env python3
"""Create a metadata-only inventory of a legacy Unity player build."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import struct
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

UNITY_VERSION = re.compile(rb"\b\d+\.\d+\.\d+[abfp]\d+\b")
SENSITIVE_NAMES = {"savegame", "screenshot", "output_log.txt"}
SENSITIVE_SUFFIXES = {".zrdata", ".config", ".ini"}


def sha256_file(path: Path, chunk_size: int = 1024 * 1024) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(chunk_size), b""):
            digest.update(chunk)
    return digest.hexdigest()


def pe_architecture(path: Path) -> str | None:
    """Return a friendly PE architecture without loading or executing the file."""
    try:
        with path.open("rb") as stream:
            if stream.read(2) != b"MZ":
                return None
            stream.seek(0x3C)
            pe_offset = struct.unpack("<I", stream.read(4))[0]
            stream.seek(pe_offset)
            if stream.read(4) != b"PE\0\0":
                return None
            machine = struct.unpack("<H", stream.read(2))[0]
    except (OSError, struct.error):
        return None
    return {0x014C: "x86", 0x8664: "x86_64", 0xAA64: "arm64"}.get(machine, f"unknown-0x{machine:04x}")


def find_unity_versions(paths: list[Path]) -> list[str]:
    versions: set[str] = set()
    for path in paths:
        if not path.is_file():
            continue
        try:
            with path.open("rb") as stream:
                data = stream.read(8 * 1024 * 1024)
        except OSError:
            continue
        versions.update(match.decode("ascii") for match in UNITY_VERSION.findall(data))
    return sorted(versions)


def is_sensitive(relative: Path) -> bool:
    lowered = [part.lower() for part in relative.parts]
    return (
        any(part in SENSITIVE_NAMES for part in lowered)
        or relative.suffix.lower() in SENSITIVE_SUFFIXES
    )


def inspect(root: Path) -> dict:
    root = root.resolve(strict=True)
    if not root.is_dir():
        raise ValueError(f"reference root is not a directory: {root}")

    files = sorted((p for p in root.rglob("*") if p.is_file()), key=lambda p: p.relative_to(root).as_posix().lower())
    data_dirs = sorted(p for p in root.glob("*_Data") if p.is_dir())
    managed_dirs = [p / "Managed" for p in data_dirs if (p / "Managed").is_dir()]
    assemblies = sorted((p for d in managed_dirs for p in d.glob("*.dll")), key=lambda p: p.name.lower())
    executables = sorted(root.glob("*.exe"), key=lambda p: p.name.lower())
    levels = [p for d in data_dirs for p in d.glob("level[0-9]*") if p.name[5:].isdigit()]
    shared_assets = [p for d in data_dirs for p in d.glob("sharedassets*.assets")]
    version_sources = [p / "mainData" for p in data_dirs] + [p / "output_log.txt" for p in data_dirs]
    suffix_counts = Counter(p.suffix.lower() or "<none>" for p in files)
    sensitive = [p.relative_to(root).as_posix() for p in files if is_sensitive(p.relative_to(root))]
    sensitive_dirs = [p.relative_to(root).as_posix() + "/" for p in root.rglob("*") if p.is_dir() and p.name.lower() in SENSITIVE_NAMES]
    manifest = [
        {
            "path": p.relative_to(root).as_posix(),
            "bytes": p.stat().st_size,
            "sha256": sha256_file(p),
        }
        for p in files
    ]

    return {
        "schema_version": 1,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "reference": {
            "root_name": root.name,
            "file_count": len(files),
            "total_bytes": sum(p.stat().st_size for p in files),
        },
        "engine": {"unity_versions": find_unity_versions(version_sources)},
        "executables": [
            {"path": p.relative_to(root).as_posix(), "bytes": p.stat().st_size, "architecture": pe_architecture(p), "sha256": sha256_file(p)}
            for p in executables
        ],
        "managed_assemblies": [
            {"path": p.relative_to(root).as_posix(), "bytes": p.stat().st_size, "sha256": sha256_file(p)}
            for p in assemblies
        ],
        "content": {
            "level_files": len(levels),
            "shared_asset_files": len(shared_assets),
            "files_by_suffix": dict(sorted(suffix_counts.items())),
        },
        "sensitive_runtime_paths": sorted(set(sensitive_dirs + sensitive)),
        "file_manifest": manifest,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True, help="Legacy build directory to inspect")
    parser.add_argument("--output", type=Path, help="Write JSON report here; stdout when omitted")
    args = parser.parse_args(argv)
    try:
        root = args.root.resolve(strict=True)
        if args.output:
            output = args.output.resolve()
            if output == root or root in output.parents:
                parser.error("--output must be outside the reference tree")
        report = inspect(root)
        payload = json.dumps(report, indent=2, ensure_ascii=False) + "\n"
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(payload, encoding="utf-8")
        else:
            sys.stdout.write(payload)
        return 0
    except (OSError, ValueError) as exc:
        parser.error(str(exc))
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
