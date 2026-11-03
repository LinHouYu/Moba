using System.Collections.Generic;
using UnityEngine;
using Moba.Runtime.Hero;

namespace Moba.Runtime.Combat
{
    /// <summary>
    /// 无 AI、不会自主移动的训练假人。
    /// 支持承受伤害、眩晕（含旧版云中君1秒眩晕验证）、大招内圈击飞、5层抓击印记持续增伤与满5层移动额外撕裂伤害检测。
    /// 所有核心参数均暴露在 Inspector 中便于实时调试。
    /// </summary>
    [DisallowMultipleComponent]
    public class MobaTargetDummy : MonoBehaviour
    {
        [Header("Dummy Stats (Inspector Tunable)")]
        [SerializeField] private string dummyName = "训练假人";
        [SerializeField] [Range(500f, 20000f)] private float maxHp = 6000f;
        [SerializeField] private float currentHp = 6000f;
        [Tooltip("脱离受击多久后自动回满血量，方便无限连招测试")]
        [SerializeField] [Range(1f, 15f)] private float autoRecoverDelay = 4f;

        [Header("Claw Mark Settings (五层抓击印记)")]
        [SerializeField] [Range(1f, 15f)] private float clawMarkDuration = 6f;
        [SerializeField] [Range(0.2f, 2f)] private float bleedTickInterval = 0.5f;
        [SerializeField] [Range(1f, 100f)] private float bleedDamagePerStack = 14f;
        [SerializeField] [Range(0.02f, 0.5f)] private float damageBonusPerStackRatio = 0.12f;
        [Tooltip("叠满5层印记后，敌方每移动1米触发的额外撕裂伤害（未满5层移动绝无额外伤害）")]
        [SerializeField] [Range(5f, 200f)] private float fullMarkMoveExtraDamagePerMeter = 50f;

        [Header("Knockup Visual Settings")]
        [SerializeField] [Range(0.5f, 4f)] private float knockupHeight = 1.65f;

        [Header("Visual & UI References")]
        [SerializeField] private Transform visualTransform;
        [SerializeField] private MeshRenderer visualMeshRenderer;
        [SerializeField] private Transform lockReticleTransform;
        [SerializeField] private SpriteRenderer lockReticleSpriteRenderer;
        [SerializeField] private Transform headBarRoot;
        [SerializeField] private UnityEngine.UI.Image hpFillImage;
        [SerializeField] private UnityEngine.UI.Text hpText;
        [SerializeField] private UnityEngine.UI.Text statusText;
        [SerializeField] private UnityEngine.UI.Image[] clawPipImages = new UnityEngine.UI.Image[5];
        [SerializeField] private Font popupFont;

        [Header("Target Lock Shader State")]
        [SerializeField] private bool isTargetLocked = false;
        [SerializeField] private bool isTargetInAttackRange = false;

        private MaterialPropertyBlock lockPropertyBlock;
        private static readonly int OutlineColorProp = Shader.PropertyToID("_OutlineColor");
        private static readonly int RimIntensityProp = Shader.PropertyToID("_RimIntensity");

        [Header("Live Debug (Read-Only)")]
        [SerializeField] private int debugClawStacks = 0;
        [SerializeField] private float debugStunRemaining = 0f;
        [SerializeField] private float debugKnockupRemaining = 0f;
        [SerializeField] private float debugMarkTimeRemaining = 0f;

        private float knockupTotalDuration = 0.75f;
        private float bleedTickTimer = 0f;
        private float lastHitTimestamp = -99f;
        private float statusHintTimer = 0f;
        private string transientStatusHint = string.Empty;
        private Vector3 lastPlanarPosition;
        private float baseVisualLocalY = 1f;

        private struct FloatingPopup
        {
            public RectTransform Rect;
            public UnityEngine.UI.Text Label;
            public float Elapsed;
            public float Duration;
            public Color BaseColor;
            public Vector2 StartAnchoredPos;
        }

