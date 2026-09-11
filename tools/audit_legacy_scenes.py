#!/usr/bin/env python3
"""Audit AssetRipper Unity scenes for migration risks without loading legacy code."""

from __future__ import annotations

import argparse
import csv
import json
import re
from collections import Counter, defaultdict, deque
from pathlib import Path


GUID_RE = re.compile(r"guid:[ \t]*([0-9a-f]{32})")
HEADER_RE = re.compile(r"^--- !u!\d+ &-?\d+\s*\n([A-Za-z0-9_]+):", re.MULTILINE)
NAME_RE = re.compile(r"^[ \t]*m_Name:[ \t]*(.*)$", re.MULTILINE)
SUBSET_RE = re.compile(r"^[ \t]*m_SubsetIndices:[ \t]*([0-9a-fA-F]+)[ \t]*$", re.MULTILINE)
LIGHTMAP_RE = re.compile(r"^[ \t]*m_LightmapIndex:[ \t]*(-?\d+)", re.MULTILINE)
MISSION_RE = re.compile(r"^M(\d)(\d{2})_")
MISSION_CODE_RE = re.compile(r"^M(\d+)_")
BUILTIN_GUIDS = {
    "0000000000000000e000000000000000",
    "0000000000000000f000000000000000",
}

TEXT_EXTENSIONS = {
    ".unity", ".prefab", ".mat", ".asset", ".anim", ".controller", ".overridecontroller",
    ".shader", ".compute", ".txt", ".json", ".xml", ".cs"
}


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return ""


def index_assets(assets: Path) -> tuple[dict[str, Path], list[Path]]:
    guid_to_asset: dict[str, Path] = {}
    scenes: list[Path] = []
    for meta in assets.rglob("*.meta"):
        match = GUID_RE.search(read_text(meta))
        if not match:
            continue
        asset = meta.with_suffix("")
        if asset.exists():
            guid_to_asset[match.group(1)] = asset
    scene_root = assets / "Scene"
    if scene_root.is_dir():
        scenes = sorted(scene_root.glob("*.unity"), key=lambda item: item.name.lower())
    return guid_to_asset, scenes


def category(name: str) -> str:
    if name.startswith("M"):
        return "mission"
    if name.startswith("T"):
        return "town"
    if name.startswith("L"):
        return "lobby"
    if name.startswith("G"):
        return "guild"
    if name.startswith("A"):
        return "account"
    return "other"


def dependency_closure(direct: set[str], index: dict[str, Path], cache: dict[Path, set[str]]) -> tuple[set[Path], set[str]]:
    found: set[Path] = set()
    unresolved: set[str] = set()
    queue = deque(direct)
    seen_guids: set[str] = set()
    while queue:
        guid = queue.popleft()
        if guid in seen_guids:
            continue
        seen_guids.add(guid)
        path = index.get(guid)
        if path is None:
            unresolved.add(guid)
            continue
        if path in found:
            continue
        found.add(path)
        if path.suffix.lower() not in TEXT_EXTENSIONS:
            continue
        if path not in cache:
            cache[path] = {guid for guid in GUID_RE.findall(read_text(path)) if guid not in BUILTIN_GUIDS and guid != "0" * 32}
        queue.extend(cache[path])
    return found, unresolved


