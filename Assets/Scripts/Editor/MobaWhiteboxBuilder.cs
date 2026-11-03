using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Moba.Runtime.Input;
using Moba.Runtime.Movement;
using Moba.Runtime.CameraSystem;
using Moba.Runtime.Hero;
using Moba.Runtime.Combat;
using Moba.Runtime.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Moba.Editor
{
    public static class MobaWhiteboxBuilder
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string RebuildTriggerFile = "Temp/rebuild_scene.trigger";
        private const string RebuildDoneFile = "Temp/rebuild_scene.done";

        private const string FontAssetPath = "Assets/UI/NKGMoba/Fonts/SourceHanSansCN-Medium.otf";
        private const string RingSpritePath = "Assets/UI/NKGMoba/Sprites/Ring_Clean.png";
        private const string CircleSpritePath = "Assets/UI/NKGMoba/Sprites/Circle_Base.png";
        private const string SolidCircleSpritePath = "Assets/UI/NKGMoba/Sprites/Circle_Solid.png";
        private const string HeadBarBgPath = "Assets/UI/NKGMoba/Sprites/HeadBar_Bg.png";
        private const string HeadBarHpPath = "Assets/UI/NKGMoba/Sprites/HeadBar_HP.png";
        private const string HeadBarMpPath = "Assets/UI/NKGMoba/Sprites/HeadBar_MP.png";
        private const string IconAttackPath = "Assets/UI/NKGMoba/Sprites/Icon_Attack_Claw.png";
        private const string IconSkill1Path = "Assets/UI/NKGMoba/Sprites/Icon_Skill1_WindDive.png";
        private const string IconSkill2Path = "Assets/UI/NKGMoba/Sprites/Icon_Skill2_ParryWhirl.png";
        private const string IconSkill3Path = "Assets/Resources/skill1.jpg";

        private const string HeroAttackAudioPath = "Assets/Audio/Hero_Attack.wav";
        private const string HeroSkill1AudioPath = "Assets/Audio/Hero_Skill1_Dive.wav";
        private const string HeroSkill2AudioPath = "Assets/Audio/Hero_Skill2_Parry.wav";
        private const string HeroSkill3FlyAudioPath = "Assets/Audio/Hero_Skill3_Fly.wav";
        private const string HeroSkill3SlamAudioPath = "Assets/Audio/Hero_Skill3_Slam.wav";

        [InitializeOnLoadMethod]
        private static void AutoCheckAndBuildScene()
        {
            EditorApplication.update -= PollRebuildTrigger;
            EditorApplication.update += PollRebuildTrigger;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                EnsureSceneUpgradedIfOpen();
            };
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                EnsureSceneUpgradedIfOpen();
            }
        }

        private static void EnsureSceneUpgradedIfOpen()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path == ScenePath || activeScene.name == "Game")
            {
                Camera cam = Camera.main;
                var player = GameObject.Find("Moba_Hero_Player");
                var dummy = GameObject.Find("Moba_Target_Dummy");
                var touchZone = GameObject.Find("Joystick_TouchZone");
                var actionRoot = GameObject.Find("ActionButtons_Root");

                bool needsUpgrade = player == null
                    || player.transform.Find("Hero_Visual") == null
                    || player.transform.Find("Hero_AttackRange_Circle") == null
                    || player.GetComponent<YunZhongJunSkillController>() == null
                    || dummy == null
                    || dummy.transform.Find("Target_Lock_Reticle") == null
                    || dummy.GetComponent<MobaTargetDummy>() == null
                    || GameObject.Find("Whitebox_Ground") == null
                    || GameObject.Find("Intimidation_Ring_Group") == null
                    || GameObject.Find("Ult_FeatherRain_Root") == null
                    || cam == null
                    || cam.GetComponent<MobaCameraFollow>() == null
                    || touchZone == null
                    || touchZone.GetComponent<MobaVirtualJoystickUI>() == null
                    || actionRoot == null;

                if (needsUpgrade)
                {
                    Debug.Log("[MobaWhiteboxBuilder] Upgrading Game.unity with Yun Zhongjun Hero, Stationary Dummy & Mobile Skill UI...");
                    BuildScene();
                }
            }
        }

        private static void PollRebuildTrigger()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (!File.Exists(RebuildTriggerFile)) return;

            try { File.Delete(RebuildTriggerFile); } catch { }
            BuildScene();
        }

        [MenuItem("Moba/Build Whitebox Scene in Game.unity")]
        public static void BuildScene()
        {
            EnsureNKGMobaSpritesConfigured();
            EnsureMaterialFolderExists();

            var currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.path != ScenePath)
            {
                currentScene = EditorSceneManager.OpenScene(ScenePath);
            }

            Font uiFont = AssetDatabase.LoadAssetAtPath<Font>(FontAssetPath)
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Sprite ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingSpritePath);
            Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
            Sprite solidCircleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SolidCircleSpritePath);
            Sprite barBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeadBarBgPath);
            Sprite barHpSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeadBarHpPath);
            Sprite barMpSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeadBarMpPath);
            Sprite iconAttack = AssetDatabase.LoadAssetAtPath<Sprite>(IconAttackPath);
            Sprite iconSkill1 = AssetDatabase.LoadAssetAtPath<Sprite>(IconSkill1Path);
            Sprite iconSkill2 = AssetDatabase.LoadAssetAtPath<Sprite>(IconSkill2Path);
            Sprite iconSkill3 = AssetDatabase.LoadAssetAtPath<Sprite>(IconSkill3Path);

            // 1. Ground
            GameObject ground = GameObject.Find("Whitebox_Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Whitebox_Ground";
            }
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            AssignSavedLitMaterial(ground.GetComponent<MeshRenderer>(), "Assets/Materials/Whitebox_Ground_Mat.mat", new Color(0.18f, 0.22f, 0.26f));

            // 2. Obstacles
            GameObject obstaclesParent = GameObject.Find("Whitebox_Obstacles");
            if (obstaclesParent == null)
            {
                obstaclesParent = new GameObject("Whitebox_Obstacles");
                Vector3[] positions = new Vector3[]
                {
                    new Vector3(-9f, 1.5f, 8f),
                    new Vector3(9f, 1.5f, 8f),
                    new Vector3(-10f, 1.5f, -6f),
                    new Vector3(10f, 1.5f, -6f),
                    new Vector3(0f, 1.5f, 16f)
                };
                Vector3[] scales = new Vector3[]
                {
                    new Vector3(2.5f, 3f, 2.5f),
                    new Vector3(2.5f, 3f, 2.5f),
                    new Vector3(2.5f, 3f, 2.5f),
                    new Vector3(2.5f, 3f, 2.5f),
                    new Vector3(6f, 3f, 2f)
                };

                for (int i = 0; i < positions.Length; i++)
                {
                    GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    p.name = $"Pillar_{i + 1}";
                    p.transform.SetParent(obstaclesParent.transform, false);
                    p.transform.position = positions[i];
                    p.transform.localScale = scales[i];
                    AssignSavedLitMaterial(p.GetComponent<MeshRenderer>(), "Assets/Materials/Whitebox_Obstacle_Mat.mat", new Color(0.42f, 0.46f, 0.52f));
                }
            }

            // 3. Player Hero (云中君 — 保持原胶囊体模型不变)
            GameObject player = BuildOrUpgradePlayer(ringSprite, barBgSprite, barHpSprite, barMpSprite, circleSprite, uiFont);

            // 4. Stationary Non-AI Target Dummy (静止无AI假人，置于云中君正前方便于直接测试)
            BuildOrUpgradeTargetDummy(ringSprite, barBgSprite, barHpSprite, circleSprite, uiFont);

            // 5. UI Canvas (保留左下虚拟摇杆 + 右下云中君全套技能与普攻/补刀/推塔虚拟按键，不含回城/回血/加速召唤师技能)
            var joystickInput = player.GetComponent<MobaJoystickInput>();
            var heroSkillCtrl = player.GetComponent<YunZhongJunSkillController>();
            BuildMobileHUDCanvas(
                joystickInput,
                heroSkillCtrl,
                circleSprite,
                solidCircleSprite,
                ringSprite,
                iconAttack,
                iconSkill1,
                iconSkill2,
                iconSkill3,
                uiFont);

            // 6. Main Camera & Camera Follow
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData != null)
            {
                camData.renderPostProcessing = false;
                camData.allowHDROutput = false;
            }

            var follow = cam.GetComponent<MobaCameraFollow>();
            if (follow == null)
            {
                follow = cam.gameObject.AddComponent<MobaCameraFollow>();
            }
            follow.Target = player.transform;
            follow.Offset = new Vector3(0f, 15f, -11.5f);
            cam.transform.position = player.transform.position + follow.Offset;
            cam.transform.LookAt(player.transform.position + Vector3.up * 1.2f);

            // Save Scene
            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            AssetDatabase.SaveAssets();

            try
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(RebuildDoneFile, $"OK|{System.DateTime.Now:O}");
            }
            catch { }

            Debug.Log($"[MobaWhiteboxBuilder] Successfully built Yun Zhongjun & Dummy scene in {ScenePath}!");
        }

        private static GameObject BuildOrUpgradePlayer(
            Sprite ringSprite,
            Sprite barBgSprite,
            Sprite barHpSprite,
            Sprite barMpSprite,
            Sprite circleSprite,
            Font uiFont)
        {
            GameObject player = GameObject.Find("Moba_Hero_Player");
            if (player != null && player.transform.Find("Hero_Visual") == null)
            {
                Object.DestroyImmediate(player);
                player = null;
            }

            if (player == null)
            {
                player = new GameObject("Moba_Hero_Player");
                var cc = player.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
                cc.center = new Vector3(0f, 1f, 0f);
                cc.stepOffset = 0.3f;
                cc.slopeLimit = 45f;
                cc.minMoveDistance = 0f;

                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Hero_Visual";
                visual.transform.SetParent(player.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1f, 0f);
                var visualCol = visual.GetComponent<Collider>();
                if (visualCol != null) Object.DestroyImmediate(visualCol);
                AssignSavedLitMaterial(visual.GetComponent<MeshRenderer>(), "Assets/Materials/Hero_Material.mat", new Color(0.15f, 0.55f, 0.95f));

                GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visor.name = "Direction_Visor";
                visor.transform.SetParent(visual.transform, false);
                visor.transform.localPosition = new Vector3(0f, 0.4f, 0.5f);
                visor.transform.localScale = new Vector3(0.35f, 0.2f, 0.5f);
                var visorCol = visor.GetComponent<Collider>();
                if (visorCol != null) Object.DestroyImmediate(visorCol);
                AssignSavedLitMaterial(visor.GetComponent<MeshRenderer>(), "Assets/Materials/Visor_Material.mat", new Color(1f, 0.85f, 0.2f));
            }
            else
            {
                var visual = player.transform.Find("Hero_Visual");
                if (visual != null)
                {
                    AssignSavedLitMaterial(visual.GetComponent<MeshRenderer>(), "Assets/Materials/Hero_Material.mat", new Color(0.15f, 0.55f, 0.95f));
                    var visor = visual.Find("Direction_Visor");
                    if (visor != null)
                    {
                        AssignSavedLitMaterial(visor.GetComponent<MeshRenderer>(), "Assets/Materials/Visor_Material.mat", new Color(1f, 0.85f, 0.2f));
                    }
                }
            }

            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;

            var joystickInput = player.GetComponent<MobaJoystickInput>();
            if (joystickInput == null)
            {
                joystickInput = player.AddComponent<MobaJoystickInput>();
            }
            var heroMovement = player.GetComponent<MobaHeroMovement>();
            if (heroMovement == null)
            {
                heroMovement = player.AddComponent<MobaHeroMovement>();
            }
            heroMovement.SetInputProvider(joystickInput);

            // Clean old effect children before rebuilding fresh references
            DestroyIfExists(player.transform, "Skill2_ParryAura");
            DestroyIfExists(player.transform, "Ultimate_Circles_Group");
            DestroyIfExists(player.transform, "Player_HeadBar");
            DestroyIfExists(player.transform, "Hero_AttackRange_Circle");

            var existingIntimidation = GameObject.Find("Intimidation_Ring_Group");
            if (existingIntimidation != null) Object.DestroyImmediate(existingIntimidation);

            var existingRain = GameObject.Find("Ult_FeatherRain_Root");
            if (existingRain != null) Object.DestroyImmediate(existingRain);

            // A. 一技能威慑圈组（目标外侧红色小圈 + 更外层金色威慑圈）
            GameObject intimidationGroup = new GameObject("Intimidation_Ring_Group");
            intimidationGroup.transform.position = new Vector3(0f, 0.03f, 4.5f);

            Transform redInnerRing = CreateGroundRingSprite(
                "Target_InnerRedRing",
                intimidationGroup.transform,
                ringSprite,
                new Color(1f, 0.22f, 0.22f, 0.92f),
                diameter: 2.2f,
                sortingOrder: 5);

            Transform goldOuterRing = CreateGroundRingSprite(
                "Outer_DeterrenceGoldRing",
                intimidationGroup.transform,
                ringSprite,
                new Color(1f, 0.84f, 0.22f, 0.95f),
                diameter: 8.4f,
                sortingOrder: 4);

            intimidationGroup.SetActive(false);

            // B. 二技能免控格挡 Buff 光环
            Transform s2Aura = CreateGroundRingSprite(
                "Skill2_ParryAura",
                player.transform,
                ringSprite,
                new Color(0.32f, 1f, 0.88f, 0.9f),
                diameter: 3.4f,
                sortingOrder: 6);
            s2Aura.localPosition = new Vector3(0f, 0.85f, 0f);
            s2Aura.gameObject.SetActive(false);

            // C. 大招内外双圈（内圈击飞+叠印记，外圈不晕）
            GameObject ultGroup = new GameObject("Ultimate_Circles_Group");
            ultGroup.transform.SetParent(player.transform, false);
            ultGroup.transform.localPosition = new Vector3(0f, 0.04f, 0f);

            Transform ultInnerVis = CreateGroundRingSprite(
                "Ult_InnerCircle",
                ultGroup.transform,
                ringSprite,
                new Color(1f, 0.35f, 0.25f, 0.95f),
                diameter: 4.4f,
                sortingOrder: 7);

            Transform ultOuterVis = CreateGroundRingSprite(
                "Ult_OuterCircle",
                ultGroup.transform,
                ringSprite,
                new Color(0.35f, 0.92f, 1f, 0.88f),
                diameter: 9.6f,
                sortingOrder: 6);

            ultGroup.SetActive(false);

            // D. 大招 Unity 粒子系统（抓击下雨效果）
            GameObject rainRoot = BuildFeatherRainParticleSystem(circleSprite);

            // E. 云中君头顶状态条（等级15 + 生命条 + 气息条 + 状态文本）
            Transform playerHeadBar = BuildHeroWorldHeadBar(
                player.transform,
                barBgSprite,
                barHpSprite,
                barMpSprite,
                circleSprite,
                uiFont,
                out UnityEngine.UI.Image hpFill,
                out UnityEngine.UI.Image breathFill,
                out UnityEngine.UI.Text badgeText);

            // F. 仿王者荣耀攻击范围地面指示金圈（未攻击时隐藏，攻击/战斗时显示，随形态射程动态缩放）
            Transform attackRangeCircle = CreateGroundRingSprite(
                "Hero_AttackRange_Circle",
                player.transform,
                ringSprite,
                new Color(1f, 0.85f, 0.25f, 0.90f),
                diameter: 6.8f,
                sortingOrder: 5);
            attackRangeCircle.gameObject.SetActive(false);

            // G. 角色音频组件与各技能/普攻音效加载绑定
            var audioSrc = player.GetComponent<AudioSource>();
            if (audioSrc == null)
            {
                audioSrc = player.AddComponent<AudioSource>();
            }
            audioSrc.playOnAwake = false;

            AudioClip attackClip = AssetDatabase.LoadAssetAtPath<AudioClip>(HeroAttackAudioPath);
            AudioClip s1Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(HeroSkill1AudioPath);
            AudioClip s2Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(HeroSkill2AudioPath);
            AudioClip ultFlyClip = AssetDatabase.LoadAssetAtPath<AudioClip>(HeroSkill3FlyAudioPath);
            AudioClip ultSlamClip = AssetDatabase.LoadAssetAtPath<AudioClip>(HeroSkill3SlamAudioPath);

            var skillCtrl = player.GetComponent<YunZhongJunSkillController>();
            if (skillCtrl == null)
            {
                skillCtrl = player.AddComponent<YunZhongJunSkillController>();
            }
            skillCtrl.BindVisualAndUIReferences(
                intimidationGroup.transform,
                redInnerRing,
                goldOuterRing,
                s2Aura,
                ultGroup.transform,
                ultInnerVis,
                ultOuterVis,
                rainRoot.transform,
                rainRoot.GetComponentInChildren<ParticleSystem>(true),
                playerHeadBar,
                hpFill,
                breathFill,
                badgeText,
                uiFont);

            skillCtrl.BindAudioAndAttackRangeReferences(
                attackRangeCircle,
                audioSrc,
                attackClip,
                s1Clip,
                s2Clip,
                ultFlyClip,
                ultSlamClip);

            return player;
        }

        private static void BuildOrUpgradeTargetDummy(
            Sprite ringSprite,
            Sprite barBgSprite,
            Sprite barHpSprite,
            Sprite circleSprite,
            Font uiFont)
        {
            var oldDummy = GameObject.Find("Moba_Target_Dummy");
            if (oldDummy != null)
            {
                Object.DestroyImmediate(oldDummy);
            }

            GameObject dummy = new GameObject("Moba_Target_Dummy");
            dummy.transform.position = new Vector3(0f, 0f, 4.5f);
            dummy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var capsuleCol = dummy.AddComponent<CapsuleCollider>();
            capsuleCol.height = 2f;
            capsuleCol.radius = 0.55f;
            capsuleCol.center = new Vector3(0f, 1f, 0f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Dummy_Visual";
            visual.transform.SetParent(dummy.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            var vCol = visual.GetComponent<Collider>();
            if (vCol != null) Object.DestroyImmediate(vCol);
            AssignTargetLockMaterial(visual.GetComponent<MeshRenderer>(), "Assets/Materials/Dummy_Material.mat", new Color(0.88f, 0.24f, 0.26f));

            // Ground Target Lock Reticle (外层红色/攻击范围内黄色偏橘色锁定指示环)
            Transform lockReticle = CreateGroundRingSprite(
                "Target_Lock_Reticle",
                dummy.transform,
                ringSprite,
                new Color(1f, 0.18f, 0.18f, 0.95f),
                diameter: 2.2f,
                sortingOrder: 6);
            lockReticle.localPosition = new Vector3(0f, 0.04f, 0f);

            // Base pedestal so stationary dummy looks clearly anchored
            GameObject basePlate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basePlate.name = "Dummy_Pedestal";
            basePlate.transform.SetParent(dummy.transform, false);
            basePlate.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            basePlate.transform.localScale = new Vector3(1.4f, 0.05f, 1.4f);
            var pCol = basePlate.GetComponent<Collider>();
            if (pCol != null) Object.DestroyImmediate(pCol);
            AssignSavedLitMaterial(basePlate.GetComponent<MeshRenderer>(), "Assets/Materials/Dummy_Base_Mat.mat", new Color(0.32f, 0.26f, 0.26f));

            // WorldSpace HeadBar with 5 Claw Mark Pips
            Transform headBar = BuildDummyWorldHeadBar(
                dummy.transform,
                barBgSprite,
                barHpSprite,
                circleSprite,
                uiFont,
                out UnityEngine.UI.Image hpFill,
                out UnityEngine.UI.Text hpLabel,
                out UnityEngine.UI.Text stateLabel,
                out UnityEngine.UI.Image[] pips);

            var dummyComp = dummy.AddComponent<MobaTargetDummy>();
            dummyComp.BindUIReferences(visual.transform, headBar, hpFill, hpLabel, stateLabel, pips, uiFont);
            dummyComp.BindLockVisualReferences(visual.GetComponent<MeshRenderer>(), lockReticle, lockReticle.GetComponent<SpriteRenderer>());
        }

        private static GameObject BuildFeatherRainParticleSystem(Sprite particleSprite)
        {
            GameObject rainRoot = new GameObject("Ult_FeatherRain_Root");
            rainRoot.transform.position = Vector3.zero;

            GameObject emitterObj = new GameObject("Ult_FeatherRain_Particles");
            emitterObj.transform.SetParent(rainRoot.transform, false);
            emitterObj.transform.localPosition = new Vector3(0f, 6.8f, 0f);
            emitterObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Emit straight downward (+Z -> -Y)

            var ps = emitterObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 24f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.88f, 0.32f, 0.95f),
                new Color(0.38f, 0.95f, 1f, 0.95f));
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 160f;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 45)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 4.8f;
            shape.radiusThickness = 1f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.92f, 0.45f), 0f),
                    new GradientColorKey(new Color(0.35f, 0.92f, 1f), 0.7f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.25f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.95f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = grad;

            var psRenderer = emitterObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            psRenderer.velocityScale = 0.08f;
            psRenderer.lengthScale = 3.2f;

            Material particleMat = GetOrCreateParticleMaterial(particleSprite != null ? particleSprite.texture : null);
            psRenderer.sharedMaterial = particleMat;

            return rainRoot;
        }

        private static Transform BuildHeroWorldHeadBar(
            Transform parent,
            Sprite bgSprite,
            Sprite hpSprite,
            Sprite mpSprite,
            Sprite circleSprite,
            Font uiFont,
            out UnityEngine.UI.Image hpFill,
            out UnityEngine.UI.Image breathFill,
            out UnityEngine.UI.Text badgeText)
        {
            GameObject canvasGo = new GameObject("Player_HeadBar", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = new Vector3(0f, 2.9f, 0f);
            canvasGo.transform.localRotation = Quaternion.Euler(50f, 0f, 0f);
            canvasGo.transform.localScale = new Vector3(0.0135f, 0.0135f, 0.0135f);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            RectTransform rootRect = canvasGo.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(340f, 100f);

            // State Text on Top
            badgeText = CreateLabel("State_Text", canvasGo.transform, uiFont, "云中君 [飞鸡(展翅)] 气息:100/100", 20, new Vector2(0f, 34f), new Vector2(460f, 32f), new Color(0.75f, 0.96f, 1f));

            // Bar Container (王者荣耀加粗外框风格)
            GameObject barBgObj = CreateUIImage("Bar_Bg", canvasGo.transform, bgSprite, new Color(0.05f, 0.06f, 0.08f, 0.95f), new Vector2(10f, -2f), new Vector2(260f, 36f));

            // Green HP Fill (加粗明亮绿条)
            GameObject hpObj = CreateUIImage("HP_Fill", barBgObj.transform, hpSprite, new Color(0.18f, 0.88f, 0.32f, 1f), new Vector2(0f, 6f), new Vector2(254f, 20f));
            hpFill = hpObj.GetComponent<UnityEngine.UI.Image>();
            ConfigureFilledHorizontal(hpFill);

            // 仿王者荣耀血条格：每 1000 生命值一格竖直分割线
            float maxHeroHp = 6200f;
            int heroTicks = Mathf.FloorToInt(maxHeroHp / 1000f);
            for (int i = 1; i <= heroTicks; i++)
            {
                float frac = (i * 1000f) / maxHeroHp;
                float xPos = -127f + frac * 254f;
                GameObject tickGo = CreateUIImage($"HpTick_{i}", barBgObj.transform, null, new Color(0.04f, 0.06f, 0.08f, 0.92f), new Vector2(xPos, 6f), new Vector2(2f, 20f));
                tickGo.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            }

            // Breath (气息) Fill underneath HP (金橘色气息条)
            GameObject breathObj = CreateUIImage("Breath_Fill", barBgObj.transform, mpSprite, new Color(0.98f, 0.68f, 0.12f, 1f), new Vector2(0f, -9.5f), new Vector2(254f, 10f));
            breathFill = breathObj.GetComponent<UnityEngine.UI.Image>();
            ConfigureFilledHorizontal(breathFill);

            // Level 15 Circle Badge on Left (matching HoK screenshot)
            GameObject lvlBadge = CreateUIImage("Level_Badge", canvasGo.transform, circleSprite, new Color(0.08f, 0.10f, 0.14f, 0.98f), new Vector2(-132f, -2f), new Vector2(42f, 42f));
            CreateUIImage("Level_Badge_Ring", lvlBadge.transform, null, new Color(0.85f, 0.72f, 0.38f, 0.95f), Vector2.zero, new Vector2(42f, 42f));
            CreateLabel("Level_Text", lvlBadge.transform, uiFont, "15", 20, Vector2.zero, new Vector2(42f, 42f), new Color(1f, 0.92f, 0.48f));

            return canvasGo.transform;
        }

        private static Transform BuildDummyWorldHeadBar(
            Transform parent,
            Sprite bgSprite,
            Sprite hpSprite,
            Sprite circleSprite,
            Font uiFont,
            out UnityEngine.UI.Image hpFill,
            out UnityEngine.UI.Text hpLabel,
            out UnityEngine.UI.Text stateLabel,
            out UnityEngine.UI.Image[] pips)
        {
            GameObject canvasGo = new GameObject("Dummy_HeadBar", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = new Vector3(0f, 2.95f, 0f);
            canvasGo.transform.localRotation = Quaternion.Euler(50f, 0f, 0f);
            canvasGo.transform.localScale = new Vector3(0.0135f, 0.0135f, 0.0135f);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 21;

            RectTransform rootRect = canvasGo.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(340f, 110f);

            stateLabel = CreateLabel("Dummy_Status", canvasGo.transform, uiFont, "静止靶假人 (无AI)", 20, new Vector2(0f, 52f), new Vector2(460f, 32f), new Color(0.92f, 0.94f, 0.98f));

            // 5 Claw Mark Pips (抓击印记 1~5 层指示槽)
            GameObject pipRow = new GameObject("ClawMark_Pips", typeof(RectTransform));
            pipRow.transform.SetParent(canvasGo.transform, false);
            RectTransform pipRowRt = pipRow.GetComponent<RectTransform>();
            pipRowRt.anchoredPosition = new Vector2(0f, 24f);
            pipRowRt.sizeDelta = new Vector2(200f, 26f);

            pips = new UnityEngine.UI.Image[5];
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * 36f;
                GameObject pipObj = CreateUIImage($"MarkPip_{i + 1}", pipRow.transform, circleSprite, new Color(0.18f, 0.20f, 0.25f, 0.85f), new Vector2(x, 0f), new Vector2(26f, 26f));
                pips[i] = pipObj.GetComponent<UnityEngine.UI.Image>();
            }

            // Red HP Bar (加粗血条 + 仿王者荣耀 1000 HP 分割线)
            GameObject barBgObj = CreateUIImage("Dummy_Bar_Bg", canvasGo.transform, bgSprite, new Color(0.06f, 0.06f, 0.08f, 0.95f), new Vector2(0f, -8f), new Vector2(260f, 30f));
            GameObject hpObj = CreateUIImage("Dummy_HP_Fill", barBgObj.transform, hpSprite, new Color(0.92f, 0.20f, 0.20f, 1f), Vector2.zero, new Vector2(254f, 24f));
            hpFill = hpObj.GetComponent<UnityEngine.UI.Image>();
            ConfigureFilledHorizontal(hpFill);

            float maxDummyHp = 6000f;
            int dummyTicks = Mathf.FloorToInt(maxDummyHp / 1000f);
            for (int i = 1; i <= dummyTicks; i++)
            {
                float frac = (i * 1000f) / maxDummyHp;
                float xPos = -127f + frac * 254f;
                GameObject tickGo = CreateUIImage($"DummyHpTick_{i}", barBgObj.transform, null, new Color(0.05f, 0.02f, 0.02f, 0.92f), new Vector2(xPos, 0f), new Vector2(2f, 24f));
                tickGo.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            }

            hpLabel = CreateLabel("Dummy_HP_Text", barBgObj.transform, uiFont, "训练假人  6000/6000", 16, Vector2.zero, new Vector2(250f, 28f), Color.white);

            return canvasGo.transform;
        }

        private static void BuildMobileHUDCanvas(
            MobaJoystickInput joystickInput,
            YunZhongJunSkillController heroSkillCtrl,
            Sprite circleSprite,
            Sprite solidCircleSprite,
            Sprite ringSprite,
            Sprite iconAttack,
            Sprite iconSkill1,
            Sprite iconSkill2,
            Sprite iconSkill3,
            Font uiFont)
        {
            GameObject existingCanvas = GameObject.Find("Moba_UI_Canvas");
            if (existingCanvas != null)
            {
                Object.DestroyImmediate(existingCanvas);
            }

            GameObject canvasObj = new GameObject("Moba_UI_Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObj.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Ensure single EventSystem
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
            }

            // 1. 左下角：虚拟摇杆（严格限制在左半屏 0~0.48，加大尺寸方便手机触控）
            GameObject touchZoneObj = new GameObject("Joystick_TouchZone", typeof(RectTransform));
            touchZoneObj.transform.SetParent(canvasObj.transform, false);
            RectTransform touchZoneRect = touchZoneObj.GetComponent<RectTransform>();
            touchZoneRect.anchorMin = new Vector2(0f, 0f);
            touchZoneRect.anchorMax = new Vector2(0.48f, 0.85f);
            touchZoneRect.pivot = new Vector2(0f, 0f);
            touchZoneRect.offsetMin = Vector2.zero;
            touchZoneRect.offsetMax = Vector2.zero;

            var touchZoneImg = touchZoneObj.AddComponent<UnityEngine.UI.Image>();
            touchZoneImg.color = new Color(0f, 0f, 0f, 0.001f);
            touchZoneImg.raycastTarget = true;

            GameObject bgObj = new GameObject("Joystick_Background", typeof(RectTransform));
            bgObj.transform.SetParent(touchZoneObj.transform, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0f);
            bgRect.anchorMax = new Vector2(0f, 0f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = new Vector2(265f, 255f);
            bgRect.sizeDelta = new Vector2(320f, 320f);

            var bgImg = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImg.sprite = circleSprite;
            bgImg.color = new Color(0.12f, 0.16f, 0.22f, 0.65f);
            bgImg.raycastTarget = false;

            CanvasGroup bgCanvasGroup = bgObj.AddComponent<CanvasGroup>();
            bgCanvasGroup.alpha = 0.72f;

            // Outer decorative ring on joystick
            CreateUIImage("Joystick_Ring", bgObj.transform, ringSprite, new Color(0.55f, 0.78f, 1f, 0.55f), Vector2.zero, new Vector2(320f, 320f));

            GameObject handleObj = new GameObject("Joystick_Handle", typeof(RectTransform));
            handleObj.transform.SetParent(bgObj.transform, false);
            RectTransform handleRect = handleObj.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(126f, 126f);

            var handleImg = handleObj.AddComponent<UnityEngine.UI.Image>();
            handleImg.sprite = circleSprite;
            handleImg.color = new Color(0.82f, 0.94f, 1f, 0.92f);
            handleImg.raycastTarget = false;

            var joystickUI = touchZoneObj.AddComponent<MobaVirtualJoystickUI>();
            joystickUI.HandleRange = 135f;
            joystickUI.SetReferences(bgRect, handleRect, bgCanvasGroup);
            joystickInput.BindVirtualJoystick(joystickUI);

            // 2. 右下角：角色专属攻击与技能虚拟按键组（加大按键尺寸，适配全面屏手机与拇指触控）
            GameObject actionRoot = new GameObject("ActionButtons_Root", typeof(RectTransform));
            actionRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform actionRt = actionRoot.GetComponent<RectTransform>();
            actionRt.anchorMin = new Vector2(1f, 0f);
            actionRt.anchorMax = new Vector2(1f, 0f);
            actionRt.pivot = new Vector2(1f, 0f);
            actionRt.anchoredPosition = Vector2.zero;
            actionRt.sizeDelta = new Vector2(740f, 640f);

            // 普攻主按钮 + 补刀 + 推塔（尺寸大幅加大，按键视觉饱满清晰）
            var btnBasicAttack = CreateActionCircleButton(
                "Btn_BasicAttack",
                actionRoot.transform,
                anchoredPos: new Vector2(-175f, 160f),
                size: 216f,
                circleSprite: circleSprite,
                solidCircleSprite: solidCircleSprite,
                ringSprite: ringSprite,
                iconSprite: iconAttack,
                bgColor: new Color(0.16f, 0.24f, 0.34f, 0.92f),
                ringColor: new Color(0.88f, 0.78f, 0.45f, 0.95f),
                tagText: "普攻",
                uiFont: uiFont,
                tagFontSize: 26);

            var btnLastHit = CreateActionCircleButton(
                "Btn_LastHit",
                actionRoot.transform,
                anchoredPos: new Vector2(-360f, 85f),
                size: 112f,
                circleSprite: circleSprite,
                solidCircleSprite: solidCircleSprite,
                ringSprite: ringSprite,
                iconSprite: null,
                bgColor: new Color(0.15f, 0.22f, 0.3f, 0.9f),
                ringColor: new Color(0.65f, 0.8f, 0.95f, 0.85f),
                tagText: "补刀",
                uiFont: uiFont,
                tagFontSize: 22,
                centerTag: true);

            var btnPushTower = CreateActionCircleButton(
                "Btn_PushTower",
                actionRoot.transform,
                anchoredPos: new Vector2(-95f, 370f),
                size: 112f,
                circleSprite: circleSprite,
                solidCircleSprite: solidCircleSprite,
                ringSprite: ringSprite,
                iconSprite: null,
                bgColor: new Color(0.15f, 0.22f, 0.3f, 0.9f),
                ringColor: new Color(0.65f, 0.8f, 0.95f, 0.85f),
                tagText: "推塔",
                uiFont: uiFont,
                tagFontSize: 22,
                centerTag: true);

            // 一技能：天隙鸣（标签：加速，保持原技能贴图）
            var btnSkill1 = CreateSkillCircleButton(
                "Btn_Skill1",
                actionRoot.transform,
                anchoredPos: new Vector2(-515f, 135f),
                size: 154f,
                circleSprite: circleSprite,
                solidCircleSprite: solidCircleSprite,
                ringSprite: ringSprite,
                iconSprite: iconSkill1,
                subtitleTag: "加速",
                uiFont: uiFont,
                out UnityEngine.UI.Image s1CdMask,
                out UnityEngine.UI.Text s1CdText,
                out UnityEngine.UI.Image s1HighlightRing,
                out GameObject s1LockedOverlay,
                createLockOverlay: true);
            if (s1HighlightRing != null) s1HighlightRing.gameObject.SetActive(false);

            // 二技能：若英·华彩（标签：免控，附格挡Buff激活高亮环）
            var btnSkill2 = CreateSkillCircleButton(
                "Btn_Skill2",
                actionRoot.transform,
                anchoredPos: new Vector2(-405f, 315f),
                size: 154f,
                circleSprite: circleSprite,
                solidCircleSprite: solidCircleSprite,
                ringSprite: ringSprite,
                iconSprite: iconSkill2,
                subtitleTag: "免控",
                uiFont: uiFont,
                out UnityEngine.UI.Image s2CdMask,
                out UnityEngine.UI.Text s2CdText,
                out UnityEngine.UI.Image s2HighlightRing,
                out _,
                createLockOverlay: false);

            // 三技能大招：风雷引（标签：伤害，贴图设定为 skill1.jpg，纯实心圆遮罩裁剪，中心绝不挖空）
            var btnSkill3 = CreateSkillCircleButton(
                "Btn_Skill3",
                actionRoot.transform,
                anchoredPos: new Vector2(-225f, 480f),
                size: 164f,
                circleSprite: circleSprite,
                solidCircleSprite: solidCircleSprite,
                ringSprite: ringSprite,
                iconSprite: iconSkill3,
                subtitleTag: "伤害",
                uiFont: uiFont,
                out UnityEngine.UI.Image s3CdMask,
                out UnityEngine.UI.Text s3CdText,
                out UnityEngine.UI.Image s3HighlightRing,
                out _,
                createLockOverlay: false);
            if (s3HighlightRing != null) s3HighlightRing.gameObject.SetActive(false);

            var hud = canvasObj.AddComponent<YunZhongJunBattleHUD>();
            hud.SetReferences(
                heroSkillCtrl,
                btnBasicAttack,
                btnLastHit,
                btnPushTower,
                btnSkill1,
                s1CdMask,
                s1CdText,
                s1LockedOverlay,
                btnSkill2,
                s2CdMask,
                s2CdText,
                s2HighlightRing,
                btnSkill3,
                s3CdMask,
                s3CdText);
        }

        private static UnityEngine.UI.Button CreateActionCircleButton(
            string name,
            Transform parent,
            Vector2 anchoredPos,
            float size,
            Sprite circleSprite,
            Sprite solidCircleSprite,
            Sprite ringSprite,
            Sprite iconSprite,
            Color bgColor,
            Color ringColor,
            string tagText,
            Font uiFont,
            int tagFontSize,
            bool centerTag = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(size, size);

            var bgImg = go.AddComponent<UnityEngine.UI.Image>();
            bgImg.sprite = circleSprite;
            bgImg.color = bgColor;
            bgImg.raycastTarget = true;

            if (iconSprite != null)
            {
                GameObject maskGo = new GameObject("Action_Icon_Mask", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
                maskGo.transform.SetParent(go.transform, false);
                RectTransform maskRt = maskGo.GetComponent<RectTransform>();
                maskRt.anchorMin = new Vector2(0.5f, 0.5f);
                maskRt.anchorMax = new Vector2(0.5f, 0.5f);
                maskRt.pivot = new Vector2(0.5f, 0.5f);
                maskRt.anchoredPosition = Vector2.zero;
                float innerSize = size * 0.84f;
                maskRt.sizeDelta = new Vector2(innerSize, innerSize);

                var maskImg = maskGo.GetComponent<UnityEngine.UI.Image>();
                maskImg.sprite = solidCircleSprite;
                maskImg.raycastTarget = false;

                var maskComp = maskGo.GetComponent<UnityEngine.UI.Mask>();
                maskComp.showMaskGraphic = false;

                GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                iconGo.transform.SetParent(maskGo.transform, false);
                RectTransform iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;

                var iconImg = iconGo.GetComponent<UnityEngine.UI.Image>();
                iconImg.sprite = iconSprite;
                iconImg.color = new Color(0.95f, 0.96f, 1f, 0.92f);
                iconImg.preserveAspect = false;
                iconImg.raycastTarget = false;
            }

            CreateUIImage("Border_Ring", go.transform, ringSprite, ringColor, Vector2.zero, new Vector2(size, size));

            Vector2 labelOffset = centerTag ? Vector2.zero : new Vector2(0f, -size * 0.32f);
            CreateLabel("Label", go.transform, uiFont, tagText, tagFontSize, labelOffset, new Vector2(size, 32f), new Color(1f, 0.95f, 0.82f));

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bgImg;
            return btn;
        }

        private static UnityEngine.UI.Button CreateSkillCircleButton(
            string name,
            Transform parent,
            Vector2 anchoredPos,
            float size,
            Sprite circleSprite,
            Sprite solidCircleSprite,
            Sprite ringSprite,
            Sprite iconSprite,
            string subtitleTag,
            Font uiFont,
            out UnityEngine.UI.Image cdMask,
            out UnityEngine.UI.Text cdText,
            out UnityEngine.UI.Image highlightRing,
            out GameObject lockedOverlay,
            bool createLockOverlay)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(size, size);

            var bgImg = go.AddComponent<UnityEngine.UI.Image>();
            bgImg.sprite = circleSprite;
            bgImg.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);
            bgImg.raycastTarget = true;

            if (iconSprite != null)
            {
                // 核心需求：使用纯实心圆遮罩 (SolidCircle)，确保贴图严格裁剪为圆形且中心 100% 完整显示，绝不会在中心抠出空心洞！
                GameObject maskGo = new GameObject("Icon_Mask_Container", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
                maskGo.transform.SetParent(go.transform, false);
                RectTransform maskRt = maskGo.GetComponent<RectTransform>();
                maskRt.anchorMin = new Vector2(0.5f, 0.5f);
                maskRt.anchorMax = new Vector2(0.5f, 0.5f);
                maskRt.pivot = new Vector2(0.5f, 0.5f);
                maskRt.anchoredPosition = Vector2.zero;
                float innerSize = size * 0.88f;
                maskRt.sizeDelta = new Vector2(innerSize, innerSize);

                var maskImg = maskGo.GetComponent<UnityEngine.UI.Image>();
                maskImg.sprite = solidCircleSprite;
                maskImg.raycastTarget = false;

                var maskComp = maskGo.GetComponent<UnityEngine.UI.Mask>();
                maskComp.showMaskGraphic = false;

                GameObject iconGo = new GameObject("Skill_Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                iconGo.transform.SetParent(maskGo.transform, false);
                RectTransform iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;

                var iconImg = iconGo.GetComponent<UnityEngine.UI.Image>();
                iconImg.sprite = iconSprite;
                iconImg.color = Color.white;
                iconImg.preserveAspect = false; // 紧贴撑满内部圆形，不留黑边或边缘
                iconImg.raycastTarget = false;
            }

            CreateUIImage("Gold_Border", go.transform, ringSprite, new Color(0.95f, 0.82f, 0.42f, 0.95f), Vector2.zero, new Vector2(size, size));

            // Active Buff Highlight Ring (e.g. Skill 2 免控格挡Buff)
            GameObject hlObj = CreateUIImage("Buff_Highlight", go.transform, ringSprite, new Color(0.3f, 1f, 0.88f, 1f), Vector2.zero, new Vector2(size + 18f, size + 18f));
            highlightRing = hlObj.GetComponent<UnityEngine.UI.Image>();
            hlObj.SetActive(false);

            // Subtitle Tag ("加速" / "免控" / "伤害") matching Honor of Kings screenshot
            CreateLabel("Subtitle_Tag", go.transform, uiFont, subtitleTag, 18, new Vector2(0f, -size * 0.34f), new Vector2(size, 28f), new Color(1f, 0.94f, 0.72f));

            // Grounded Lock Overlay for Skill 1 ("需飞行")
            lockedOverlay = null;
            if (createLockOverlay)
            {
                lockedOverlay = CreateUIImage("Grounded_Lock_Overlay", go.transform, solidCircleSprite, new Color(0.05f, 0.05f, 0.08f, 0.76f), Vector2.zero, new Vector2(size * 0.88f, size * 0.88f));
                CreateLabel("Lock_Text", lockedOverlay.transform, uiFont, "需飞行", 20, Vector2.zero, new Vector2(size, 34f), new Color(1f, 0.6f, 0.3f));
                lockedOverlay.SetActive(false);
            }

            // Radial 360 Cooldown Mask (adapted from NKGMoba SkillProBar)
            GameObject cdObj = CreateUIImage("CD_Mask", go.transform, solidCircleSprite, new Color(0.04f, 0.06f, 0.1f, 0.78f), Vector2.zero, new Vector2(size * 0.88f, size * 0.88f));
            cdMask = cdObj.GetComponent<UnityEngine.UI.Image>();
            cdMask.type = UnityEngine.UI.Image.Type.Filled;
            cdMask.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            cdMask.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            cdMask.fillClockwise = false;
            cdMask.fillAmount = 0f;
            cdObj.SetActive(false);

            cdText = CreateLabel("CD_Text", go.transform, uiFont, "5", 32, Vector2.zero, new Vector2(size, size), Color.white);
            cdText.gameObject.SetActive(false);

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bgImg;
            return btn;
        }

        private static Transform CreateGroundRingSprite(
            string name,
            Transform parent,
            Sprite ringSprite,
            Color color,
            float diameter,
            int sortingOrder)
        {
            GameObject ringGo = new GameObject(name);
            ringGo.transform.SetParent(parent, false);
            ringGo.transform.localPosition = Vector3.zero;
            ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var sr = ringGo.AddComponent<SpriteRenderer>();
            sr.sprite = ringSprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;

            // Normalize sprite pixel size to 1 meter unit before applying diameter
            float spriteWorldWidth = (ringSprite != null && ringSprite.bounds.size.x > 0.01f)
                ? ringSprite.bounds.size.x
                : 1f;
            // Store normalized child or adjust pixelsPerUnit via importer so 1 scale = 1 meter!
            float scale = diameter / spriteWorldWidth;
            ringGo.transform.localScale = new Vector3(scale, scale, 1f);
            return ringGo.transform;
        }

        private static GameObject CreateUIImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            Vector2 anchoredPos,
            Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.AddComponent<UnityEngine.UI.Image>();
            if (sprite != null) img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return go;
        }

        private static UnityEngine.UI.Text CreateLabel(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            Vector2 anchoredPos,
            Vector2 size,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var txt = go.AddComponent<UnityEngine.UI.Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.color = color;
            txt.raycastTarget = false;

            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            return txt;
        }

        private static void ConfigureFilledHorizontal(UnityEngine.UI.Image img)
        {
            img.type = UnityEngine.UI.Image.Type.Filled;
            img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            img.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
        }

        private static void DestroyIfExists(Transform parent, string childName)
        {
            var t = parent.Find(childName);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        private static void EnsureMaterialFolderExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
        }

        private static void AssignSavedLitMaterial(MeshRenderer renderer, string assetPath, Color color)
        {
            if (renderer == null) return;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(lit);
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(mat);
            }
            renderer.sharedMaterial = mat;
        }

        private static void AssignTargetLockMaterial(MeshRenderer renderer, string assetPath, Color baseColor)
        {
            if (renderer == null) return;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Shader lockShader = Shader.Find("Moba/TargetLock") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(lockShader);
                mat.SetColor("_BaseColor", baseColor);
                mat.SetColor("_OutlineColor", new Color(1f, 0.18f, 0.18f, 1f));
                mat.SetFloat("_OutlineWidth", 0.045f);
                mat.SetFloat("_RimIntensity", 1.2f);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                if (mat.shader != lockShader && lockShader != null) mat.shader = lockShader;
                mat.SetColor("_BaseColor", baseColor);
                mat.SetColor("_OutlineColor", new Color(1f, 0.18f, 0.18f, 1f));
                mat.SetFloat("_OutlineWidth", 0.045f);
                mat.SetFloat("_RimIntensity", 1.2f);
                EditorUtility.SetDirty(mat);
            }
            renderer.sharedMaterial = mat;
        }

        private static Material GetOrCreateParticleMaterial(Texture2D particleTex)
        {
            const string path = "Assets/Materials/FeatherRain_Particle.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader sh = Shader.Find("Sprites/Default")
                    ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                mat = new Material(sh);
                if (particleTex != null) mat.mainTexture = particleTex;
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static void EnsureNKGMobaSpritesConfigured()
        {
            string[] spritePaths = new string[]
            {
                RingSpritePath,
                CircleSpritePath,
                SolidCircleSpritePath,
                HeadBarBgPath,
                HeadBarHpPath,
                HeadBarMpPath,
                IconAttackPath,
                IconSkill1Path,
                IconSkill2Path,
                IconSkill3Path
            };

            foreach (string path in spritePaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }

                // Set Ring_Clean (512x512, ring diameter 490px) & Circle_Base (1024x1024) PPU so 1 world unit == exact ring diameter!
                if (path == RingSpritePath && Mathf.Abs(importer.spritePixelsPerUnit - 490f) > 0.1f)
                {
                    importer.spritePixelsPerUnit = 490f;
                    dirty = true;
                }
                else if (path == CircleSpritePath && Mathf.Abs(importer.spritePixelsPerUnit - 1024f) > 0.1f)
                {
                    importer.spritePixelsPerUnit = 1024f;
                    dirty = true;
                }
                else if (path == SolidCircleSpritePath && Mathf.Abs(importer.spritePixelsPerUnit - 512f) > 0.1f)
                {
                    importer.spritePixelsPerUnit = 512f;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                }
                else if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
        }
    }
}
