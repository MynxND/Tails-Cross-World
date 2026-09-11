"""Strict client-to-server intent contracts for the clean-room protocol."""

from __future__ import annotations

import math
import re
from typing import Any

ID = re.compile(r"^[a-z][a-z0-9_.-]{1,63}$")
FORBIDDEN_AUTHORITY_FIELDS = {
    "damage", "heal", "hp", "mp", "sp", "ko", "experience", "currency",
    "drop_ids", "reward_ids", "quest_completed", "status_duration",
}


class MessageError(ValueError):
    pass


def _exact(obj: dict[str, Any], fields: set[str], path: str) -> None:
    forbidden = obj.keys() & FORBIDDEN_AUTHORITY_FIELDS
    if forbidden:
        raise MessageError(f"{path} contains server-owned fields: {', '.join(sorted(forbidden))}")
    missing, extra = fields - obj.keys(), obj.keys() - fields
    if missing:
        raise MessageError(f"{path} missing fields: {', '.join(sorted(missing))}")
    if extra:
        raise MessageError(f"{path} unknown fields: {', '.join(sorted(extra))}")


def _id(value: Any, path: str) -> str:
    if not isinstance(value, str) or not ID.fullmatch(value):
        raise MessageError(f"{path} must be a valid identifier")
    return value


def _integer(value: Any, path: str, low: int, high: int) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or not low <= value <= high:
        raise MessageError(f"{path} must be an integer from {low} to {high}")
    return value


def _vector(value: Any, path: str, maximum: float) -> None:
    if not isinstance(value, list) or len(value) != 3:
        raise MessageError(f"{path} must be a three-number array")
    for index, component in enumerate(value):
        if isinstance(component, bool) or not isinstance(component, (int, float)) or not math.isfinite(component) or abs(component) > maximum:
            raise MessageError(f"{path}[{index}] is outside the allowed range")


def validate_client_message(message: Any) -> dict[str, Any]:
    if not isinstance(message, dict):
        raise MessageError("message must be an object")
    _exact(message, {"protocol_version", "sequence", "kind", "payload"}, "message")
    if message["protocol_version"] != 1:
        raise MessageError("unsupported protocol_version")
    _integer(message["sequence"], "message.sequence", 0, 2_147_483_647)
    kind, payload = message["kind"], message["payload"]
    if not isinstance(kind, str) or not isinstance(payload, dict):
        raise MessageError("message kind and payload have invalid types")

    if kind == "movement_input":
        _exact(payload, {"actor_id", "direction", "client_tick"}, "payload")
        _id(payload["actor_id"], "payload.actor_id")
        _vector(payload["direction"], "payload.direction", 1.0)
        _integer(payload["client_tick"], "payload.client_tick", 0, 2_147_483_647)
    elif kind == "action_request":
        _exact(payload, {"actor_id", "skill_id", "target_id", "aim"}, "payload")
        for field in ("actor_id", "skill_id", "target_id"):
            _id(payload[field], f"payload.{field}")
        _vector(payload["aim"], "payload.aim", 1.0)
    elif kind == "revive_request":
        _exact(payload, {"actor_id", "target_id"}, "payload")
        _id(payload["actor_id"], "payload.actor_id")
        _id(payload["target_id"], "payload.target_id")
    elif kind == "inventory_swap_request":
        _exact(payload, {"actor_id", "source_slot", "destination_slot"}, "payload")
        _id(payload["actor_id"], "payload.actor_id")
        _integer(payload["source_slot"], "payload.source_slot", 0, 255)
        _integer(payload["destination_slot"], "payload.destination_slot", 0, 255)
        if payload["source_slot"] == payload["destination_slot"]:
            raise MessageError("source_slot and destination_slot must differ")
    elif kind in {"herd_pen_request", "herd_death_report"}:
        _exact(payload, {"actor_id", "mupo_id"}, "payload")
        _id(payload["actor_id"], "payload.actor_id")
        _id(payload["mupo_id"], "payload.mupo_id")
    else:
        raise MessageError(f"unsupported message kind: {kind!r}")
    return message