def audit_scene(scene: Path, assets: Path, index: dict[str, Path], cache: dict[Path, set[str]]) -> dict:
    text = read_text(scene)
    components = Counter(HEADER_RE.findall(text))
    names = [value.strip() for value in NAME_RE.findall(text)]
    direct_guids = {guid for guid in GUID_RE.findall(text) if guid not in BUILTIN_GUIDS and guid != "0" * 32}
    dependencies, unresolved = dependency_closure(direct_guids, index, cache)
    dep_types = Counter(path.suffix.lower() or "<none>" for path in dependencies)
    dep_names = [path.name.lower() for path in dependencies]
    static_subset_count = len(SUBSET_RE.findall(text))
    combined_mesh_dependencies = sum("combined mesh" in name for name in dep_names)
    # Unity 3.x serialized 254/255 as unassigned lightmap sentinels.
    nonnegative_lightmaps = sum(0 <= int(value) < 254 for value in LIGHTMAP_RE.findall(text))
    terrain_count = max(components["Terrain"], components["TerrainCollider"])
    script_dependencies = [path for path in dependencies if path.suffix.lower() == ".cs"]
    legacy_particles = sum(components[key] for key in (
        "ParticleEmitter", "EllipsoidParticleEmitter", "MeshParticleEmitter", "ParticleAnimator",
        "ParticleRenderer"
    ))
    structure_names = sum(
        any(token in name.lower() for token in ("fence", "gate", "door", "wall", "barrier"))
        for name in names
    )
    mission = MISSION_RE.match(scene.name)
    risks: list[str] = []
    if static_subset_count or combined_mesh_dependencies:
        risks.append("legacy_static_batch")
    if unresolved:
        risks.append("unresolved_guid")
    if script_dependencies or components["MonoBehaviour"]:
        risks.append("legacy_runtime_script")
    if nonnegative_lightmaps:
        risks.append("legacy_lightmap")
    if terrain_count:
        risks.append("terrain_upgrade")
    if legacy_particles:
        risks.append("legacy_particle")
    if components["Animation"]:
        risks.append("legacy_animation")
    if components["AudioSource"]:
        risks.append("audio_migration")
    if structure_names and (static_subset_count or combined_mesh_dependencies):
        risks.append("structure_visibility")

    return {
        "scene": scene.name,
        "category": category(scene.name),
        "chapter_hint": int(mission.group(1)) if mission else None,
        "mission_hint": int(mission.group(2)) if mission else None,
        "bytes": scene.stat().st_size,
        "game_objects": components["GameObject"],
        "renderers": components["MeshRenderer"] + components["SkinnedMeshRenderer"],
        "mesh_filters": components["MeshFilter"],
        "skinned_meshes": components["SkinnedMeshRenderer"],
        "colliders": sum(value for key, value in components.items() if key.endswith("Collider")),
        "terrains": terrain_count,
        "mono_behaviours": components["MonoBehaviour"],
        "animations": components["Animation"] + components["Animator"],
        "audio_sources": components["AudioSource"],
        "legacy_particles": legacy_particles,
        "lightmapped_renderers": nonnegative_lightmaps,
        "legacy_subset_renderers": static_subset_count,
        "combined_mesh_dependencies": combined_mesh_dependencies,
        "structure_objects": structure_names,
        "direct_guid_references": len(direct_guids),
        "dependency_assets": len(dependencies),
        "mesh_dependencies": dep_types[".asset"],
        "material_dependencies": dep_types[".mat"],
        "texture_dependencies": dep_types[".png"] + dep_types[".tga"] + dep_types[".jpg"] + dep_types[".dds"],
        "script_dependencies": len(script_dependencies),
        "unresolved_guids": len(unresolved),
        "unresolved_guid_values": sorted(unresolved),
        "risk_count": len(risks),
        "risks": risks,
    }


