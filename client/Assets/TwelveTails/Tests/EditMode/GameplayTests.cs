using NUnit.Framework;
using TwelveTails.Gameplay;
using UnityEngine;
using System.IO;

namespace TwelveTails.Tests
{
    public sealed class GameplayTests
    {
        [Test]
        public void ChapterOneRoutesToCarronHarvestInsteadOfTrainingGround()
        {
            Assert.That(ChapterMenu.ChapterScene(1), Is.EqualTo("CarronHarvest"));
            Assert.That(ChapterMenu.ChapterScene(1), Is.Not.EqualTo("TrainingGround"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ChapterMenu.ChapterScene(2));
        }

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
        public void FirstDamageSpawnerCreatesThreeActorsOnlyOnce()
        {
            var nest = new GameObject("Nest");
            var target = new GameObject("Target");
            var greenPrefab = new GameObject("Green Prefab");
            var redPrefab = new GameObject("Red Prefab");
            try
            {
                var health = nest.AddComponent<Health>();
                health.Configure(60);
                var spawner = nest.AddComponent<SpawnOnFirstDamage>();
                spawner.Configure(greenPrefab, redPrefab, target.transform, null, null, null);

                health.ApplyDamage(1);
                health.ApplyDamage(1);

                Assert.That(spawner.HasSpawned, Is.True);
                Assert.That(spawner.SpawnedCount, Is.EqualTo(3));
            }
            finally
            {
                foreach (var spawned in Object.FindObjectsByType<MonsterChase>(FindObjectsSortMode.None))
                    Object.DestroyImmediate(spawned.gameObject);
                Object.DestroyImmediate(redPrefab);
                Object.DestroyImmediate(greenPrefab);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(nest);
            }
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
        public void QuestObjectiveCanBeConfiguredForAnotherMissionTarget()
        {
            var gameObject = new GameObject("Quest");
            try
            {
                var quest = gameObject.AddComponent<QuestProgress>();
                quest.Configure("monster.stingbug", 3);
                Assert.That(quest.RegisterDefeat("monster.carron"), Is.False);
                Assert.That(quest.RegisterDefeat("monster.stingbug"), Is.False);
                Assert.That(quest.RegisterDefeat("monster.stingbug"), Is.False);
                Assert.That(quest.RegisterDefeat("monster.stingbug"), Is.True);
                Assert.That(quest.IsComplete, Is.True);
            }
            finally { Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void InteractionObjectiveCompletesAndRewardsOnlyOnce()
        {
            var missionObject = new GameObject("Interaction Mission");
            var npcObject = new GameObject("MiniCat");
            try
            {
                var quest = missionObject.AddComponent<QuestProgress>();
                quest.Configure("npc.minicat", 1, "Talk to");
                var progress = missionObject.AddComponent<PlayerProgress>();
                var target = npcObject.AddComponent<QuestInteractionTarget>();
                target.Configure("npc.minicat", quest, progress, null, 20, 1);

                Assert.That(quest.ObjectiveText, Is.EqualTo("Talk to npc.minicat (0/1)"));
                Assert.That(target.Interact(), Is.True);
                Assert.That(target.Interact(), Is.False);
                Assert.That(progress.Experience, Is.EqualTo(20));
                Assert.That(progress.PotionCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(npcObject);
                Object.DestroyImmediate(missionObject);
            }
        }

        [Test]
        public void KnockoutObjectiveRestoresTargetAndRewardsAfterThreeKnockouts()
        {
            var missionObject = new GameObject("Knockout Mission");
            var targetObject = new GameObject("Boldas");
            try
            {
                var quest = missionObject.AddComponent<QuestProgress>();
                quest.Configure("npc.boldas", 3, "Knock out");
                var progress = missionObject.AddComponent<PlayerProgress>();
                var health = targetObject.AddComponent<Health>();
                health.Configure(20);
                var target = targetObject.AddComponent<KnockoutObjectiveTarget>();
                target.Configure("npc.boldas", quest, progress, null, 30, 2);

                for (var knockout = 1; knockout <= 3; knockout++)
                {
                    Assert.That(health.ApplyDamage(20), Is.EqualTo(20));
                    Assert.That(health.Current, Is.EqualTo(20));
                    Assert.That(quest.Defeats, Is.EqualTo(knockout));
                }

                Assert.That(quest.IsComplete, Is.True);
                Assert.That(progress.Experience, Is.EqualTo(30));
                Assert.That(progress.PotionCount, Is.EqualTo(2));
                health.ApplyDamage(20);
                Assert.That(progress.Experience, Is.EqualTo(30));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(missionObject);
            }
        }

        [Test]
        public void MeleeAttackCanHitKnockoutObjectiveTarget()
        {
            var player = new GameObject("Player");
            var targetObject = new GameObject("Boldas");
            try
            {
                var attack = player.AddComponent<MeleeAttack>();
                targetObject.transform.position = Vector3.forward;
                targetObject.AddComponent<CapsuleCollider>();
                var health = targetObject.AddComponent<Health>();
                health.Configure(30);
                var target = targetObject.AddComponent<KnockoutObjectiveTarget>();
                target.Configure("npc.boldas", null, null, null, 0, 0);

                Assert.That(attack.Attack(), Is.True);
                Assert.That(health.Current, Is.EqualTo(20));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void DefeatAndProtectMissionRewardsOnlyAfterRequiredDefeats()
        {
            var missionObject = new GameObject("Protect Mission");
            var protectedObject = new GameObject("Goat Farmer");
            try
            {
                var protectedHealth = protectedObject.AddComponent<Health>();
                protectedHealth.Configure(50);
                var quest = missionObject.AddComponent<QuestProgress>();
                quest.Configure("monster.stingbug", 2);
                var progress = missionObject.AddComponent<PlayerProgress>();
                var mission = missionObject.AddComponent<DefeatAndProtectMission>();
                mission.Configure(protectedHealth, "Goat Farmer", quest, progress, null, 40, 2);

                Assert.That(mission.RegisterDefeat("monster.stingbug"), Is.False);
                Assert.That(mission.RegisterDefeat("monster.other"), Is.False);
                Assert.That(mission.RegisterDefeat("monster.stingbug"), Is.True);
                Assert.That(mission.IsComplete, Is.True);
                Assert.That(progress.Experience, Is.EqualTo(40));
                Assert.That(progress.PotionCount, Is.EqualTo(2));
                Assert.That(mission.RegisterDefeat("monster.stingbug"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(protectedObject);
                Object.DestroyImmediate(missionObject);
            }
        }

        [Test]
        public void DefeatAndProtectMissionFailsWithoutRewardWhenTargetDies()
        {
            var missionObject = new GameObject("Protect Mission");
            var protectedObject = new GameObject("Goat Farmer");
            try
            {
                var protectedHealth = protectedObject.AddComponent<Health>();
                protectedHealth.Configure(10);
                var quest = missionObject.AddComponent<QuestProgress>();
                quest.Configure("monster.stingbug", 1);
                var progress = missionObject.AddComponent<PlayerProgress>();
                var mission = missionObject.AddComponent<DefeatAndProtectMission>();
                mission.Configure(protectedHealth, "Goat Farmer", quest, progress, null, 40, 2);

                protectedHealth.ApplyDamage(10);

                Assert.That(mission.IsFailed, Is.True);
                Assert.That(mission.RegisterDefeat("monster.stingbug"), Is.False);
                Assert.That(mission.IsComplete, Is.False);
                Assert.That(progress.RewardClaimed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(protectedObject);
                Object.DestroyImmediate(missionObject);
            }
        }

        [Test]
        public void MupoHerdRequiresSixUniqueLiveMupo()
        {
            var progress = new MupoHerdProgress(6);
            for (var index = 1; index <= 6; index++)
            {
                var id = $"mupo-{index}";
                Assert.That(progress.RegisterMupo(id), Is.True);
                Assert.That(progress.RegisterPenEntry(id), Is.EqualTo(index == 6));
            }

            Assert.That(progress.IsComplete, Is.True);
            Assert.That(progress.PennedCount, Is.EqualTo(6));
            Assert.That(progress.RegisterPenEntry("mupo-1"), Is.False);
        }

        [Test]
        public void MupoHerdIgnoresDuplicatesAndFailsWhenAMupoDies()
        {
            var progress = new MupoHerdProgress(6);
            Assert.That(progress.RegisterMupo("mupo-1"), Is.True);
            Assert.That(progress.RegisterMupo("mupo-1"), Is.False);
            Assert.That(progress.RegisterPenEntry("unknown"), Is.False);
            Assert.That(progress.RegisterDeath("mupo-1"), Is.True);
            Assert.That(progress.IsFailed, Is.True);
            Assert.That(progress.RegisterMupo("mupo-2"), Is.False);
            Assert.That(progress.RegisterDeath("mupo-1"), Is.False);
        }

        [Test]
        public void MupoHerdRejectsInvalidRequiredCount()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MupoHerdProgress(0));
        }

        [Test]
        public void LethalMupoDamageFailsTheMission()
        {
            var missionObject = new GameObject("Mupo Mission");
            var targetObject = new GameObject("Mupo Target");
            try
            {
                var progress = missionObject.AddComponent<PlayerProgress>();
                var mission = missionObject.AddComponent<MupoHerdMission>();
                mission.Configure(progress, null, 1);
                targetObject.AddComponent<Health>().Configure(10);
                var target = targetObject.AddComponent<MupoHerdTarget>();
                target.ConfigureId("mupo-1");
                Assert.That(mission.RegisterTarget(target), Is.True);

                target.Health.ApplyDamage(10);

                Assert.That(mission.IsFailed, Is.True);
                Assert.That(progress.RewardClaimed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(missionObject);
            }
        }

        [Test]
        public void SkillExecutorAppliesConfiguredDamageAndCooldown()
        {
            var playerObject = new GameObject("Skill User") { transform = { position = Vector3.zero } };
            var targetObject = new GameObject("Skill Target") { transform = { position = new Vector3(0f, 0f, 1f) } };
            try
            {
                var executor = playerObject.AddComponent<SkillExecutor>();
                executor.Configure(new[]
                {
                    new SkillDefinition { id = "skill.test", animationClip = "nAttack1", damage = 12, cooldownSeconds = 2f, range = 2f }
                });
                targetObject.AddComponent<SphereCollider>().radius = .5f;
                var health = targetObject.AddComponent<Health>();
                health.Configure(30);
                var quest = targetObject.AddComponent<QuestProgress>();
                var progress = targetObject.AddComponent<PlayerProgress>();
                var saves = targetObject.AddComponent<SaveCoordinator>();
                saves.Configure(progress, quest);
                targetObject.AddComponent<EnemyTarget>().Configure(quest, progress, saves);
                Physics.SyncTransforms();
                Assert.That(executor.ExecuteSkill("skill.test"), Is.True);
                Assert.That(health.Current, Is.EqualTo(18));
                Assert.That(executor.CanExecute("skill.test"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void SkillExecutorRejectsInsufficientResource()
        {
            var gameObject = new GameObject("Skill User");
            try
            {
                var executor = gameObject.AddComponent<SkillExecutor>();
                executor.Configure(new[]
                {
                    new SkillDefinition { id = "skill.expensive", animationClip = "cAttack1", damage = 30, resourceCost = 10, range = 2f }
                }, 5);
                Assert.That(executor.ExecuteSkill("skill.expensive"), Is.False);
                Assert.That(executor.Resource, Is.EqualTo(5));
            }
            finally { Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void SkillExecutorAppliesDamageOnlyAtConfiguredHitTime()
        {
            var playerObject = new GameObject("Skill User") { transform = { position = Vector3.zero } };
            var targetObject = new GameObject("Skill Target") { transform = { position = new Vector3(0f, 0f, 1f) } };
            try
            {
                var executor = playerObject.AddComponent<SkillExecutor>();
                executor.Configure(new[]
                {
                    new SkillDefinition { id = "skill.timed", damage = 12, cooldownSeconds = 1f, hitDelaySeconds = .3f, range = 2f }
                });
                targetObject.AddComponent<SphereCollider>().radius = .5f;
                var health = targetObject.AddComponent<Health>();
                health.Configure(30);
                var quest = targetObject.AddComponent<QuestProgress>();
                var progress = targetObject.AddComponent<PlayerProgress>();
                var saves = targetObject.AddComponent<SaveCoordinator>();
                saves.Configure(progress, quest);
                targetObject.AddComponent<EnemyTarget>().Configure(quest, progress, saves);
                Physics.SyncTransforms();

                Assert.That(executor.ExecuteSkill("skill.timed", 10f), Is.True);
                Assert.That(executor.IsWindingUp, Is.True);
                Assert.That(health.Current, Is.EqualTo(30));
                Assert.That(executor.AdvanceAction(10.29f), Is.False);
                Assert.That(health.Current, Is.EqualTo(30));
                Assert.That(executor.AdvanceAction(10.3f), Is.True);
                Assert.That(executor.IsWindingUp, Is.False);
                Assert.That(health.Current, Is.EqualTo(18));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void TimedSkillCanKnockOutMissionTarget()
        {
            var playerObject = new GameObject("Skill User") { transform = { position = Vector3.zero } };
            var targetObject = new GameObject("Knockout Target") { transform = { position = new Vector3(0f, 0f, 1f) } };
            try
            {
                var executor = playerObject.AddComponent<SkillExecutor>();
                executor.Configure(new[]
                {
                    new SkillDefinition { id = "skill.timed", damage = 10, hitDelaySeconds = .2f, range = 2f }
                });
                targetObject.AddComponent<SphereCollider>().radius = .5f;
                targetObject.AddComponent<Health>().Configure(10);
                var quest = targetObject.AddComponent<QuestProgress>();
                quest.Configure("npc.target", 1, "Knock out");
                var progress = targetObject.AddComponent<PlayerProgress>();
                var target = targetObject.AddComponent<KnockoutObjectiveTarget>();
                target.Configure("npc.target", quest, progress, null, 0, 0);
                Physics.SyncTransforms();

                Assert.That(executor.ExecuteSkill("skill.timed", 20f), Is.True);
                Assert.That(quest.IsComplete, Is.False);
                Assert.That(executor.AdvanceAction(20.2f), Is.True);
                Assert.That(quest.IsComplete, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void SkillActionLocksMovementThroughRecovery()
        {
            var playerObject = new GameObject("Skill User");
            try
            {
                playerObject.AddComponent<CharacterController>();
                var motor = playerObject.AddComponent<PlayerMotor>();
                var executor = playerObject.AddComponent<SkillExecutor>();
                executor.Configure(new[]
                {
                    new SkillDefinition { id = "skill.timed", damage = 10, hitDelaySeconds = .2f, actionDurationSeconds = .6f, range = 2f }
                });

                Assert.That(executor.ExecuteSkill("skill.timed", 10f), Is.True);
                Assert.That(motor.CanMove, Is.False);
                executor.AdvanceAction(10.2f);
                Assert.That(executor.IsWindingUp, Is.False);
                Assert.That(motor.CanMove, Is.False);
                executor.AdvanceAction(10.6f);
                Assert.That(motor.CanMove, Is.True);
            }
            finally { Object.DestroyImmediate(playerObject); }
        }

        [Test]
        public void SkillComboQueuesOnlyInsideConfiguredWindow()
        {
            var playerObject = new GameObject("Skill User");
            try
            {
                var executor = playerObject.AddComponent<SkillExecutor>();
                executor.Configure(new[]
                {
                    new SkillDefinition
                    {
                        id = "skill.first", damage = 10, hitDelaySeconds = .1f, actionDurationSeconds = .5f,
                        comboWindowStartSeconds = .25f, comboWindowEndSeconds = .45f, comboNextSkillId = "skill.second", range = 2f
                    },
                    new SkillDefinition { id = "skill.second", damage = 20, hitDelaySeconds = .2f, actionDurationSeconds = .6f, range = 2f }
                });

                Assert.That(executor.ExecuteSkill("skill.first", 20f), Is.True);
                Assert.That(executor.ExecuteSkill("skill.second", 20.2f), Is.False);
                Assert.That(executor.ExecuteSkill("skill.second", 20.3f), Is.True);
                Assert.That(executor.ActiveSkillId, Is.EqualTo("skill.first"));
                executor.AdvanceAction(20.5f);
                Assert.That(executor.ActiveSkillId, Is.EqualTo("skill.second"));
                Assert.That(executor.IsWindingUp, Is.True);
            }
            finally { Object.DestroyImmediate(playerObject); }
        }

        [Test]
        public void MeleeInputUsesBasicSkillImpactTiming()
        {
            var playerObject = new GameObject("Player") { transform = { position = Vector3.zero } };
            var targetObject = new GameObject("Target") { transform = { position = new Vector3(0f, 0f, 1f) } };
            try
            {
                var melee = playerObject.AddComponent<MeleeAttack>();
                var executor = playerObject.AddComponent<SkillExecutor>();
                executor.Configure(new[]
                {
                    new SkillDefinition { id = "skill.basic_slash", damage = 10, hitDelaySeconds = .12f, actionDurationSeconds = .5f, range = 2f }
                });
                targetObject.AddComponent<SphereCollider>().radius = .5f;
                var health = targetObject.AddComponent<Health>();
                health.Configure(20);
                var quest = targetObject.AddComponent<QuestProgress>();
                var progress = targetObject.AddComponent<PlayerProgress>();
                var saves = targetObject.AddComponent<SaveCoordinator>();
                saves.Configure(progress, quest);
                targetObject.AddComponent<EnemyTarget>().Configure(quest, progress, saves);
                Physics.SyncTransforms();

                Assert.That(melee.Attack(), Is.True);
                Assert.That(health.Current, Is.EqualTo(20));
                Assert.That(executor.AdvanceAction(float.MaxValue), Is.True);
                Assert.That(health.Current, Is.EqualTo(10));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(playerObject);
            }
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
                ProgressSave.Write(path, new ProgressSaveData
                {
                    experience = 25,
                    potionCount = 1,
                    questComplete = true,
                    rewardClaimed = true,
                    mupoPennedIds = new[] { "mupo-1", "mupo-2" },
                    mupoMissionFailed = false
                });
                var loaded = ProgressSave.Read(path);
                Assert.That(loaded.experience, Is.EqualTo(25));
                Assert.That(loaded.questComplete, Is.True);
                Assert.That(loaded.schemaVersion, Is.EqualTo(2));
                Assert.That(loaded.mupoPennedIds, Is.EqualTo(new[] { "mupo-1", "mupo-2" }));
                Assert.That(loaded.mupoMissionFailed, Is.False);
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

        [Test]
        public void EveryCharacterAnimatorContainsMappedSkillStates()
        {
            foreach (var id in CharacterRoster.Ids)
            {
                var name = char.ToUpperInvariant(id[0]) + id.Substring(1);
                var prefab = Resources.Load<GameObject>($"Characters/{name}");
                var instance = Object.Instantiate(prefab);
                try
                {
                    var animator = instance.GetComponent<Animator>();
                    animator.Update(0f);
                    var basicAttack = id == "rabbit" ? "nAttack" : "nAttack1";
                    foreach (var state in new[] { basicAttack, "nAttack2", "cAttack1" })
                        Assert.That(animator.HasState(0, AnimatorMotionDriver.StateHash(state)), Is.True, $"{id} is missing Animator state {state}");
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }

        [Test]
        public void PrivateOriginalCharactersContainMappedAttackClipsWhenAvailable()
        {
            var firstPrefab = Resources.Load<GameObject>("OriginalCharacters/Wolf");
            if (firstPrefab == null) Assert.Ignore("Private original character assets are not installed.");
            foreach (var id in CharacterRoster.Ids)
            {
                var name = char.ToUpperInvariant(id[0]) + id.Substring(1);
                var prefab = Resources.Load<GameObject>($"OriginalCharacters/{name}");
                var animation = prefab.GetComponentInChildren<Animation>(true);
                Assert.That(animation, Is.Not.Null, $"{id} is missing its original Animation component");
                var basicAttack = id == "rabbit" ? "nAttack" : "nAttack1";
                Assert.That(animation.GetClip(basicAttack), Is.Not.Null, $"{id} is missing original clip {basicAttack}");
                Assert.That(prefab.GetComponentInChildren<LegacyAnimationDriver>(true), Is.Not.Null, $"{id} is missing the clean runtime legacy-animation adapter");
                var instance = Object.Instantiate(prefab);
                try
                {
                    var driver = instance.GetComponentInChildren<LegacyAnimationDriver>(true);
                    Assert.That(driver.PlaySkillAnimation(basicAttack), Is.True, $"{id} could not play original clip {basicAttack}");
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }

    }
}
