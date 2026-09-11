from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from tools.audit_legacy_scenes import audit_scene, index_assets


class LegacySceneAuditTests(unittest.TestCase):
    def test_detects_real_static_subset_and_ignores_empty_field(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            assets = Path(temp) / "Assets"
            scene_dir = assets / "Scene"
            scene_dir.mkdir(parents=True)
            scene = scene_dir / "M101_Test.unity"
            scene.write_text(
                """--- !u!1 &1
GameObject:
  m_Name: Fence
--- !u!23 &2
MeshRenderer:
  m_LightmapIndex: 255
  m_SubsetIndices:
  m_StaticBatchRoot: {fileID: 0}
--- !u!23 &3
MeshRenderer:
  m_LightmapIndex: 0
  m_SubsetIndices: 07000000
  m_Materials:
  - {fileID: 10302, guid: 0000000000000000e000000000000000, type: 0}
""",
                encoding="utf-8",
            )
            index, scenes = index_assets(assets)
            result = audit_scene(scenes[0], assets, index, {})
            self.assertEqual(result["legacy_subset_renderers"], 1)
            self.assertEqual(result["lightmapped_renderers"], 1)
            self.assertEqual(result["unresolved_guids"], 0)
            self.assertIn("legacy_static_batch", result["risks"])


if __name__ == "__main__":
    unittest.main()
