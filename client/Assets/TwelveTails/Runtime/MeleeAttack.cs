using UnityEngine;
using UnityEngine.InputSystem;

namespace TwelveTails.Gameplay
{
    public sealed class MeleeAttack : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float range = 2.25f;
        [SerializeField, Min(1)] private int damage = 10;
        [SerializeField] private LayerMask targetMask = ~0;

        private void Update()
        {
            if (Keyboard.current?.spaceKey.wasPressedThisFrame == true) Attack();
        }

        public bool Attack()
        {
            GetComponentInChildren<CharacterAnimationDriver>()?.PlayAttack();
            var center = transform.position + transform.forward * (range * 0.6f);
            foreach (var collider in Physics.OverlapSphere(center, range, targetMask, QueryTriggerInteraction.Collide))
            {
                if (!collider.TryGetComponent<EnemyTarget>(out var enemy) || enemy.Health.IsDefeated) continue;
                enemy.TakeHit(damage);
                return true;
            }
            return false;
        }
    }
}
