#!/usr/bin/env python3
"""Safely classify legacy .zrData files without deserializing them."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from collections import Counter
from pathlib import Path

NRBF_HEADER = bytes.fromhex("0001000000ffffffff0100000000000000")


def entropy(data: bytes) -> float:
    if not data:
        return 0.0
    counts = Counter(data)
    length = len(data)
    return -sum((count / length) * math.log2(count / length) for count in counts.values())


def inspect_file(path: Path) -> dict:
    data = path.read_bytes()
    return {
        "name": path.name,
        "bytes": len(data),
        "sha256": hashlib.sha256(data).hexdigest(),
        "format": "dotnet-binaryformatter-nrbf" if data.startswith(NRBF_HEADER) else "unknown",
        "header_hex": data[:16].hex(),
        "shannon_entropy": round(entropy(data), 4),
    }


def inspect_directory(root: Path) -> dict:
    root = root.resolve(strict=True)
    if not root.is_dir():
        raise ValueError("save root must be a directory")
    return {
        "schema_version": 1,
        "safety": "classification-only; no BinaryFormatter deserialization",
        "files": [inspect_file(path) for path in sorted(root.glob("*.zrData"), key=lambda p: p.name.lower())],
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args(argv)
    root = args.root.resolve(strict=True)
    output = args.output.resolve()
    if output == root or root in output.parents:
        parser.error("--output must be outside the save reference tree")
    payload = json.dumps(inspect_directory(root), indent=2) + "\n"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(payload, encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
