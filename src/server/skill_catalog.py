"""Validated server-side view of the shared skill catalog."""

from __future__ import annotations

from dataclasses import dataclass
import json
from pathlib import Path


@dataclass(frozen=True)
class ServerSkill:
    skill_id: str
    character_id: str
    damage: int
    cooldown_seconds: float
    hit_count: int
    hit_times_seconds: tuple[float, ...]
    resource_cost: int
    range: float
    target_shape: str
    target_width: float
    target_height: float
    max_targets: int
    impact_radius: float


def load_skill_catalog(path: Path | None = None) -> dict[str, ServerSkill]:
    source = path or Path(__file__).resolve().parents[2] / "content" / "v1" / "skills.json"
    document = json.loads(source.read_text(encoding="utf-8"))
    if document.get("schema_version") != 1 or not isinstance(document.get("skills"), list):
        raise ValueError("unsupported skill catalog")
    catalog: dict[str, ServerSkill] = {}
    for raw in document["skills"]:
        skill = ServerSkill(
            skill_id=raw["id"],
            character_id=raw["character_id"],
            damage=raw["damage"],
            cooldown_seconds=raw["cooldown_seconds"],
            hit_count=raw["hit_count"],
            hit_times_seconds=tuple(raw["hit_times_seconds"]),
            resource_cost=raw["resource_cost"],
            range=raw["range"],
            target_shape=raw["target_shape"],
            target_width=raw["target_width"],
            target_height=raw["target_height"],
            max_targets=raw["max_targets"],
            impact_radius=raw["impact_radius"],
        )
        if skill.skill_id in catalog:
            raise ValueError(f"duplicate skill: {skill.skill_id}")
        catalog[skill.skill_id] = skill
    return catalog