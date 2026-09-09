using UnityEngine;
using UnityEngine.InputSystem;

namespace TwelveTails.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 6f;
        private CharacterController controller = null!;

        private void Awake() => controller = GetComponent<CharacterController>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            var x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            var z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            var direction = Vector3.ClampMagnitude(new Vector3(x, 0f, z), 1f);
            controller.SimpleMove(direction * speed);
            if (direction.sqrMagnitude > 0.01f)
                transform.forward = Vector3.Slerp(transform.forward, direction, 12f * Time.deltaTime);
        }
    }
}
