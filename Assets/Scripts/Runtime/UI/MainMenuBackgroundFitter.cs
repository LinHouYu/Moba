using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Moba.Runtime.UI
{
    /// <summary>
    /// 主界面背景自适应组件：
    /// 自动根据当前屏幕分辨率与图片原始尺寸（2752x1536），通过 Envelope 模式自适应拉伸铺满全屏，
    /// 彻底防止在任意比例屏幕（16:9, 18:9, 19.5:9, 20:9, 21:9 超宽屏, 4:3 平板）上出现黑边。
    /// 包含完备的防重入与变化阈值校验，彻底避免引发 UGUI 布局死循环卡死。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class MainMenuBackgroundFitter : MonoBehaviour
    {
        [Header("Target Image (背景图片组件)")]
        [SerializeField] private UnityEngine.UI.Image targetImage;
        [SerializeField] private UnityEngine.UI.RawImage targetRawImage;

        [Header("Original Image Dimensions (main.jfif 原始分辨率: 2752x1536)")]
        [SerializeField] private float originalWidth = 2752f;
        [SerializeField] private float originalHeight = 1536f;

        [Header("Fit Settings")]
        [Tooltip("防止亚像素黑边缝隙的安全多余放大比例")]
        [SerializeField] [Range(1.0f, 1.15f)] private float safetyMargin = 1.025f;

        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private Vector2 lastScreenSize = Vector2.zero;
        private bool isApplyingFit = false;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();
            EnsureBackgroundLoaded();
            ApplyEnvelopeFit();
        }

        private void Start()
        {
            ApplyEnvelopeFit();
        }

        private void Update()
        {
            Vector2 currentSize = new Vector2(Screen.width, Screen.height);
            if (currentSize != lastScreenSize)
            {
                lastScreenSize = currentSize;
                ApplyEnvelopeFit();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            // 防重入：尺寸变更时仅在屏幕/画布尺寸真实改变时触发，严禁无条件重设 sizeDelta
            if (isApplyingFit) return;

            Vector2 currentSize = new Vector2(Screen.width, Screen.height);
            if (currentSize != lastScreenSize)
            {
                lastScreenSize = currentSize;
                ApplyEnvelopeFit();
            }
        }

        /// <summary>
        /// 确保 main.jfif 图片成功加载并赋予背景组件。
        /// </summary>
        public void EnsureBackgroundLoaded()
        {
            if (targetImage == null)
            {
                targetImage = GetComponent<UnityEngine.UI.Image>();
            }
            if (targetRawImage == null)
            {
                targetRawImage = GetComponent<UnityEngine.UI.RawImage>();
            }

            bool hasSprite = targetImage != null && targetImage.sprite != null;
            bool hasTexture = targetRawImage != null && targetRawImage.texture != null;
            if (hasSprite || hasTexture) return;

            // 1. 尝试从 Resources.Load<Sprite>("main") 读取已导入的 Sprite
            Sprite loadedSprite = Resources.Load<Sprite>("main");
            if (loadedSprite != null)
            {
                if (targetImage != null) targetImage.sprite = loadedSprite;
                return;
            }

            // 2. 尝试从磁盘路径加载 main.jfif 二进制字节直接创建 Texture2D/Sprite (编辑器与本地 PC 保底)
            string[] possiblePaths = new string[]
            {
                Path.Combine(Application.dataPath, "Resources", "main.jfif"),
                Path.Combine(Application.dataPath, "..", "Assets", "Resources", "main.jfif"),
                "Assets/Resources/main.jfif"
            };

            foreach (var p in possiblePaths)
            {
                string fullPath = Path.GetFullPath(p);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        byte[] fileBytes = File.ReadAllBytes(fullPath);
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (tex.LoadImage(fileBytes))
                        {
                            tex.name = "main_jfif_runtime";
                            originalWidth = tex.width;
                            originalHeight = tex.height;

                            if (targetImage != null)
                            {
                                targetImage.sprite = Sprite.Create(
                                    tex,
                                    new Rect(0, 0, tex.width, tex.height),
                                    new Vector2(0.5f, 0.5f),
                                    100f,
                                    0,
                                    SpriteMeshType.FullRect);
                            }
                            else if (targetRawImage != null)
                            {
                                targetRawImage.texture = tex;
                            }
                            return;
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 执行 Envelope Parent 全屏防黑边拉伸对齐（带防死循环重入保护）。
        /// </summary>
        public void ApplyEnvelopeFit()
        {
            if (isApplyingFit) return;

            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null) return;

            try
            {
                isApplyingFit = true;

                // 锚点锁定在正中心 (0.5, 0.5)
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = Vector2.zero;

                if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();

                float viewWidth = Screen.width;
                float viewHeight = Screen.height;

                if (rootCanvas != null)
                {
                    var canvasRect = rootCanvas.GetComponent<RectTransform>();
                    if (canvasRect != null && canvasRect.rect.width > 0 && canvasRect.rect.height > 0)
                    {
                        viewWidth = canvasRect.rect.width;
                        viewHeight = canvasRect.rect.height;
                    }
                }

                Vector2 newSize = MainMenuLogic.CalculateBackgroundEnvelopeDimensions(
                    viewWidth,
                    viewHeight,
                    originalWidth,
                    originalHeight,
                    safetyMargin);

                // 仅在尺寸差值大于 0.5 像素时才进行实质性赋值，彻底阻断任何布局重算死循环
                if (Vector2.Distance(rectTransform.sizeDelta, newSize) > 0.5f)
                {
                    rectTransform.sizeDelta = newSize;
                }
            }
            finally
            {
                isApplyingFit = false;
            }
        }
    }
}
