using NUnit.Framework;
using UnityEngine;
using Moba.Runtime.UI;

namespace Moba.Tests
{
    [TestFixture]
    public class MainMenuLogicTests
    {
        [Test]
        [TestCase(1920f, 1080f, 2752f, 1536f)] // 16:9 标准宽屏
        [TestCase(2400f, 1080f, 2752f, 1536f)] // 20:9 现代长屏手机 (Redmi K70 等)
        [TestCase(3040f, 1440f, 2752f, 1536f)] // 19:9 2K 旗舰机
        [TestCase(2560f, 1080f, 2752f, 1536f)] // 21:9 超宽带鱼屏
        [TestCase(2048f, 1536f, 2752f, 1536f)] // 4:3 平板电脑
        public void BackgroundEnvelope_OnVariousAspectRatios_GuaranteesZeroBlackBars(
            float screenW, float screenH, float imgW, float imgH)
        {
            Vector2 envelopeSize = MainMenuLogic.CalculateBackgroundEnvelopeDimensions(
                screenW, screenH, imgW, imgH, marginFactor: 1.02f);

            // 验证宽度与高度都严格大于或等于屏幕尺寸（绝不会露出黑边）
            Assert.That(envelopeSize.x, Is.GreaterThanOrEqualTo(screenW), $"宽度不足可能产生黑边: {envelopeSize.x} < {screenW}");
            Assert.That(envelopeSize.y, Is.GreaterThanOrEqualTo(screenH), $"高度不足可能产生黑边: {envelopeSize.y} < {screenH}");

            // 验证辅助函数判定通过
            Assert.That(MainMenuLogic.VerifyNoBlackBars(envelopeSize, screenW, screenH), Is.True);

            // 验证原始宽高比保持不变（无拉伸形变）
            float originalAspect = imgW / imgH;
            float envelopeAspect = envelopeSize.x / envelopeSize.y;
            Assert.That(envelopeAspect, Is.EqualTo(originalAspect).Within(0.001f));
        }

        [Test]
        public void LoadingProgress_SmoothInterpolation_FollowsAsyncAndMinDuration()
        {
            float minDuration = 2.0f;

            // 0 秒时进度为 0%
            float p0 = MainMenuLogic.CalculateSmoothLoadingProgress(0.0f, 0.0f, minDuration);
            Assert.That(p0, Is.EqualTo(0f));

            // 1.0 秒时（时间过半），即使底层异步瞬间到 0.9（即准备完成），展示进度也受时间平滑限制为 50%
            float p1 = MainMenuLogic.CalculateSmoothLoadingProgress(0.9f, 1.0f, minDuration);
            Assert.That(p1, Is.EqualTo(0.5f).Within(0.01f));

            // 2.0 秒时（时间到达），且异步为 0.9/1.0，展示进度到达 100%
            float p2 = MainMenuLogic.CalculateSmoothLoadingProgress(0.9f, 2.0f, minDuration);
            Assert.That(p2, Is.EqualTo(1.0f).Within(0.01f));

            // 格式化文本检验
            string textHalf = MainMenuLogic.FormatLoadingStatusText(0.5f);
            Assert.That(textHalf, Does.Contain("50%"));

            string textComplete = MainMenuLogic.FormatLoadingStatusText(1.0f);
            Assert.That(textComplete, Does.Contain("加载完毕"));
        }

        [Test]
        public void SliderConfiguration_NoHandleAndGoldenOrangeColor_ValidatesCorrectly()
        {
            // 正常配置：无 handle，金偏橘色
            Color goldenOrange = MainMenuLogic.GoldenOrangePrimary;
            bool valid = MainMenuLogic.ValidateSliderConfiguration(hasHandle: false, goldenOrange, out string reason);
            Assert.That(valid, Is.True, reason);

            // 非法配置1：带有 handle 小圆圈
            bool withHandle = MainMenuLogic.ValidateSliderConfiguration(hasHandle: true, goldenOrange, out string handleReason);
            Assert.That(withHandle, Is.False);
            Assert.That(handleReason, Does.Contain("不得包含 handle"));

            // 非法配置2：使用冷蓝色而不是金色偏橘色
            Color coldBlue = new Color(0.1f, 0.5f, 1.0f, 1.0f);
            bool badColor = MainMenuLogic.ValidateSliderConfiguration(hasHandle: false, coldBlue, out string colorReason);
            Assert.That(badColor, Is.False);
            Assert.That(colorReason, Does.Contain("必须是金色偏橘色"));
        }

        [Test]
        public void GoldenOrangeThemeColors_MeetMobaColorPaletteSpecifications()
        {
            Color primary = MainMenuLogic.GoldenOrangePrimary;
            Color title = MainMenuLogic.GoldenYellowTitle;
            Color accent = MainMenuLogic.GoldenOrangeAccent;

            // 红色通道充足（> 0.9），绿色通道中等偏高（呈现金黄与橙色），蓝色通道较低
            Assert.That(primary.r, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(primary.g, Is.GreaterThan(0.55f).And.LessThan(0.75f));
            Assert.That(primary.b, Is.LessThan(0.25f));

            Assert.That(title.r, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(title.g, Is.GreaterThan(0.8f)); // 标题偏亮金黄

            Assert.That(accent.r, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(accent.g, Is.LessThan(0.6f)); // 重色偏橘红橙
        }
    }
}
