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
            GetComponentInChildren<AnimatorMotionDriver>()?.PlayAttack();
            var center = transform.position + transform.forward * (range * 0.6f);
            foreach (var collider in Physics.OverlapSphere(center, range, targetMask, QueryTriggerInteraction.Collide))
            {
                if (collider.TryGetComponent<EnemyTarget>(out var enemy) && !enemy.Health.IsDefeated)
                {
                    enemy.TakeHit(damage);
                    return true;
                }
                if (collider.TryGetComponent<KnockoutObjectiveTarget>(out var knockoutTarget) && !knockoutTarget.Health.IsDefeated)
                {
                    knockoutTarget.TakeHit(damage);
                    return true;
                }
                var mupo = collider.GetComponentInParent<MupoHerdTarget>();
                if (mupo != null && !mupo.Health.IsDefeated)
                {
                    mupo.Health.ApplyDamage(damage);
                    return true;
                }
            }
            return false;
        }
    }
}