        private readonly List<FloatingPopup> activePopups = new List<FloatingPopup>(12);

        public float MaxHp => maxHp;
        public float CurrentHp => currentHp;
        public int ClawStacks => debugClawStacks;
        public float StunRemaining => debugStunRemaining;
        public float KnockupRemaining => debugKnockupRemaining;
        public bool IsStunned => debugStunRemaining > 0f;
        public bool IsKnockedUp => debugKnockupRemaining > 0f;

        public void BindUIReferences(
            Transform visual,
            Transform headBar,
            UnityEngine.UI.Image hpFill,
            UnityEngine.UI.Text hpLabel,
            UnityEngine.UI.Text stateLabel,
            UnityEngine.UI.Image[] pips,
            Font uiFont)
        {
            visualTransform = visual;
            visualMeshRenderer = visual != null ? visual.GetComponent<MeshRenderer>() : null;
            headBarRoot = headBar;
            hpFillImage = hpFill;
            hpText = hpLabel;
            statusText = stateLabel;
            clawPipImages = pips;
            popupFont = uiFont;
        }

        public void BindLockVisualReferences(MeshRenderer meshRenderer, Transform reticle, SpriteRenderer reticleSr)
        {
            visualMeshRenderer = meshRenderer;
            lockReticleTransform = reticle;
            lockReticleSpriteRenderer = reticleSr;
        }

        /// <summary>
        /// 目标锁定外层 Shader 与地面锁定环响应：
        /// 攻击范围外呈现外层红色 Shader / 环；
        /// 进入云中君普攻攻击范围内则变为黄色偏橘色，告知玩家已锁定并可实施打击。
        /// </summary>
        public void SetLockVisual(bool isLocked, bool isInAttackRange)
        {
            isTargetLocked = isLocked;
            isTargetInAttackRange = isInAttackRange;

            Color lockCol = YunZhongJunLogic.GetTargetLockOutlineColor(isInAttackRange);

            if (visualMeshRenderer != null)
            {
                if (lockPropertyBlock == null) lockPropertyBlock = new MaterialPropertyBlock();
                visualMeshRenderer.GetPropertyBlock(lockPropertyBlock);
                lockPropertyBlock.SetColor(OutlineColorProp, isLocked ? lockCol : new Color(0.2f, 0.05f, 0.05f, 0.5f));
                lockPropertyBlock.SetFloat(RimIntensityProp, isLocked ? (isInAttackRange ? 1.6f : 1.1f) : 0.2f);
                visualMeshRenderer.SetPropertyBlock(lockPropertyBlock);
            }

            if (lockReticleTransform != null)
            {
                lockReticleTransform.gameObject.SetActive(isLocked);
                if (lockReticleSpriteRenderer != null)
                {
                    lockReticleSpriteRenderer.color = lockCol;
                }
            }
        }

        private void Awake()
        {
            AutoResolveReferences();
            currentHp = maxHp;
            lastPlanarPosition = new Vector3(transform.position.x, 0f, transform.position.z);
            RefreshHeadBarUI();
        }

        private void AutoResolveReferences()
        {
            if (visualTransform == null)
            {
                visualTransform = transform.Find("Dummy_Visual");
            }
            if (visualTransform != null)
            {
                baseVisualLocalY = 1f;
                if (visualMeshRenderer == null)
                {
                    visualMeshRenderer = visualTransform.GetComponent<MeshRenderer>();
                }
            }
            if (headBarRoot == null)
            {
                headBarRoot = transform.Find("Dummy_HeadBar");
            }
            if (lockReticleTransform == null)
            {
                lockReticleTransform = transform.Find("Target_Lock_Reticle");
                if (lockReticleTransform != null)
                {
                    lockReticleSpriteRenderer = lockReticleTransform.GetComponent<SpriteRenderer>();
                }
            }
        }

