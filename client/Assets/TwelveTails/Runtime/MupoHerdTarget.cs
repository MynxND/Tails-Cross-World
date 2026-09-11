using UnityEngine;

namespace TwelveTails.Gameplay
{
    [RequireComponent(typeof(Health))]
    public sealed class MupoHerdTarget : MonoBehaviour
    {
        [SerializeField] private string mupoId = string.Empty;
        [SerializeField, Min(0.1f)] private float fleeRadius = 4f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.2f;
        private MupoHerdMission mission = null!;
        private Transform player = null!;
        private CharacterController controller = null!;
        private Health health = null!;
        public Health Health
        {
            get
            {
                AttachHealth();
                return health;
            }
        }

        public string MupoId => mupoId;

        private void Awake()
        {
            AttachHealth();
            var herdMission = FindAnyObjectByType<MupoHerdMission>();
            if (herdMission != null) herdMission.RegisterTarget(this);
        }

        private void AttachHealth()
        {
            if (health != null) return;
            health = GetComponent<Health>();
            if (health != null) health.Defeated += OnDefeated;
        }

        public void Configure(MupoHerdMission herdMission)
        {
            mission = herdMission;
            player = GameObject.FindWithTag("Player")?.transform;
            controller = GetComponent<CharacterController>();
        }

        public void ConfigureId(string id) => mupoId = id;

        private void Update()
        {
            if (mission == null || mission.IsComplete || mission.IsFailed || player == null) return;
            var offset = transform.position - player.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > fleeRadius * fleeRadius) return;
            var direction = offset.sqrMagnitude < 0.01f ? Vector3.forward : offset.normalized;
            var motion = direction * (moveSpeed * Time.deltaTime);
            if (controller != null) controller.Move(motion);
            else transform.position += motion;
        }

        public void ReportDeath()
        {
            mission?.RegisterDeath(mupoId);
            FindAnyObjectByType<LanGameClient>()?.SendMupoDeath(mupoId);
        }

        private void OnDefeated()
        {
            ReportDeath();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (health != null) health.Defeated -= OnDefeated;
        }
    }
}