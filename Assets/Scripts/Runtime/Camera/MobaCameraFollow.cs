using UnityEngine;

namespace Moba.Runtime.CameraSystem
{
    /// <summary>
    /// Smooth top-down isometric MOBA camera follower.
    /// Runs in LateUpdate to prevent jitter after character movement.
    /// </summary>
    public class MobaCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Camera Geometry")]
        [Tooltip("Relative position offset from the target.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 15f, -11f);

        [Tooltip("Fixed camera pitch angle in degrees.")]
        [SerializeField] private float pitchAngle = 53f;

        [Header("Smoothing")]
        [Tooltip("Smooth damp time (smaller = tighter follow, larger = looser).")]
        [SerializeField] private float smoothTime = 0.12f;

        private Vector3 currentVelocity = Vector3.zero;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public Vector3 Offset
        {
            get => offset;
            set => offset = value;
        }

        private void Start()
        {
            UpdateRotation();
            if (target != null)
            {
                // Snap directly on start
                transform.position = target.position + offset;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 targetPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
            UpdateRotation();
        }

        private void UpdateRotation()
        {
            transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateRotation();
        }
#endif
    }
}
