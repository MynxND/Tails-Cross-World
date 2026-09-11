import unittest

from tools.extract_skill_effects import _combat_entry


def _tree_entry(effect_key: str) -> dict[str, object]:
    return {
        "effect_key": effect_key,
        "tier": 1,
        "cost_mp": 4,
        "cost_mana": 0,
    }


class SkillEffectExtractionTests(unittest.TestCase):
    def test_simultaneous_damage_calls_are_one_temporal_hit(self) -> None:
        source = """
public virtual IEnumerator RPC_megalodon(Vector3 position)
{
    PlayAnimation("megalodon", 0f);
    Damage.FindAreaTarget(position, 4f, 2f, 1);
    mChar.hit(1, firstTarget, 10, 0, 0, Vector3.zero);
    mChar.hit(1, secondTarget, 10, 0, 0, Vector3.zero);
}
"""

        entry, reasons = _combat_entry("whale", _tree_entry("megalodon"), source)

        self.assertEqual([], reasons)
        self.assertIsNotNone(entry)
        self.assertEqual([0.0], entry["hit_times_seconds"])
        self.assertEqual(1, entry["hit_count"])

    def test_generic_effect_handler_only_supplies_missing_animation(self) -> None:
        source = """
public virtual IEnumerator RPC_warCapital(Vector3 position)
{
    Damage.FindAreaTarget(position, 3f, 2f, 1);
    mChar.hit(1, target, 12, 0, 0, Vector3.zero);
}
public virtual IEnumerator RPC_assemble1(string command, Vector3 position)
{
    PlayAnimation("assemble", 0f);
    Damage.FindAreaTarget(position, 99f, 2f, 1);
    mChar.hit(1, target, 999, 0, 0, Vector3.zero);
}
public virtual void BeginWarCapital(Vector3 position)
{
    RPC_assemble1("warCapital", position);
}
"""

        entry, reasons = _combat_entry("mole", _tree_entry("warCapital"), source)

        self.assertEqual([], reasons)
        self.assertIsNotNone(entry)
        self.assertEqual("assemble", entry["animation_clip"])
        self.assertEqual(12, entry["damage"])
        self.assertEqual(3.0, entry["range"])


if __name__ == "__main__":
    unittest.main()