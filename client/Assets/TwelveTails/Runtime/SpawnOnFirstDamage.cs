using System;
using UnityEngine;

namespace TwelveTails.Gameplay
{
    [RequireComponent(typeof(Health))]
    public sealed class SpawnOnFirstDamage : MonoBehaviour
    {
        [SerializeField] private GameObject greenPrefab = null!;
        [SerializeField] private GameObject redPrefab = null!;
        [SerializeField] private Transform attackTarget = null!;
        [SerializeField] private QuestProgress quest = null!;
        [SerializeField] private PlayerProgress progress = null!;
        [SerializeField] private SaveCoordinator saves = null!;
        private Health health = null!;
        public bool HasSpawned { get; private set; }
        public int SpawnedCount { get; private set; }

        public void Configure(
            GameObject greenActorPrefab,
            GameObject redActorPrefab,
            Transform target,
            QuestProgress questProgress,
            PlayerProgress playerProgress,
            SaveCoordinator saveCoordinator)
        {
            if (greenActorPrefab == null) throw new ArgumentNullException(nameof(greenActorPrefab));
            if (redActorPrefab == null) throw new ArgumentNullException(nameof(redActorPrefab));
            if (target == null) throw new ArgumentNullException(nameof(target));
            greenPrefab = greenActorPrefab;
            redPrefab = redActorPrefab;
            attackTarget = target;
            quest = questProgress;
            progress = playerProgress;
            saves = saveCoordinator;
            health = GetComponent<Health>();
            health.Damaged -= OnDamaged;
            health.Damaged += OnDamaged;
        }

        private void OnDamaged(int amount)
        {
            if (amount <= 0 || HasSpawned) return;
            HasSpawned = true;
            SpawnActor(greenPrefab, "monster.stingbug.green", new Vector3(0f, .1f, 5f), 24, 1.5f, 5);
            SpawnActor(greenPrefab, "monster.stingbug.green", new Vector3(2f, .1f, -3.5f), 24, 1.5f, 5);
            SpawnActor(redPrefab, "monster.stingbug.red", new Vector3(-2f, .1f, -3.5f), 30, 1.7f, 7);
        }

        private void SpawnActor(GameObject prefab, string entityId, Vector3 localOffset, int maximumHealth, float speed, int damage)
        {
            var root = new GameObject($"Spawned {entityId}");
            root.transform.position = transform.TransformPoint(localOffset);
            root.transform.rotation = transform.rotation;
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.2f;
            controller.radius = .55f;
            root.AddComponent<Health>().Configure(maximumHealth);
            root.AddComponent<EnemyTarget>().ConfigureEntity(entityId, quest, progress, saves, 0, 0);
            root.AddComponent<MonsterChase>().Configure(attackTarget, speed, damage);
            var visual = Instantiate(prefab, root.transform);
            visual.name = $"Original {entityId} Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            SpawnedCount++;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }
    }
}