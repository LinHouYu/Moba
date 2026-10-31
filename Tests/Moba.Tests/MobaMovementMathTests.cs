using NUnit.Framework;
using UnityEngine;
using Moba.Runtime.Movement;

namespace Moba.Tests
{
    [TestFixture]
    public class MobaMovementMathTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void ApplyDeadzone_WhenBelowDeadzone_ReturnsZero()
        {
            Vector2 input = new Vector2(0.05f, 0.05f); // magnitude ~0.0707
            float deadzone = 0.15f;

            Vector2 result = MobaMovementMath.ApplyDeadzone(input, deadzone);

            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ApplyDeadzone_WhenFullInput_ReturnsFullMagnitude()
        {
            Vector2 input = new Vector2(0f, 1f);
            float deadzone = 0.2f;

            Vector2 result = MobaMovementMath.ApplyDeadzone(input, deadzone);

            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void ApplyDeadzone_WhenMidway_LinearlyRemapsMagnitude()
        {
            // Input magnitude = 0.6, deadzone = 0.2 -> expected magnitude = (0.6 - 0.2)/(1 - 0.2) = 0.4 / 0.8 = 0.5
            Vector2 input = new Vector2(0.6f, 0f);
            float deadzone = 0.2f;

            Vector2 result = MobaMovementMath.ApplyDeadzone(input, deadzone);

            Assert.That(result.x, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void InputToWorldDirection_MapsJoystickToTopDownXZPlane()
        {
            Vector2 joystickUp = new Vector2(0f, 1f);
            Vector3 worldUp = MobaMovementMath.InputToWorldDirection(joystickUp);
            Assert.That(worldUp, Is.EqualTo(new Vector3(0f, 0f, 1f)));

            Vector2 joystickRight = new Vector2(1f, 0f);
            Vector3 worldRight = MobaMovementMath.InputToWorldDirection(joystickRight);
            Assert.That(worldRight, Is.EqualTo(new Vector3(1f, 0f, 0f)));
        }

        [TestCase(0f, 1f, 0f)]       // Up -> 0 deg
        [TestCase(1f, 0f, 90f)]      // Right -> 90 deg
        [TestCase(0f, -1f, 180f)]    // Down -> 180 deg
        [TestCase(-1f, 0f, 270f)]    // Left -> 270 deg
        [TestCase(1f, 1f, 45f)]      // Up-Right -> 45 deg
        [TestCase(-1f, 1f, 315f)]    // Up-Left -> 315 deg
        public void CalculateTargetAngle_ReturnsExpectedAngles(float x, float y, float expectedAngle)
        {
            float angle = MobaMovementMath.CalculateTargetAngle(new Vector2(x, y));
            Assert.That(angle, Is.EqualTo(expectedAngle).Within(Tolerance));
        }

        [Test]
        public void RotateTowardsAngle_SmoothlyInterpolatesWithSpeed()
        {
            float current = 0f;
            float target = 90f;
            float speed = 180f; // 180 deg / sec
            float dt = 0.25f;   // should move 45 deg

            float result = MobaMovementMath.RotateTowardsAngle(current, target, speed, dt);
            Assert.That(result, Is.EqualTo(45f).Within(Tolerance));
        }

        [Test]
        public void RotateTowardsAngle_HandlesShortestPathWrapAround()
        {
            float current = 10f;
            float target = 350f; // shortest difference is -20 deg
            float speed = 100f;
            float dt = 0.1f;    // moves 10 deg towards 350

            float result = MobaMovementMath.RotateTowardsAngle(current, target, speed, dt);
            // 10 - 10 = 0 -> in MoveTowardsAngle, 0 or 360
            Assert.That(result, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void UpdateSpeed_AcceleratesSmoothly()
        {
            float current = 0f;
            float maxSpeed = 6f;
            float accel = 20f;
            float dt = 0.1f;

            float speed = MobaMovementMath.UpdateSpeed(current, 1f, maxSpeed, accel, 40f, dt);

            // 0 + 20 * 0.1 = 2.0
            Assert.That(speed, Is.EqualTo(2.0f).Within(Tolerance));
        }

        [Test]
        public void UpdateSpeed_DeceleratesSmoothlyWhenInputStops()
        {
            float current = 6f;
            float maxSpeed = 6f;
            float decel = 30f;
            float dt = 0.1f;

            float speed = MobaMovementMath.UpdateSpeed(current, 0f, maxSpeed, 20f, decel, dt);

            // 6 - 30 * 0.1 = 3.0
            Assert.That(speed, Is.EqualTo(3.0f).Within(Tolerance));
        }

        [Test]
        public void UpdateSpeed_ClampsToPartialInputTargetSpeed()
        {
            float current = 0f;
            float maxSpeed = 10f;
            float inputMag = 0.5f; // target speed = 5.0f
            float accel = 50f;
            float dt = 1.0f; // huge step, should clamp to 5.0f

            float speed = MobaMovementMath.UpdateSpeed(current, inputMag, maxSpeed, accel, 20f, dt);

            Assert.That(speed, Is.EqualTo(5.0f).Within(Tolerance));
        }
    }
}
