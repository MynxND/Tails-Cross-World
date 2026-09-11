from __future__ import annotations
import json
from pathlib import Path
import tempfile
import unittest
from tools.content_validator.validate_content import ContentError, validate


class ContentValidatorTests(unittest.TestCase):
    def test_repository_content_is_valid(self) -> None:
        validate(Path("content/v1"))

    def test_missing_character_variant_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in ("characters.json", "training_chapter.json"):
                data = json.loads((Path("content/v1") / name).read_text(encoding="utf-8"))
                if name == "training_chapter.json":
                    data["equipment"][0]["variants"].pop()
                (root / name).write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaisesRegex(ContentError, "all 12 variants"):
                validate(root)
