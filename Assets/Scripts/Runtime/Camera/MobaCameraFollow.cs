using UnityEngine;
using Moba.Runtime.Movement;
using Moba.Runtime.Hero;

namespace Moba.Runtime.CameraSystem
{
    /// <summary>
    /// Smooth top-down isometric MOBA camera follower with micro-shake feedback for Skill 1 collision and Skill 3 ground slam.
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

        [Header("Camera Micro-Shake (技能1撞击与技能3砸地摄像机微震动)")]
        [SerializeField] [Range(0.05f, 0.8f)] private float skill1ShakeDuration = 0.22f;
        [SerializeField] [Range(0.02f, 1.0f)] private float skill1ShakeMagnitude = 0.18f;
        [SerializeField] [Range(0.05f, 1.0f)] private float skill3ShakeDuration = 0.38f;
        [SerializeField] [Range(0.02f, 1.5f)] private float skill3ShakeMagnitude = 0.32f;
        [SerializeField] [Range(10f, 60f)] private float shakeFrequency = 32f;

        private Vector3 currentVelocity = Vector3.zero;
        private Vector3 smoothedBasePosition = Vector3.zero;
        private bool hasInitializedBasePosition = false;

        private float activeShakeElapsed = 0f;
        private float activeShakeDuration = 0f;
        private float activeShakeMagnitude = 0f;
        private float activeShakeFrequency = 32f;

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

        public bool IsShaking => activeShakeDuration > 0f && activeShakeElapsed < activeShakeDuration;

        /// <summary>
        /// Triggers a camera micro-shake with custom duration, amplitude, and frequency.
        /// </summary>
        public void TriggerShake(float duration, float magnitude, float frequency = 32f)
        {
            activeShakeDuration = Mathf.Max(0.02f, duration);
            activeShakeMagnitude = Mathf.Max(0f, magnitude);
            activeShakeFrequency = Mathf.Max(5f, frequency);
            activeShakeElapsed = 0f;
        }

        /// <summary>
        /// Skill 1 撞击敌人摄像机微微震动。
        /// </summary>
        public void TriggerSkill1ImpactShake()
        {
            TriggerShake(skill1ShakeDuration, skill1ShakeMagnitude, shakeFrequency);
        }

        /// <summary>
        /// Skill 3 飞天砸向地面摄像机微微震动。
        /// </summary>
        public void TriggerSkill3SlamShake()
        {
            TriggerShake(skill3ShakeDuration, skill3ShakeMagnitude, shakeFrequency);
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
                smoothedBasePosition = target.position + offset;
                transform.position = smoothedBasePosition;
                hasInitializedBasePosition = true;
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
            if (!hasInitializedBasePosition)
            {
                smoothedBasePosition = transform.position;
                hasInitializedBasePosition = true;
            }

            smoothedBasePosition = Vector3.SmoothDamp(smoothedBasePosition, targetPosition, ref currentVelocity, smoothTime);

            Vector3 shakeOffset = Vector3.zero;
            if (activeShakeDuration > 0f && activeShakeElapsed < activeShakeDuration)
            {
                shakeOffset = YunZhongJunLogic.EvaluateCameraShakeOffset(
                    activeShakeElapsed,
                    activeShakeDuration,
                    activeShakeMagnitude,
                    activeShakeFrequency);
                activeShakeElapsed += Time.deltaTime;
                if (activeShakeElapsed >= activeShakeDuration)
                {
                    activeShakeDuration = 0f;
                    activeShakeElapsed = 0f;
                }
            }

            transform.position = smoothedBasePosition + shakeOffset;
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
