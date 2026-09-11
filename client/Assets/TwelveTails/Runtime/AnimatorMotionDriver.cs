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
    }
}
