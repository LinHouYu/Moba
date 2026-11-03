using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Moba.Runtime.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Moba.Editor
{
    /// <summary>
    /// 主界面 Main.unity 自动化构建器：
    /// 1. 顶部专业 MOBA 字体【翼龙农药】标题
    /// 2. 背景自动拉伸铺满全屏（Envelope Parent 防黑边，支持 main.jfif，移除导致死循环冲突的 AspectRatioFitter）
    /// 3. 中间居中无边框【开始游戏】按钮（确保点击层级与射线穿透正常）
    /// 4. 全屏与按钮点击金橘色粒子光环反馈（兼容新输入系统）
    /// 5. 金偏橘色进度条（无小圆圈⚪手柄）与异步场景加载
    /// 6. 严禁锁定 Scene 视图相机，默认保持 3D 透视模式，支持右键 + WASD 自由漫游
    /// </summary>
    public static class MobaMainMenuBuilder
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string FontAssetPath = "Assets/UI/NKGMoba/Fonts/SourceHanSansCN-Medium.otf";
        private const string RingSpritePath = "Assets/UI/NKGMoba/Sprites/Ring_Clean.png";
        private const string MainJfifPath = "Assets/Resources/main.jfif";
        private const string StartSoundPath = "Assets/Audio/UI_StartGame.wav";
        private const string TriggerFile = "Temp/build_main_scene.trigger";

        [InitializeOnLoadMethod]
        private static void AutoRegisterMainScene()
        {
            EditorApplication.update -= PollBuildTrigger;
            EditorApplication.update += PollBuildTrigger;

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                UnlockSceneViewCamera();

                if (!File.Exists(MainScenePath))
                {
                    Debug.Log("[MobaMainMenuBuilder] 检测到 Main.unity 不存在，自动执行生成...");
                    BuildMainScene();
                }
                else
                {
                    EnsureBuildSettingsConfigured();
                }
            };
        }

        private static void PollBuildTrigger()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (!File.Exists(TriggerFile)) return;

            try { File.Delete(TriggerFile); } catch { }
            BuildMainScene();
        }

        [MenuItem("Moba/Unlock Scene View Camera (解锁Scene相机WASD自由漫游)")]
        public static void UnlockSceneViewCamera()
        {
            foreach (SceneView sv in SceneView.sceneViews)
            {
                if (sv != null)
                {
                    sv.in2DMode = false;
                    sv.isRotationLocked = false;
                    sv.orthographic = false;
                    sv.Repaint();
                }
            }
            Debug.Log("[MobaMainMenuBuilder] 已恢复 Scene 视图为 3D 透视模式并解锁相机旋转！鼠标右键 + WASD 漫游功能完全正常。");
        }

        [MenuItem("Moba/Build Main Menu Scene (生成 Main.unity)")]
        public static void BuildMainScene()
        {
            EnsureMainJfifImported();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Main";

            Font uiFont = AssetDatabase.LoadAssetAtPath<Font>(FontAssetPath)
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Sprite ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingSpritePath);
            Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MainJfifPath);

            // 1. Main Camera (标准 3D 透视相机，绝不锁定 Scene 视图或强制 2D 模式)
            GameObject camGo = new GameObject("Main_Camera", typeof(Camera), typeof(AudioListener));
            Camera cam = camGo.GetComponent<Camera>();
            camGo.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
            cam.orthographic = false;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // 2. EventSystem
            GameObject eventSystemGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif

            // 3. Canvas (Screen Space - Overlay)
            GameObject canvasGo = new GameObject("Main_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 4. Background (自适应铺满全屏防黑边，由 MainMenuBackgroundFitter 单独管理，严禁挂载 AspectRatioFitter 造成冲突死循环)
            GameObject bgGo = new GameObject("Background_Image", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(MainMenuBackgroundFitter));
            bgGo.transform.SetParent(canvasGo.transform, false);

            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0.5f, 0.5f);
            bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(2752f, 1536f);

            UnityEngine.UI.Image bgImg = bgGo.GetComponent<UnityEngine.UI.Image>();
            bgImg.sprite = bgSprite;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;

            MainMenuBackgroundFitter fitter = bgGo.GetComponent<MainMenuBackgroundFitter>();
            fitter.EnsureBackgroundLoaded();
            fitter.ApplyEnvelopeFit();

            // 5. Dark Vignette Overlay
            GameObject overlayGo = new GameObject("Vignette_Overlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            overlayGo.transform.SetParent(canvasGo.transform, false);
            RectTransform overlayRt = overlayGo.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;

            UnityEngine.UI.Image overlayImg = overlayGo.GetComponent<UnityEngine.UI.Image>();
            overlayImg.color = new Color(0.04f, 0.05f, 0.08f, 0.32f);
            overlayImg.raycastTarget = false;

            // 6. Click Particles Overlay (置于背景上方、按钮下方，绝不阻挡按钮交互)
            GameObject clickFxGo = new GameObject("ClickEffect_Overlay", typeof(RectTransform), typeof(MainMenuClickEffect));
            clickFxGo.transform.SetParent(canvasGo.transform, false);

            RectTransform clickFxRt = clickFxGo.GetComponent<RectTransform>();
            clickFxRt.anchorMin = Vector2.zero;
            clickFxRt.anchorMax = Vector2.one;
            clickFxRt.offsetMin = Vector2.zero;
            clickFxRt.offsetMax = Vector2.zero;

            MainMenuClickEffect clickFx = clickFxGo.GetComponent<MainMenuClickEffect>();
            clickFx.SetSprites(ringSprite, null);

            // 7. Top Title (翼龙农药)
            GameObject titleGo = new GameObject("Top_Title", typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Outline), typeof(UnityEngine.UI.Shadow));
            titleGo.transform.SetParent(canvasGo.transform, false);

            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1.0f);
            titleRt.anchorMax = new Vector2(0.5f, 1.0f);
            titleRt.pivot = new Vector2(0.5f, 1.0f);
            titleRt.anchoredPosition = new Vector2(0f, -85f);
            titleRt.sizeDelta = new Vector2(960f, 160f);

            UnityEngine.UI.Text titleTxt = titleGo.GetComponent<UnityEngine.UI.Text>();
            titleTxt.font = uiFont;
            titleTxt.text = "翼龙农药";
            titleTxt.fontSize = 82;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = MainMenuLogic.GoldenYellowTitle;
            titleTxt.raycastTarget = false;

            UnityEngine.UI.Outline titleOutline = titleGo.GetComponent<UnityEngine.UI.Outline>();
            titleOutline.effectColor = new Color(0.14f, 0.06f, 0.01f, 0.96f);
            titleOutline.effectDistance = new Vector2(4.5f, -4.5f);

            UnityEngine.UI.Shadow titleShadow = titleGo.GetComponent<UnityEngine.UI.Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.88f);
            titleShadow.effectDistance = new Vector2(6f, -6f);

            // 8. Center Start Game Button (居中文字开始游戏，无按钮框)
            GameObject startBtnGo = new GameObject("Center_StartGameButton", typeof(RectTransform), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
            startBtnGo.transform.SetParent(canvasGo.transform, false);

            RectTransform startBtnRt = startBtnGo.GetComponent<RectTransform>();
            startBtnRt.anchorMin = new Vector2(0.5f, 0.44f);
            startBtnRt.anchorMax = new Vector2(0.5f, 0.44f);
            startBtnRt.pivot = new Vector2(0.5f, 0.5f);
            startBtnRt.anchoredPosition = Vector2.zero;
            startBtnRt.sizeDelta = new Vector2(540f, 140f);

            UnityEngine.UI.Image startBtnImg = startBtnGo.GetComponent<UnityEngine.UI.Image>();
            startBtnImg.color = new Color(1f, 1f, 1f, 0.001f); // 极低透明度保证百分百捕获点击，且人眼看不到任何边框
            startBtnImg.raycastTarget = true;

            UnityEngine.UI.Button startBtn = startBtnGo.GetComponent<UnityEngine.UI.Button>();
            startBtn.targetGraphic = startBtnImg;

            CanvasGroup startBtnCg = startBtnGo.GetComponent<CanvasGroup>();

            // 按钮子文字：开始游戏
            GameObject startTxtGo = new GameObject("Text_StartGame", typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Outline), typeof(UnityEngine.UI.Shadow));
            startTxtGo.transform.SetParent(startBtnGo.transform, false);

            RectTransform startTxtRt = startTxtGo.GetComponent<RectTransform>();
            startTxtRt.anchorMin = Vector2.zero;
            startTxtRt.anchorMax = Vector2.one;
            startTxtRt.offsetMin = Vector2.zero;
            startTxtRt.offsetMax = Vector2.zero;

            UnityEngine.UI.Text startTxt = startTxtGo.GetComponent<UnityEngine.UI.Text>();
            startTxt.font = uiFont;
            startTxt.text = "开始游戏";
            startTxt.fontSize = 58;
            startTxt.fontStyle = FontStyle.Bold;
            startTxt.alignment = TextAnchor.MiddleCenter;
            startTxt.color = MainMenuLogic.GoldenOrangePrimary; // 黄色偏橘色设定
            startTxt.raycastTarget = false;

            UnityEngine.UI.Outline startTxtOutline = startTxtGo.GetComponent<UnityEngine.UI.Outline>();
            startTxtOutline.effectColor = new Color(0.12f, 0.05f, 0.01f, 0.95f);
            startTxtOutline.effectDistance = new Vector2(3.5f, -3.5f);

            UnityEngine.UI.Shadow startTxtShadow = startTxtGo.GetComponent<UnityEngine.UI.Shadow>();
            startTxtShadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            startTxtShadow.effectDistance = new Vector2(4f, -4f);

            // 9. Loading Container & Golden-Orange Slider (点击后出现，无小圆圈⚪手柄)
            GameObject loadingContainerGo = new GameObject("Loading_Container", typeof(RectTransform));
            loadingContainerGo.transform.SetParent(canvasGo.transform, false);

            RectTransform loadingContainerRt = loadingContainerGo.GetComponent<RectTransform>();
            loadingContainerRt.anchorMin = new Vector2(0.5f, 0.44f);
            loadingContainerRt.anchorMax = new Vector2(0.5f, 0.44f);
            loadingContainerRt.pivot = new Vector2(0.5f, 0.5f);
            loadingContainerRt.anchoredPosition = Vector2.zero;
            loadingContainerRt.sizeDelta = new Vector2(740f, 100f);

            // 加载状态提示文字
            GameObject statusTxtGo = new GameObject("Loading_StatusText", typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Outline));
            statusTxtGo.transform.SetParent(loadingContainerGo.transform, false);

            RectTransform statusTxtRt = statusTxtGo.GetComponent<RectTransform>();
            statusTxtRt.anchorMin = new Vector2(0f, 1f);
            statusTxtRt.anchorMax = new Vector2(1f, 1f);
            statusTxtRt.pivot = new Vector2(0.5f, 1f);
            statusTxtRt.anchoredPosition = new Vector2(0f, 4f);
            statusTxtRt.sizeDelta = new Vector2(0f, 42f);

            UnityEngine.UI.Text statusTxt = statusTxtGo.GetComponent<UnityEngine.UI.Text>();
            statusTxt.font = uiFont;
            statusTxt.text = "正在加载对战战场... 0%";
            statusTxt.fontSize = 24;
            statusTxt.fontStyle = FontStyle.Bold;
            statusTxt.alignment = TextAnchor.MiddleCenter;
            statusTxt.color = MainMenuLogic.GoldenYellowTitle;
            statusTxt.raycastTarget = false;

            UnityEngine.UI.Outline statusOutline = statusTxtGo.GetComponent<UnityEngine.UI.Outline>();
            statusOutline.effectColor = new Color(0.1f, 0.04f, 0.01f, 0.9f);
            statusOutline.effectDistance = new Vector2(1.8f, -1.8f);

            // Slider 实体 (无⚪)
            GameObject sliderGo = new GameObject("Loading_Slider", typeof(RectTransform), typeof(UnityEngine.UI.Slider));
            sliderGo.transform.SetParent(loadingContainerGo.transform, false);

            RectTransform sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.5f, 0f);
            sliderRt.anchorMax = new Vector2(0.5f, 0f);
            sliderRt.pivot = new Vector2(0.5f, 0f);
            sliderRt.anchoredPosition = new Vector2(0f, 10f);
            sliderRt.sizeDelta = new Vector2(700f, 24f);

            UnityEngine.UI.Slider slider = sliderGo.GetComponent<UnityEngine.UI.Slider>();
            slider.transition = UnityEngine.UI.Selectable.Transition.None;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;

            // Slider 背景槽
            GameObject sliderBgGo = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
            sliderBgGo.transform.SetParent(sliderGo.transform, false);

            RectTransform sliderBgRt = sliderBgGo.GetComponent<RectTransform>();
            sliderBgRt.anchorMin = Vector2.zero;
            sliderBgRt.anchorMax = Vector2.one;
            sliderBgRt.offsetMin = Vector2.zero;
            sliderBgRt.offsetMax = Vector2.zero;

            UnityEngine.UI.Image sliderBgImg = sliderBgGo.GetComponent<UnityEngine.UI.Image>();
            sliderBgImg.color = MainMenuLogic.SliderBackgroundTrack;
            sliderBgImg.raycastTarget = false;

            UnityEngine.UI.Outline sliderBgOutline = sliderBgGo.GetComponent<UnityEngine.UI.Outline>();
            sliderBgOutline.effectColor = new Color(1.0f, 0.65f, 0.12f, 0.45f);
            sliderBgOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Slider Fill Area & Fill Bar (金色偏橘色)
            GameObject fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(sliderGo.transform, false);

            RectTransform fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = new Vector2(2f, 2f);
            fillAreaRt.offsetMax = new Vector2(-2f, -2f);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);

            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.sizeDelta = Vector2.zero;

            UnityEngine.UI.Image fillImg = fillGo.GetComponent<UnityEngine.UI.Image>();
            fillImg.color = MainMenuLogic.GoldenOrangePrimary; // 核心要求：金色偏橘色
            fillImg.raycastTarget = false;

            slider.fillRect = fillRt;
            slider.handleRect = null; // 核心要求：不要有 slider 那个⚪

            loadingContainerGo.SetActive(false); // 初始时隐藏

            // 10. MainMenuController (挂在 Canvas 上)
            AudioClip startAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(StartSoundPath);
            AudioSource audioSource = canvasGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            MainMenuController controller = canvasGo.AddComponent<MainMenuController>();
            controller.BindComponents(
                titleTxt,
                startBtn,
                startTxt,
                startBtnCg,
                loadingContainerGo,
                slider,
                fillImg,
                sliderBgImg,
                statusTxt,
                clickFx,
                audioSource,
                startAudio);

            // 保存场景文件
            string scenesDir = Path.GetDirectoryName(MainScenePath);
            if (!Directory.Exists(scenesDir)) Directory.CreateDirectory(scenesDir);

            EditorSceneManager.SaveScene(scene, MainScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EnsureBuildSettingsConfigured();
            UnlockSceneViewCamera();

            Debug.Log($"[MobaMainMenuBuilder] 成功生成主界面场景: {MainScenePath} 并配置 Build Settings！");
        }

        private static void EnsureMainJfifImported()
        {
            if (File.Exists(MainJfifPath))
            {
                AssetDatabase.ImportAsset(MainJfifPath, ImportAssetOptions.ForceUpdate);
            }
        }

        public static void EnsureBuildSettingsConfigured()
        {
            var scenes = EditorBuildSettings.scenes;
            var targetScenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };

            bool changed = scenes.Length < 2
                || scenes[0].path != MainScenePath
                || scenes[1].path != GameScenePath;

            if (changed)
            {
                EditorBuildSettings.scenes = targetScenes;
                Debug.Log("[MobaMainMenuBuilder] 已更新 BuildSettings 场景列表：[0] Main.unity, [1] Game.unity");
            }
        }
    }
}
