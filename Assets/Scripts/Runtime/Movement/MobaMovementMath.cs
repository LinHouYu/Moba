using System;
using UnityEngine;

namespace Moba.Runtime.Movement
{
    /// <summary>
    /// Pure mathematical calculations for MOBA character movement, rotation, and input response.
    /// Completely decoupled from Unity scene state for easy and reliable unit testing.
    /// </summary>
    public static class MobaMovementMath
    {
        /// <summary>
        /// Applies a deadzone to a 2D joystick input vector and scales the remaining range from 0 to 1.
        /// </summary>
        /// <param name="input">Raw joystick input vector with magnitude up to 1.</param>
        /// <param name="deadzone">Deadzone threshold (between 0 and 1).</param>
        /// <returns>Processed input vector with magnitude in [0, 1].</returns>
        public static Vector2 ApplyDeadzone(Vector2 input, float deadzone)
        {
            float magnitude = input.magnitude;
            if (magnitude <= deadzone || deadzone >= 1f)
            {
                return Vector2.zero;
            }

            // Remap magnitude from [deadzone, 1] to [0, 1]
            float remappedMagnitude = (magnitude - deadzone) / (1f - deadzone);
            remappedMagnitude = Mathf.Clamp01(remappedMagnitude);

            return input.normalized * remappedMagnitude;
        }

        /// <summary>
        /// Converts 2D joystick input (X = horizontal, Y = vertical) into a 3D world movement direction.
        /// Standard MOBA top-down: Up (+Y) maps to world forward (+Z), Right (+X) maps to world right (+X).
        /// </summary>
        public static Vector3 InputToWorldDirection(Vector2 input)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        /// <summary>
        /// Calculates target Y rotation angle in degrees for a given 2D direction.
        /// (0, 1) [Up] -> 0 degrees
        /// (1, 0) [Right] -> 90 degrees
        /// (0, -1) [Down] -> 180 degrees
        /// (-1, 0) [Left] -> 270 degrees
        /// </summary>
        public static float CalculateTargetAngle(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            float angle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }
            return angle;
        }

        /// <summary>
        /// Rotates the current angle towards the target angle with a given max angular speed.
        /// Handles 360-degree wrapping correctly.
        /// </summary>
        public static float RotateTowardsAngle(float currentAngle, float targetAngle, float turnSpeedDegPerSec, float deltaTime)
        {
            if (deltaTime <= 0f || turnSpeedDegPerSec <= 0f)
            {
                return currentAngle;
            }

            return Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeedDegPerSec * deltaTime);
        }

        /// <summary>
        /// Updates the current speed based on whether there is movement input (accelerating vs decelerating).
        /// </summary>
        public static float UpdateSpeed(
            float currentSpeed,
            float inputMagnitude,
            float maxSpeed,
            float acceleration,
            float deceleration,
            float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return currentSpeed;
            }

            float targetSpeed = inputMagnitude > 0.001f ? maxSpeed * Mathf.Clamp01(inputMagnitude) : 0f;

            if (currentSpeed < targetSpeed)
            {
                // Accelerating
                return Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * deltaTime);
            }
            else
            {
                // Decelerating / Braking
                return Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * deltaTime);
            }
        }
    }
}
