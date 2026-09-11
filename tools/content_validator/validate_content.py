"""Validate cross-referenced Phase 4 content without loading game code."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import re
import sys

IDENTIFIER = re.compile(r"^[a-z][a-z0-9_.-]{1,63}$")
EXPECTED_CHARACTERS = {"wolf", "bison", "panda", "whale", "mole", "rabbit", "monkey", "sheep", "penguin", "bat", "chameleon", "cat"}


class ContentError(ValueError):
    pass


def _ids(values: list[dict], label: str) -> set[str]:
    result = [value.get("id") for value in values]
    if any(not isinstance(value, str) or not IDENTIFIER.fullmatch(value) for value in result):
        raise ContentError(f"{label} contains an invalid id")
    if len(result) != len(set(result)):
        raise ContentError(f"{label} contains duplicate ids")
    return set(result)


def validate(root: Path) -> None:
    characters = json.loads((root / "characters.json").read_text(encoding="utf-8"))
    chapter = json.loads((root / "training_chapter.json").read_text(encoding="utf-8"))
    if characters.get("schema_version") != 1 or chapter.get("schema_version") != 1:
        raise ContentError("unsupported schema version")
    roster = characters.get("characters")
    if not isinstance(roster, list) or _ids(roster, "characters") != EXPECTED_CHARACTERS:
        raise ContentError("the roster must contain the 12 required characters")
    if len({value.get("class") for value in roster}) != 12:
        raise ContentError("each character must have a unique base class")
    if any(not value.get("prefab", "").startswith("Characters/") for value in roster):
        raise ContentError("each character must declare a prefab address")
    maps, quests = _ids(chapter["maps"], "maps"), _ids(chapter["quests"], "quests")
    items, equipment = _ids(chapter["items"], "items"), chapter["equipment"]
    definition = chapter["chapter"]
    if set(definition["maps"]) - maps or set(definition["quests"]) - quests:
        raise ContentError("chapter contains unresolved references")
    for quest in chapter["quests"]:
        if quest["map"] not in maps or set(quest["reward_items"]) - items:
            raise ContentError(f"quest {quest['id']} contains unresolved references")
    for item in equipment:
        if set(item["variants"]) != EXPECTED_CHARACTERS:
            raise ContentError(f"equipment {item['id']} must have all 12 variants")


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
