from __future__ import annotations

import copy
import unittest

from src.protocol.messages import MessageError, validate_client_message


def message(kind: str, payload: dict) -> dict:
    return {"protocol_version": 1, "sequence": 7, "kind": kind, "payload": payload}


class ProtocolMessageTests(unittest.TestCase):
    def test_accepts_intent_messages(self) -> None:
        messages = [
            message("movement_input", {"actor_id": "actor.cat", "direction": [0.0, 0.0, 1.0], "client_tick": 12}),
            message("action_request", {"actor_id": "actor.cat", "skill_id": "skill.basic", "target_id": "monster.slime", "aim": [0, 0, 1]}),
            message("revive_request", {"actor_id": "actor.cat", "target_id": "actor.mole"}),
            message("inventory_swap_request", {"actor_id": "actor.cat", "source_slot": 0, "destination_slot": 1}),
        ]
        for value in messages:
            with self.subTest(kind=value["kind"]):
                self.assertIs(validate_client_message(value), value)

    def test_client_cannot_supply_damage_or_rewards(self) -> None:
        value = message("action_request", {"actor_id": "actor.cat", "skill_id": "skill.basic", "target_id": "monster.slime", "aim": [0, 0, 1], "damage": 999999})
        with self.assertRaisesRegex(MessageError, "server-owned fields: damage"):
            validate_client_message(value)

    def test_rejects_non_finite_and_oversized_vectors(self) -> None:
        for direction in ([float("nan"), 0, 0], [2, 0, 0]):
            with self.subTest(direction=direction):
                value = message("movement_input", {"actor_id": "actor.cat", "direction": direction, "client_tick": 12})
                with self.assertRaisesRegex(MessageError, "outside the allowed range"):
                    validate_client_message(value)

    def test_rejects_same_inventory_slot(self) -> None:
        value = message("inventory_swap_request", {"actor_id": "actor.cat", "source_slot": 3, "destination_slot": 3})
        with self.assertRaisesRegex(MessageError, "must differ"):
            validate_client_message(value)

    def test_rejects_unknown_message_and_version(self) -> None:
        value = message("award_currency", {})
        with self.assertRaisesRegex(MessageError, "unsupported message kind"):
            validate_client_message(value)
        value = copy.deepcopy(value)
        value["protocol_version"] = 2
        with self.assertRaisesRegex(MessageError, "unsupported protocol_version"):
            validate_client_message(value)
