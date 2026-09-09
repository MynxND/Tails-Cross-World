using UnityEngine;

namespace TwelveTails.Gameplay
{
    [RequireComponent(typeof(Health))]
    public sealed class EnemyTarget : MonoBehaviour
    {
        [SerializeField] private string entityId = "monster.training_dummy";
        [SerializeField] private QuestProgress quest = null!;
        [SerializeField] private PlayerProgress progress = null!;
        [SerializeField] private SaveCoordinator saves = null!;
        public Health Health { get; private set; } = null!;

        private void Awake()
        {
            Health = GetComponent<Health>();
            Health.Defeated += OnDefeated;
        }

        public void Configure(QuestProgress questProgress, PlayerProgress playerProgress, SaveCoordinator saveCoordinator)
        {
            quest = questProgress;
            progress = playerProgress;
            saves = saveCoordinator;
        }
        public void TakeHit(int amount) => Health.ApplyDamage(amount);

        private void OnDefeated()
        {
            if (quest != null && quest.RegisterDefeat(entityId))
            {
                progress?.GrantQuestReward(25, 1);
                saves?.Save();
            }
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Defeated -= OnDefeated;
        }
    }
}
