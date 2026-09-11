using UnityEngine;

namespace TwelveTails.Gameplay
{
    [RequireComponent(typeof(Health))]
    public sealed class DefeatAnimationDriver : MonoBehaviour
    {
        [SerializeField] private string defeatClipName = "ko";
        [SerializeField, Min(0f)] private float deactivateDelayOverride;
        private LegacyAnimationDriver legacyAnimation = null!;
        private Health observedHealth = null!;
        private bool deactivateAfterPresentation = true;
        private float remaining;
        public bool IsPresenting { get; private set; }

        private void Update() => Advance(Time.deltaTime);

        public void Configure(float deactivateDelay = 0f)
        {
            deactivateDelayOverride = Mathf.Max(0f, deactivateDelay);
        }

        public void ObserveHealth(bool deactivateAfter)
        {
            observedHealth = GetComponent<Health>();
            observedHealth.Defeated -= OnObservedDefeat;
            observedHealth.Defeated += OnObservedDefeat;
            deactivateAfterPresentation = deactivateAfter;
        }

        public bool PlayAndDeactivate()
        {
            SuspendCombat();
            if (legacyAnimation == null) legacyAnimation = GetComponentInChildren<LegacyAnimationDriver>(true);
            if (legacyAnimation == null)
            {
                var animation = GetComponentInChildren<Animation>(true);
                if (animation != null) legacyAnimation = animation.gameObject.AddComponent<LegacyAnimationDriver>();
            }
            if (legacyAnimation == null || !legacyAnimation.PlayAnimation(defeatClipName)) return false;
            remaining = deactivateDelayOverride > 0f ? deactivateDelayOverride : legacyAnimation.ClipDuration(defeatClipName);
            IsPresenting = remaining > 0f;
            if (!IsPresenting && deactivateAfterPresentation) gameObject.SetActive(false);
            return true;
        }

        public void Advance(float deltaSeconds)
        {
            if (!IsPresenting || deltaSeconds < 0f) return;
            remaining -= deltaSeconds;
            if (remaining > 0f) return;
            IsPresenting = false;
            if (deactivateAfterPresentation) gameObject.SetActive(false);
        }

        private void OnObservedDefeat() => PlayAndDeactivate();

        private void SuspendCombat()
        {
            var playerMotor = GetComponent<PlayerMotor>();
            if (playerMotor != null) playerMotor.enabled = false;
            var chase = GetComponent<MonsterChase>();
            if (chase != null) chase.enabled = false;
            var controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;
        }

        private void OnDestroy()
        {
            if (observedHealth != null) observedHealth.Defeated -= OnObservedDefeat;
        }
    }
}