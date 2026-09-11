using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class SkillProjectile : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private float remainingLifetime;
        private int damage;
        private float homingRadiansPerSecond;
        private Transform target = null!;
        private LayerMask targetMask;
        private SkillDefinition definition = null!;
        public bool HasImpacted { get; private set; }

        public void Configure(Vector3 travelDirection, SkillDefinition skill, LayerMask mask, Transform homingTarget = null)
        {
            direction = travelDirection.sqrMagnitude > 0f ? travelDirection.normalized : Vector3.forward;
            definition = skill;
            speed = Mathf.Max(0f, skill.projectileSpeed);
            remainingLifetime = Mathf.Max(.01f, skill.projectileLifetimeSeconds);
            damage = Mathf.Max(0, skill.damage);
            homingRadiansPerSecond = Mathf.Max(0f, skill.projectileHomingRadiansPerSecond);
            target = homingTarget;
            targetMask = mask;
        }

        private void Update() => Advance(Time.deltaTime);

        public void Advance(float deltaSeconds)
        {
            if (HasImpacted || deltaSeconds <= 0f) return;
            if (target != null && homingRadiansPerSecond > 0f)
            {
                var targetDirection = target.position - transform.position;
                if (targetDirection.sqrMagnitude > 0f)
                    direction = Vector3.RotateTowards(direction, targetDirection.normalized, homingRadiansPerSecond * deltaSeconds, 0f).normalized;
            }
            transform.position += direction * (speed * deltaSeconds);
            remainingLifetime -= deltaSeconds;
            if (remainingLifetime <= 0f) Despawn();
        }

        public bool TryImpact(Collider collider)
        {
            if (HasImpacted || collider == null || (targetMask.value & (1 << collider.gameObject.layer)) == 0) return false;
            var health = ResolveTargetHealth(collider);
            if (health == null || health.IsDefeated) return false;
            health.ApplyDamage(damage);
            ApplyStatus(health);
            HasImpacted = true;
            Despawn();
            return true;
        }

        private void OnTriggerEnter(Collider other) => TryImpact(other);

        private Health ResolveTargetHealth(Collider collider)
        {
            var enemy = collider.GetComponentInParent<EnemyTarget>();
            if (enemy != null) return enemy.Health;
            var knockout = collider.GetComponentInParent<KnockoutObjectiveTarget>();
            if (knockout != null) return knockout.Health;
            var mupo = collider.GetComponentInParent<MupoHerdTarget>();
            return mupo == null ? null : mupo.Health;
        }

        private void ApplyStatus(Health health)
        {
            if (string.IsNullOrWhiteSpace(definition.statusEffectId) || definition.statusDurationSeconds <= 0f) return;
            var effects = health.GetComponent<StatusEffectController>();
            if (effects == null) effects = health.gameObject.AddComponent<StatusEffectController>();
            effects.ApplyStatus(definition.statusEffectId, definition.statusDurationSeconds, definition.statusTickSeconds,
                definition.statusDamagePerTick, definition.statusMovementMultiplier);
        }

        private void Despawn()
        {
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}