        /// <summary>
        /// 对假人造成直接技能或普攻伤害（自动计算当前抓击印记层数的增伤加成）。
        /// </summary>
        public float TakeSkillOrAttackDamage(float baseDamage, int addClawStacks = 0, string customPopupPrefix = "")
        {
            lastHitTimestamp = Time.time;

            // 先根据现有印记计算持续增伤，再叠新印记（或叠层后立刻刷新持续时间）
            float finalDamage = YunZhongJunLogic.CalculateDamageWithClawMarkBonus(
                baseDamage,
                debugClawStacks,
                damageBonusPerStackRatio);

            if (addClawStacks > 0)
            {
                AddClawMarkStacks(addClawStacks);
            }

            ApplyRawHpLoss(finalDamage);

            int rounded = Mathf.Max(1, Mathf.RoundToInt(finalDamage));
            Color popupCol = debugClawStacks >= YunZhongJunLogic.MaxClawMarkStacks
                ? new Color(1f, 0.28f, 0.28f)
                : new Color(1f, 0.88f, 0.32f);

            string text = string.IsNullOrEmpty(customPopupPrefix)
                ? $"-{rounded}"
                : $"{customPopupPrefix} -{rounded}";
            SpawnFloatingText(text, popupCol, 28);

            RefreshHeadBarUI();
            return finalDamage;
        }

        public void AddClawMarkStacks(int count)
        {
            if (count <= 0) return;
            lastHitTimestamp = Time.time;
            int prev = debugClawStacks;
            debugClawStacks = YunZhongJunLogic.AddClawMarkStacks(debugClawStacks, count, YunZhongJunLogic.MaxClawMarkStacks);
            debugMarkTimeRemaining = clawMarkDuration;

            if (prev < YunZhongJunLogic.MaxClawMarkStacks && debugClawStacks >= YunZhongJunLogic.MaxClawMarkStacks)
            {
                ShowTransientStatus("5层抓击印记叠满！撕裂生效", 1.8f);
                SpawnFloatingText("五层撕裂!", new Color(1f, 0.22f, 0.25f), 30);
            }

            RefreshHeadBarUI();
        }

        public void ApplyStun(float duration, string reasonLabel = "眩晕")
        {
            if (duration <= 0f) return;
            lastHitTimestamp = Time.time;
            debugStunRemaining = Mathf.Max(debugStunRemaining, duration);
            SpawnFloatingText($"{reasonLabel} {duration:0.0}s!", new Color(1f, 0.92f, 0.2f), 28);
            RefreshHeadBarUI();
        }

        public void ApplyKnockup(float duration)
        {
            if (duration <= 0f) return;
            lastHitTimestamp = Time.time;
            knockupTotalDuration = duration;
            debugKnockupRemaining = duration;
            SpawnFloatingText("内圈击飞!", new Color(0.4f, 0.95f, 1f), 30);
            RefreshHeadBarUI();
        }

        public void ShowTransientStatus(string hint, float duration = 1.2f)
        {
            transientStatusHint = hint;
            statusHintTimer = duration;
            RefreshHeadBarUI();
        }

