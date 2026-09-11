from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from tools.import_legacy_assets import static_batch_mesh_candidates


class LegacyAssetImportTests(unittest.TestCase):
    def test_static_batch_adds_matching_visual_mesh_only(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            assets = Path(temp) / "Assets"
            mesh = assets / "Mesh"
            scene = assets / "Scene"
            mesh.mkdir(parents=True)
            scene.mkdir()
            visual = mesh / "PlainFence_short.asset"
            collision = mesh / "PlainFence_short_c.asset"
            visual.write_text("mesh", encoding="utf-8")
            collision.write_text("collider", encoding="utf-8")
            source = scene / "M101_Test.unity"
            source.write_text(
                """--- !u!1 &1
GameObject:
  m_Name: Plain_Fence_short
--- !u!23 &2
MeshRenderer:
  m_GameObject: {fileID: 1}
  m_SubsetIndices: 07000000
""",
                encoding="utf-8",
            )
            candidates, unresolved = static_batch_mesh_candidates([source], assets)
            self.assertEqual(candidates, {visual})
            self.assertEqual(unresolved, {})


if __name__ == "__main__":
    unittest.main()
