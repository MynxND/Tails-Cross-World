using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class QuestProgress : MonoBehaviour
    {
        [SerializeField] private string targetId = "monster.training_dummy";
        [SerializeField, Min(1)] private int requiredDefeats = 1;
        public int Defeats { get; private set; }
        public bool IsComplete => Defeats >= requiredDefeats;
        public string ObjectiveText => IsComplete
            ? "Quest complete - Carron defeated!"
            : $"Defeat Carron ({Defeats}/{requiredDefeats})";

        public bool RegisterDefeat(string defeatedId)
        {
            if (IsComplete || defeatedId != targetId) return false;
            Defeats++;
            return IsComplete;
        }

        public void Restore(bool complete) => Defeats = complete ? requiredDefeats : 0;
    }
}
