using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Moba.Runtime.Movement;

namespace Moba.Runtime.Input
{
    public enum JoystickMode
    {
        Floating, // Honors of Kings style: touches anywhere in touch zone center the joystick
        Fixed     // Stays in a fixed anchored position
    }

    /// <summary>
    /// uGUI-based mobile virtual joystick with support for Floating and Fixed modes.
    /// Implements IJoystickInputProvider.
    /// </summary>
    public class MobaVirtualJoystickUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IJoystickInputProvider
    {
        [Header("Components")]
        [SerializeField] private RectTransform backgroundRect;
        [SerializeField] private RectTransform handleRect;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private JoystickMode joystickMode = JoystickMode.Floating;
        [SerializeField] private float handleRange = 100f;
        [SerializeField] [Range(0f, 0.5f)] private float deadzone = 0.12f;
        [SerializeField] private float idleAlpha = 0.6f;
        [SerializeField] private float activeAlpha = 1.0f;

        private Vector2 defaultBackgroundAnchoredPosition;
        private Camera targetCamera;
        private Canvas rootCanvas;
        private Vector2 currentInput = Vector2.zero;
        private bool isPointerDown = false;

        public Vector2 MoveInput => currentInput;
        public bool HasMoveInput => currentInput.sqrMagnitude > 0.0001f;

        private void Awake()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                targetCamera = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
            }

            if (backgroundRect != null)
            {
                defaultBackgroundAnchoredPosition = backgroundRect.anchoredPosition;
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
            if (backgroundRect != null)
            {
                defaultBackgroundAnchoredPosition = backgroundRect.anchoredPosition;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPointerDown = true;
            SetAlpha(activeAlpha);

            if (joystickMode == JoystickMode.Floating && backgroundRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)backgroundRect.parent,
                    eventData.position,
                    targetCamera,
                    out Vector2 localPoint);

                backgroundRect.anchoredPosition = localPoint;
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (backgroundRect == null || handleRect == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                backgroundRect,
                eventData.position,
                targetCamera,
                out Vector2 localPoint);

            // Clamp handle displacement to handleRange
            float distance = localPoint.magnitude;
            Vector2 direction = distance > 0.0001f ? localPoint / distance : Vector2.zero;
            float clampedDist = Mathf.Min(distance, handleRange);

            handleRect.anchoredPosition = direction * clampedDist;

            // Normalize raw vector in [-1, 1]
            Vector2 rawInput = direction * (clampedDist / handleRange);

            // Apply deadzone & smooth curve
            currentInput = MobaMovementMath.ApplyDeadzone(rawInput, deadzone);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPointerDown = false;
            currentInput = Vector2.zero;

            if (handleRect != null)
            {
                handleRect.anchoredPosition = Vector2.zero;
            }

            if (joystickMode == JoystickMode.Floating && backgroundRect != null)
            {
                backgroundRect.anchoredPosition = defaultBackgroundAnchoredPosition;
            }

            SetAlpha(idleAlpha);
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
