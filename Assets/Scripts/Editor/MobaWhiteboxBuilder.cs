using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Moba.Runtime.Input;
using Moba.Runtime.Movement;
using Moba.Runtime.CameraSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Moba.Editor
{
    public static class MobaWhiteboxBuilder
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [InitializeOnLoadMethod]
        private static void AutoCheckAndBuildScene()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.path == ScenePath || activeScene.name == "Game")
                {
                    Camera cam = Camera.main;
                    var player = GameObject.Find("Moba_Hero_Player");
                    var touchZone = GameObject.Find("Joystick_TouchZone");
                    var handle = GameObject.Find("Joystick_Handle");

                    bool needsUpgrade = player == null
                        || player.transform.Find("Hero_Visual") == null
                        || GameObject.Find("Whitebox_Ground") == null
                        || cam == null
                        || cam.GetComponent<MobaCameraFollow>() == null
                        || touchZone == null
                        || touchZone.GetComponent<MobaVirtualJoystickUI>() == null
                        || handle == null;

                    if (needsUpgrade)
                    {
                        Debug.Log("[MobaWhiteboxBuilder] Auto-repairing MOBA scene setup...");
                        BuildScene();
                    }
                }
            };
        }

        [MenuItem("Moba/Build Whitebox Scene in Game.unity")]
        public static void BuildScene()
        {
            var currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.path != ScenePath)
            {
                currentScene = EditorSceneManager.OpenScene(ScenePath);
            }

            // 1. Ground
            GameObject ground = GameObject.Find("Whitebox_Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Whitebox_Ground";
                Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
            }
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(8f, 1f, 8f); // 80x80 meters

            var groundRenderer = ground.GetComponent<MeshRenderer>();
            if (groundRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.name = "Whitebox_Ground_Mat";
                mat.color = new Color(0.18f, 0.22f, 0.26f);
                groundRenderer.sharedMaterial = mat;
            }

            // 2. Obstacles
            GameObject obstaclesParent = GameObject.Find("Whitebox_Obstacles");
            if (obstaclesParent == null)
            {
                obstaclesParent = new GameObject("Whitebox_Obstacles");
                Undo.RegisterCreatedObjectUndo(obstaclesParent, "Create Obstacles Parent");

                Vector3[] positions = new Vector3[]
                {
                    new Vector3(-8f, 1.5f, 8f),
                    new Vector3(8f, 1.5f, 8f),
                    new Vector3(-10f, 1.5f, -6f),
                    new Vector3(10f, 1.5f, -6f),
                    new Vector3(0f, 1.5f, 14f)
                };

                Vector3[] scales = new Vector3[]
                {
                    new Vector3(2.5f, 3f, 2.5f),
                    new Vector3(2.5f, 3f, 2.5f),
                    new Vector3(2.5f, 3f, 2.5f),
                    new Vector3(2.5f, 3f, 2.5f),
                    new Vector3(6f, 3f, 2f)
                };

                var obstacleMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                obstacleMat.name = "Whitebox_Obstacle_Mat";
                obstacleMat.color = new Color(0.42f, 0.46f, 0.52f);

                for (int i = 0; i < positions.Length; i++)
                {
                    GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    p.name = $"Pillar_{i + 1}";
                    p.transform.parent = obstaclesParent.transform;
                    p.transform.position = positions[i];
                    p.transform.localScale = scales[i];

                    var r = p.GetComponent<MeshRenderer>();
                    if (r != null) r.sharedMaterial = obstacleMat;
                }
            }

            // 3. Player (Standard Humanoid / Character Hierarchy: Root at Feet Y = 0)
            GameObject player = GameObject.Find("Moba_Hero_Player");
            if (player != null && player.transform.Find("Hero_Visual") == null)
            {
                // Rebuild old mismatch player into standard humanoid hierarchy
                Undo.DestroyObjectImmediate(player);
                player = null;
            }

            if (player == null)
            {
                // Root GameObject with pivot at FEET (0, 0, 0)
                player = new GameObject("Moba_Hero_Player");
                player.transform.position = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(player, "Create Player");

                // CharacterController with feet at 0 and head at 2.0m
                var cc = player.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
                cc.center = new Vector3(0f, 1f, 0f); // Center at Y = 1.0, base touches ground at Y = 0.0
                cc.stepOffset = 0.3f;
                cc.slopeLimit = 45f;
                cc.minMoveDistance = 0f;

                // Child Visual Model: Capsule offset by +1.0m Y so bottom aligns with feet at Y = 0.0
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Hero_Visual";
                visual.transform.SetParent(player.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1f, 0f);
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                var visualCol = visual.GetComponent<Collider>();
                if (visualCol != null) Object.DestroyImmediate(visualCol);

                var heroRenderer = visual.GetComponent<MeshRenderer>();
                if (heroRenderer != null)
                {
                    var heroMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    heroMat.name = "Hero_Material";
                    heroMat.color = new Color(0.15f, 0.55f, 0.95f);
                    heroRenderer.sharedMaterial = heroMat;
                }

                // Direction Visor on head
                GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visor.name = "Direction_Visor";
                visor.transform.SetParent(visual.transform, false);
                visor.transform.localPosition = new Vector3(0f, 0.4f, 0.5f);
                visor.transform.localScale = new Vector3(0.35f, 0.2f, 0.5f);

                var visorCol = visor.GetComponent<Collider>();
                if (visorCol != null) Object.DestroyImmediate(visorCol);

                var visorRenderer = visor.GetComponent<MeshRenderer>();
                if (visorRenderer != null)
                {
                    var visorMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    visorMat.name = "Visor_Material";
                    visorMat.color = new Color(1f, 0.85f, 0.2f);
                    visorRenderer.sharedMaterial = visorMat;
                }
            }
            player.transform.position = Vector3.zero;

            var joystickInput = player.GetComponent<MobaJoystickInput>() ?? player.AddComponent<MobaJoystickInput>();
            var heroMovement = player.GetComponent<MobaHeroMovement>() ?? player.AddComponent<MobaHeroMovement>();
            heroMovement.SetInputProvider(joystickInput);

            // 4. UI Canvas & Virtual Joystick
            GameObject canvasObj = GameObject.Find("Moba_UI_Canvas");
            if (canvasObj != null)
            {
                Undo.DestroyObjectImmediate(canvasObj);
                canvasObj = null;
            }

            canvasObj = new GameObject("Moba_UI_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Ensure EventSystem
            var existingEventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (existingEventSystem == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
                Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
            }

            // Touch Zone
            GameObject touchZoneObj = new GameObject("Joystick_TouchZone", typeof(RectTransform));
            touchZoneObj.transform.SetParent(canvasObj.transform, false);

            RectTransform touchZoneRect = touchZoneObj.GetComponent<RectTransform>();
            touchZoneRect.anchorMin = new Vector2(0f, 0f);
            touchZoneRect.anchorMax = new Vector2(0.5f, 0.85f);
            touchZoneRect.pivot = new Vector2(0f, 0f);
            touchZoneRect.offsetMin = Vector2.zero;
            touchZoneRect.offsetMax = Vector2.zero;

            Image touchZoneImg = touchZoneObj.AddComponent<Image>();
            touchZoneImg.color = new Color(0f, 0f, 0f, 0.001f);
            touchZoneImg.raycastTarget = true;

            // Background
            GameObject bgObj = new GameObject("Joystick_Background", typeof(RectTransform));
            bgObj.transform.SetParent(touchZoneObj.transform, false);

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0f);
            bgRect.anchorMax = new Vector2(0f, 0f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = new Vector2(250f, 250f);
            bgRect.sizeDelta = new Vector2(220f, 220f);

            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.15f, 0.2f, 0.65f);
            bgImg.raycastTarget = false;

            CanvasGroup bgCanvasGroup = bgObj.AddComponent<CanvasGroup>();
            bgCanvasGroup.alpha = 0.65f;

            // Handle
            GameObject handleObj = new GameObject("Joystick_Handle", typeof(RectTransform));
            handleObj.transform.SetParent(bgObj.transform, false);

            RectTransform handleRect = handleObj.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(90f, 90f);

            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.color = new Color(0.85f, 0.92f, 1f, 0.85f);
            handleImg.raycastTarget = false;

            // Virtual Joystick Component
            var joystickUI = touchZoneObj.AddComponent<MobaVirtualJoystickUI>();
            joystickUI.SetReferences(bgRect, handleRect, bgCanvasGroup);
            joystickInput.BindVirtualJoystick(joystickUI);

            // 5. Main Camera & Camera Follow
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                Undo.RegisterCreatedObjectUndo(camObj, "Create Camera");
            }

            var follow = cam.GetComponent<MobaCameraFollow>() ?? cam.gameObject.AddComponent<MobaCameraFollow>();
            follow.Target = player.transform;
            follow.Offset = new Vector3(0f, 16f, -12f);

            // Mark Scene Dirty and Save
            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);

            Debug.Log($"[MobaWhiteboxBuilder] Successfully built and saved MOBA Whitebox scene to {ScenePath}!");
        }
    }
}
