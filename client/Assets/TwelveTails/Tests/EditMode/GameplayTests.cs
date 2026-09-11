using NUnit.Framework;
using TwelveTails.Gameplay;
using UnityEngine;
using System.IO;

namespace TwelveTails.Tests
{
    public sealed class GameplayTests
    {
        [Test]
        public void DamageAndHealingAreClamped()
        {
            var gameObject = new GameObject();
            try
            {
                var health = gameObject.AddComponent<Health>();
                health.Configure(30);
                Assert.That(health.ApplyDamage(50), Is.EqualTo(30));
                Assert.That(health.Current, Is.Zero);
                Assert.That(health.ApplyHealing(10), Is.Zero);
            }
            finally { Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void QuestOnlyCountsItsConfiguredTargetOnce()
        {
            var gameObject = new GameObject();
            try
            {
                var quest = gameObject.AddComponent<QuestProgress>();
                Assert.That(quest.RegisterDefeat("monster.other"), Is.False);
                Assert.That(quest.RegisterDefeat("monster.training_dummy"), Is.True);
                Assert.That(quest.RegisterDefeat("monster.training_dummy"), Is.False);
                Assert.That(quest.Defeats, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void RewardCanOnlyBeGrantedOnce()
        {
            var gameObject = new GameObject();
            try
            {
                var progress = gameObject.AddComponent<PlayerProgress>();
                Assert.That(progress.GrantQuestReward(25, 1), Is.True);
                Assert.That(progress.GrantQuestReward(25, 1), Is.False);
                Assert.That(progress.Experience, Is.EqualTo(25));
                Assert.That(progress.PotionCount, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void SaveRoundTripAndChecksumRejectionWork()
        {
            var directory = Path.Combine(Application.temporaryCachePath, "twelve-tails-tests", System.Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "save.json");
            try
            {
                ProgressSave.Write(path, new ProgressSaveData { experience = 25, potionCount = 1, questComplete = true, rewardClaimed = true });
                var loaded = ProgressSave.Read(path);
                Assert.That(loaded.experience, Is.EqualTo(25));
                Assert.That(loaded.questComplete, Is.True);
                File.WriteAllText(path, File.ReadAllText(path).Replace("\"experience\": 25", "\"experience\": 999"));
                Assert.Throws<InvalidDataException>(() => ProgressSave.Read(path));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void CharacterRosterContainsTwelveUniqueCharactersAndClasses()
        {
            Assert.That(CharacterRoster.Ids, Has.Length.EqualTo(12));
            Assert.That(CharacterRoster.Classes, Has.Length.EqualTo(12));
            Assert.That(new System.Collections.Generic.HashSet<string>(CharacterRoster.Ids).Count, Is.EqualTo(12));
            Assert.That(new System.Collections.Generic.HashSet<string>(CharacterRoster.Classes).Count, Is.EqualTo(12));
        }

        [Test]
        public void EveryCharacterHasALoadablePrefab()
        {
            foreach (var id in CharacterRoster.Ids)
            {
                var name = char.ToUpperInvariant(id[0]) + id.Substring(1);
                var prefab = Resources.Load<GameObject>($"Characters/{name}");
                Assert.That(prefab, Is.Not.Null, $"Missing prefab for {id}");
                var names = new System.Collections.Generic.HashSet<string>();
                foreach (var child in prefab.GetComponentsInChildren<Transform>(true)) names.Add(child.name);
                Assert.That(names, Does.Contain("Armor"), $"Missing armor variant for {id}");
                Assert.That(names, Does.Contain("WeaponSocket"), $"Missing weapon socket for {id}");
                Assert.That(prefab.GetComponent<CharacterAnimationDriver>() != null || prefab.GetComponent<AnimatorMotionDriver>() != null, Is.True, $"Missing animation driver for {id}");
                var animator = prefab.GetComponent<Animator>();
                if (prefab.GetComponent<AnimatorMotionDriver>() != null)
                    Assert.That(animator != null && animator.runtimeAnimatorController != null, Is.True, $"Missing Animator Controller for {id}");
            }
        }
    }
}
