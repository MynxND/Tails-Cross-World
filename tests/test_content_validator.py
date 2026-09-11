from __future__ import annotations
import json
from pathlib import Path
import tempfile
import unittest
from tools.content_validator.validate_content import ContentError, validate


class ContentValidatorTests(unittest.TestCase):
    CONTENT_FILES = ("characters.json", "training_chapter.json", "skills.json", "animation_profiles.json", "chapters.json", "chapter1_missions.json")

    def test_repository_content_is_valid(self) -> None:
        validate(Path("content/v1"))

    def test_missing_character_variant_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in self.CONTENT_FILES:
                data = json.loads((Path("content/v1") / name).read_text(encoding="utf-8"))
                if name == "training_chapter.json":
                    data["equipment"][0]["variants"].pop()
                (root / name).write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaisesRegex(ContentError, "all 12 variants"):
                validate(root)

    def test_missing_skill_reference_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in self.CONTENT_FILES:
                data = json.loads((Path("content/v1") / name).read_text(encoding="utf-8"))
                if name == "animation_profiles.json":
                    data["profiles"][0]["skills"][0]["skill_id"] = "skill.missing"
                (root / name).write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaisesRegex(ContentError, "unresolved"):
                validate(root)

    def test_missing_combo_skill_reference_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in self.CONTENT_FILES:
                data = json.loads((Path("content/v1") / name).read_text(encoding="utf-8"))
                if name == "skills.json":
                    data["skills"][0]["combo_next_skill_id"] = "skill.missing"
                (root / name).write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaisesRegex(ContentError, "unresolved combo skill"):
                validate(root)

    def test_combo_window_must_fit_action_duration(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in self.CONTENT_FILES:
                data = json.loads((Path("content/v1") / name).read_text(encoding="utf-8"))
                if name == "skills.json":
                    data["skills"][0]["combo_window_end_seconds"] = data["skills"][0]["action_duration_seconds"] + 0.1
                (root / name).write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaisesRegex(ContentError, "invalid combo window"):
                validate(root)

    def test_hit_sequence_must_fit_action_duration(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in self.CONTENT_FILES:
                data = json.loads((Path("content/v1") / name).read_text(encoding="utf-8"))
                if name == "skills.json":
                    data["skills"][0]["hit_count"] = 2
                    data["skills"][0]["hit_interval_seconds"] = data["skills"][0]["action_duration_seconds"]
                (root / name).write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaisesRegex(ContentError, "hit sequence outside"):
                validate(root)

    def test_explicit_hit_schedule_must_match_count(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in self.CONTENT_FILES:
                data = json.loads((Path("content/v1") / name).read_text(encoding="utf-8"))
                if name == "skills.json":
                    data["skills"][-1]["hit_times_seconds"] = [0.4, 0.7]
                (root / name).write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaisesRegex(ContentError, "invalid explicit hit schedule"):
                validate(root)

    def test_oriented_target_requires_positive_dimensions(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in self.CONTENT_FILES:
                data = json.loads((Path("content/v1") / name).read_text(encoding="utf-8"))
                if name == "skills.json":
                    data["skills"][0]["target_shape"] = "oriented_box"
                (root / name).write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaisesRegex(ContentError, "incomplete oriented target dimensions"):
                validate(root)

    def test_mission_objective_count_must_match_actor_positions(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in self.CONTENT_FILES:
                data = json.loads((Path("content/v1") / name).read_text(encoding="utf-8"))
                if name == "chapter1_missions.json":
                    data["missions"][0]["objective"]["count"] += 1
                (root / name).write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaisesRegex(ContentError, "objective count does not match"):
                validate(root)

    def test_knockout_objective_reuses_one_actor_for_multiple_rounds(self) -> None:
        validate(Path("content/v1"))
        missions = json.loads((Path("content/v1") / "chapter1_missions.json").read_text(encoding="utf-8"))["missions"]
        mission = next(item for item in missions if item["id"] == "mission.m106_boldas_recruitment")
        self.assertEqual(mission["objective"]["kind"], "knockout")
        self.assertEqual(mission["objective"]["count"], 3)
        self.assertEqual(sum(len(group["positions"]) for group in mission["actors"] if group["entity_id"] == mission["objective"]["target_id"]), 1)

    def test_interaction_only_mission_does_not_require_combat_actors(self) -> None:
        missions = json.loads((Path("content/v1") / "chapter1_missions.json").read_text(encoding="utf-8"))["missions"]
        mission = next(item for item in missions if item["id"] == "mission.m107_request_from_alcacia")
        self.assertEqual(mission["objective"]["kind"], "interact")
        self.assertEqual(mission["actors"], [])
        validate(Path("content/v1"))
