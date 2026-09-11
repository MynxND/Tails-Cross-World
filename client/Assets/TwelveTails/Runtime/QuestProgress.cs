using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class QuestProgress : MonoBehaviour
    {
        [SerializeField] private string targetId = "monster.training_dummy";
        [SerializeField] private string objectiveVerb = "Defeat";
        [SerializeField, Min(1)] private int requiredDefeats = 1;
        public int Defeats { get; private set; }
        public bool IsComplete => Defeats >= requiredDefeats;
        public string ObjectiveText => IsComplete
            ? "Objective complete!"
            : $"{objectiveVerb} {targetId} ({Defeats}/{requiredDefeats})";

        public void Configure(string objectiveTargetId, int count, string verb = "Defeat")
        {
            if (string.IsNullOrWhiteSpace(objectiveTargetId)) throw new System.ArgumentException("Objective target is required.", nameof(objectiveTargetId));
            if (count < 1) throw new System.ArgumentOutOfRangeException(nameof(count));
            if (string.IsNullOrWhiteSpace(verb)) throw new System.ArgumentException("Objective verb is required.", nameof(verb));
            targetId = objectiveTargetId;
            requiredDefeats = count;
            objectiveVerb = verb;
            Defeats = 0;
        }

        public bool RegisterProgress(string progressedTargetId)
        {
            if (IsComplete || progressedTargetId != targetId) return false;
            Defeats++;
            return IsComplete;
        }

        public bool RegisterDefeat(string defeatedId) => RegisterProgress(defeatedId);

        public void Restore(bool complete) => Defeats = complete ? requiredDefeats : 0;
    }
}
