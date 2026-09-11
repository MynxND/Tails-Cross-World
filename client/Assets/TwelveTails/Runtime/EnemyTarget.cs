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
        [SerializeField] private DefeatAndProtectMission protectMission = null!;
        private Health health = null!;
        public Health Health
        {
            get
            {
                if (health == null) health = GetComponent<Health>();
                return health;
            }
        }

        private void Awake()
        {
            Health.Defeated += OnDefeated;
        }

        public void Configure(QuestProgress questProgress, PlayerProgress playerProgress, SaveCoordinator saveCoordinator)
        {
            quest = questProgress;
            progress = playerProgress;
            saves = saveCoordinator;
        }

        public void ConfigureEntity(string id, QuestProgress questProgress, PlayerProgress playerProgress, SaveCoordinator saveCoordinator, int experienceReward = 25, int potionReward = 1)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new System.ArgumentException("Entity ID is required.", nameof(id));
            entityId = id;
            quest = questProgress;
            progress = playerProgress;
            saves = saveCoordinator;
            rewardExperience = Mathf.Max(0, experienceReward);
            rewardPotions = Mathf.Max(0, potionReward);
        }

        public void ConfigureForMission(string id, DefeatAndProtectMission mission)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new System.ArgumentException("Entity ID is required.", nameof(id));
            if (mission == null) throw new System.ArgumentNullException(nameof(mission));
            entityId = id;
            protectMission = mission;
        }

        [SerializeField, Min(0)] private int rewardExperience = 25;
        [SerializeField, Min(0)] private int rewardPotions = 1;
        public void TakeHit(int amount) => Health.ApplyDamage(amount);

        private void OnDefeated()
        {
            if (protectMission != null)
            {
                protectMission.RegisterDefeat(entityId);
            }
            else if (quest != null && quest.RegisterDefeat(entityId))
            {
                progress?.GrantQuestReward(rewardExperience, rewardPotions);
                saves?.Save();
            }
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (health != null) health.Defeated -= OnDefeated;
        }
    }
}
