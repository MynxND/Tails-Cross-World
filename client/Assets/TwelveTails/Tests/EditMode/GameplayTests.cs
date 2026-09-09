using NUnit.Framework;
using TwelveTails.Gameplay;
using UnityEngine;

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
    }
}
