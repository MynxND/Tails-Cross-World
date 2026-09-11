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
                Assert.That(Resources.Load<GameObject>($"Characters/{name}"), Is.Not.Null, $"Missing prefab for {id}");
            }
        }
    }
}
