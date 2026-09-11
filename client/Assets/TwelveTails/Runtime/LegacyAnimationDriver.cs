using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class LegacyAnimationDriver : MonoBehaviour
    {
        private Animation legacyAnimation = null!;
        public string LastPlayedClip { get; private set; } = string.Empty;

        private void Awake() => legacyAnimation = GetComponent<Animation>();

        public bool CanPlay(string clipName)
        {
            if (legacyAnimation == null) legacyAnimation = GetComponent<Animation>();
            return legacyAnimation != null && !string.IsNullOrWhiteSpace(clipName) && legacyAnimation.GetClip(clipName) != null;
        }

        public float ClipDuration(string clipName)
        {
            if (!CanPlay(clipName)) return 0f;
            return legacyAnimation.GetClip(clipName).length;
        }

        public bool PlaySkillAnimation(string clipName)
        {
            return PlayAnimation(clipName);
        }

        public bool PlayAnimation(string clipName)
        {
            if (!CanPlay(clipName)) return false;
            legacyAnimation.CrossFade(clipName, .05f);
            LastPlayedClip = clipName;
            return true;
        }
    }
}