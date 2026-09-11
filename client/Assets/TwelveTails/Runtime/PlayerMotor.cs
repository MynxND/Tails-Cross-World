using UnityEngine;
using UnityEngine.InputSystem;

namespace TwelveTails.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 6f;
        private CharacterController controller = null!;
        private SkillExecutor skillExecutor = null!;
        private StatusEffectController statusEffects = null!;

        public bool CanMove
        {
            get
            {
                if (skillExecutor == null) skillExecutor = GetComponent<SkillExecutor>();
                if (statusEffects == null) statusEffects = GetComponent<StatusEffectController>();
                return (skillExecutor == null || !skillExecutor.IsActionActive) && (statusEffects == null || !statusEffects.IsStunned);
            }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            skillExecutor = GetComponent<SkillExecutor>();
            statusEffects = GetComponent<StatusEffectController>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!CanMove)
            {
                controller.SimpleMove(Vector3.zero);
                return;
            }
            var x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            var z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            var direction = Vector3.ClampMagnitude(new Vector3(x, 0f, z), 1f);
            var movementMultiplier = statusEffects == null ? 1f : statusEffects.MovementMultiplier;
            controller.SimpleMove(direction * (speed * movementMultiplier));
            if (direction.sqrMagnitude > 0.01f)
                transform.forward = Vector3.Slerp(transform.forward, direction, 12f * Time.deltaTime);
        }
    }
}
