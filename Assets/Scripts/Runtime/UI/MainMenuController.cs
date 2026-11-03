using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Moba.Runtime.UI
{
    /// <summary>
    /// 主界面控制器：
    /// 1. 顶部 MOBA 风格标题【翼龙农药】
    /// 2. 居中无边框【开始游戏】文字按钮（整体黄色偏橘色设定）
    /// 3. 点击全屏与按钮金橘色火花光环特效
    /// 4. 点击后按钮渐隐并切换为金偏橘色进度条（无小圆圈⚪手柄）平滑异步加载 Game 场景
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Top Title (翼龙农药)")]
        [SerializeField] private UnityEngine.UI.Text titleText;
        [SerializeField] private string titleContent = "翼龙农药";

        [Header("Center Start Button (居中无按钮框)")]
        [SerializeField] private UnityEngine.UI.Button startButton;
        [SerializeField] private UnityEngine.UI.Text startButtonText;
        [SerializeField] private CanvasGroup startButtonCanvasGroup;

        [Header("Loading Slider (金色偏橘色·无小圆圈手柄)")]
        [SerializeField] private GameObject loadingContainer;
        [SerializeField] private UnityEngine.UI.Slider loadingSlider;
        [SerializeField] private UnityEngine.UI.Image sliderFillImage;
        [SerializeField] private UnityEngine.UI.Image sliderBackgroundImage;
        [SerializeField] private UnityEngine.UI.Text loadingStatusText;
        [SerializeField] [Range(0.8f, 5.0f)] private float minLoadingDuration = 1.6f;
        [SerializeField] private string targetSceneName = "Game";

        [Header("Click Particles Effect")]
        [SerializeField] private MainMenuClickEffect clickEffect;

        [Header("Audio / Feel Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip startSoundClip;
        [SerializeField] private bool triggerClickVibration = true;

        private bool isStartingGame = false;
        private Coroutine loadingCoroutine;

        public void BindComponents(
            UnityEngine.UI.Text title,
            UnityEngine.UI.Button btnStart,
            UnityEngine.UI.Text txtStart,
            CanvasGroup cgStart,
            GameObject loadContainer,
            UnityEngine.UI.Slider slider,
            UnityEngine.UI.Image fillImg,
            UnityEngine.UI.Image bgImg,
            UnityEngine.UI.Text statusTxt,
            MainMenuClickEffect fx,
            AudioSource audioSrc = null,
            AudioClip startClip = null)
        {
            titleText = title;
            startButton = btnStart;
            startButtonText = txtStart;
            startButtonCanvasGroup = cgStart;
            loadingContainer = loadContainer;
            loadingSlider = slider;
            sliderFillImage = fillImg;
            sliderBackgroundImage = bgImg;
            loadingStatusText = statusTxt;
            clickEffect = fx;
            audioSource = audioSrc;
            startSoundClip = startClip;
        }

        private void Awake()
        {
            ApplyThemeColors();
            EnforceSliderNoHandle();
            ResetInitialUIState();
        }

        private void Start()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartButtonClicked);
                startButton.onClick.AddListener(OnStartButtonClicked);
            }
        }

        /// <summary>
        /// 设定黄色偏橘色 MOBA 主题配色
        /// </summary>
        private void ApplyThemeColors()
        {
            if (titleText != null)
            {
                titleText.text = titleContent;
                titleText.color = MainMenuLogic.GoldenYellowTitle;
            }

            if (startButtonText != null)
            {
                startButtonText.color = MainMenuLogic.GoldenOrangePrimary;
            }

            if (sliderFillImage != null)
            {
                sliderFillImage.color = MainMenuLogic.GoldenOrangePrimary;
            }

            if (sliderBackgroundImage != null)
            {
                sliderBackgroundImage.color = MainMenuLogic.SliderBackgroundTrack;
            }

            if (loadingStatusText != null)
            {
                loadingStatusText.color = MainMenuLogic.GoldenYellowTitle;
            }
        }

        /// <summary>
        /// 严格遵循用户要求：不要有 slider 那个⚪（小圆圈手柄），仅保留平滑底槽与金橘色填充条。
        /// </summary>
        public void EnforceSliderNoHandle()
        {
            if (loadingSlider != null)
            {
                if (loadingSlider.handleRect != null)
                {
                    loadingSlider.handleRect.gameObject.SetActive(false);
                    loadingSlider.handleRect = null;
                }

                // 搜索名为 "Handle" 或 "Handle Slide Area" 的子物体并隐藏
                Transform handleArea = loadingSlider.transform.Find("Handle Slide Area");
                if (handleArea != null)
                {
                    handleArea.gameObject.SetActive(false);
                }
            }
        }

        private void ResetInitialUIState()
        {
            isStartingGame = false;
            if (loadingContainer != null)
            {
                loadingContainer.SetActive(false);
            }

            if (startButton != null)
            {
                startButton.gameObject.SetActive(true);
                startButton.interactable = true;
            }

            if (startButtonCanvasGroup != null)
            {
                startButtonCanvasGroup.alpha = 1.0f;
                startButtonCanvasGroup.interactable = true;
                startButtonCanvasGroup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// 点击【开始游戏】响应：
        /// 播放点击粒子效果，按钮渐隐，淡入金橘色无⚪加载条，并启动异步平滑加载 Game 场景。
        /// </summary>
        public void OnStartButtonClicked()
        {
            if (isStartingGame) return;
            isStartingGame = true;

            // 1. 触发点击粒子光环爆发与音效
            if (audioSource != null && startSoundClip != null)
            {
                audioSource.PlayOneShot(startSoundClip);
            }

            if (clickEffect != null && startButton != null)
            {
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, startButton.transform.position);
                clickEffect.SpawnClickEffect(screenPos, scaleMultiplier: 1.6f);
            }

            if (triggerClickVibration)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { Handheld.Vibrate(); } catch { }
#endif
            }

            // 2. 启动渐隐与加载协程
            if (loadingCoroutine != null) StopCoroutine(loadingCoroutine);
            loadingCoroutine = StartCoroutine(CoTransitionAndLoadGame());
        }

        private IEnumerator CoTransitionAndLoadGame()
        {
            // 按钮淡出过渡 (0.28秒)
            float fadeTimer = 0f;
            float fadeDuration = 0.28f;
            while (fadeTimer < fadeDuration)
            {
                fadeTimer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(fadeTimer / fadeDuration);
                if (startButtonCanvasGroup != null)
                {
                    startButtonCanvasGroup.alpha = 1f - t;
                }
                yield return null;
            }

            if (startButton != null)
            {
                startButton.gameObject.SetActive(false);
            }

            // 激活金色偏橘色加载条组件
            if (loadingContainer != null)
            {
                loadingContainer.SetActive(true);
            }

            if (loadingSlider != null)
            {
                loadingSlider.value = 0f;
            }

            if (loadingStatusText != null)
            {
                loadingStatusText.text = MainMenuLogic.FormatLoadingStatusText(0f);
            }

            // 启动异步场景加载
            AsyncOperation asyncOp = null;
            try
            {
                asyncOp = SceneManager.LoadSceneAsync(targetSceneName);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MainMenuController] 异步加载 {targetSceneName} 场景异常: {ex.Message}");
            }

            if (asyncOp != null)
            {
                asyncOp.allowSceneActivation = false;
            }

            float elapsed = 0f;
            float displayProgress = 0f;

            while (displayProgress < 0.999f)
            {
                elapsed += Time.unscaledDeltaTime;
                float rawAsync = asyncOp != null ? asyncOp.progress : 1.0f;
                displayProgress = MainMenuLogic.CalculateSmoothLoadingProgress(rawAsync, elapsed, minLoadingDuration);

                if (loadingSlider != null)
                {
                    loadingSlider.value = displayProgress;
                }

                if (loadingStatusText != null)
                {
                    loadingStatusText.text = MainMenuLogic.FormatLoadingStatusText(displayProgress);
                }

                // 当进度达到 100% 且底层资源读取完毕 (>= 0.9)
                if (displayProgress >= 0.995f && (asyncOp == null || asyncOp.progress >= 0.9f))
                {
                    break;
                }

                yield return null;
            }

            if (loadingSlider != null) loadingSlider.value = 1.0f;
            if (loadingStatusText != null) loadingStatusText.text = MainMenuLogic.FormatLoadingStatusText(1.0f);

            // 稍作停顿以供玩家看清 100% 完成反馈
            yield return new WaitForSecondsRealtime(0.25f);

            if (asyncOp != null)
            {
                asyncOp.allowSceneActivation = true;
            }
        }
    }
}
