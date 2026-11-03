using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Moba.Runtime.UI
{
    /// <summary>
    /// 主界面点击粒子与涟漪特效控制器：
    /// 在玩家点击屏幕或按下按钮时，在触控点生成金色偏橘色的扩散光环与散射星芒粒子，
    /// 提供纯正 MOBA 手感与视觉反馈。
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuClickEffect : MonoBehaviour, IPointerDownHandler
    {
        private class ActiveParticle
        {
            public RectTransform Rect;
            public UnityEngine.UI.Image Graphic;
            public Vector2 Velocity;
            public float Elapsed;
            public float Duration;
            public float StartScale;
            public float EndScale;
            public Color BaseColor;
            public bool IsRing;
        }

        [Header("Particle Visual Sprites")]
        [SerializeField] private Sprite rippleRingSprite;
        [SerializeField] private Sprite sparkParticleSprite;

        [Header("Colors (金色偏橘色设定)")]
        [SerializeField] private Color rippleColor = new Color(1.0f, 0.72f, 0.20f, 0.95f);
        [SerializeField] private Color sparkColorPrimary = new Color(1.0f, 0.85f, 0.35f, 1.0f);
        [SerializeField] private Color sparkColorOrange = new Color(1.0f, 0.50f, 0.10f, 1.0f);

        [Header("Settings")]
        [SerializeField] [Range(6, 20)] private int sparkCountPerClick = 10;
        [SerializeField] [Range(80f, 350f)] private float sparkSpeed = 180f;

        private RectTransform rootRect;
        private Canvas parentCanvas;
        private readonly List<ActiveParticle> activeParticles = new List<ActiveParticle>(32);
        private readonly Queue<RectTransform> particlePool = new Queue<RectTransform>(32);

        private void Awake()
        {
            rootRect = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();
            EnsureSprites();
        }

        private void EnsureSprites()
        {
            if (rippleRingSprite == null)
            {
                rippleRingSprite = Resources.Load<Sprite>("Ring_Clean");
            }
        }

        public void SetSprites(Sprite ring, Sprite spark)
        {
            rippleRingSprite = ring;
            sparkParticleSprite = spark;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            SpawnClickEffect(eventData.position);
        }

        private void Update()
        {
            // 检测全局输入（支持多点触控与鼠标左键）
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                SpawnClickEffect(UnityEngine.Input.mousePosition);
            }

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    SpawnClickEffect(touch.position);
                }
            }

            UpdateActiveParticles(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 在指定屏幕坐标生成点击光环与粒子爆发。
        /// </summary>
        public void SpawnClickEffect(Vector2 screenPosition, float scaleMultiplier = 1.0f)
        {
            if (rootRect == null) return;

            Vector2 localPoint;
            Camera eventCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? parentCanvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPosition, eventCam, out localPoint))
            {
                localPoint = screenPosition;
            }

            // 1. 生成扩散光环涟漪 (Expanding Ripple Ring)
            SpawnRippleRing(localPoint, scaleMultiplier);

            // 2. 生成向四周散射的金橘色火花粒子 (Radial Sparks)
            int count = Mathf.RoundToInt(sparkCountPerClick * scaleMultiplier);
            for (int i = 0; i < count; i++)
            {
                float angle = (i * 360f / count) + Random.Range(-15f, 15f);
                float rad = angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float speed = sparkSpeed * Random.Range(0.6f, 1.4f) * scaleMultiplier;

                Color col = (i % 2 == 0) ? sparkColorPrimary : sparkColorOrange;
                SpawnSpark(localPoint, dir * speed, col, scaleMultiplier);
            }
        }

        private void SpawnRippleRing(Vector2 localPos, float scaleMult)
        {
            var go = GetOrCreateParticleObject("RippleRing");
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = localPos;
            rt.sizeDelta = new Vector2(75f, 75f);
            rt.localScale = Vector3.one * 0.25f * scaleMult;

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = rippleRingSprite;
            img.color = rippleColor;
            img.raycastTarget = false;

            activeParticles.Add(new ActiveParticle
            {
                Rect = rt,
                Graphic = img,
                Velocity = Vector2.zero,
                Elapsed = 0f,
                Duration = 0.38f,
                StartScale = 0.25f * scaleMult,
                EndScale = 1.6f * scaleMult,
                BaseColor = rippleColor,
                IsRing = true
            });
        }

        private void SpawnSpark(Vector2 localPos, Vector2 velocity, Color color, float scaleMult)
        {
            var go = GetOrCreateParticleObject("Spark");
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = localPos;
            rt.sizeDelta = new Vector2(16f, 16f);
            rt.localScale = Vector3.one * Random.Range(0.8f, 1.3f) * scaleMult;

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sparkParticleSprite != null ? sparkParticleSprite : rippleRingSprite;
            img.color = color;
            img.raycastTarget = false;

            activeParticles.Add(new ActiveParticle
            {
                Rect = rt,
                Graphic = img,
                Velocity = velocity,
                Elapsed = 0f,
                Duration = Random.Range(0.35f, 0.52f),
                StartScale = rt.localScale.x,
                EndScale = 0.1f,
                BaseColor = color,
                IsRing = false
            });
        }

        private void UpdateActiveParticles(float dt)
        {
            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                var p = activeParticles[i];
                if (p.Rect == null)
                {
                    activeParticles.RemoveAt(i);
                    continue;
                }

                p.Elapsed += dt;
                float t = Mathf.Clamp01(p.Elapsed / p.Duration);

                if (p.IsRing)
                {
                    // 光环平滑放大变淡
                    float curScale = Mathf.Lerp(p.StartScale, p.EndScale, t);
                    p.Rect.localScale = Vector3.one * curScale;
                    Color c = p.BaseColor;
                    c.a = (1f - t * t) * p.BaseColor.a;
                    p.Graphic.color = c;
                }
                else
                {
                    // 粒子物理位移、阻尼衰减与渐变隐去
                    p.Rect.anchoredPosition += p.Velocity * dt;
                    p.Velocity *= Mathf.Pow(0.12f, dt); // 摩擦阻尼
                    float curScale = Mathf.Lerp(p.StartScale, p.EndScale, t);
                    p.Rect.localScale = Vector3.one * curScale;
                    Color c = p.BaseColor;
                    c.a = (1f - t) * p.BaseColor.a;
                    p.Graphic.color = c;
                }

                if (p.Elapsed >= p.Duration)
                {
                    RecycleParticleObject(p.Rect);
                    activeParticles.RemoveAt(i);
                }
            }
        }

        private RectTransform GetOrCreateParticleObject(string namePrefix)
        {
            if (particlePool.Count > 0)
            {
                var recycled = particlePool.Dequeue();
                recycled.gameObject.name = namePrefix;
                recycled.gameObject.SetActive(true);
                return recycled;
            }

            GameObject go = new GameObject(namePrefix, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(rootRect, false);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            return go.GetComponent<RectTransform>();
        }

        private void RecycleParticleObject(RectTransform rt)
        {
            if (rt == null) return;
            rt.gameObject.SetActive(false);
            particlePool.Enqueue(rt);
        }
    }
}
