using UnityEngine;

namespace TwelveTails.Gameplay
{
    [RequireComponent(typeof(Health))]
    public sealed class KnockoutObjectiveTarget : MonoBehaviour
    {
        [SerializeField] private string targetId = string.Empty;
        private Health health;
        private QuestProgress quest;
        private PlayerProgress progress;
        private SaveCoordinator saves;
        private int experienceReward;
        private int potionReward;
        private bool rewardGranted;
        public Health Health => health != null ? health : health = GetComponent<Health>();

        public void TakeHit(int amount) => Health.ApplyDamage(amount);

        public void Configure(
            string objectiveTargetId,
            QuestProgress questProgress,
            PlayerProgress playerProgress,
            SaveCoordinator saveCoordinator,
            int experience,
            int potions)
        {
            targetId = objectiveTargetId;
            quest = questProgress;
            progress = playerProgress;
            saves = saveCoordinator;
            experienceReward = experience;
            potionReward = potions;
            AttachHealth();
        }

        private void OnEnable() => AttachHealth();

        private void AttachHealth()
        {
            health = Health;
            health.Defeated -= HandleKnockout;
            health.Defeated += HandleKnockout;
        }

        private void OnDisable()
        {
            if (health != null) health.Defeated -= HandleKnockout;
        }

        private void HandleKnockout()
        {
            var completed = quest != null && quest.RegisterProgress(targetId);
            health.RestoreToFull();
            if (!completed || rewardGranted) return;
            rewardGranted = true;
            progress?.GrantQuestReward(experienceReward, potionReward);
            saves?.Save();
        }
    }
}