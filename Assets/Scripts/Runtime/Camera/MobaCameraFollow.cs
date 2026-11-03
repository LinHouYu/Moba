using UnityEngine;
using Moba.Runtime.Movement;

namespace Moba.Runtime.CameraSystem
{
    /// <summary>
    /// Smooth top-down isometric MOBA camera follower.
    /// Runs strictly in Play mode LateUpdate to prevent jitter after character movement.
    /// Never runs in Edit mode, preserving Scene View free camera WASD navigation.
    /// </summary>
    [DisallowMultipleComponent]
    public class MobaCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Camera Geometry (Inspector Tunable)")]
        [Tooltip("Relative position offset from the target (X = lateral, Y = height, Z = forward/back).")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 16f, -12f);

        [Tooltip("Fixed camera pitch angle in degrees (MOBA isometric typically 50° - 55°).")]
        [SerializeField] [Range(30f, 80f)] private float pitchAngle = 53f;

        [Header("Smoothing")]
        [Tooltip("Smooth damp time in seconds (smaller = tighter follow, larger = looser).")]
        [SerializeField] [Range(0.01f, 0.5f)] private float smoothTime = 0.12f;

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

        public float PitchAngle
        {
            get => pitchAngle;
            set
            {
                pitchAngle = value;
                UpdateRotation();
            }
        }

        private void Awake()
        {
            EnsureTarget();
        }

        private void Start()
        {
            EnsureTarget();
            UpdateRotation();
            if (target != null)
            {
                transform.position = target.position + offset;
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;

            if (target == null)
            {
                EnsureTarget();
                if (target == null)
                {
                    return;
                }
            }

            Vector3 targetPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
            UpdateRotation();
        }

        public void EnsureTarget()
        {
            if (target != null) return;

            var hero = Object.FindAnyObjectByType<MobaHeroMovement>();
            if (hero != null)
            {
                target = hero.transform;
            }
            else
            {
                var playerObj = GameObject.Find("Moba_Hero_Player");
                if (playerObj != null)
                {
                    target = playerObj.transform;
                }
            }
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
