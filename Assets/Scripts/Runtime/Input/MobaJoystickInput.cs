using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Moba.Runtime.Input
{
    /// <summary>
    /// Unified input manager that aggregates Virtual Joystick input and Keyboard (WASD/Arrows).
    /// </summary>
    public class MobaJoystickInput : MonoBehaviour, IJoystickInputProvider
    {
        [SerializeField] private MobaVirtualJoystickUI virtualJoystick;
        [SerializeField] private bool enableKeyboardFallback = true;

        private Vector2 combinedInput = Vector2.zero;

        public Vector2 MoveInput => combinedInput;
        public bool HasMoveInput => combinedInput.sqrMagnitude > 0.0001f;

        public void BindVirtualJoystick(MobaVirtualJoystickUI joystick)
        {
            virtualJoystick = joystick;
        }

        private void Update()
        {
            Vector2 input = Vector2.zero;

            // 1. Try Virtual Joystick first
            if (virtualJoystick != null && virtualJoystick.HasMoveInput)
            {
                input = virtualJoystick.MoveInput;
            }
            // 2. Fallback to Keyboard if enabled
            else if (enableKeyboardFallback)
            {
                input = ReadKeyboardInput();
            }

            // Clamp input magnitude to 1
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            combinedInput = input;
        }

        private Vector2 ReadKeyboardInput()
        {
            Vector2 kb = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) kb.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) kb.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) kb.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) kb.x -= 1f;
                return kb;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            kb.x = UnityEngine.Input.GetAxisRaw("Horizontal");
            kb.y = UnityEngine.Input.GetAxisRaw("Vertical");
#endif

            return kb;
        }
    }
}
