from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from tools.save_probe.inspect_zrdata import NRBF_HEADER, entropy, inspect_directory, inspect_file


class SaveProbeTests(unittest.TestCase):
    def test_detects_nrbf_header_without_deserializing(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "CharacterData0.zrData"
            path.write_bytes(NRBF_HEADER + b"synthetic-fixture")
            result = inspect_file(path)
            self.assertEqual(result["format"], "dotnet-binaryformatter-nrbf")
            self.assertEqual(result["bytes"], len(NRBF_HEADER) + len(b"synthetic-fixture"))

    def test_unknown_and_empty_inputs_are_safe(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "bad.zrData"
            path.write_bytes(b"")
            self.assertEqual(inspect_file(path)["format"], "unknown")
            self.assertEqual(entropy(b""), 0.0)

    def test_directory_ignores_unrelated_files(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "one.zrData").write_bytes(NRBF_HEADER)
            (root / "notes.txt").write_text("private", encoding="utf-8")
            report = inspect_directory(root)
            self.assertEqual([entry["name"] for entry in report["files"]], ["one.zrData"])
            self.assertIn("no BinaryFormatter", report["safety"])
