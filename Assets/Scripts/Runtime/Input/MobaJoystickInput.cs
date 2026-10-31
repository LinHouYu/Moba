using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Moba.Runtime.Input
{
    /// <summary>
    /// Unified input manager that aggregates Virtual Joystick input and Keyboard (WASD/Arrows).
    /// Auto-resolves and binds Virtual Joystick UI if unassigned.
    /// </summary>
    [DisallowMultipleComponent]
    public class MobaJoystickInput : MonoBehaviour, IJoystickInputProvider
    {
        [Header("References")]
        [SerializeField] private MobaVirtualJoystickUI virtualJoystick;

        [Header("Settings")]
        [SerializeField] private bool enableKeyboardFallback = true;

        [Header("Debug")]
        [SerializeField] private Vector2 currentCombinedInput = Vector2.zero;

        public Vector2 MoveInput => currentCombinedInput;
        public bool HasMoveInput => currentCombinedInput.sqrMagnitude > 0.0001f;

        public void BindVirtualJoystick(MobaVirtualJoystickUI joystick)
        {
            virtualJoystick = joystick;
        }

        private void Awake()
        {
            EnsureVirtualJoystick();
        }

        private void Start()
        {
            EnsureVirtualJoystick();
        }

        private void EnsureVirtualJoystick()
        {
            if (virtualJoystick != null) return;

            virtualJoystick = Object.FindAnyObjectByType<MobaVirtualJoystickUI>();
            if (virtualJoystick == null)
            {
                var touchZone = GameObject.Find("Joystick_TouchZone");
                if (touchZone != null)
                {
                    virtualJoystick = touchZone.GetComponent<MobaVirtualJoystickUI>() ?? touchZone.AddComponent<MobaVirtualJoystickUI>();
                }
            }
        }

        private void Update()
        {
            if (virtualJoystick == null)
            {
                EnsureVirtualJoystick();
            }

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

            currentCombinedInput = input;
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
