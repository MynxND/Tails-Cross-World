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
        public string animationClip = string.Empty;
        public int damage;
        public float cooldownSeconds;
        public float hitDelaySeconds;
        public float actionDurationSeconds;
        public float comboWindowStartSeconds;
        public float comboWindowEndSeconds;
        public string comboNextSkillId = string.Empty;
        public int resourceCost;
        public float range;
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
        [SerializeField] private LayerMask targetMask = ~0;
        private readonly Dictionary<string, float> cooldowns = new();
        private CharacterAnimationDriver proceduralAnimation = null!;
        private AnimatorMotionDriver animatorMotion = null!;
        private SkillDefinition activeSkill = null!;
        private SkillDefinition queuedSkill = null!;
        private float actionStartedAt;
        private float pendingHitAt;
        private float actionEndsAt;
        private bool impactApplied;

        public int Resource => resource;
        public IReadOnlyList<SkillDefinition> Skills => skills;
        public bool IsWindingUp => activeSkill != null && !impactApplied;
        public bool IsActionActive => activeSkill != null;
        public string ActiveSkillId => activeSkill?.id ?? string.Empty;

        public void Configure(IEnumerable<SkillDefinition> definitions, int initialResource = 100)
        {
            skills = definitions == null ? Array.Empty<SkillDefinition>() : new List<SkillDefinition>(definitions).ToArray();
            maximumResource = Mathf.Max(0, initialResource);
            resource = maximumResource;
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
        }

        private void Update()
        {
            AdvanceAction(Time.time);
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) ExecuteSkill("skill.basic_slash");
            if (keyboard.digit2Key.wasPressedThisFrame) ExecuteSkill("skill.power_strike");
            if (keyboard.digit3Key.wasPressedThisFrame) ExecuteSkill("skill.class_special");
        }

        public bool ExecuteSkill(string skillId) => ExecuteSkill(skillId, Time.time);

        public bool ExecuteSkill(string skillId, float actionTime)
        {
            var definition = FindSkill(skillId);
            if (definition == null || definition.damage < 0 || definition.range <= 0f) return false;
            if (activeSkill != null) return QueueCombo(definition, actionTime);
            return StartAction(definition, actionTime);
        }

        private bool StartAction(SkillDefinition definition, float actionTime)
        {
            if (resource < definition.resourceCost) return false;
            if (cooldowns.TryGetValue(definition.id, out var readyAt) && actionTime < readyAt) return false;

            var clipName = ResolveAnimationClip(definition);
            proceduralAnimation?.PlaySkillAnimation(clipName);
            animatorMotion?.PlaySkillAnimation(clipName);
            resource -= definition.resourceCost;
            cooldowns[definition.id] = actionTime + Mathf.Max(0f, definition.cooldownSeconds);
            var actionDuration = Mathf.Max(definition.actionDurationSeconds, definition.hitDelaySeconds);
            if (actionDuration <= 0f) return ApplyDamage(definition);
            activeSkill = definition;
            actionStartedAt = actionTime;
            pendingHitAt = actionTime + definition.hitDelaySeconds;
            actionEndsAt = actionTime + actionDuration;
            impactApplied = definition.hitDelaySeconds <= 0f;
            if (impactApplied) ApplyDamage(definition);
            return true;
        }

        public bool AdvanceAction(float actionTime)
        {
            if (activeSkill == null) return false;
            var changed = false;
            if (!impactApplied && actionTime >= pendingHitAt)
            {
                impactApplied = true;
                changed = ApplyDamage(activeSkill);
            }
            if (actionTime < actionEndsAt) return changed;
            var nextSkill = queuedSkill;
            ClearAction();
            return nextSkill == null ? changed : StartAction(nextSkill, actionTime) || changed;
        }

        public bool CanExecute(string skillId)
        {
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
            activeSkill = null;
            queuedSkill = null;
            actionStartedAt = 0f;
            pendingHitAt = 0f;
            actionEndsAt = 0f;
            impactApplied = false;
        }

        private SkillDefinition FindSkill(string skillId)
        {
            foreach (var skill in skills)
                if (skill != null && skill.id == skillId) return skill;
            return null;
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
            var center = transform.position + transform.forward * (definition.range * .6f);
            foreach (var collider in Physics.OverlapSphere(center, definition.range, targetMask, QueryTriggerInteraction.Collide))
            {
                var enemy = collider.GetComponentInParent<EnemyTarget>();
                if (enemy != null && !enemy.Health.IsDefeated)
                {
                    enemy.TakeHit(definition.damage);
                    return true;
                }
                var knockout = collider.GetComponentInParent<KnockoutObjectiveTarget>();
                if (knockout != null && !knockout.Health.IsDefeated)
                {
                    knockout.TakeHit(definition.damage);
                    return true;
                }
                var mupo = collider.GetComponentInParent<MupoHerdTarget>();
                if (mupo != null && !mupo.Health.IsDefeated)
                {
                    mupo.Health.ApplyDamage(definition.damage);
                    return true;
                }
            }
            return false;
        }
    }
}
