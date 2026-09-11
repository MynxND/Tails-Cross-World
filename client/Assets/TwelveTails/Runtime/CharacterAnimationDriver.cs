using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        private Transform rig = null!;
        private Transform leftArm = null!;
        private Transform rightArm = null!;
        private Transform leftLeg = null!;
        private Transform rightLeg = null!;
        private float attackUntil;

        private void Awake()
        {
            rig = transform.Find("Rig");
            if (rig == null) { enabled = false; return; }
            leftArm = rig.Find("LeftArm"); rightArm = rig.Find("RightArm");
            leftLeg = rig.Find("LeftLeg"); rightLeg = rig.Find("RightLeg");
        }

        public void PlayAttack() => attackUntil = Time.time + .22f;

        private void Update()
        {
            var controller = GetComponentInParent<CharacterController>();
            var moving = controller != null && controller.velocity.sqrMagnitude > .05f;
            var phase = Time.time * (moving ? 11f : 2.2f);
            rig.localPosition = new Vector3(0, Mathf.Sin(phase) * (moving ? .055f : .018f), 0);
            var swing = moving ? Mathf.Sin(phase) * 28f : Mathf.Sin(phase) * 3f;
            leftArm.localRotation = Quaternion.Euler(swing, 0, 0);
            rightArm.localRotation = Time.time < attackUntil ? Quaternion.Euler(-75f, 0, -30f) : Quaternion.Euler(-swing, 0, 0);
            leftLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(swing, 0, 0);
        }
    }
}
