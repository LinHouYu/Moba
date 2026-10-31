using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Moba.Runtime.Input;
using Moba.Runtime.Movement;
using Moba.Runtime.CameraSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Moba.Runtime.Bootstrap
{
    /// <summary>
    /// Automatic Whitebox Scene Bootstrap for MOBA Demo.
    /// Builds ground, obstacles, player capsule, camera follow, and virtual joystick UI at runtime.
    /// Can also be triggered in Editor.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class MobaDemoBootstrap : MonoBehaviour
    {
        [Header("Auto-Setup Options")]
        [SerializeField] private bool autoBuildOnStart = true;
        [SerializeField] private Vector3 playerSpawnPosition = new Vector3(0f, 1f, 0f);

        private GameObject playerInstance;
        private MobaVirtualJoystickUI joystickUIInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitOnPlay()
        {
            if (FindFirstObjectByType<MobaDemoBootstrap>() != null)
            {
                return;
            }

            GameObject bootstrapObj = new GameObject("[MobaDemoBootstrap]");
            var bootstrap = bootstrapObj.AddComponent<MobaDemoBootstrap>();
            bootstrap.BuildDemoScene();
        }

        private void Start()
        {
            if (autoBuildOnStart && playerInstance == null)
            {
                BuildDemoScene();
            }
        }

        [ContextMenu("Build Demo Scene")]
        public void BuildDemoScene()
        {
            EnsureEventSystem();
            EnsureGround();
            EnsureObstacles();
            EnsurePlayer();
            EnsureCamera();
            EnsureUI();
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        private void EnsureGround()
        {
            if (GameObject.Find("Whitebox_Ground") != null) return;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Whitebox_Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(8f, 1f, 8f); // 80x80 meters

            // Create a simple dark gray material for contrast
            var renderer = ground.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.2f, 0.23f, 0.28f);
                renderer.sharedMaterial = mat;
            }
        }

        private void EnsureObstacles()
        {
            if (GameObject.Find("Whitebox_Obstacles") != null) return;

            GameObject parent = new GameObject("Whitebox_Obstacles");
            Vector3[] positions = new Vector3[]
            {
                new Vector3(-6f, 1.5f, 6f),
                new Vector3(6f, 1.5f, 6f),
                new Vector3(-8f, 1.5f, -6f),
                new Vector3(8f, 1.5f, -6f),
                new Vector3(0f, 1.5f, 12f)
            };

            var pillarMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            pillarMat.color = new Color(0.45f, 0.48f, 0.55f);

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = $"Pillar_{i + 1}";
                pillar.transform.parent = parent.transform;
                pillar.transform.position = positions[i];
                pillar.transform.localScale = new Vector3(2f, 3f, 2f);

                var r = pillar.GetComponent<MeshRenderer>();
                if (r != null) r.sharedMaterial = pillarMat;
            }
        }

        private void EnsurePlayer()
        {
            playerInstance = GameObject.Find("Moba_Hero_Player");
            if (playerInstance != null) return;

            // 1. Create Capsule Body
            playerInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerInstance.name = "Moba_Hero_Player";
            playerInstance.transform.position = playerSpawnPosition;

            // Remove default capsule collider and add CharacterController
            var defaultCol = playerInstance.GetComponent<Collider>();
            if (defaultCol != null) DestroyImmediate(defaultCol);

            var cc = playerInstance.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0f, 1f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;

            // Hero visual color
            var bodyRenderer = playerInstance.GetComponent<MeshRenderer>();
            if (bodyRenderer != null)
            {
                var heroMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                heroMat.color = new Color(0.18f, 0.55f, 0.95f); // Hero Blue
                bodyRenderer.sharedMaterial = heroMat;
            }

            // 2. Add Direction Visor / Nose pointing forward (+Z)
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Direction_Visor";
            visor.transform.parent = playerInstance.transform;
            visor.transform.localPosition = new Vector3(0f, 1.4f, 0.5f);
            visor.transform.localScale = new Vector3(0.35f, 0.2f, 0.5f);

            var visorCol = visor.GetComponent<Collider>();
            if (visorCol != null) DestroyImmediate(visorCol);

            var visorRenderer = visor.GetComponent<MeshRenderer>();
            if (visorRenderer != null)
            {
                var visorMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                visorMat.color = new Color(1f, 0.85f, 0.2f); // Golden yellow
                visorRenderer.sharedMaterial = visorMat;
            }

            // 3. Add Movement & Input components
            var inputMgr = playerInstance.AddComponent<MobaJoystickInput>();
            var movement = playerInstance.AddComponent<MobaHeroMovement>();
            movement.SetInputProvider(inputMgr);
        }

        private void EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            var follow = cam.GetComponent<MobaCameraFollow>();
            if (follow == null)
            {
                follow = cam.gameObject.AddComponent<MobaCameraFollow>();
            }

            if (playerInstance != null)
            {
                follow.Target = playerInstance.transform;
            }
        }

        private void EnsureUI()
        {
            if (GameObject.Find("Moba_UI_Canvas") != null) return;

            // 1. Create Canvas
            GameObject canvasObj = new GameObject("Moba_UI_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // 2. Left-hand Touch Zone (covers left 45% of the screen)
            GameObject touchZoneObj = new GameObject("Joystick_TouchZone");
            touchZoneObj.transform.SetParent(canvasObj.transform, false);

            RectTransform touchZoneRect = touchZoneObj.AddComponent<RectTransform>();
            touchZoneRect.anchorMin = new Vector2(0f, 0f);
            touchZoneRect.anchorMax = new Vector2(0.5f, 0.8f);
            touchZoneRect.offsetMin = Vector2.zero;
            touchZoneRect.offsetMax = Vector2.zero;

            Image touchZoneImg = touchZoneObj.AddComponent<Image>();
            touchZoneImg.color = new Color(0f, 0f, 0f, 0.001f); // Transparent but raycastable

            // 3. Joystick Background
            GameObject bgObj = new GameObject("Joystick_Background");
            bgObj.transform.SetParent(touchZoneObj.transform, false);

            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0f);
            bgRect.anchorMax = new Vector2(0f, 0f);
            bgRect.anchoredPosition = new Vector2(250f, 250f);
            bgRect.sizeDelta = new Vector2(200f, 200f);

            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.12f, 0.16f, 0.65f); // Dark translucent base
            bgImg.raycastTarget = false;

            CanvasGroup bgCanvasGroup = bgObj.AddComponent<CanvasGroup>();

            // 4. Joystick Handle
            GameObject handleObj = new GameObject("Joystick_Handle");
            handleObj.transform.SetParent(bgObj.transform, false);

            RectTransform handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(80f, 80f);

            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.color = new Color(0.85f, 0.9f, 1f, 0.85f); // Light glowing handle
            handleImg.raycastTarget = false;

            // 5. Setup Joystick UI Component
            joystickUIInstance = touchZoneObj.AddComponent<MobaVirtualJoystickUI>();
            joystickUIInstance.SetReferences(bgRect, handleRect, bgCanvasGroup);

            // 6. Connect to Player Input
            if (playerInstance != null)
            {
                var inputMgr = playerInstance.GetComponent<MobaJoystickInput>();
                if (inputMgr != null)
                {
                    inputMgr.BindVirtualJoystick(joystickUIInstance);
                }
            }
        }
    }
}
