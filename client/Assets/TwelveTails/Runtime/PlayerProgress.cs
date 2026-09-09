using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class PlayerProgress : MonoBehaviour
    {
        public int Experience { get; private set; }
        public int PotionCount { get; private set; }
        public bool RewardClaimed { get; private set; }

        public bool GrantQuestReward(int experience, int potions)
        {
            if (RewardClaimed || experience < 0 || potions < 0) return false;
            Experience += experience;
            PotionCount += potions;
            RewardClaimed = true;
            return true;
        }

        public void Restore(int experience, int potions, bool rewardClaimed)
        {
            Experience = Mathf.Max(0, experience);
            PotionCount = Mathf.Max(0, potions);
            RewardClaimed = rewardClaimed;
        }
    }
}