        private void ApplyRawHpLoss(float amount)
        {
            if (amount <= 0f) return;
            currentHp = Mathf.Max(1f, currentHp - amount);
            if (currentHp <= 1.5f)
            {
                // 假人不死保底：回满血继续练习
                currentHp = maxHp;
                SpawnFloatingText("假人生命重置", new Color(0.45f, 1f, 0.55f), 24);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. 眩晕倒计时
            if (debugStunRemaining > 0f)
            {
                debugStunRemaining = Mathf.Max(0f, debugStunRemaining - dt);
            }

            // 2. 击飞抛物线表现（假人本身无AI不水平移动，被内圈击飞时仅在原地Y轴抛起下落）
            if (debugKnockupRemaining > 0f)
            {
                debugKnockupRemaining = Mathf.Max(0f, debugKnockupRemaining - dt);
                float progress = 1f - (debugKnockupRemaining / Mathf.Max(0.05f, knockupTotalDuration));
                float yLift = 4f * knockupHeight * progress * (1f - progress);
                if (visualTransform != null)
                {
                    visualTransform.localPosition = new Vector3(0f, baseVisualLocalY + yLift, 0f);
                }
            }
            else if (visualTransform != null && Mathf.Abs(visualTransform.localPosition.y - baseVisualLocalY) > 0.001f)
            {
                visualTransform.localPosition = new Vector3(0f, baseVisualLocalY, 0f);
            }

            // 3. 抓击印记持续流血与超时衰减
            if (debugClawStacks > 0)
            {
                debugMarkTimeRemaining -= dt;
                bleedTickTimer += dt;

                if (bleedTickTimer >= bleedTickInterval)
                {
                    bleedTickTimer -= bleedTickInterval;
                    float bleedDmg = YunZhongJunLogic.CalculatePeriodicClawBleedDamage(debugClawStacks, bleedDamagePerStack);
                    if (bleedDmg > 0f)
                    {
                        lastHitTimestamp = Time.time;
                        ApplyRawHpLoss(bleedDmg);
                        SpawnFloatingText($"流血({debugClawStacks}层) -{Mathf.RoundToInt(bleedDmg)}", new Color(1f, 0.45f, 0.3f), 22);
                    }
                }

                if (debugMarkTimeRemaining <= 0f)
                {
                    debugClawStacks = 0;
                    debugMarkTimeRemaining = 0f;
                    bleedTickTimer = 0f;
                }
            }

            // 4. 敌方移动额外伤害检测：严格验证“不叠满5层印记，敌方移动就不会有额外伤害”
            // 假人无AI不会自己动，但若在编辑器Scene中拖动假人或受外力位移时自动按规则结算
            Vector3 currentPlanar = new Vector3(transform.position.x, 0f, transform.position.z);
            float movedDist = Vector3.Distance(currentPlanar, lastPlanarPosition);
            if (movedDist > 0.01f)
            {
                lastPlanarPosition = currentPlanar;
                float moveExtraDmg = YunZhongJunLogic.CalculateMovementExtraDamage(
                    debugClawStacks,
                    movedDist,
                    fullMarkMoveExtraDamagePerMeter,
                    YunZhongJunLogic.MaxClawMarkStacks);

                if (moveExtraDmg > 0f)
                {
                    lastHitTimestamp = Time.time;
                    ApplyRawHpLoss(moveExtraDmg);
                    SpawnFloatingText($"满印记移动撕裂 -{Mathf.Max(1, Mathf.RoundToInt(moveExtraDmg))}", new Color(1f, 0.15f, 0.2f), 28);
                }
            }

            // 5. 提示文本计时与脱战自动回满生命
            if (statusHintTimer > 0f)
            {
                statusHintTimer = Mathf.Max(0f, statusHintTimer - dt);
                if (statusHintTimer <= 0f) transientStatusHint = string.Empty;
            }

            if (currentHp < maxHp && Time.time - lastHitTimestamp >= autoRecoverDelay && debugClawStacks == 0)
            {
                currentHp = maxHp;
            }

            UpdatePopups(dt);
            RefreshHeadBarUI();
        }

        private void LateUpdate()
        {
            if (headBarRoot != null && UnityEngine.Camera.main != null)
            {
                headBarRoot.rotation = UnityEngine.Camera.main.transform.rotation;
            }
        }

        public void RefreshHeadBarUI()
        {
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 1f;
            }

            if (hpText != null)
            {
                hpText.text = $"{dummyName}  {Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)}";
            }

            if (clawPipImages != null)
            {
                for (int i = 0; i < clawPipImages.Length; i++)
                {
                    if (clawPipImages[i] == null) continue;
                    bool active = i < debugClawStacks;
                    if (!active)
                    {
                        clawPipImages[i].color = new Color(0.2f, 0.22f, 0.26f, 0.75f);
                    }
                    else if (debugClawStacks >= YunZhongJunLogic.MaxClawMarkStacks)
                    {
                        clawPipImages[i].color = new Color(1f, 0.18f, 0.22f, 1f);
                    }
                    else
                    {
                        clawPipImages[i].color = new Color(1f, 0.72f, 0.18f, 1f);
                    }
                }
            }

            if (statusText != null)
            {
                if (debugKnockupRemaining > 0f)
                {
                    statusText.text = $"[内圈击飞 {debugKnockupRemaining:0.1}s] 印记:{debugClawStacks}/5";
                    statusText.color = new Color(0.45f, 0.95f, 1f);
                }
                else if (debugStunRemaining > 0f)
                {
                    statusText.text = $"[眩晕 {debugStunRemaining:0.1}s] 印记:{debugClawStacks}/5";
                    statusText.color = new Color(1f, 0.92f, 0.25f);
                }
                else if (!string.IsNullOrEmpty(transientStatusHint))
                {
                    statusText.text = transientStatusHint;
                    statusText.color = new Color(1f, 0.65f, 0.25f);
                }
                else if (debugClawStacks >= YunZhongJunLogic.MaxClawMarkStacks)
                {
                    statusText.text = $"抓击印记: 5/5 [满层撕裂·移动受额外伤]";
                    statusText.color = new Color(1f, 0.28f, 0.28f);
                }
                else if (debugClawStacks > 0)
                {
                    statusText.text = $"抓击印记: {debugClawStacks}/5 (未满5层移动无额外伤)";
                    statusText.color = new Color(1f, 0.8f, 0.3f);
                }
                else
                {
                    statusText.text = "静止靶假人 (无AI)";
                    statusText.color = new Color(0.85f, 0.88f, 0.92f, 0.85f);
                }
            }
        }