def read_mission_catalog(export_project: Path) -> dict:
    path = export_project / "Assets" / "Scripts" / "Assembly-UnityScript" / "MissionData.cs"
    if not path.is_file():
        return {"available": False, "case_count": 0, "story_count": 0, "story_by_hundreds": {}, "story_codes": []}
    text = read_text(path)
    cases: list[tuple[int, bool]] = []
    for block in re.split(r"(?=[ \t]*case[ \t]+\d+[ \t]*:)", text):
        match = re.search(r"case[ \t]+(\d+)[ \t]*:", block)
        if match:
            cases.append((int(match.group(1)), "eMissionType.story" in block))
    story_codes = [code for code, is_story in cases if is_story]
    return {
        "available": True,
        "case_count": len(cases),
        "story_count": len(story_codes),
        "story_by_hundreds": dict(sorted(Counter(code // 100 for code in story_codes).items())),
        "story_codes": story_codes,
    }


def summarize(rows: list[dict], level_file_count: int | None, expected_chapters: int,
              expected_stages_per_chapter: int, mission_catalog: dict) -> dict:
    categories = Counter(row["category"] for row in rows)
    chapters = Counter(row["chapter_hint"] for row in rows if row["chapter_hint"] is not None)
    risk_scenes: dict[str, int] = defaultdict(int)
    unresolved_frequency: Counter[str] = Counter()
    for row in rows:
        for risk in row["risks"]:
            risk_scenes[risk] += 1
        unresolved_frequency.update(row["unresolved_guid_values"])
    totals = {
        key: sum(int(row[key]) for row in rows)
        for key in (
            "bytes", "game_objects", "renderers", "mesh_filters", "skinned_meshes", "colliders",
            "terrains", "mono_behaviours", "animations", "audio_sources", "legacy_particles",
            "lightmapped_renderers", "legacy_subset_renderers", "combined_mesh_dependencies",
            "structure_objects", "unresolved_guids"
        )
    }
    scene_codes = sorted({
        int(match.group(1))
        for row in rows
        if (match := MISSION_CODE_RE.match(row["scene"]))
    })
    return {
        "scene_count": len(rows),
        "legacy_level_file_count": level_file_count,
        "unexported_level_difference": None if level_file_count is None else level_file_count - len(rows),
        "categories": dict(sorted(categories.items())),
        "mission_chapter_hints": {str(key): value for key, value in sorted(chapters.items())},
        "expected_main_chapters": expected_chapters,
        "expected_stages_per_chapter": expected_stages_per_chapter,
        "expected_main_stage_count": expected_chapters * expected_stages_per_chapter,
        "unique_exported_mission_codes": len(scene_codes),
        "exported_mission_codes": scene_codes,
        "legacy_mission_catalog": mission_catalog,
        "risk_scene_counts": dict(sorted(risk_scenes.items())),
        "unresolved_guid_frequency": dict(unresolved_frequency.most_common()),
        "totals": totals,
    }


def write_markdown(path: Path, source: Path, summary: dict, rows: list[dict]) -> None:
    risk_labels = {
        "legacy_static_batch": "Static-batched combined meshes need individual-mesh reconstruction",
        "unresolved_guid": "One or more referenced GUIDs are absent from the export",
        "legacy_runtime_script": "Legacy UnityScript/MonoBehaviour behavior must be reimplemented in C#",
        "legacy_lightmap": "Legacy lightmap assignments need rebaking or removal",
        "terrain_upgrade": "Terrain data/material needs Unity 6 conversion and visual QA",
        "legacy_particle": "Legacy particle components need ParticleSystem/VFX migration",
        "legacy_animation": "Legacy Animation/Animator clips need binding and playback validation",
        "audio_migration": "Audio clips, mixer behavior, rolloff, and triggers need validation",
        "structure_visibility": "Fence/gate/door/wall renderers are exposed to static-batch loss",
    }
    lines = [
        "# Legacy scene migration audit",
        "",
        f"Source: `{source}`",
        "",
        "This is a metadata-only audit. It does not execute legacy scripts or modify the exported project.",
        "",
        "## Coverage",
        "",
        f"- Exported Unity scenes audited: **{summary['scene_count']}**",
        f"- Original player level files reported: **{summary['legacy_level_file_count'] or 'unknown'}**",
        f"- Level/export difference requiring reconciliation: **{summary['unexported_level_difference'] if summary['unexported_level_difference'] is not None else 'unknown'}**",
        f"- Scene categories: `{json.dumps(summary['categories'], sort_keys=True)}`",
        f"- Mission filename chapter hints: `{json.dumps(summary['mission_chapter_hints'], sort_keys=True)}`",
        f"- User-defined main-story target: **{summary['expected_main_chapters']} chapters x {summary['expected_stages_per_chapter']} stages = {summary['expected_main_stage_count']} stages**",
        f"- Unique mission codes represented by exported scene names: **{summary['unique_exported_mission_codes']}**",
        f"- Legacy `MissionData` cases/story entries: **{summary['legacy_mission_catalog']['case_count']} / {summary['legacy_mission_catalog']['story_count']}**",
        f"- Unresolved GUID frequency: `{json.dumps(summary['unresolved_guid_frequency'], sort_keys=True)}`",
        "",
        "The filename chapter hints cover the chapter number encoded in the exported `Mxyz` scene name. They are not proof that the complete 12-chapter mission catalog was exported.",
        "",
        "## Migration findings",
        "",
        "| Risk | Affected scenes | Required action |",
        "|---|---:|---|",
    ]
    for risk, count in sorted(summary["risk_scene_counts"].items(), key=lambda item: (-item[1], item[0])):
        lines.append(f"| `{risk}` | {count} | {risk_labels[risk]} |")
    totals = summary["totals"]
    lines += [
        "",
        "## Inventory totals",
        "",
        "| Item | Count |",
        "|---|---:|",
    ]
    for key, value in totals.items():
        lines.append(f"| `{key}` | {value} |")
    lines += [
        "",
        "## Highest-risk scenes",
        "",
        "| Scene | Category | Risks | Objects | Renderers | Scripts | Static subsets | Unresolved GUIDs |",
        "|---|---|---:|---:|---:|---:|---:|---:|",
    ]
    ranked = sorted(rows, key=lambda row: (
        row["risk_count"], row["unresolved_guids"], row["legacy_subset_renderers"], row["game_objects"]
    ), reverse=True)
    for row in ranked[:50]:
        lines.append(
            f"| `{row['scene']}` | {row['category']} | {row['risk_count']} | {row['game_objects']} | "
            f"{row['renderers']} | {row['mono_behaviours']} | {row['legacy_subset_renderers']} | {row['unresolved_guids']} |"
        )
    lines += [
        "",
        "## Required migration gates",
        "",
        "1. Reconcile all original level files against exported scenes and explain every missing or duplicate level.",
        "2. Replace legacy static-batch references with original individual meshes before creating Unity 6 prefabs.",
        "3. Convert shaders/materials and verify texture, alpha, normal-map, emission, and two-sided behavior.",
        "4. Upgrade terrain data and perform visual checks for splat layers, trees, detail meshes, water, and bounds.",
        "5. Remove legacy MonoBehaviours only after recording their type and rebuilding required behavior in C#.",
        "6. Rebind animation clips and validate default, loop, attack, hit, death, and event timing.",
        "7. Convert legacy particles and trails, then validate sorting, blending, lifetime, and attachment points.",
        "8. Rebuild lighting per scene; legacy lightmap indices alone are not a valid Unity 6 lighting result.",
        "9. Validate audio clips, 3D rolloff, looping, triggers, and mixer levels.",
        "10. Run an automated structural validator and a captured visual comparison for every playable scene.",
        "",
        "The companion CSV contains one row per scene. The JSON artifact retains the complete machine-readable results.",
    ]
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--export-project", required=True, type=Path)
    parser.add_argument("--output-json", required=True, type=Path)
    parser.add_argument("--output-csv", required=True, type=Path)
    parser.add_argument("--output-markdown", required=True, type=Path)
    parser.add_argument("--level-file-count", type=int)
    parser.add_argument("--expected-chapters", type=int, default=12)
    parser.add_argument("--expected-stages-per-chapter", type=int, default=8)
    args = parser.parse_args()
    assets = args.export_project.resolve() / "Assets"
    index, scenes = index_assets(assets)
    if not scenes:
        raise SystemExit(f"No scenes found under {assets / 'Scene'}")
    cache: dict[Path, set[str]] = {}
    rows = [audit_scene(scene, assets, index, cache) for scene in scenes]
    mission_catalog = read_mission_catalog(args.export_project.resolve())
    summary = summarize(rows, args.level_file_count, args.expected_chapters,
                        args.expected_stages_per_chapter, mission_catalog)
    payload = {"schema_version": 1, "source": str(args.export_project.resolve()), "summary": summary, "scenes": rows}
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    fieldnames = [key for key in rows[0] if key not in ("risks", "unresolved_guid_values")] + ["risks", "unresolved_guid_values"]
    args.output_csv.parent.mkdir(parents=True, exist_ok=True)
    with args.output_csv.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            csv_row = dict(row)
            csv_row["risks"] = ";".join(row["risks"])
            csv_row["unresolved_guid_values"] = ";".join(row["unresolved_guid_values"])
            writer.writerow(csv_row)
    write_markdown(args.output_markdown, args.export_project.resolve(), summary, rows)
    print(json.dumps(summary, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
