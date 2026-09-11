using System;
using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximum = 100;
        public int Maximum => maximum;
        public int Current { get; private set; }
        public bool IsDefeated => Current == 0;
        public event Action<int> Damaged;
        public event Action Defeated;

        private void Awake() => Current = maximum;

        public void Configure(int maximumHealth)
        {
            maximum = Mathf.Max(1, maximumHealth);
            Current = maximum;
        }

        public int ApplyDamage(int amount)
        {
            if (amount <= 0 || IsDefeated) return 0;
            var applied = Mathf.Min(Current, amount);
            Current -= applied;
            Damaged?.Invoke(applied);
            if (Current == 0) Defeated?.Invoke();
            return applied;
        }

        public int ApplyHealing(int amount)
        {
            if (amount <= 0 || IsDefeated) return 0;
            var applied = Mathf.Min(maximum - Current, amount);
            Current += applied;
            return applied;
        }

        public void RestoreToFull() => Current = maximum;
    }
}
