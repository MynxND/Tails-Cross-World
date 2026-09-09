using UnityEngine;

namespace TwelveTails.Gameplay
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target = null!;
        [SerializeField] private Vector3 offset = new(0f, 8f, -10f);
        public void Configure(Transform followTarget) => target = followTarget;

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.Lerp(transform.position, target.position + offset, 8f * Time.deltaTime);
            transform.LookAt(target.position + Vector3.up);
        }
    }
}
