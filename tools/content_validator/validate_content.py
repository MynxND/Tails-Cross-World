"""Validate cross-referenced Phase 4 content without loading game code."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import re
import sys

IDENTIFIER = re.compile(r"^[a-z][a-z0-9_.-]{1,63}$")
EXPECTED_CHARACTERS = {"wolf", "bison", "panda", "whale", "mole", "rabbit", "monkey", "sheep", "penguin", "bat", "chameleon", "cat"}
MISSION_FIELDS = {"id", "scene_name", "environment_resource", "player_spawn", "objective", "reward", "actors", "interactables", "evidence"}


class ContentError(ValueError):
    pass


def _ids(values: list[dict], label: str, field: str = "id") -> set[str]:
    result = [value.get(field) for value in values]
    if any(not isinstance(value, str) or not IDENTIFIER.fullmatch(value) for value in result):
        raise ContentError(f"{label} contains an invalid id")
    if len(result) != len(set(result)):
        raise ContentError(f"{label} contains duplicate ids")
    return set(result)


def _position(value: object, label: str) -> None:
    if not isinstance(value, dict) or set(value) != {"x", "y", "z"} or any(not isinstance(value[axis], (int, float)) for axis in ("x", "y", "z")):
        raise ContentError(f"{label} must have numeric x, y, and z fields")


def _mission_catalog(document: dict) -> None:
    missions = document.get("missions")
    if not isinstance(missions, list) or not missions:
            raise ContentError("mission catalog must contain at least one mission with a valid objective")
    _ids(missions, "missions")
    for mission in missions:
        if set(mission) != MISSION_FIELDS:
            raise ContentError(f"mission {mission.get('id')} has unsupported or missing fields")
        if not isinstance(mission["scene_name"], str) or not mission["scene_name"]:
            raise ContentError(f"mission {mission['id']} has an invalid scene name")
        if not isinstance(mission["environment_resource"], str) or not mission["environment_resource"].startswith("OriginalChapter1Maps/"):
            raise ContentError(f"mission {mission['id']} has an invalid environment resource")
        _position(mission["player_spawn"], f"mission {mission['id']} player spawn")
        objective = mission["objective"]
        if not isinstance(objective, dict) or set(objective) != {"kind", "target_id", "count"}:
            raise ContentError(f"mission {mission['id']} has an invalid objective; expected fields: kind, target_id, count")
        if objective["kind"] not in {"defeat", "interact", "knockout", "duel"} or not isinstance(objective["target_id"], str) or not IDENTIFIER.fullmatch(objective["target_id"]):
            raise ContentError(f"mission {mission['id']} has an unsupported objective")
        if not isinstance(objective["count"], int) or not 1 <= objective["count"] <= 9999:
            raise ContentError(f"mission {mission['id']} has an invalid objective count")
        reward = mission["reward"]
        if not isinstance(reward, dict) or set(reward) != {"experience", "potions"}:
            raise ContentError(f"mission {mission['id']} has an invalid reward")
        if any(not isinstance(reward[field], int) or reward[field] < 0 for field in reward):
            raise ContentError(f"mission {mission['id']} has a negative reward")
        actors = mission["actors"]
        if not isinstance(actors, list) or (not actors and objective["kind"] != "interact"):
            raise ContentError(f"mission {mission['id']} has no actors")
        actor_ids = set()
        objective_actor_count = 0
        for actor in actors:
            required = {"id", "entity_id", "prefab_resource", "health", "move_speed", "attack_damage", "positions", "spawn_on_first_damage"}
            if not isinstance(actor, dict) or set(actor) != required:
                raise ContentError(f"mission {mission['id']} has an invalid actor group")
            if not isinstance(actor["id"], str) or not IDENTIFIER.fullmatch(actor["id"]) or actor["id"] in actor_ids:
                raise ContentError(f"mission {mission['id']} has an invalid or duplicate actor group id")
            actor_ids.add(actor["id"])
            if not isinstance(actor["entity_id"], str) or not IDENTIFIER.fullmatch(actor["entity_id"]):
                raise ContentError(f"mission {mission['id']} has an invalid actor entity id")
            if not isinstance(actor["prefab_resource"], str) or not actor["prefab_resource"].startswith(("OriginalMonsters/", "OriginalNpcs/", "OriginalCharacters/")):
                raise ContentError(f"mission {mission['id']} has an invalid actor resource")
            if not isinstance(actor["health"], int) or actor["health"] < 1:
                raise ContentError(f"mission {mission['id']} has invalid actor health")
            if not isinstance(actor["move_speed"], (int, float)) or actor["move_speed"] < 0:
                raise ContentError(f"mission {mission['id']} has invalid actor speed")
            if not isinstance(actor["attack_damage"], int) or actor["attack_damage"] < 0:
                raise ContentError(f"mission {mission['id']} has invalid actor damage")
            if not isinstance(actor["positions"], list) or not actor["positions"]:
                raise ContentError(f"mission {mission['id']} actor group has no positions")
            for index, position in enumerate(actor["positions"]):
                _position(position, f"mission {mission['id']} actor position {index}")
            if actor["entity_id"] == objective["target_id"]:
                objective_actor_count += len(actor["positions"])
            spawn = actor["spawn_on_first_damage"]
            if not isinstance(spawn, dict) or set(spawn) != {"enabled", "green_prefab_resource", "red_prefab_resource"} or not isinstance(spawn["enabled"], bool):
                raise ContentError(f"mission {mission['id']} has invalid first-damage spawn behavior")
            if spawn["enabled"]:
                resource_fields = ("green_prefab_resource", "red_prefab_resource")
                if any(not isinstance(spawn[field], str) or not spawn[field].startswith("OriginalMonsters/") for field in resource_fields):
                    raise ContentError(f"mission {mission['id']} has invalid first-damage spawn resources")
            elif spawn["green_prefab_resource"] or spawn["red_prefab_resource"]:
                raise ContentError(f"mission {mission['id']} disabled first-damage spawn must not declare resources")
        interactables = mission["interactables"]
        if not isinstance(interactables, list):
            raise ContentError(f"mission {mission['id']} has invalid interactables")
        interactable_ids = set()
        objective_interactable_count = 0
        for interactable in interactables:
            required = {"id", "target_id", "prefab_resource", "position"}
            if not isinstance(interactable, dict) or set(interactable) != required:
                raise ContentError(f"mission {mission['id']} has an invalid interactable")
            if not isinstance(interactable["id"], str) or not IDENTIFIER.fullmatch(interactable["id"]) or interactable["id"] in interactable_ids:
                raise ContentError(f"mission {mission['id']} has an invalid or duplicate interactable id")
            interactable_ids.add(interactable["id"])
            if not isinstance(interactable["target_id"], str) or not IDENTIFIER.fullmatch(interactable["target_id"]):
                raise ContentError(f"mission {mission['id']} has an invalid interactable target id")
            if not isinstance(interactable["prefab_resource"], str) or not interactable["prefab_resource"].startswith("OriginalNpcs/"):
                raise ContentError(f"mission {mission['id']} has an invalid interactable resource")
            _position(interactable["position"], f"mission {mission['id']} interactable position")
            if interactable["target_id"] == objective["target_id"]:
                objective_interactable_count += 1
        objective_source_count = objective_interactable_count if objective["kind"] == "interact" else objective_actor_count
        expected_source_count = 1 if objective["kind"] == "knockout" else objective["count"]
        if objective_source_count != expected_source_count:
            raise ContentError(f"mission {mission['id']} objective count does not match actor positions")
        if not isinstance(mission["evidence"], str) or not mission["evidence"]:
            raise ContentError(f"mission {mission['id']} is missing evidence")


def validate(root: Path) -> None:
    characters = json.loads((root / "characters.json").read_text(encoding="utf-8"))
    chapter = json.loads((root / "training_chapter.json").read_text(encoding="utf-8"))
    skills = json.loads((root / "skills.json").read_text(encoding="utf-8"))
    profiles = json.loads((root / "animation_profiles.json").read_text(encoding="utf-8"))
    chapters = json.loads((root / "chapters.json").read_text(encoding="utf-8"))
    missions = json.loads((root / "chapter1_missions.json").read_text(encoding="utf-8"))
    if any(document.get("schema_version") != 1 for document in (characters, chapter, skills, profiles, chapters, missions)):
        raise ContentError("unsupported schema version")
    _mission_catalog(missions)
    roster = characters.get("characters")
    if not isinstance(roster, list) or _ids(roster, "characters") != EXPECTED_CHARACTERS:
        raise ContentError("the roster must contain the 12 required characters")
    if len({value.get("class") for value in roster}) != 12:
        raise ContentError("each character must have a unique base class")
    if any(not value.get("prefab", "").startswith("Characters/") for value in roster):
        raise ContentError("each character must declare a prefab address")
    maps, quests = _ids(chapter["maps"], "maps"), _ids(chapter["quests"], "quests")
    items, equipment = _ids(chapter["items"], "items"), chapter["equipment"]
    spawns = _ids(chapter["spawns"], "spawns")
    portals = _ids(chapter["portals"], "portals")
    npcs = _ids(chapter["npcs"], "npcs")
    definition = chapter["chapter"]
    if set(definition["maps"]) - maps or set(definition["quests"]) - quests:
        raise ContentError("chapter contains unresolved references")
    for quest in chapter["quests"]:
        if quest["map"] not in maps or set(quest["reward_items"]) - items:
            raise ContentError(f"quest {quest['id']} contains unresolved references")
    for map_definition in chapter["maps"]:
        if set(map_definition["spawns"]) - spawns or set(map_definition["portals"]) - portals or set(map_definition["npcs"]) - npcs:
            raise ContentError(f"map {map_definition['id']} contains unresolved references")
    for portal in chapter["portals"]:
        if portal["destination_map"] not in maps:
            raise ContentError(f"portal {portal['id']} has an unresolved destination")
    for group in (chapter["spawns"], chapter["portals"], chapter["npcs"]):
        for definition in group:
            position = definition.get("position")
            if not isinstance(position, list) or len(position) != 3 or any(not isinstance(value, (int, float)) for value in position):
                raise ContentError(f"{definition['id']} must have a three-number position")
    for item in equipment:
        if set(item["variants"]) != EXPECTED_CHARACTERS:
            raise ContentError(f"equipment {item['id']} must have all 12 variants")
    skill_ids = _ids(skills.get("skills", []), "skills")
    if not skill_ids:
        raise ContentError("skills must contain at least one skill")
    for skill in skills["skills"]:
        if not isinstance(skill.get("name_key"), str) or not skill["name_key"]:
            raise ContentError(f"skill {skill.get('id')} has an invalid name key")
        if not isinstance(skill.get("animation_clip"), str) or not skill["animation_clip"]:
            raise ContentError(f"skill {skill.get('id')} has an invalid animation clip")
        if not isinstance(skill.get("damage"), int) or skill["damage"] < 0:
            raise ContentError(f"skill {skill.get('id')} has invalid damage")
        if not isinstance(skill.get("cooldown_seconds"), (int, float)) or skill["cooldown_seconds"] < 0:
            raise ContentError(f"skill {skill.get('id')} has invalid cooldown")
        expected_fields = {"id", "character_id", "name_key", "animation_clip", "damage", "cooldown_seconds", "hit_delay_seconds", "action_duration_seconds", "combo_window_start_seconds", "combo_window_end_seconds", "combo_next_skill_id", "resource_cost", "range", "projectile_speed", "projectile_lifetime_seconds", "projectile_homing_radians_per_second", "status_effect_id", "status_duration_seconds", "status_tick_seconds", "status_damage_per_tick", "status_movement_multiplier"}
        if set(skill) != expected_fields:
            raise ContentError(f"skill {skill.get('id')} has unsupported or missing fields")
        if not isinstance(skill["character_id"], str):
            raise ContentError(f"skill {skill.get('id')} has invalid character")
        if skill["character_id"] and skill["character_id"] not in EXPECTED_CHARACTERS:
            raise ContentError(f"skill {skill.get('id')} has unknown character")
        if not isinstance(skill["projectile_speed"], (int, float)) or skill["projectile_speed"] < 0 or not isinstance(skill["projectile_lifetime_seconds"], (int, float)) or skill["projectile_lifetime_seconds"] < 0 or not isinstance(skill["projectile_homing_radians_per_second"], (int, float)) or skill["projectile_homing_radians_per_second"] < 0:
            raise ContentError(f"skill {skill.get('id')} has invalid projectile configuration")
        if (skill["projectile_speed"] == 0) != (skill["projectile_lifetime_seconds"] == 0):
            raise ContentError(f"skill {skill.get('id')} has incomplete projectile configuration")
        if not isinstance(skill["status_effect_id"], str) or not isinstance(skill["status_duration_seconds"], (int, float)) or skill["status_duration_seconds"] < 0 or not isinstance(skill["status_tick_seconds"], (int, float)) or skill["status_tick_seconds"] < 0 or not isinstance(skill["status_damage_per_tick"], int) or skill["status_damage_per_tick"] < 0 or not isinstance(skill["status_movement_multiplier"], (int, float)) or skill["status_movement_multiplier"] < 0:
            raise ContentError(f"skill {skill.get('id')} has invalid status configuration")
        if bool(skill["status_effect_id"]) != (skill["status_duration_seconds"] > 0):
            raise ContentError(f"skill {skill.get('id')} has incomplete status configuration")
        if not isinstance(skill.get("hit_delay_seconds"), (int, float)) or not 0 <= skill["hit_delay_seconds"] <= skill["cooldown_seconds"]:
            raise ContentError(f"skill {skill.get('id')} has invalid hit delay")
        duration = skill.get("action_duration_seconds")
        combo_start = skill.get("combo_window_start_seconds")
        combo_end = skill.get("combo_window_end_seconds")
        combo_next = skill.get("combo_next_skill_id")
        if not isinstance(duration, (int, float)) or duration < skill["hit_delay_seconds"]:
            raise ContentError(f"skill {skill.get('id')} has invalid action duration")
        if not isinstance(combo_start, (int, float)) or not isinstance(combo_end, (int, float)) or not 0 <= combo_start <= combo_end <= duration:
            raise ContentError(f"skill {skill.get('id')} has invalid combo window")
        if not isinstance(combo_next, str) or (combo_next and combo_next not in skill_ids):
            raise ContentError(f"skill {skill.get('id')} has an unresolved combo skill")
        if not combo_next and (combo_start != 0 or combo_end != 0):
            raise ContentError(f"skill {skill.get('id')} has a combo window without a next skill")
        if not isinstance(skill.get("resource_cost"), int) or skill["resource_cost"] < 0:
            raise ContentError(f"skill {skill.get('id')} has invalid resource cost")
        if not isinstance(skill.get("range"), (int, float)) or skill["range"] <= 0:
            raise ContentError(f"skill {skill.get('id')} has invalid range")
    profile_ids = _ids(profiles.get("profiles", []), "animation profiles", "character_id")
    if profile_ids != EXPECTED_CHARACTERS:
        raise ContentError("animation profiles must cover all 12 characters")
    for profile in profiles["profiles"]:
        entries = profile.get("skills")
        if not isinstance(entries, list) or not entries:
            raise ContentError(f"animation profile {profile.get('id')} has no skills")
        entry_ids = [entry.get("skill_id") for entry in entries]
        if len(entry_ids) != len(set(entry_ids)) or set(entry_ids) - skill_ids:
            raise ContentError(f"animation profile {profile['character_id']} contains unresolved or duplicate skills")
        if any(not isinstance(entry.get("clip_name"), str) or not entry["clip_name"] for entry in entries):
            raise ContentError(f"animation profile {profile['character_id']} contains an invalid clip")
    chapter_entries = chapters.get("chapters")
    if not isinstance(chapter_entries, list) or len(chapter_entries) != 12:
        raise ContentError("chapter catalog must contain exactly 12 chapters")
    chapter_ids = _ids(chapter_entries, "chapters")
    if {entry.get("number") for entry in chapter_entries} != set(range(1, 13)):
        raise ContentError("chapter catalog must contain chapter numbers 1 through 12")
    for entry in chapter_entries:
        if entry.get("stage_count") != 8:
            raise ContentError(f"chapter {entry.get('id')} must contain 8 stages")
        if entry.get("status") not in {"planned", "in_progress", "complete"}:
            raise ContentError(f"chapter {entry.get('id')} has an invalid status")
        if not isinstance(entry.get("evidence"), str) or not entry["evidence"]:
            raise ContentError(f"chapter {entry.get('id')} is missing evidence")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path)
    args = parser.parse_args()
    try:
        validate(args.root)
    except (ContentError, KeyError, TypeError, json.JSONDecodeError) as error:
        print(f"Content validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
    print("Content validation passed.")


if __name__ == "__main__":
    main()
