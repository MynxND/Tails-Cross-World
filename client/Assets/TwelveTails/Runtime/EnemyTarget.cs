using UnityEngine;

namespace TwelveTails.Gameplay
{
    [RequireComponent(typeof(Health))]
    public sealed class EnemyTarget : MonoBehaviour
    {
        [SerializeField] private string entityId = "monster.training_dummy";
        [SerializeField] private QuestProgress quest = null!;
        public Health Health { get; private set; } = null!;

        private void Awake()
        {
            Health = GetComponent<Health>();
            Health.Defeated += OnDefeated;
        }

        public void Configure(QuestProgress questProgress) => quest = questProgress;
        public void TakeHit(int amount) => Health.ApplyDamage(amount);

        private void OnDefeated()
        {
            quest?.RegisterDefeat(entityId);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Defeated -= OnDefeated;
        }
    }
}
