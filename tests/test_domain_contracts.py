from __future__ import annotations

import copy
import unittest

from src.domain.contracts import ContractError, validate_document


VALID_DOCUMENTS = {
    "account": {"account_id": "account.demo", "display_name": "Demo", "character_ids": ["character.cat"], "status": "active"},
    "character": {"character_id": "character.cat", "account_id": "account.demo", "archetype_id": "cat", "level": 1, "experience": 0, "stats": {"hp": 100, "attack": 10}, "equipment_ids": []},
    "item": {"item_id": "item.potion", "kind": "consumable", "name_key": "item.potion.name", "max_stack": 99},
    "equipment": {"equipment_id": "equipment.wood_sword", "item_id": "item.wood_sword", "slot": "weapon", "modifiers": {"attack": 2}, "rig_variants": [f"rig.variant_{i}" for i in range(12)]},
    "quest": {"quest_id": "quest.first_hunt", "prerequisites": [], "objectives": [{"kind": "defeat", "target_id": "monster.slime", "count": 1}], "reward_item_ids": ["item.potion"], "reward_experience": 10},
    "monster": {"monster_id": "monster.slime", "level": 1, "stats": {"hp": 20, "attack": 2}, "ai_profile_id": "ai.melee", "skill_ids": [], "drop_table_id": "drop.slime"},
    "map": {"map_id": "map.training", "scene_id": "scene.training", "spawn_ids": ["spawn.start"], "portal_ids": [], "npc_ids": ["npc.guide"], "monster_group_ids": ["group.slime"], "safe_zone_ids": ["zone.start"]},
    "skill": {"skill_id": "skill.basic_slash", "character_id": "", "name_key": "skill.basic_slash.name", "animation_clip": "nAttack1", "damage": 10, "cooldown_seconds": 0.35, "hit_delay_seconds": 0.12, "action_duration_seconds": 0.5, "combo_window_start_seconds": 0.25, "combo_window_end_seconds": 0.45, "combo_next_skill_id": "skill.power_strike", "resource_cost": 0, "range": 2.25, "projectile_speed": 0.0, "projectile_lifetime_seconds": 0.0, "projectile_homing_radians_per_second": 0.0, "status_effect_id": "", "status_duration_seconds": 0.0, "status_tick_seconds": 0.0, "status_damage_per_tick": 0, "status_movement_multiplier": 1.0},
    "animation_profile": {"character_id": "cat", "skills": [{"skill_id": "skill.basic_slash", "clip_name": "nAttack1"}]},
}


class DomainContractTests(unittest.TestCase):
    def document(self, kind: str) -> dict:
        return {"schema_version": 1, "kind": kind, "data": copy.deepcopy(VALID_DOCUMENTS[kind])}

    def test_all_initial_contracts_accept_valid_documents(self) -> None:
        for kind in VALID_DOCUMENTS:
            with self.subTest(kind=kind):
                self.assertEqual(validate_document(self.document(kind))["kind"], kind)

    def test_unknown_fields_are_rejected(self) -> None:
        document = self.document("item")
        document["data"]["client_price_override"] = 0
        with self.assertRaisesRegex(ContractError, "unknown fields"):
            validate_document(document)

    def test_invalid_and_future_versions_are_rejected(self) -> None:
        document = self.document("account")
        document["schema_version"] = 2
        with self.assertRaisesRegex(ContractError, "unsupported schema_version"):
            validate_document(document)

    def test_non_finite_stats_are_rejected(self) -> None:
        document = self.document("monster")
        document["data"]["stats"]["hp"] = float("nan")
        with self.assertRaisesRegex(ContractError, "finite number"):
            validate_document(document)

    def test_equipment_requires_all_character_variants(self) -> None:
        document = self.document("equipment")
        document["data"]["rig_variants"].pop()
        with self.assertRaisesRegex(ContractError, "exactly 12"):
            validate_document(document)

    def test_duplicate_identifiers_are_rejected(self) -> None:
        document = self.document("map")
        document["data"]["npc_ids"] = ["npc.guide", "npc.guide"]
        with self.assertRaisesRegex(ContractError, "duplicate"):
            validate_document(document)
