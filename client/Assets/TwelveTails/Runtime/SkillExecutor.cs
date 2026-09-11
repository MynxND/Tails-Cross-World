using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwelveTails.Gameplay
{
    [Serializable]
    public sealed class SkillDefinition
    {
        public string id = string.Empty;
        public string characterId = string.Empty;
        public string animationClip = string.Empty;
        public int damage;
        public float cooldownSeconds;
        public float hitDelaySeconds;
        public int hitCount = 1;
        public float hitIntervalSeconds;
        public float actionDurationSeconds;
        public float comboWindowStartSeconds;
        public float comboWindowEndSeconds;
        public string comboNextSkillId = string.Empty;
        public int resourceCost;
        public float range;
        public float projectileSpeed;
        public float projectileLifetimeSeconds;
        public float projectileHomingRadiansPerSecond;
        public float impactRadius;
        public string statusEffectId = string.Empty;
        public float statusDurationSeconds;
        public float statusTickSeconds;
        public int statusDamagePerTick;
        public float statusMovementMultiplier = 1f;
    }

    [Serializable]
    public sealed class SkillAnimationBinding
    {
        public string characterId = string.Empty;
        public string skillId = string.Empty;
        public string clipName = string.Empty;
    }

    public sealed class SkillExecutor : MonoBehaviour
    {
        [SerializeField] private SkillDefinition[] skills = Array.Empty<SkillDefinition>();
        [SerializeField] private SkillAnimationBinding[] animationBindings = Array.Empty<SkillAnimationBinding>();
        [SerializeField, Min(0)] private int resource = 100;
        [SerializeField, Min(0)] private int maximumResource = 100;
        [SerializeField, Min(0f)] private float resourceRegenerationPerSecond = 5f;
        [SerializeField] private LayerMask targetMask = ~0;
        private readonly Dictionary<string, float> cooldowns = new();
        private CharacterAnimationDriver proceduralAnimation = null!;
        private AnimatorMotionDriver animatorMotion = null!;
        private LegacyAnimationDriver legacyAnimation = null!;
        private SkillDefinition activeSkill = null!;
        private SkillDefinition queuedSkill = null!;
        private float actionStartedAt;
        private float pendingHitAt;
        private float actionEndsAt;
        private float resourceRegenerationRemainder;
        private bool impactApplied;
        private int pendingHitCount;

        public int Resource => resource;
        public IReadOnlyList<SkillDefinition> Skills => skills;
        public bool IsWindingUp => activeSkill != null && !impactApplied;
        public bool IsActionActive => activeSkill != null;
        public string ActiveSkillId => activeSkill?.id ?? string.Empty;
        public event Action<SkillDefinition> SkillStarted;
        public event Action<SkillDefinition> SkillReleased;
        public event Action<SkillDefinition> SkillEnded;

        public void Configure(IEnumerable<SkillDefinition> definitions, int initialResource = 100, float regenerationPerSecond = 5f)
        {
            skills = definitions == null ? Array.Empty<SkillDefinition>() : new List<SkillDefinition>(definitions).ToArray();
            maximumResource = Mathf.Max(0, initialResource);
            resource = maximumResource;
            resourceRegenerationPerSecond = Mathf.Max(0f, regenerationPerSecond);
            resourceRegenerationRemainder = 0f;
            cooldowns.Clear();
            ClearAction();
        }

        public void ConfigureAnimations(IEnumerable<SkillAnimationBinding> bindings)
        {
            animationBindings = bindings == null ? Array.Empty<SkillAnimationBinding>() : new List<SkillAnimationBinding>(bindings).ToArray();
        }

        private void Awake()
        {
            proceduralAnimation = GetComponentInChildren<CharacterAnimationDriver>();
            animatorMotion = GetComponentInChildren<AnimatorMotionDriver>();
            legacyAnimation = GetComponentInChildren<LegacyAnimationDriver>();
        }

        private void Update()
        {
            AdvanceResource(Time.deltaTime);
            AdvanceAction(Time.time);
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) ExecuteSkill("skill.basic_slash");
            if (keyboard.digit2Key.wasPressedThisFrame) ExecuteSkill("skill.power_strike");
            if (keyboard.digit3Key.wasPressedThisFrame) ExecuteSkill("skill.class_special");
            if (keyboard.digit4Key.wasPressedThisFrame) ExecuteSkill("skill.mole_stun_grenade");
            if (keyboard.digit5Key.wasPressedThisFrame) ExecuteSkill("skill.blade_fang");
        }

        public int AdvanceResource(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || resource >= maximumResource || resourceRegenerationPerSecond <= 0f) return 0;
            resourceRegenerationRemainder += deltaSeconds * resourceRegenerationPerSecond;
            var restored = Mathf.Min(maximumResource - resource, Mathf.FloorToInt(resourceRegenerationRemainder));
            if (restored <= 0) return 0;
            resource += restored;
            resourceRegenerationRemainder -= restored;
            if (resource == maximumResource) resourceRegenerationRemainder = 0f;
            return restored;
        }

        public void ApplyAuthoritativeResource(float value)
        {
            resource = Mathf.Clamp(Mathf.FloorToInt(value), 0, maximumResource);
            resourceRegenerationRemainder = 0f;
        }

        public bool ExecuteSkill(string skillId) => ExecuteSkill(skillId, Time.time);

        public bool ExecuteSkill(string skillId, float actionTime)
        {
            var definition = FindSkill(ResolveSkillId(skillId));
            if (definition == null || definition.damage < 0 || definition.range <= 0f) return false;
            if (activeSkill != null) return QueueCombo(definition, actionTime);
            return StartAction(definition, actionTime);
        }

        private bool StartAction(SkillDefinition definition, float actionTime)
        {
            if (resource < definition.resourceCost) return false;
            if (cooldowns.TryGetValue(definition.id, out var readyAt) && actionTime < readyAt) return false;

            var clipName = ResolveAnimationClip(definition);
            if (legacyAnimation == null) legacyAnimation = GetComponentInChildren<LegacyAnimationDriver>();
            legacyAnimation?.PlaySkillAnimation(clipName);
            proceduralAnimation?.PlaySkillAnimation(clipName);
            animatorMotion?.PlaySkillAnimation(clipName);
            resource -= definition.resourceCost;
            cooldowns[definition.id] = actionTime + Mathf.Max(0f, definition.cooldownSeconds);
            SkillStarted?.Invoke(definition);
            var actionDuration = Mathf.Max(definition.actionDurationSeconds, definition.hitDelaySeconds);
            if (actionDuration <= 0f)
            {
                var changed = ApplyDamage(definition);
                SkillEnded?.Invoke(definition);
                return changed;
            }
            activeSkill = definition;
            actionStartedAt = actionTime;
            pendingHitAt = actionTime + definition.hitDelaySeconds;
            pendingHitCount = Mathf.Max(1, definition.hitCount);
            actionEndsAt = actionTime + actionDuration;
            impactApplied = definition.hitDelaySeconds <= 0f;
            if (impactApplied)
            {
                ApplyDamage(definition);
                pendingHitCount--;
                pendingHitAt += Mathf.Max(0f, definition.hitIntervalSeconds);
            }
            return true;
        }

        public bool AdvanceAction(float actionTime)
        {
            if (activeSkill == null) return false;
            var changed = false;
            while (pendingHitCount > 0 && actionTime >= pendingHitAt)
            {
                impactApplied = true;
                changed = ApplyDamage(activeSkill) || changed;
                pendingHitCount--;
                pendingHitAt += Mathf.Max(0.0001f, activeSkill.hitIntervalSeconds);
            }
            if (actionTime < actionEndsAt) return changed;
            var nextSkill = queuedSkill;
            ClearAction();
            return nextSkill == null ? changed : StartAction(nextSkill, actionTime) || changed;
        }

        public bool CanExecute(string skillId)
        {
            skillId = ResolveSkillId(skillId);
            var definition = FindSkill(skillId);
            return activeSkill == null && definition != null && resource >= definition.resourceCost &&
                (!cooldowns.TryGetValue(skillId, out var readyAt) || Time.time >= readyAt);
        }

        private bool QueueCombo(SkillDefinition definition, float actionTime)
        {
            if (queuedSkill != null || activeSkill.comboNextSkillId != definition.id) return false;
            var elapsed = actionTime - actionStartedAt;
            if (elapsed < activeSkill.comboWindowStartSeconds || elapsed > activeSkill.comboWindowEndSeconds) return false;
            queuedSkill = definition;
            return true;
        }

        private void OnDisable() => ClearAction();

        private void ClearAction()
        {
            var completedSkill = activeSkill;
            activeSkill = null;
            queuedSkill = null;
            actionStartedAt = 0f;
            pendingHitAt = 0f;
            actionEndsAt = 0f;
            impactApplied = false;
            pendingHitCount = 0;
            if (completedSkill != null) SkillEnded?.Invoke(completedSkill);
        }

        private SkillDefinition FindSkill(string skillId)
        {
            var selector = GetComponent<CharacterSelector>();
            var characterId = selector == null ? string.Empty : selector.SelectedId;
            foreach (var skill in skills)
                if (skill != null && skill.id == skillId && (string.IsNullOrEmpty(skill.characterId) || skill.characterId == characterId)) return skill;
            return null;
        }

        public string ResolveSkillId(string inputSkillId)
        {
            var selector = GetComponent<CharacterSelector>();
            return ResolveSkillId(inputSkillId, selector == null ? string.Empty : selector.SelectedId);
        }

        public string ResolveSkillId(string inputSkillId, string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || !inputSkillId.StartsWith("skill.")) return inputSkillId;
            var variantId = $"skill.{characterId}_{inputSkillId.Substring(6)}";
            foreach (var skill in skills)
                if (skill != null && skill.id == variantId && skill.characterId == characterId) return variantId;
            return inputSkillId;
        }

        private string ResolveAnimationClip(SkillDefinition definition)
        {
            var selector = GetComponent<CharacterSelector>();
            var characterId = selector == null ? string.Empty : selector.SelectedId;
            foreach (var binding in animationBindings)
                if (binding != null && binding.characterId == characterId && binding.skillId == definition.id)
                    return binding.clipName;
            return definition.animationClip;
        }

        private bool ApplyDamage(SkillDefinition definition)
        {
            SkillReleased?.Invoke(definition);
            if (definition.projectileSpeed > 0f) return SpawnProjectile(definition);
            var center = transform.position + transform.forward * (definition.range * .6f);
            foreach (var collider in Physics.OverlapSphere(center, definition.range, targetMask, QueryTriggerInteraction.Collide))
            {
                var enemy = collider.GetComponentInParent<EnemyTarget>();
                if (enemy != null && !enemy.Health.IsDefeated)
                {
                    enemy.TakeHit(definition.damage);
                    ApplyStatus(enemy.Health, definition);
                    return true;
                }
                var knockout = collider.GetComponentInParent<KnockoutObjectiveTarget>();
                if (knockout != null && !knockout.Health.IsDefeated)
                {
                    knockout.TakeHit(definition.damage);
                    ApplyStatus(knockout.Health, definition);
                    return true;
                }
                var mupo = collider.GetComponentInParent<MupoHerdTarget>();
                if (mupo != null && !mupo.Health.IsDefeated)
                {
                    mupo.Health.ApplyDamage(definition.damage);
                    ApplyStatus(mupo.Health, definition);
                    return true;
                }
            }
            return false;
        }

        private bool SpawnProjectile(SkillDefinition definition)
        {
            var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = $"Projectile {definition.id}";
            projectileObject.transform.SetPositionAndRotation(transform.position + transform.forward * .75f, transform.rotation);
            projectileObject.transform.localScale = Vector3.one * .2f;
            var collider = projectileObject.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            var body = projectileObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            projectileObject.AddComponent<SkillProjectile>().Configure(transform.forward, definition, targetMask, FindNearestTarget(definition.range));
            return true;
        }

        private Transform FindNearestTarget(float radius)
        {
            Transform nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var collider in Physics.OverlapSphere(transform.position, radius, targetMask, QueryTriggerInteraction.Collide))
            {
                if (collider.GetComponentInParent<EnemyTarget>() == null && collider.GetComponentInParent<KnockoutObjectiveTarget>() == null && collider.GetComponentInParent<MupoHerdTarget>() == null) continue;
                var distance = (collider.bounds.center - transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = collider.transform;
                nearestDistance = distance;
            }
            return nearest;
        }

        private static void ApplyStatus(Health health, SkillDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.statusEffectId) || definition.statusDurationSeconds <= 0f) return;
            var effects = health.GetComponent<StatusEffectController>();
            if (effects == null) effects = health.gameObject.AddComponent<StatusEffectController>();
            effects.ApplyStatus(definition.statusEffectId, definition.statusDurationSeconds, definition.statusTickSeconds,
                definition.statusDamagePerTick, definition.statusMovementMultiplier);
        }
    }
}
