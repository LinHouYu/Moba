using UnityEngine;
using Moba.Runtime.Input;
using Moba.Runtime.CameraSystem;

namespace Moba.Runtime.Movement
{
    /// <summary>
    /// Controls MOBA character movement, instantaneous steering, acceleration/braking, and gravity.
    /// Exposes rich configuration parameters for real-time tuning in the Unity Inspector.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class MobaHeroMovement : MonoBehaviour
    {
        [Header("Movement Settings (Inspector Tunable)")]
        [Tooltip("Maximum movement speed in meters per second (King of Glory hero speed is ~6.5 - 7.5 m/s).")]
        [SerializeField] [Range(1f, 20f)] private float moveSpeed = 6.8f;

        [Tooltip("Turn speed in degrees per second.")]
        [SerializeField] [Range(180f, 2160f)] private float turnSpeed = 1080f;

        [Tooltip("Acceleration in m/s^2 when starting to move.")]
        [SerializeField] [Range(10f, 100f)] private float acceleration = 45f;

        [Tooltip("Deceleration in m/s^2 when stopping.")]
        [SerializeField] [Range(10f, 100f)] private float deceleration = 50f;

        [Header("Physics")]
        [SerializeField] private float gravity = -20f;

        [Header("References")]
        [SerializeField] private MonoBehaviour inputProviderSource;

        [Header("Live Debugging (Read-Only)")]
        [SerializeField] private float debugCurrentSpeed = 0f;
        [SerializeField] private bool debugIsMoving = false;
        [SerializeField] private Vector3 debugMoveDirection = Vector3.forward;
        [SerializeField] private Vector2 debugInput = Vector2.zero;

        private CharacterController characterController;
        private IJoystickInputProvider inputProvider;
        private float currentHorizontalSpeed = 0f;
        private float verticalVelocity = 0f;
        private Vector3 currentMoveDirection = Vector3.forward;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        public float TurnSpeed
        {
            get => turnSpeed;
            set => turnSpeed = Mathf.Max(0f, value);
        }

        public float CurrentSpeed => currentHorizontalSpeed;
        public bool IsMoving => currentHorizontalSpeed > 0.05f;
        public Vector3 MoveDirection => currentMoveDirection;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            EnsureVisualFeetAlignment();
            ResolveInputProvider();
        }

        private void Start()
        {
            EnsureVisualFeetAlignment();
            ResolveInputProvider();
            EnsureCameraFollow();
        }

        private void EnsureCameraFollow()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<MobaCameraFollow>() ?? cam.gameObject.AddComponent<MobaCameraFollow>();
                if (follow.Target == null)
                {
                    follow.Target = transform;
                }
            }
        }

        /// <summary>
        /// Ensures standard humanoid feet alignment:
        /// If a primitive MeshFilter is directly on the root object, its center is at Y = 0 (bottom at Y = -1),
        /// which causes the character to sink into the floor by 1 meter when CharacterController stands at ground level.
        /// This moves the mesh to a child 'Hero_Visual' at Y = +center.y so feet align exactly with ground (Y = 0).
        /// </summary>
        public void EnsureVisualFeetAlignment()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            var rootMeshFilter = GetComponent<MeshFilter>();
            var rootMeshRenderer = GetComponent<MeshRenderer>();

            if (characterController != null && rootMeshFilter != null)
            {
                // Create or find Hero_Visual child
                Transform visualTransform = transform.Find("Hero_Visual");
                GameObject visualObj;
                if (visualTransform == null)
                {
                    visualObj = new GameObject("Hero_Visual");
                    visualObj.transform.SetParent(transform, false);
                }
                else
                {
                    visualObj = visualTransform.gameObject;
                }

                // Place visual mesh center at collider center height (usually +1.0m) so base touches feet level (Y = 0)
                visualObj.transform.localPosition = new Vector3(0f, characterController.center.y, 0f);
                visualObj.transform.localRotation = Quaternion.identity;
                visualObj.transform.localScale = Vector3.one;

                var childFilter = visualObj.GetComponent<MeshFilter>() ?? visualObj.AddComponent<MeshFilter>();
                childFilter.sharedMesh = rootMeshFilter.sharedMesh;

                if (rootMeshRenderer != null)
                {
                    var childRenderer = visualObj.GetComponent<MeshRenderer>() ?? visualObj.AddComponent<MeshRenderer>();
                    childRenderer.sharedMaterials = rootMeshRenderer.sharedMaterials;
                    DestroyImmediate(rootMeshRenderer);
                }

                // Reparent direction visor if it was a child of root
                var visor = transform.Find("Direction_Visor");
                if (visor != null)
                {
                    visor.SetParent(visualObj.transform, false);
                    visor.localPosition = new Vector3(0f, 0.4f, 0.5f);
                }

                DestroyImmediate(rootMeshFilter);
            }
        }

        private void ResolveInputProvider()
        {
            if (inputProviderSource is IJoystickInputProvider provider)
            {
                inputProvider = provider;
            }
            else
            {
                inputProvider = GetComponent<IJoystickInputProvider>()
                    ?? GetComponentInParent<IJoystickInputProvider>()
                    ?? FindAnyObjectByType<MobaJoystickInput>();
            }

            if (inputProvider is MobaJoystickInput joystickInput)
            {
                var joystickUI = FindAnyObjectByType<MobaVirtualJoystickUI>();
                if (joystickUI != null)
                {
                    joystickInput.BindVirtualJoystick(joystickUI);
                }
            }
        }

        public void SetInputProvider(IJoystickInputProvider provider)
        {
            inputProvider = provider;
            if (provider is MonoBehaviour mb)
            {
                inputProviderSource = mb;
            }
        }

        private void Update()
        {
            if (inputProvider == null)
            {
                ResolveInputProvider();
            }

            float dt = Time.deltaTime;
            Vector2 rawInput = inputProvider != null ? inputProvider.MoveInput : Vector2.zero;
            float inputMagnitude = rawInput.magnitude;

            debugInput = rawInput;

            // 1. Update horizontal speed (smooth acceleration & braking)
            currentHorizontalSpeed = MobaMovementMath.UpdateSpeed(
                currentHorizontalSpeed,
                inputMagnitude,
                moveSpeed,
                acceleration,
                deceleration,
                dt);

            // 2. Update rotation if input is active
            if (inputMagnitude > 0.01f)
            {
                float targetAngle = MobaMovementMath.CalculateTargetAngle(rawInput);
                float newAngle = MobaMovementMath.RotateTowardsAngle(transform.eulerAngles.y, targetAngle, turnSpeed, dt);
                transform.rotation = Quaternion.Euler(0f, newAngle, 0f);

                currentMoveDirection = MobaMovementMath.InputToWorldDirection(rawInput).normalized;
            }

            // 3. Apply gravity
            if (characterController.isGrounded)
            {
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = -2f; // Keep pinned to ground
                }
            }
            else
            {
                verticalVelocity += gravity * dt;
            }

            // 4. Calculate total velocity and apply movement
            Vector3 velocity = (transform.forward * currentHorizontalSpeed) + (Vector3.up * verticalVelocity);
            characterController.Move(velocity * dt);

            // 5. Update debug fields
            debugCurrentSpeed = currentHorizontalSpeed;
            debugIsMoving = currentHorizontalSpeed > 0.05f;
            debugMoveDirection = transform.forward;
        }
    }
}
