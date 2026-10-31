using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Moba.Runtime.Movement;

namespace Moba.Runtime.Input
{
    public enum JoystickMode
    {
        [Tooltip("Honor of Kings style: touches anywhere in touch zone move the joystick center to finger")]
        Floating,
        [Tooltip("Fixed position on screen")]
        Fixed
    }

    /// <summary>
    /// uGUI-based mobile virtual joystick with support for Floating and Fixed modes.
    /// Exposes full parameters for tuning in the Unity Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class MobaVirtualJoystickUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IJoystickInputProvider
    {
        [Header("UI Element References")]
        [Tooltip("Outer background disc of the joystick")]
        [SerializeField] private RectTransform backgroundRect;

        [Tooltip("Inner draggable thumb handle")]
        [SerializeField] private RectTransform handleRect;

        [Tooltip("CanvasGroup used for fading idle/active alpha")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Joystick Settings (Inspector Tunable)")]
        [Tooltip("Floating: moves to touch point. Fixed: stays in anchored position.")]
        [SerializeField] private JoystickMode joystickMode = JoystickMode.Floating;

        [Tooltip("Maximum distance handle can move from center (in canvas pixels)")]
        [SerializeField] [Range(30f, 300f)] private float handleRange = 100f;

        [Tooltip("Deadzone radius: movement below this threshold is ignored")]
        [SerializeField] [Range(0f, 0.4f)] private float deadzone = 0.12f;

        [Tooltip("Whether to snap background back to default position when released in Floating mode")]
        [SerializeField] private bool resetPositionOnRelease = true;

        [Header("Visual Feedback")]
        [SerializeField] [Range(0f, 1f)] private float idleAlpha = 0.65f;
        [SerializeField] [Range(0f, 1f)] private float activeAlpha = 1.0f;

        [Header("Live Debug (Read-Only)")]
        [SerializeField] private Vector2 debugInput = Vector2.zero;
        [SerializeField] private bool debugIsPointerDown = false;

        private Vector2 defaultAnchoredPosition;
        private Camera targetCamera;
        private Canvas rootCanvas;
        private Vector2 currentInput = Vector2.zero;
        private int activePointerId = int.MinValue;

        public Vector2 MoveInput => currentInput;
        public bool HasMoveInput => currentInput.sqrMagnitude > 0.0001f;
        public bool IsPointerDown => debugIsPointerDown;

        public JoystickMode Mode
        {
            get => joystickMode;
            set => joystickMode = value;
        }

        public float HandleRange
        {
            get => handleRange;
            set => handleRange = Mathf.Max(10f, value);
        }

        public float Deadzone
        {
            get => deadzone;
            set => deadzone = Mathf.Clamp(value, 0f, 0.5f);
        }

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                targetCamera = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
            }
            else
            {
                targetCamera = null;
            }

            // Ensure EventSystem exists so pointer events are dispatched
            if (UnityEngine.EventSystems.EventSystem.current == null && UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            }

            // Ensure touch zone has raycastable graphic
            var touchImg = GetComponent<Image>();
            if (touchImg != null)
            {
                touchImg.raycastTarget = true;
            }

            // Auto-resolve backgroundRect if unassigned
            if (backgroundRect == null)
            {
                var bg = transform.Find("Joystick_Background");
                if (bg != null) backgroundRect = bg.GetComponent<RectTransform>();
            }

            if (backgroundRect != null)
            {
                // Ensure background has pivot at center (0.5, 0.5)
                backgroundRect.pivot = new Vector2(0.5f, 0.5f);
                defaultAnchoredPosition = backgroundRect.anchoredPosition;

                // Auto-resolve or create handleRect if unassigned
                if (handleRect == null)
                {
                    var handle = backgroundRect.Find("Joystick_Handle");
                    if (handle != null)
                    {
                        handleRect = handle.GetComponent<RectTransform>();
                    }
                    else
                    {
                        GameObject handleObj = new GameObject("Joystick_Handle", typeof(RectTransform));
                        handleObj.transform.SetParent(backgroundRect, false);
                        handleRect = handleObj.GetComponent<RectTransform>();
                        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
                        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
                        handleRect.pivot = new Vector2(0.5f, 0.5f);
                        handleRect.anchoredPosition = Vector2.zero;
                        handleRect.sizeDelta = new Vector2(90f, 90f);

                        var handleImg = handleObj.AddComponent<Image>();
                        handleImg.color = new Color(0.85f, 0.92f, 1f, 0.85f);
                        handleImg.raycastTarget = false;
                    }
                }
            }

            if (handleRect != null)
            {
                handleRect.pivot = new Vector2(0.5f, 0.5f);
                handleRect.anchorMin = new Vector2(0.5f, 0.5f);
                handleRect.anchorMax = new Vector2(0.5f, 0.5f);
                handleRect.anchoredPosition = Vector2.zero;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null && backgroundRect != null)
                {
                    canvasGroup = backgroundRect.GetComponent<CanvasGroup>();
                }
            }

            SetAlpha(idleAlpha);
        }

        public void SetReferences(RectTransform bg, RectTransform handle, CanvasGroup group = null)
        {
            backgroundRect = bg;
            handleRect = handle;
            canvasGroup = group;
            InitializeComponents();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (debugIsPointerDown && activePointerId != eventData.pointerId)
            {
                return;
            }

            activePointerId = eventData.pointerId;
            debugIsPointerDown = true;
            SetAlpha(activeAlpha);

            if (backgroundRect == null)
            {
                return;
            }

            if (joystickMode == JoystickMode.Floating)
            {
                // Convert screen touch to parent's local space and update localPosition
                RectTransform parentRect = backgroundRect.parent as RectTransform;
                if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    targetCamera,
                    out Vector2 localPoint))
                {
                    backgroundRect.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
                }
            }

            // Immediately calculate handle offset for smooth start
            UpdateHandlePosition(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!debugIsPointerDown || eventData.pointerId != activePointerId)
            {
                return;
            }
            UpdateHandlePosition(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId && activePointerId != int.MinValue)
            {
                return;
            }

            activePointerId = int.MinValue;
            debugIsPointerDown = false;
            currentInput = Vector2.zero;
            debugInput = Vector2.zero;

            if (handleRect != null)
            {
                handleRect.anchoredPosition = Vector2.zero;
            }

            if (joystickMode == JoystickMode.Floating && resetPositionOnRelease && backgroundRect != null)
            {
                backgroundRect.anchoredPosition = defaultAnchoredPosition;
            }

            SetAlpha(idleAlpha);
        }

        private void UpdateHandlePosition(Vector2 screenPosition)
        {
            if (backgroundRect == null || handleRect == null)
            {
                return;
            }

            // Convert pointer screen position to local space inside backgroundRect
            // Since backgroundRect has pivot (0.5, 0.5), (0, 0) is precisely the center
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                backgroundRect,
                screenPosition,
                targetCamera,
                out Vector2 localInBg))
            {
                MobaMovementMath.ProcessHandleDisplacement(
                    localInBg,
                    handleRange,
                    deadzone,
                    out Vector2 handlePos,
                    out Vector2 moveInput);

                handleRect.anchoredPosition = handlePos;
                currentInput = moveInput;
                debugInput = currentInput;
            }
        }

        private void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
        }
    }
}
