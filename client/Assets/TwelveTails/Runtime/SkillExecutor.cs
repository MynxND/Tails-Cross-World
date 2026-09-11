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

        public int Resource => resource;
        public IReadOnlyList<SkillDefinition> Skills => skills;

        public void Configure(IEnumerable<SkillDefinition> definitions, int initialResource = 100)
        {
            skills = definitions == null ? Array.Empty<SkillDefinition>() : new List<SkillDefinition>(definitions).ToArray();
            maximumResource = Mathf.Max(0, initialResource);
            resource = maximumResource;
            cooldowns.Clear();
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
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) ExecuteSkill("skill.basic_slash");
            if (keyboard.digit2Key.wasPressedThisFrame) ExecuteSkill("skill.power_strike");
            if (keyboard.digit3Key.wasPressedThisFrame) ExecuteSkill("skill.class_special");
        }

        public bool ExecuteSkill(string skillId)
        {
            var definition = FindSkill(skillId);
            if (definition == null || definition.damage < 0 || definition.range <= 0f) return false;
            if (resource < definition.resourceCost) return false;
            if (cooldowns.TryGetValue(skillId, out var readyAt) && Time.time < readyAt) return false;

            var clipName = ResolveAnimationClip(definition);
            proceduralAnimation?.PlaySkillAnimation(clipName);
            animatorMotion?.PlaySkillAnimation(clipName);
            resource -= definition.resourceCost;
            cooldowns[skillId] = Time.time + Mathf.Max(0f, definition.cooldownSeconds);
            return ApplyDamage(definition);
        }

        public bool CanExecute(string skillId)
        {
            var definition = FindSkill(skillId);
            return definition != null && resource >= definition.resourceCost &&
                (!cooldowns.TryGetValue(skillId, out var readyAt) || Time.time >= readyAt);
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
