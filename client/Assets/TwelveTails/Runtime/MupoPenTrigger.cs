using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class MupoPenTrigger : MonoBehaviour
    {
        [SerializeField] private MupoHerdMission mission = null!;

        public void Configure(MupoHerdMission herdMission) => mission = herdMission;

        private void OnTriggerEnter(Collider other)
        {
            var target = other.GetComponentInParent<MupoHerdTarget>();
            if (target == null || mission == null) return;
            if (mission.RegisterPenEntryAccepted(target.MupoId))
                FindAnyObjectByType<LanGameClient>()?.SendMupoPen(target.MupoId);
        }
    }
}