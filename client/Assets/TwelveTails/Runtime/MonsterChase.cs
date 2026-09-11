using UnityEngine;

namespace TwelveTails.Gameplay
{
    [RequireComponent(typeof(Health))]
    public sealed class MonsterChase : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float detectionRadius = 12f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.5f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.8f;
        [SerializeField, Min(0f)] private float attackCooldown = 1.2f;
        [SerializeField, Min(0)] private int attackDamage = 5;
        private Transform target = null!;
        private CharacterController controller = null!;
        private Health health = null!;
        private float nextAttackAt;

        public void Configure(Transform chaseTarget, float speed = 1.8f, int damage = 5)
        {
            target = chaseTarget;
            moveSpeed = Mathf.Max(.1f, speed);
            attackDamage = Mathf.Max(0, damage);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            controller = GetComponent<CharacterController>();
            if (target == null) target = GameObject.FindWithTag("Player")?.transform;
        }

        private void Update()
        {
            if (health != null && health.IsDefeated) return;
            if (target == null) return;
            var offset = target.position - transform.position;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (distance > detectionRadius || distance < .01f) return;
            var direction = offset / distance;
            transform.forward = Vector3.Slerp(transform.forward, direction, Time.deltaTime * 8f);
            if (distance > attackRange)
            {
                var motion = direction * (moveSpeed * Time.deltaTime);
                if (controller != null) controller.Move(motion);
                else transform.position += motion;
                return;
            }
            if (Time.time < nextAttackAt) return;
            var playerHealth = target.GetComponent<Health>();
            if (playerHealth != null) playerHealth.ApplyDamage(attackDamage);
            nextAttackAt = Time.time + attackCooldown;
        }
    }
}
