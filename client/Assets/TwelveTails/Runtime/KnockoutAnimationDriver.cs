using UnityEngine;

namespace TwelveTails.Gameplay
{
    [RequireComponent(typeof(Health))]
    public sealed class KnockoutAnimationDriver : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float recoveryDelaySeconds;
        private Health health = null!;
        private LegacyAnimationDriver legacyAnimation = null!;
        private float recoveryRemaining;
        private bool recoveryPending;

        private void Awake()
        {
            AttachHealth();
        }

        private void OnEnable() => AttachHealth();

        private void Update() => AdvanceRecovery(Time.deltaTime);

        public void Configure(float recoveryDelay)
        {
            recoveryDelaySeconds = Mathf.Max(0f, recoveryDelay);
            AttachHealth();
        }

        public void AdvanceRecovery(float deltaTime)
        {
            if (!recoveryPending || deltaTime < 0f) return;
            recoveryRemaining -= deltaTime;
            if (recoveryRemaining > 0f) return;
            recoveryPending = false;
            Play("getUp");
        }

        private void OnDefeated()
        {
            Play("ko");
            recoveryRemaining = recoveryDelaySeconds > 0f
                ? recoveryDelaySeconds
                : legacyAnimation?.ClipDuration("ko") ?? 0f;
            recoveryPending = true;
            if (recoveryRemaining == 0f) AdvanceRecovery(0f);
        }

        private void Play(string clipName)
        {
            if (legacyAnimation == null) legacyAnimation = GetComponentInChildren<LegacyAnimationDriver>(true);
            legacyAnimation?.PlayAnimation(clipName);
        }

        private void AttachHealth()
        {
            health = GetComponent<Health>();
            health.Defeated -= OnDefeated;
            health.Defeated += OnDefeated;
        }

        private void OnDestroy()
        {
            if (health != null) health.Defeated -= OnDefeated;
        }
    }
}