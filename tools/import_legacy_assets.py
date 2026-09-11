"""Copy an AssetRipper scene and its complete GUID dependency closure.

The copied legacy assets are intentionally placed in the git-ignored
Assets/TwelveTails/LegacyPrivate directory. The original export is read-only.
"""

from __future__ import annotations

import argparse
import re
import shutil
from collections import deque
from pathlib import Path


GUID_LINE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)
GUID_REF = re.compile(rb"guid:\s*([0-9a-f]{32})")


def destination_relative(source: Path, source_assets: Path) -> Path:
    relative = source.relative_to(source_assets)
    # AssetRipper's decompiled UnityScript/C# output targets an obsolete runtime.
    # Preserve it for reference without letting Unity 6 compile it.
    if source.suffix.lower() == ".cs":
        return relative.with_suffix(".cs.legacy-source")
    return relative


def build_guid_index(assets_root: Path) -> dict[str, Path]:
    index: dict[str, Path] = {}
    for meta in assets_root.rglob("*.meta"):
        try:
            match = GUID_LINE.search(meta.read_text(encoding="utf-8", errors="ignore"))
        except OSError:
            continue
        if match:
            index[match.group(1)] = meta.with_suffix("")
    return index


def referenced_guids(path: Path) -> set[str]:
    try:
        data = path.read_bytes()
    except OSError:
        return set()
    # Avoid scanning large encoded image/audio payloads that cannot contain YAML refs.
    if b"guid:" not in data:
        return set()
    return {m.group(1).decode("ascii") for m in GUID_REF.finditer(data)}


def dependency_closure(entry: Path, guid_index: dict[str, Path]) -> tuple[set[Path], set[str]]:
    found: set[Path] = set()
    missing: set[str] = set()
    queue: deque[Path] = deque([entry])
    while queue:
        asset = queue.popleft()
        if asset in found:
            continue
        found.add(asset)
        for guid in referenced_guids(asset):
            dependency = guid_index.get(guid)
            if dependency is None:
                missing.add(guid)
            elif dependency not in found:
                queue.append(dependency)
    return found, missing


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--export-project", type=Path, required=True)
    parser.add_argument("--entry", required=True, help="Path relative to exported Assets")
    parser.add_argument("--destination-assets", type=Path, required=True)
    parser.add_argument("--copy", action="store_true", help="Copy after reporting the closure")
    args = parser.parse_args()

    source_assets = (args.export_project / "Assets").resolve()
    entry = (source_assets / args.entry).resolve()
    if not entry.is_file() or source_assets not in entry.parents:
        raise SystemExit(f"Entry is not a file inside exported Assets: {entry}")

    index = build_guid_index(source_assets)
    assets, missing = dependency_closure(entry, index)
    total_bytes = sum(p.stat().st_size for p in assets)
    print(f"Indexed GUIDs: {len(index)}")
    print(f"Dependency assets: {len(assets)}")
    print(f"Payload size: {total_bytes / 1024 / 1024:.1f} MiB")
    print(f"Unresolved GUIDs (normally built-in Unity resources): {len(missing)}")

    if not args.copy:
        print("Dry run only; pass --copy to import.")
        return 0

    destination = args.destination_assets.resolve()
    destination.mkdir(parents=True, exist_ok=True)
    for source in sorted(assets):
        relative = destination_relative(source, source_assets)
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        meta = Path(f"{source}.meta")
        if meta.is_file():
            shutil.copy2(meta, Path(f"{target}.meta"))
    print(f"Imported to: {destination}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
