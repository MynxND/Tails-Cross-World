using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TwelveTails.Gameplay
{
    [RequireComponent(typeof(Health))]
    public sealed class StatusEffectController : MonoBehaviour
    {
        private sealed class ActiveStatus
        {
            public float remaining;
            public float tickInterval;
            public float untilNextTick;
            public int damagePerTick;
            public float movementMultiplier;
        }

        private readonly Dictionary<string, ActiveStatus> active = new();
        private Health health = null!;

        public int ActiveCount => active.Count;
        public bool IsStunned => MovementMultiplier <= 0f;
        public float MovementMultiplier => active.Count == 0 ? 1f : active.Values.Min(status => status.movementMultiplier);

        private void Awake() => AttachHealth();
        private void OnEnable() => AttachHealth();
        private void Update() => Advance(Time.deltaTime);

        public bool ApplyStatus(string effectId, float durationSeconds, float tickIntervalSeconds, int damagePerTick, float movementMultiplier)
        {
            if (string.IsNullOrWhiteSpace(effectId) || durationSeconds <= 0f || tickIntervalSeconds < 0f || damagePerTick < 0 || movementMultiplier < 0f)
                return false;
            AttachHealth();
            active[effectId] = new ActiveStatus
            {
                remaining = durationSeconds,
                tickInterval = tickIntervalSeconds,
                untilNextTick = tickIntervalSeconds,
                damagePerTick = damagePerTick,
                movementMultiplier = movementMultiplier
            };
            return true;
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || active.Count == 0) return;
            foreach (var pair in active.ToArray())
            {
                var status = pair.Value;
                var activeDelta = Mathf.Min(deltaSeconds, status.remaining);
                status.remaining -= deltaSeconds;
                if (status.tickInterval > 0f && status.damagePerTick > 0)
                {
                    status.untilNextTick -= activeDelta;
                    while (status.untilNextTick <= 0f && !health.IsDefeated)
                    {
                        health.ApplyDamage(status.damagePerTick);
                        status.untilNextTick += status.tickInterval;
                    }
                }
                if (status.remaining <= 0f) active.Remove(pair.Key);
            }
        }

        public void Clear() => active.Clear();

        private void AttachHealth()
        {
            health = GetComponent<Health>();
            health.Defeated -= Clear;
            health.Defeated += Clear;
        }

        private void OnDestroy()
        {
            if (health != null) health.Defeated -= Clear;
        }
    }
}