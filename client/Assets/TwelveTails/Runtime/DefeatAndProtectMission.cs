using System;
using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class DefeatAndProtectMission : MonoBehaviour
    {
        [SerializeField] private Health protectedHealth = null!;
        [SerializeField] private QuestProgress quest = null!;
        [SerializeField] private PlayerProgress progress = null!;
        [SerializeField] private SaveCoordinator saves = null!;
        [SerializeField] private string protectedName = "Protected target";
        [SerializeField, Min(0)] private int experienceReward;
        [SerializeField, Min(0)] private int potionReward;

        public bool IsComplete { get; private set; }
        public bool IsFailed { get; private set; }
        public string ObjectiveText => IsFailed
            ? $"Mission failed: {protectedName} was defeated."
            : IsComplete
                ? $"Objective complete: {protectedName} is safe!"
                : $"Protect {protectedName}. {quest.ObjectiveText}";

        public void Configure(
            Health targetHealth,
            string targetName,
            QuestProgress questProgress,
            PlayerProgress playerProgress,
            SaveCoordinator saveCoordinator,
            int rewardExperience,
            int rewardPotions)
        {
            if (targetHealth == null) throw new ArgumentNullException(nameof(targetHealth));
            if (string.IsNullOrWhiteSpace(targetName)) throw new ArgumentException("Protected target name is required.", nameof(targetName));
            if (questProgress == null) throw new ArgumentNullException(nameof(questProgress));

            Unsubscribe();
            protectedHealth = targetHealth;
            protectedName = targetName;
            quest = questProgress;
            progress = playerProgress;
            saves = saveCoordinator;
            experienceReward = Mathf.Max(0, rewardExperience);
            potionReward = Mathf.Max(0, rewardPotions);
            IsComplete = false;
            IsFailed = protectedHealth.IsDefeated;
            protectedHealth.Defeated += OnProtectedTargetDefeated;
        }

        public bool RegisterDefeat(string defeatedId)
        {
            if (IsFailed || IsComplete || !quest.RegisterDefeat(defeatedId)) return false;
            IsComplete = true;
            progress?.GrantQuestReward(experienceReward, potionReward);
            saves?.Save();
            return true;
        }

        private void OnProtectedTargetDefeated()
        {
            if (!IsComplete) IsFailed = true;
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (protectedHealth != null) protectedHealth.Defeated -= OnProtectedTargetDefeated;
        }
    }
}
