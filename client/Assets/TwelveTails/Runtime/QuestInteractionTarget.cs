using System;
using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class QuestInteractionTarget : MonoBehaviour
    {
        [SerializeField] private string targetId = string.Empty;
        [SerializeField] private QuestProgress quest = null!;
        [SerializeField] private PlayerProgress progress = null!;
        [SerializeField] private SaveCoordinator saves = null!;
        [SerializeField, Min(0)] private int rewardExperience;
        [SerializeField, Min(0)] private int rewardPotions;

        public void Configure(
            string objectiveTargetId,
            QuestProgress questProgress,
            PlayerProgress playerProgress,
            SaveCoordinator saveCoordinator,
            int experienceReward,
            int potionReward)
        {
            if (string.IsNullOrWhiteSpace(objectiveTargetId)) throw new ArgumentException("Target ID is required.", nameof(objectiveTargetId));
            if (questProgress == null) throw new ArgumentNullException(nameof(questProgress));
            targetId = objectiveTargetId;
            quest = questProgress;
            progress = playerProgress;
            saves = saveCoordinator;
            rewardExperience = Mathf.Max(0, experienceReward);
            rewardPotions = Mathf.Max(0, potionReward);
        }

        public bool Interact()
        {
            if (quest == null || !quest.RegisterProgress(targetId)) return false;
            progress?.GrantQuestReward(rewardExperience, rewardPotions);
            saves?.Save();
            return true;
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player") && Input.GetKeyDown(KeyCode.E)) Interact();
        }
    }
}