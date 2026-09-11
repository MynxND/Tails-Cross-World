using System.Collections.Generic;

namespace TwelveTails.Gameplay
{
    public sealed class MupoHerdProgress
    {
        private readonly HashSet<string> pennedMupo = new();
        private readonly HashSet<string> knownMupo = new();

        public MupoHerdProgress(int requiredCount)
        {
            RequiredCount = requiredCount > 0 ? requiredCount : throw new System.ArgumentOutOfRangeException(nameof(requiredCount));
        }

        public int RequiredCount { get; }
        public int PennedCount => pennedMupo.Count;
        public IReadOnlyCollection<string> PennedIds => pennedMupo;
        public bool IsComplete => !IsFailed && PennedCount == RequiredCount;
        public bool IsFailed { get; private set; }

        public bool RegisterMupo(string mupoId)
        {
            if (string.IsNullOrWhiteSpace(mupoId) || IsFailed || IsComplete) return false;
            return knownMupo.Add(mupoId);
        }

        public bool RegisterPenEntry(string mupoId)
        {
            if (string.IsNullOrWhiteSpace(mupoId) || IsFailed || IsComplete || !knownMupo.Contains(mupoId)) return false;
            pennedMupo.Add(mupoId);
            return IsComplete;
        }

        public bool RegisterDeath(string mupoId)
        {
            if (string.IsNullOrWhiteSpace(mupoId) || IsFailed || !knownMupo.Contains(mupoId)) return false;
            IsFailed = true;
            return true;
        }

        public void Restore(IEnumerable<string> pennedIds, bool failed)
        {
            pennedMupo.Clear();
            if (pennedIds != null)
            {
                foreach (var mupoId in pennedIds)
                {
                    if (knownMupo.Contains(mupoId)) pennedMupo.Add(mupoId);
                }
            }
            IsFailed = failed;
        }
    }
}