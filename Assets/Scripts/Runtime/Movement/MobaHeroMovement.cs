using UnityEngine;
using Moba.Runtime.Input;

namespace Moba.Runtime.Movement
{
    /// <summary>
    /// Controls MOBA character movement, instantaneous steering, acceleration/braking, and gravity.
    /// Uses CharacterController for physics/collision resolution.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MobaHeroMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Maximum movement speed in meters per second (King of Glory hero speed is ~6.5 - 7.5 m/s).")]
        [SerializeField] private float moveSpeed = 6.8f;

        [Tooltip("Turn speed in degrees per second.")]
        [SerializeField] private float turnSpeed = 1080f;

        [Tooltip("Acceleration in m/s^2 when starting to move.")]
        [SerializeField] private float acceleration = 45f;

        [Tooltip("Deceleration in m/s^2 when stopping.")]
        [SerializeField] private float deceleration = 50f;

        [Header("Physics")]
        [SerializeField] private float gravity = -20f;

        [Header("References")]
        [SerializeField] private MonoBehaviour inputProviderSource;

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

            if (inputProviderSource is IJoystickInputProvider provider)
            {
                inputProvider = provider;
            }
            else
            {
                inputProvider = GetComponent<IJoystickInputProvider>();
                if (inputProvider == null)
                {
                    inputProvider = GetComponentInParent<IJoystickInputProvider>();
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
            float dt = Time.deltaTime;
            Vector2 rawInput = inputProvider != null ? inputProvider.MoveInput : Vector2.zero;
            float inputMagnitude = rawInput.magnitude;

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
                    verticalVelocity = -2f; // Slight downward push to keep grounded
                }
            }
            else
            {
                verticalVelocity += gravity * dt;
            }

            // 4. Calculate total velocity and apply movement
            Vector3 velocity = (transform.forward * currentHorizontalSpeed) + (Vector3.up * verticalVelocity);
            characterController.Move(velocity * dt);
        }
    }
}