        private void SpawnFloatingText(string content, Color color, int fontSize)
        {
            if (headBarRoot == null || !Application.isPlaying) return;

            GameObject go = new GameObject("DmgPopup", typeof(RectTransform));
            go.transform.SetParent(headBarRoot, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector2 startPos = new Vector2(Random.Range(-35f, 35f), 48f + Random.Range(0f, 18f));
            rt.anchoredPosition = startPos;
            rt.sizeDelta = new Vector2(360f, 50f);

            var lbl = go.AddComponent<UnityEngine.UI.Text>();
            lbl.font = popupFont != null ? popupFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lbl.text = content;
            lbl.fontSize = fontSize;
            lbl.fontStyle = FontStyle.Bold;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = color;
            lbl.raycastTarget = false;

            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            activePopups.Add(new FloatingPopup
            {
                Rect = rt,
                Label = lbl,
                Elapsed = 0f,
                Duration = 0.85f,
                BaseColor = color,
                StartAnchoredPos = startPos
            });
        }

        private void UpdatePopups(float dt)
        {
            for (int i = activePopups.Count - 1; i >= 0; i--)
            {
                var p = activePopups[i];
                if (p.Rect == null)
                {
                    activePopups.RemoveAt(i);
                    continue;
                }

                p.Elapsed += dt;
                float t = Mathf.Clamp01(p.Elapsed / p.Duration);
                p.Rect.anchoredPosition = p.StartAnchoredPos + new Vector2(0f, 65f * t);
                Color c = p.BaseColor;
                c.a = 1f - t * t;
                p.Label.color = c;

                if (p.Elapsed >= p.Duration)
                {
                    Destroy(p.Rect.gameObject);
                    activePopups.RemoveAt(i);
                }
                else
                {
                    activePopups[i] = p;
                }
            }
        }

        [ContextMenu("调试：模拟假人攻击云中君（验证威慑圈30%减伤与二技能格挡）")]
        public void DebugSimulateHitOnPlayer()
        {
            var hero = FindAnyObjectByType<YunZhongJunSkillController>();
            if (hero != null)
            {
                hero.ReceiveEnemyHit(this, 500f, applyControl: true);
            }
        }
    }
}
