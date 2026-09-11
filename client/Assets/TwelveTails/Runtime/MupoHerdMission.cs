using System.Collections.Generic;
using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class MupoHerdMission : MonoBehaviour
    {
        [SerializeField, Min(1)] private int requiredMupo = 6;
        [SerializeField] private int rewardExperience = 50;
        [SerializeField] private int rewardPotions = 1;
        [SerializeField] private PlayerProgress progress = null!;
        [SerializeField] private SaveCoordinator saves = null!;
        private readonly Dictionary<string, MupoHerdTarget> targets = new();
        private MupoHerdProgress herd = null!;

        public int PennedCount => herd?.PennedCount ?? 0;
        public int RequiredCount => herd?.RequiredCount ?? requiredMupo;
        public bool IsComplete => herd != null && herd.IsComplete;
        public bool IsFailed => herd != null && herd.IsFailed;
        public IReadOnlyCollection<string> PennedIds => herd?.PennedIds ?? System.Array.Empty<string>();
        public string ObjectiveText => IsComplete
            ? "Mupo Round Up complete!"
            : IsFailed ? "Mupo Round Up failed - a Mupo was lost."
            : $"Herd Mupo into the pen ({PennedCount}/{RequiredCount})";

        public void Configure(PlayerProgress playerProgress, SaveCoordinator saveCoordinator, int count = 6)
        {
            progress = playerProgress;
            saves = saveCoordinator;
            requiredMupo = count;
            herd = new MupoHerdProgress(requiredMupo);
        }

        private void Awake()
        {
            if (progress == null) progress = GetComponent<PlayerProgress>();
            if (saves == null) saves = GetComponent<SaveCoordinator>();
            herd = new MupoHerdProgress(requiredMupo);
            foreach (var target in FindObjectsByType<MupoHerdTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                RegisterTarget(target);
        }

        public bool RegisterTarget(MupoHerdTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.MupoId) || herd == null) return false;
            if (!herd.RegisterMupo(target.MupoId)) return false;
            targets[target.MupoId] = target;
            target.Configure(this);
            return true;
        }

        public bool RegisterPenEntry(string mupoId)
        {
            if (herd == null || !herd.RegisterPenEntry(mupoId)) return false;
            if (IsComplete)
            {
                progress?.GrantQuestReward(rewardExperience, rewardPotions);
                saves?.Save();
            }
            return IsComplete;
        }

        public bool RegisterPenEntryAccepted(string mupoId)
        {
            var before = PennedCount;
            RegisterPenEntry(mupoId);
            return PennedCount > before;
        }

        public bool RegisterDeath(string mupoId)
        {
            return herd != null && herd.RegisterDeath(mupoId);
        }

        public void Restore(string[] pennedIds, bool failed)
        {
            herd?.Restore(pennedIds, failed);
        }
    }
}