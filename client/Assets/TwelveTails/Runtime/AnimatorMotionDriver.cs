using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class AnimatorMotionDriver : MonoBehaviour
    {
        private Animator animator = null!;
        private CharacterController controller = null!;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            controller = GetComponentInParent<CharacterController>();
        }

        private void Update()
        {
            if (animator != null)
                animator.SetFloat("Speed", controller == null ? 0f : controller.velocity.magnitude);
        }

        public void PlayAttack()
        {
            if (animator != null) animator.SetTrigger("Attack");
        }

        public void PlaySkillAnimation(string clipName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(clipName)) return;
            if (animator.HasState(0, Animator.StringToHash(clipName)))
                animator.CrossFadeInFixedTime(clipName, .05f);
            else animator.SetTrigger("Attack");
        }
    }
}
