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

        public bool CanMove
        {
            get
            {
                if (skillExecutor == null) skillExecutor = GetComponent<SkillExecutor>();
                return skillExecutor == null || !skillExecutor.IsActionActive;
            }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            skillExecutor = GetComponent<SkillExecutor>();
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
            controller.SimpleMove(direction * speed);
            if (direction.sqrMagnitude > 0.01f)
                transform.forward = Vector3.Slerp(transform.forward, direction, 12f * Time.deltaTime);
        }
    }
}
