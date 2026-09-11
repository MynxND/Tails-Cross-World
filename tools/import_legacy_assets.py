"""Copy an AssetRipper scene and its complete GUID dependency closure.

The copied legacy assets are intentionally placed in the git-ignored
Assets/TwelveTails/LegacyPrivate directory. The original export is read-only.
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
from collections import deque
from pathlib import Path


GUID_LINE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)
GUID_REF = re.compile(rb"guid:\s*([0-9a-f]{32})")
BUILTIN_GUIDS = {
    "00000000000000000000000000000000",
    "0000000000000000e000000000000000",
    "0000000000000000f000000000000000",
}
NAME_REF = re.compile(r"^[ \t]*m_Name:[ \t]*(.+)$", re.MULTILINE)
STATIC_SUBSET = re.compile(r"^[ \t]*m_SubsetIndices:[ \t]*[0-9a-fA-F]+[ \t]*$", re.MULTILINE)


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
    return {
        guid for match in GUID_REF.finditer(data)
        if (guid := match.group(1).decode("ascii")) not in BUILTIN_GUIDS
    }


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


def normalized_mesh_name(value: str) -> str:
    value = re.sub(r"(?i)(^|[^a-z0-9])(tri|model|collision|collider|n)(?=$|[^a-z0-9])", " ", value)
    return "".join(character.lower() for character in value if character.isalnum())


def static_batch_mesh_candidates(entries: list[Path], source_assets: Path) -> tuple[set[Path], dict[str, list[str]]]:
    mesh_root = source_assets / "Mesh"
    by_name: dict[str, list[Path]] = {}
    for mesh in mesh_root.glob("*.asset"):
        lower = mesh.stem.lower()
        if "collision" in lower or lower.endswith("_c") or "collider" in lower:
            continue
        by_name.setdefault(normalized_mesh_name(mesh.stem), []).append(mesh)

    extras: set[Path] = set()
    unresolved: dict[str, list[str]] = {}
    for entry in entries:
        text = entry.read_text(encoding="utf-8", errors="ignore")
        if not STATIC_SUBSET.search(text):
            continue
        object_names: dict[str, str] = {}
        subset_objects: set[str] = set()
        for document in re.split(r"(?=^--- !u!)", text, flags=re.MULTILINE):
            header = re.match(r"--- !u!\d+ &(-?\d+)[ \t]*\r?\n([A-Za-z0-9_]+):", document)
            if not header:
                continue
            if header.group(2) == "GameObject":
                name = NAME_REF.search(document)
                if name:
                    object_names[header.group(1)] = name.group(1).strip()
            elif header.group(2) == "MeshRenderer" and STATIC_SUBSET.search(document):
                owner = re.search(r"m_GameObject:[ \t]*\{fileID:[ \t]*(-?\d+)\}", document)
                if owner:
                    subset_objects.add(owner.group(1))
        names = sorted({object_names[owner] for owner in subset_objects if owner in object_names})
        missing_names: list[str] = []
        for name in names:
            object_key = normalized_mesh_name(name)
            candidates = list(by_name.get(object_key, []))
            if not candidates:
                object_without_number = object_key.rstrip("0123456789")
                candidates = [
                    mesh
                    for mesh_key, meshes in by_name.items()
                    if mesh_key.endswith(object_key) or
                       mesh_key.rstrip("0123456789") == object_without_number
                    for mesh in meshes
                ]
            if len(candidates) == 1:
                extras.add(candidates[0])
            elif len(candidates) > 1:
                # Preserve every plausible source; Unity validation decides which mesh is correct.
                extras.update(candidates)
            else:
                missing_names.append(name)
        if missing_names:
            unresolved[entry.name] = missing_names
    return extras, unresolved


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--export-project", type=Path, required=True)
    parser.add_argument("--entry", action="append", default=[],
                        help="Path relative to exported Assets; repeat for multiple roots")
    parser.add_argument("--entry-glob", action="append", default=[],
                        help="Glob relative to exported Assets; repeat for multiple groups")
    parser.add_argument("--destination-assets", type=Path, required=True)
    parser.add_argument("--copy", action="store_true", help="Copy after reporting the closure")
    parser.add_argument("--include-static-batch-meshes", action="store_true",
                        help="Also include likely individual visual meshes omitted by old static batching")
    parser.add_argument("--manifest", type=Path, help="Write a machine-readable import plan")
    args = parser.parse_args()

    source_assets = (args.export_project / "Assets").resolve()
    index = build_guid_index(source_assets)
    entry_paths: list[Path] = []
    for relative_entry in args.entry:
        entry_paths.append((source_assets / relative_entry).resolve())
    for pattern in args.entry_glob:
        entry_paths.extend(path.resolve() for path in source_assets.glob(pattern) if path.is_file())
    entry_paths = sorted(set(entry_paths))
    if not entry_paths:
        raise SystemExit("At least one --entry or --entry-glob must match an asset")
    assets: set[Path] = set()
    missing: set[str] = set()
    for entry in entry_paths:
        if not entry.is_file() or source_assets not in entry.parents:
            raise SystemExit(f"Entry is not a file inside exported Assets: {entry}")
        entry_assets, entry_missing = dependency_closure(entry, index)
        assets.update(entry_assets)
        missing.update(entry_missing)
    static_extras: set[Path] = set()
    static_unresolved: dict[str, list[str]] = {}
    if args.include_static_batch_meshes:
        static_extras, static_unresolved = static_batch_mesh_candidates(entry_paths, source_assets)
        for extra in static_extras:
            extra_assets, extra_missing = dependency_closure(extra, index)
            assets.update(extra_assets)
            missing.update(extra_missing)
    total_bytes = sum(p.stat().st_size for p in assets)
    print(f"Indexed GUIDs: {len(index)}")
    print(f"Dependency assets: {len(assets)}")
    print(f"Payload size: {total_bytes / 1024 / 1024:.1f} MiB")
    print(f"Unresolved GUIDs (normally built-in Unity resources): {len(missing)}")
    print(f"Static-batch individual mesh candidates: {len(static_extras)}")
    print(f"Static-batch scenes needing manual name resolution: {len(static_unresolved)}")

    if args.manifest:
        manifest = {
            "schema_version": 1,
            "source": str(source_assets),
            "entries": [str(path.relative_to(source_assets)).replace("\\", "/") for path in entry_paths],
            "asset_count": len(assets),
            "payload_bytes": total_bytes,
            "missing_guids": sorted(missing),
            "static_batch_mesh_candidates": [
                str(path.relative_to(source_assets)).replace("\\", "/") for path in sorted(static_extras)
            ],
            "static_batch_unresolved_names": static_unresolved,
        }
        args.manifest.parent.mkdir(parents=True, exist_ok=True)
        args.manifest.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

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
