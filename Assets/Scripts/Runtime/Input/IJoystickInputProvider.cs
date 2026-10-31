using UnityEngine;

namespace Moba.Runtime.Input
{
    /// <summary>
    /// Contract for providing movement joystick input to characters or systems.
    /// </summary>
    public interface IJoystickInputProvider
    {
        /// <summary>
        /// Movement vector where X is horizontal (-1 left, +1 right) and Y is vertical (-1 down, +1 up).
        /// Magnitude is clamped to [0, 1].
        /// </summary>
        Vector2 MoveInput { get; }

        /// <summary>
        /// True when the player is providing active movement input above the deadzone.
        /// </summary>
        bool HasMoveInput { get; }
    }
}
