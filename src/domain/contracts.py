"""Versioned Phase 1 domain contracts with strict validation."""

from __future__ import annotations

import math
import re
from collections.abc import Callable
from typing import Any

ID_PATTERN = re.compile(r"^[a-z][a-z0-9_.-]{1,63}$")
CURRENT_SCHEMA_VERSION = 1


class ContractError(ValueError):
    pass


def _object(value: Any, path: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ContractError(f"{path} must be an object")
    return value


def _exact_fields(value: dict[str, Any], required: set[str], path: str) -> None:
    missing = required - value.keys()
    extra = value.keys() - required
    if missing:
        raise ContractError(f"{path} missing fields: {', '.join(sorted(missing))}")
    if extra:
        raise ContractError(f"{path} unknown fields: {', '.join(sorted(extra))}")


def _string(value: Any, path: str, *, identifier: bool = False, maximum: int = 128) -> str:
    if not isinstance(value, str) or not value or len(value) > maximum:
        raise ContractError(f"{path} must be a non-empty string of at most {maximum} characters")
    if identifier and not ID_PATTERN.fullmatch(value):
        raise ContractError(f"{path} is not a valid identifier")
    return value


def _integer(value: Any, path: str, *, minimum: int = 0, maximum: int = 2_147_483_647) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or not minimum <= value <= maximum:
        raise ContractError(f"{path} must be an integer from {minimum} to {maximum}")
    return value


def _number(value: Any, path: str, *, minimum: float = 0.0, maximum: float = 1_000_000.0) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or not minimum <= value <= maximum:
        raise ContractError(f"{path} must be a finite number from {minimum} to {maximum}")
    return float(value)


def _id_list(value: Any, path: str, *, maximum: int = 256) -> list[str]:
    if not isinstance(value, list) or len(value) > maximum:
        raise ContractError(f"{path} must be an array with at most {maximum} entries")
    result = [_string(item, f"{path}[{index}]", identifier=True) for index, item in enumerate(value)]
    if len(result) != len(set(result)):
        raise ContractError(f"{path} contains duplicate identifiers")
    return result


def _stats(value: Any, path: str) -> dict[str, float]:
    obj = _object(value, path)
    allowed = {"hp", "mp", "attack", "defense", "speed"}
    if not obj or obj.keys() - allowed:
        raise ContractError(f"{path} must contain only known stat names")
    return {name: _number(amount, f"{path}.{name}") for name, amount in obj.items()}


def _account(data: dict[str, Any]) -> None:
    _exact_fields(data, {"account_id", "display_name", "character_ids", "status"}, "data")
    _string(data["account_id"], "data.account_id", identifier=True)
    _string(data["display_name"], "data.display_name", maximum=32)
    _id_list(data["character_ids"], "data.character_ids", maximum=12)
    if data["status"] not in {"active", "suspended"}:
        raise ContractError("data.status must be active or suspended")


def _character(data: dict[str, Any]) -> None:
    _exact_fields(data, {"character_id", "account_id", "archetype_id", "level", "experience", "stats", "equipment_ids"}, "data")
    for field in ("character_id", "account_id", "archetype_id"):
        _string(data[field], f"data.{field}", identifier=True)
    _integer(data["level"], "data.level", minimum=1, maximum=999)
    _integer(data["experience"], "data.experience")
    _stats(data["stats"], "data.stats")
    _id_list(data["equipment_ids"], "data.equipment_ids", maximum=16)


def _item(data: dict[str, Any]) -> None:
    _exact_fields(data, {"item_id", "kind", "name_key", "max_stack"}, "data")
    _string(data["item_id"], "data.item_id", identifier=True)
    if data["kind"] not in {"consumable", "material", "equipment", "quest"}:
        raise ContractError("data.kind is unsupported")
    _string(data["name_key"], "data.name_key", identifier=True)
    _integer(data["max_stack"], "data.max_stack", minimum=1, maximum=9999)


def _equipment(data: dict[str, Any]) -> None:
    _exact_fields(data, {"equipment_id", "item_id", "slot", "modifiers", "rig_variants"}, "data")
    _string(data["equipment_id"], "data.equipment_id", identifier=True)
    _string(data["item_id"], "data.item_id", identifier=True)
    if data["slot"] not in {"weapon", "head", "body", "hands", "feet", "accessory"}:
        raise ContractError("data.slot is unsupported")
    _stats(data["modifiers"], "data.modifiers")
    variants = _id_list(data["rig_variants"], "data.rig_variants", maximum=12)
    if len(variants) != 12:
        raise ContractError("data.rig_variants must define exactly 12 character variants")


def _quest(data: dict[str, Any]) -> None:
    _exact_fields(data, {"quest_id", "prerequisites", "objectives", "reward_item_ids", "reward_experience"}, "data")
    _string(data["quest_id"], "data.quest_id", identifier=True)
    _id_list(data["prerequisites"], "data.prerequisites")
    objectives = data["objectives"]
    if not isinstance(objectives, list) or not 1 <= len(objectives) <= 64:
        raise ContractError("data.objectives must contain 1 to 64 entries")
    for index, objective in enumerate(objectives):
        obj = _object(objective, f"data.objectives[{index}]")
        _exact_fields(obj, {"kind", "target_id", "count"}, f"data.objectives[{index}]")
        if obj["kind"] not in {"talk", "defeat", "collect", "enter_map"}:
            raise ContractError(f"data.objectives[{index}].kind is unsupported")
        _string(obj["target_id"], f"data.objectives[{index}].target_id", identifier=True)
        _integer(obj["count"], f"data.objectives[{index}].count", minimum=1, maximum=9999)
    _id_list(data["reward_item_ids"], "data.reward_item_ids")
    _integer(data["reward_experience"], "data.reward_experience")


def _monster(data: dict[str, Any]) -> None:
    _exact_fields(data, {"monster_id", "level", "stats", "ai_profile_id", "skill_ids", "drop_table_id"}, "data")
    _string(data["monster_id"], "data.monster_id", identifier=True)
    _integer(data["level"], "data.level", minimum=1, maximum=999)
    _stats(data["stats"], "data.stats")
    _string(data["ai_profile_id"], "data.ai_profile_id", identifier=True)
    _id_list(data["skill_ids"], "data.skill_ids")
    _string(data["drop_table_id"], "data.drop_table_id", identifier=True)


def _map(data: dict[str, Any]) -> None:
    _exact_fields(data, {"map_id", "scene_id", "spawn_ids", "portal_ids", "npc_ids", "monster_group_ids", "safe_zone_ids"}, "data")
    _string(data["map_id"], "data.map_id", identifier=True)
    _string(data["scene_id"], "data.scene_id", identifier=True)
    for field in ("spawn_ids", "portal_ids", "npc_ids", "monster_group_ids", "safe_zone_ids"):
        _id_list(data[field], f"data.{field}")


def _skill(data: dict[str, Any]) -> None:
    _exact_fields(data, {"skill_id", "name_key", "animation_clip", "damage", "cooldown_seconds", "hit_delay_seconds", "action_duration_seconds", "combo_window_start_seconds", "combo_window_end_seconds", "combo_next_skill_id", "resource_cost", "range"}, "data")
    _string(data["skill_id"], "data.skill_id", identifier=True)
    _string(data["name_key"], "data.name_key", identifier=True)
    _string(data["animation_clip"], "data.animation_clip", maximum=128)
    _integer(data["damage"], "data.damage", maximum=100000)
    _number(data["cooldown_seconds"], "data.cooldown_seconds", minimum=0.0, maximum=3600.0)
    _number(data["hit_delay_seconds"], "data.hit_delay_seconds", minimum=0.0, maximum=60.0)
    _number(data["action_duration_seconds"], "data.action_duration_seconds", minimum=0.0, maximum=60.0)
    _number(data["combo_window_start_seconds"], "data.combo_window_start_seconds", minimum=0.0, maximum=60.0)
    _number(data["combo_window_end_seconds"], "data.combo_window_end_seconds", minimum=0.0, maximum=60.0)
    if data["combo_next_skill_id"]:
        _string(data["combo_next_skill_id"], "data.combo_next_skill_id", identifier=True)
    elif data["combo_next_skill_id"] != "":
        raise ContractError("data.combo_next_skill_id must be a string")
    _integer(data["resource_cost"], "data.resource_cost", maximum=100000)
    _number(data["range"], "data.range", minimum=0.1, maximum=100.0)


def _animation_profile(data: dict[str, Any]) -> None:
    _exact_fields(data, {"character_id", "skills"}, "data")
    _string(data["character_id"], "data.character_id", identifier=True)
    skills = data["skills"]
    if not isinstance(skills, list) or not skills:
        raise ContractError("data.skills must contain at least one entry")
    seen: set[str] = set()
    for index, entry in enumerate(skills):
        obj = _object(entry, f"data.skills[{index}]")
        _exact_fields(obj, {"skill_id", "clip_name"}, f"data.skills[{index}]")
        skill_id = _string(obj["skill_id"], f"data.skills[{index}].skill_id", identifier=True)
        if skill_id in seen:
            raise ContractError(f"data.skills contains duplicate skill: {skill_id}")
        seen.add(skill_id)
        _string(obj["clip_name"], f"data.skills[{index}].clip_name", maximum=128)


VALIDATORS: dict[str, Callable[[dict[str, Any]], None]] = {
    "account": _account,
    "character": _character,
    "item": _item,
    "equipment": _equipment,
    "quest": _quest,
    "monster": _monster,
    "map": _map,
    "skill": _skill,
    "animation_profile": _animation_profile,
}


def validate_document(document: Any) -> dict[str, Any]:
    root = _object(document, "document")
    _exact_fields(root, {"schema_version", "kind", "data"}, "document")
    if root["schema_version"] != CURRENT_SCHEMA_VERSION:
        raise ContractError(f"unsupported schema_version: {root['schema_version']!r}")
    kind = _string(root["kind"], "document.kind", identifier=True)
    if kind not in VALIDATORS:
        raise ContractError(f"unsupported document kind: {kind}")
    data = _object(root["data"], "document.data")
    VALIDATORS[kind](data)
    return root
