using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class LegacyAnimationDriver : MonoBehaviour
    {
        private Animation legacyAnimation = null!;

        private void Awake() => legacyAnimation = GetComponent<Animation>();

        public bool CanPlay(string clipName)
        {
            if (legacyAnimation == null) legacyAnimation = GetComponent<Animation>();
            return legacyAnimation != null && !string.IsNullOrWhiteSpace(clipName) && legacyAnimation.GetClip(clipName) != null;
        }

        public bool PlaySkillAnimation(string clipName)
        {
            if (!CanPlay(clipName)) return false;
            legacyAnimation.CrossFade(clipName, .05f);
            return true;
        }
    }
}