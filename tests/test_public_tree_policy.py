from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from tools.check_public_tree import violation


class PublicTreePolicyTests(unittest.TestCase):
    def test_blocks_legacy_extensions_and_pe_signatures(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.assertIsNotNone(violation(Path("client/Game.dll"), root))
            disguised = root / "notes.txt"
            disguised.write_bytes(b"MZpayload")
            self.assertEqual(violation(Path("notes.txt"), root), "PE executable signature")

    def test_allows_clean_room_source(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "src" / "server.cs"
            source.parent.mkdir()
            source.write_text("public class Server {}", encoding="utf-8")
            self.assertIsNone(violation(Path("src/server.cs"), root))


if __name__ == "__main__":
    unittest.main()
