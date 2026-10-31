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

        [TestCase(-100f, 0f, -1f, 0f)]      // Left
        [TestCase(100f, 0f, 1f, 0f)]        // Right
        [TestCase(0f, 100f, 0f, 1f)]        // Up
        [TestCase(0f, -100f, 0f, -1f)]      // Down
        [TestCase(-100f, 100f, -0.7071f, 0.7071f)] // Top-Left (clamped to unit circle)
        [TestCase(100f, -100f, 0.7071f, -0.7071f)] // Bottom-Right (clamped to unit circle)
        public void ProcessHandleDisplacement_AccuratelyOutputsAllFourQuadrants(float dx, float dy, float expectedX, float expectedY)
        {
            float handleRange = 100f;
            float deadzone = 0f; // zero deadzone to test raw angle mapping

            MobaMovementMath.ProcessHandleDisplacement(
                new Vector2(dx, dy),
                handleRange,
                deadzone,
                out Vector2 handlePos,
                out Vector2 moveInput);

            Assert.That(moveInput.x, Is.EqualTo(expectedX).Within(0.01f));
            Assert.That(moveInput.y, Is.EqualTo(expectedY).Within(0.01f));
        }

        [Test]
        public void ProcessHandleDisplacement_ClampsHandlePositionToRange()
        {
            float handleRange = 80f;
            // Drag 200 pixels to the left
            Vector2 displacement = new Vector2(-200f, 0f);

            MobaMovementMath.ProcessHandleDisplacement(
                displacement,
                handleRange,
                0.1f,
                out Vector2 handlePos,
                out Vector2 moveInput);

            // Handle should be clamped to 80
            Assert.That(handlePos.x, Is.EqualTo(-80f).Within(Tolerance));
            Assert.That(handlePos.y, Is.EqualTo(0f).Within(Tolerance));

            // Move input should be full left (-1, 0)
            Assert.That(moveInput.x, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(moveInput.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ProcessHandleDisplacement_WhenInsideDeadzone_OutputsZeroInput()
        {
            float handleRange = 100f;
            float deadzone = 0.2f;
            Vector2 smallDisplacement = new Vector2(10f, 0f); // 10% magnitude < 20% deadzone

            MobaMovementMath.ProcessHandleDisplacement(
                smallDisplacement,
                handleRange,
                deadzone,
                out Vector2 handlePos,
                out Vector2 moveInput);

            Assert.That(handlePos.x, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(moveInput.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(moveInput.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ValidateFeetAlignment_WhenStandardHumanoid_AlignsWithGroundLevel()
        {
            float height = 2.0f;
            Vector3 center = new Vector3(0f, 1.0f, 0f);
            float visualOffsetY = 1.0f;

            bool isAligned = MobaMovementMath.ValidateFeetAlignment(height, center, visualOffsetY, out float bottomY);

            Assert.That(isAligned, Is.True);
            Assert.That(bottomY, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ValidateFeetAlignment_WhenVisualSinks_DetectsMismatch()
        {
            // Old bug: visual mesh at Y = 0 causes bottom to sink to Y = -1.0
            float height = 2.0f;
            Vector3 center = new Vector3(0f, 1.0f, 0f);
            float visualOffsetY = 0.0f;

            bool isAligned = MobaMovementMath.ValidateFeetAlignment(height, center, visualOffsetY, out float bottomY);

            Assert.That(isAligned, Is.False);
        }

        [Test]
        public void ValidateFeetAlignment_WithCustomHeroHeight_CalculatesProperCenter()
        {
            float height = 2.4f;
            Vector3 center = new Vector3(0f, 1.2f, 0f);
            float visualOffsetY = 1.2f;

            bool isAligned = MobaMovementMath.ValidateFeetAlignment(height, center, visualOffsetY, out float bottomY);

            Assert.That(isAligned, Is.True);
            Assert.That(bottomY, Is.EqualTo(0f).Within(Tolerance));
        }
    }
}
