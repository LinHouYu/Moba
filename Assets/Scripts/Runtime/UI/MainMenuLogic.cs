using UnityEngine;

namespace Moba.Runtime.UI
{
    /// <summary>
    /// 主界面 UI 纯逻辑与算法类（可直接脱离 MonoBehaviour 进行完备单元测试）：
    /// 1. 背景防黑边自适应尺寸算法（Envelope Parent 保证任意长宽比无黑边）
    /// 2. 平滑加载进度计算（融合真实异步加载与最小展示时间）
    /// 3. 金色偏橘色 MOBA 主题配色方案定义
    /// 4. 进度条无小圆圈配置校验
    /// </summary>
    public static class MainMenuLogic
    {
        // 核心色板：王者/MOBA 经典金色偏橘色
        public static readonly Color GoldenOrangePrimary = new Color(1.0f, 0.65f, 0.12f, 1.0f);     // 金偏橘主色 (#FFA61F)
        public static readonly Color GoldenYellowTitle = new Color(1.0f, 0.88f, 0.45f, 1.0f);       // 标题亮金色 (#FFE073)
        public static readonly Color GoldenOrangeAccent = new Color(1.0f, 0.50f, 0.05f, 1.0f);      // 深金橘重色 (#FF800D)
        public static readonly Color SliderBackgroundTrack = new Color(0.12f, 0.09f, 0.06f, 0.85f);  // 进度条深暗底槽色
        public static readonly Color SliderFillGlow = new Color(1.0f, 0.72f, 0.18f, 1.0f);         // 进度条发光金橘色

        /// <summary>
        /// 计算背景图完全填满屏幕（Envelope Parent）所需的宽高，确保任何屏幕比例下都不会出现左右或上下黑边。
        /// </summary>
        /// <param name="screenWidth">当前屏幕或 Canvas 宽度</param>
        /// <param name="screenHeight">当前屏幕或 Canvas 高度</param>
        /// <param name="imageWidth">图片原始宽度（例如 2752）</param>
        /// <param name="imageHeight">图片原始高度（例如 1536）</param>
        /// <param name="marginFactor">防亚像素缝隙的安全余量（默认 1.02，即多放大 2%）</param>
        /// <returns>计算后能够完全覆盖屏幕的 RectTransform 尺寸</returns>
        public static Vector2 CalculateBackgroundEnvelopeDimensions(
            float screenWidth,
            float screenHeight,
            float imageWidth,
            float imageHeight,
            float marginFactor = 1.02f)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || imageWidth <= 0f || imageHeight <= 0f)
            {
                return new Vector2(Mathf.Max(1f, screenWidth), Mathf.Max(1f, screenHeight));
            }

            float scaleX = screenWidth / imageWidth;
            float scaleY = screenHeight / imageHeight;
            float bestScale = Mathf.Max(scaleX, scaleY) * Mathf.Max(1.0f, marginFactor);

            return new Vector2(imageWidth * bestScale, imageHeight * bestScale);
        }

        /// <summary>
        /// 计算平滑加载进度（将 Unity AsyncOperation 的 0~0.9 映射为 0~1.0，并结合最小过渡时间，防止瞬间闪过）。
        /// </summary>
        /// <param name="asyncProgress">Unity 异步操作原始进度 (0.0f ~ 0.9f 载入中, 1.0f 准备就绪)</param>
        /// <param name="elapsedTime">自开始加载后已流逝的真实秒数</param>
        /// <param name="minDuration">平滑加载条最小展示秒数（例如 1.8 秒）</param>
        /// <returns>平滑展示的进度值 (0.0f ~ 1.0f)</returns>
        public static float CalculateSmoothLoadingProgress(float asyncProgress, float elapsedTime, float minDuration)
        {
            // Unity 异步加载 progress 规范：0.0 ~ 0.9 表示读取资源完成，0.9 ~ 1.0 表示激活场景
            float normalizedAsync = asyncProgress >= 0.9f ? 1.0f : Mathf.Clamp01(asyncProgress / 0.9f);

            float timeRatio = minDuration > 0.001f ? Mathf.Clamp01(elapsedTime / minDuration) : 1.0f;

            // 加载条必须同时满足真实异步进度与平滑过渡时间
            return Mathf.Min(normalizedAsync, timeRatio);
        }

        /// <summary>
        /// 格式化进度条状态文本。
        /// </summary>
        public static string FormatLoadingStatusText(float progress)
        {
            int percent = Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);
            if (percent < 100)
            {
                return $"正在加载对战战场... {percent}%";
            }
            return "加载完毕，即将进入对战！";
        }

        /// <summary>
        /// 校验 Slider 配置是否符合“不要有 slider 那个⚪（小圆圈手柄）”且颜色为金色偏橘色。
        /// </summary>
        public static bool ValidateSliderConfiguration(bool hasHandle, Color fillColor, out string failureReason)
        {
            if (hasHandle)
            {
                failureReason = "Slider 不得包含 handle 小圆圈手柄";
                return false;
            }

            // 验证填充颜色是否符合黄色偏橘色（Red > Green 且 Blue 较低，呈现暖金/橙黄色）
            bool isGoldenOrange = fillColor.r > 0.8f && fillColor.g >= 0.45f && fillColor.g <= 0.85f && fillColor.b <= 0.35f;
            if (!isGoldenOrange)
            {
                failureReason = $"填充色必须是金色偏橘色，当前颜色: R={fillColor.r:0.00}, G={fillColor.g:0.00}, B={fillColor.b:0.00}";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// 校验背景覆盖率是否满足“完全防止黑边出现”。
        /// </summary>
        public static bool VerifyNoBlackBars(Vector2 bgSize, float screenWidth, float screenHeight)
        {
            return bgSize.x >= screenWidth && bgSize.y >= screenHeight;
        }
    }
}
