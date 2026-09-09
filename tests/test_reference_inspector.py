from __future__ import annotations

import hashlib
import json
import struct
import tempfile
import unittest
from pathlib import Path

from tools.reference_inspector.inspect_reference import inspect, main, pe_architecture


def write_minimal_pe(path: Path, machine: int = 0x014C) -> None:
    data = bytearray(256)
    data[0:2] = b"MZ"
    struct.pack_into("<I", data, 0x3C, 128)
    data[128:132] = b"PE\0\0"
    struct.pack_into("<H", data, 132, machine)
    path.write_bytes(data)


class ReferenceInspectorTests(unittest.TestCase):
    def make_reference(self, base: Path) -> Path:
        root = base / "reference"
        data = root / "Game_Data"
        managed = data / "Managed"
        managed.mkdir(parents=True)
        write_minimal_pe(root / "Game.exe")
        (data / "mainData").write_bytes(b"header 3.5.7f6 footer")
        (data / "level0").write_bytes(b"level")
        (data / "sharedassets0.assets").write_bytes(b"asset")
        (managed / "Assembly-CSharp.dll").write_bytes(b"assembly")
        (data / "SaveGame").mkdir()
        (data / "SaveGame" / "Character.zrData").write_bytes(b"private")
        return root

    def test_inventory_detects_build_without_mutating_it(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = self.make_reference(Path(temp))
            before = {p.relative_to(root): hashlib.sha256(p.read_bytes()).hexdigest() for p in root.rglob("*") if p.is_file()}
            report = inspect(root)
            after = {p.relative_to(root): hashlib.sha256(p.read_bytes()).hexdigest() for p in root.rglob("*") if p.is_file()}
            self.assertEqual(before, after)
            self.assertEqual(report["engine"]["unity_versions"], ["3.5.7f6"])
            self.assertEqual(report["executables"][0]["architecture"], "x86")
            self.assertEqual(report["content"]["level_files"], 1)
            self.assertEqual(report["content"]["shared_asset_files"], 1)
            self.assertEqual(len(report["managed_assemblies"]), 1)
            self.assertEqual(len(report["file_manifest"]), report["reference"]["file_count"])
            self.assertTrue(all(len(entry["sha256"]) == 64 for entry in report["file_manifest"]))
            self.assertIn("Game_Data/SaveGame/Character.zrData", report["sensitive_runtime_paths"])

    def test_output_inside_reference_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = self.make_reference(Path(temp))
            with self.assertRaises(SystemExit):
                main(["--root", str(root), "--output", str(root / "report.json")])
            self.assertFalse((root / "report.json").exists())

    def test_json_output_is_written_outside_reference(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            root = self.make_reference(base)
            output = base / "reports" / "inventory.json"
            self.assertEqual(main(["--root", str(root), "--output", str(output)]), 0)
            payload = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(payload["schema_version"], 1)

    def test_invalid_pe_is_not_reported_as_an_architecture(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "bad.exe"
            path.write_bytes(b"not a PE")
            self.assertIsNone(pe_architecture(path))


if __name__ == "__main__":
    unittest.main()
