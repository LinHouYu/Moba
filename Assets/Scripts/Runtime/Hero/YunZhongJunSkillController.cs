using System.Collections.Generic;
using UnityEngine;
using Moba.Runtime.Movement;
using Moba.Runtime.Combat;

namespace Moba.Runtime.Hero
{
    /// <summary>
    /// 云中君完整角色控制器（模型保持原胶囊体不变，所有数值均暴露在 Inspector 可实时调试）：
    /// 1. 展翅飞行（飞鸡）与折翼行走（走地鸡）双形态及气息机制
    /// 2. 一技能【天隙鸣】：俯冲命中生成目标红小圈 + 外层金色威慑圈（圈内减免目标30%伤害）；
    ///    命中后向后倒飞且 Y 轴向上抬升，不按普攻取消倒飞则会飞出威慑圈导致威慑消失；
    ///    保留旧版云中君 BUG：描述眩晕 0.5 秒，实际眩晕 1.0 秒（多一倍）。
    /// 3. 二技能【若英·华彩】：开启免控/格挡 Buff 状态，可格挡伤害与控制；走地鸡状态下二技能配合大招可秒变飞鸡。
    /// 4. 三技能【风雷引】：内外双圈，外圈砸不晕敌人，内圈才可击飞敌人并叠加 1 层抓击印记，落地播放 Unity 粒子抓击下雨特效。
    /// 5. 被动【抓击印记】：共 5 层，叠层持续增伤，且必须叠满 5 层印记敌方移动才会受到额外撕裂伤害。
    /// </summary>
    [RequireComponent(typeof(MobaHeroMovement))]
    [DisallowMultipleComponent]
    public class YunZhongJunSkillController : MonoBehaviour
    {
        private enum Skill1State
        {
            Idle = 0,
            Diving = 1,
            RecoilingBackward = 2
        }

        [Header("Hero Base Stats & Breath (飞鸡/走地鸡气息参数)")]
        [SerializeField] private float maxHp = 6200f;
        [SerializeField] private float currentHp = 6200f;
        [SerializeField] private float maxBreath = 100f;
        [SerializeField] [Range(0f, 100f)] private float currentBreath = 100f;
        [SerializeField] private YunZhongJunForm currentForm = YunZhongJunForm.Flying;
        [SerializeField] [Range(3f, 15f)] private float flyingMoveSpeed = 7.4f;
        [SerializeField] [Range(3f, 15f)] private float groundedMoveSpeed = 5.6f;
        [Tooltip("战斗状态持续秒数（期间飞行态若不持续爪击会消耗气息直至变走地鸡）")]
        [SerializeField] [Range(1f, 12f)] private float combatStateDuration = 5f;
        [SerializeField] [Range(0f, 50f)] private float combatBreathDrainPerSec = 18f;
        [SerializeField] [Range(0f, 50f)] private float outOfCombatBreathRegenPerSec = 12f;
        [SerializeField] [Range(0f, 40f)] private float flyingClawBreathGain = 16f;

        [Header("Basic Attack (普攻 / 补刀 / 推塔)")]
        [SerializeField] [Range(0.15f, 1.5f)] private float attackCooldown = 0.42f;
        [SerializeField] [Range(1.5f, 6f)] private float flyingAttackRange = 3.4f;
        [SerializeField] [Range(3f, 9f)] private float groundedAttackRange = 5.8f;
        [SerializeField] private float flyingAttackDamage = 185f;
        [SerializeField] private float groundedAttackDamage = 140f;

        [Header("Skill 1: 天隙鸣 (威慑圈30%减伤 / 倒飞Y轴抬升 / 旧版1秒眩晕BUG)")]
        [TextArea(2, 3)]
        [SerializeField] private string skill1Description = "加速俯冲撕裂目标，造成物理伤害并眩晕0.5秒；命中生成威慑圈降低目标对云中君30%伤害。可点普攻取消后跳倒飞。";
        [SerializeField] [Range(1f, 15f)] private float skill1Cooldown = 5.5f;
        [SerializeField] private float skill1Damage = 280f;
        [SerializeField] [Range(8f, 30f)] private float skill1DiveSpeed = 16.5f;
        [SerializeField] [Range(0.15f, 0.8f)] private float skill1DiveDuration = 0.36f;
        [Tooltip("一技能面板描述的眩晕时间（0.5秒）")]
        [SerializeField] private float skill1DescribedStunDuration = 0.5f;
        [Tooltip("保留旧版云中君BUG：勾选后实际眩晕时间为1.0秒，足足比描述0.5秒多一倍")]
        [SerializeField] private bool enableLegacyDoubleStunBug = true;
        [Tooltip("旧版BUG实际生效眩晕时间（1.0秒）")]
        [SerializeField] private float skill1ActualBugStunDuration = 1.0f;
        [Tooltip("威慑圈半径（米）：出圈即失去30%减伤威慑效果")]
        [SerializeField] [Range(2f, 8f)] private float intimidationRadius = 4.2f;
        [SerializeField] [Range(1f, 10f)] private float intimidationDuration = 5.0f;
        [SerializeField] [Range(0f, 0.9f)] private float intimidationDamageReduction = 0.30f;
        [Tooltip("一技能命中后若不按普攻取消，向后倒飞的总水平距离（大于威慑圈半径则倒飞出圈失效）")]
        [SerializeField] [Range(2f, 8f)] private float recoilDistance = 4.7f;
        [Tooltip("一技能后跳倒飞持续时间（此窗口内点普攻即可取消倒飞留在威慑圈内）")]
        [SerializeField] [Range(0.25f, 1.2f)] private float recoilDuration = 0.62f;
        [Tooltip("一技能倒飞时 Y 轴向上移动的最大抛物线高度")]
        [SerializeField] [Range(0.2f, 3f)] private float recoilArcHeight = 1.15f;

        [Header("Skill 2: 若英·华彩 (免控格挡Buff & 配合大招秒变飞鸡)")]
        [SerializeField] [Range(1f, 15f)] private float skill2Cooldown = 6.0f;
        [SerializeField] [Range(1.5f, 6f)] private float skill2Radius = 3.6f;
        [SerializeField] private float skill2Damage = 220f;
        [Tooltip("二技能免控格挡 Buff 持续时间（期间可格挡控制/伤害，且配合大招可让走地鸡秒变飞鸡）")]
        [SerializeField] [Range(0.4f, 3f)] private float skill2BuffDuration = 1.5f;
        [SerializeField] [Range(10f, 100f)] private float skill2ParryBreathGain = 50f;

        [Header("Skill 3: 风雷引 (大招内外双圈 / 内圈击飞叠印记 / 粒子抓击雨)")]
        [SerializeField] [Range(2f, 30f)] private float skill3Cooldown = 10.0f;
        [SerializeField] [Range(0.5f, 2.5f)] private float ultAscentDuration = 1.15f;
        [SerializeField] [Range(2f, 7f)] private float ultSkyHeight = 4.2f;
        [Tooltip("大招内圈半径：仅内圈可击飞敌人并叠加1层抓击印记")]
        [SerializeField] [Range(1f, 4f)] private float ultInnerRadius = 2.2f;
        [Tooltip("大招外圈半径：外圈砸不晕敌人")]
        [SerializeField] [Range(2.5f, 8f)] private float ultOuterRadius = 4.8f;
        [SerializeField] private float ultInnerDamage = 420f;
        [SerializeField] private float ultOuterDamage = 240f;
        [SerializeField] [Range(0.3f, 1.5f)] private float ultKnockupDuration = 0.75f;
        [Tooltip("单独释放大招基础回复气息（50点）；若配合二技能Buff释放则直接回满100秒变飞鸡")]
        [SerializeField] [Range(10f, 100f)] private float ultBaseBreathGain = 50f;
        [SerializeField] [Range(0.5f, 4f)] private float ultFeatherRainDuration = 1.8f;
        [SerializeField] private float ultRainTickDamage = 65f;

        [Header("Scene Visual & UI References")]
        [SerializeField] private Transform attackRangeIndicator;
        [SerializeField] private Transform intimidationRingRoot;
        [SerializeField] private Transform targetInnerRedRing;
        [SerializeField] private Transform outerGoldDeterrenceRing;
        [SerializeField] private Transform skill2ParryAura;
        [SerializeField] private Transform ultCirclesRoot;
        [SerializeField] private Transform ultInnerCircleVisual;
        [SerializeField] private Transform ultOuterCircleVisual;
        [SerializeField] private Transform featherRainRoot;
        [SerializeField] private ParticleSystem featherRainParticles;
        [SerializeField] private Transform headBarRoot;
        [SerializeField] private UnityEngine.UI.Image hpFillImage;
        [SerializeField] private UnityEngine.UI.Image breathFillImage;
        [SerializeField] private UnityEngine.UI.Text stateBadgeText;
        [SerializeField] private Font popupFont;

        [Header("Audio Settings (Inspector Tunable)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip attackSoundClip;
        [SerializeField] private AudioClip skill1SoundClip;
        [SerializeField] private AudioClip skill2SoundClip;
        [SerializeField] private AudioClip ultAscendSoundClip;
        [SerializeField] private AudioClip ultSlamSoundClip;

        [Header("Live Debug State (Read-Only)")]
        [SerializeField] private float skill1CdRemaining = 0f;
        [SerializeField] private float skill2CdRemaining = 0f;
        [SerializeField] private float skill3CdRemaining = 0f;
        [SerializeField] private float attackCdRemaining = 0f;
        [SerializeField] private float skill2BuffRemaining = 0f;
        [SerializeField] private bool isIntimidationActive = false;
        [SerializeField] private float intimidationRemaining = 0f;
        [SerializeField] private bool isUntargetable = false;

        private MobaHeroMovement heroMovement;
        private Skill1State skill1State = Skill1State.Idle;
        private float skill1StateTimer = 0f;
        private Vector3 skill1DiveDir = Vector3.forward;
        private Vector3 recoilStartPos;
        private Vector3 recoilBackwardDir;
        private Vector3 lastAppliedRecoilHoriz;
        private float recoilVerticalOffset = 0f;

        private MobaTargetDummy intimidatedTarget;
        private float combatRemaining = 0f;

        private bool isInUltAscent = false;
        private float ultAscentTimer = 0f;
        private bool isFeatherRainActive = false;
        private float featherRainTimer = 0f;
        private float featherRainTickTimer = 0f;
        private Vector3 featherRainCenter;

        private string transientHeroHint = string.Empty;
        private float transientHeroHintTimer = 0f;

        private struct HeroPopup
        {
            public RectTransform Rect;
            public UnityEngine.UI.Text Label;
            public float Elapsed;
            public float Duration;
            public Color BaseColor;
            public Vector2 StartPos;
        }

        private readonly List<HeroPopup> heroPopups = new List<HeroPopup>(8);

        public YunZhongJunForm CurrentForm => currentForm;
        public float CurrentBreath => currentBreath;
        public float MaxBreath => maxBreath;
        public float CurrentHp => currentHp;
        public float MaxHp => maxHp;

        public float Skill1CdRemaining => skill1CdRemaining;
        public float Skill1Cooldown => skill1Cooldown;
        public string Skill1Description => skill1Description;

        public float Skill2CdRemaining => skill2CdRemaining;
        public float Skill2Cooldown => skill2Cooldown;
        public bool IsSkill2BuffActive => skill2BuffRemaining > 0f;
        public float Skill2BuffRemaining => skill2BuffRemaining;

        public float Skill3CdRemaining => skill3CdRemaining;
        public float Skill3Cooldown => skill3Cooldown;
        public bool IsInUltAscent => isInUltAscent;

        public bool IsIntimidationActive => isIntimidationActive;
        public bool IsRecoilingFromSkill1 => skill1State == Skill1State.RecoilingBackward;
        public bool CanCastSkill1 => currentForm == YunZhongJunForm.Flying && skill1CdRemaining <= 0f && !isInUltAscent;
        public bool CanCastSkill2 => skill2CdRemaining <= 0f && !isInUltAscent;
        public bool CanCastSkill3 => skill3CdRemaining <= 0f && !isInUltAscent;

        public void BindVisualAndUIReferences(
            Transform intimidationGroup,
            Transform redInnerRing,
            Transform goldOuterRing,
            Transform s2Aura,
            Transform ultGroup,
            Transform ultInnerVis,
            Transform ultOuterVis,
            Transform rainRoot,
            ParticleSystem rainPs,
            Transform headBar,
            UnityEngine.UI.Image hpFill,
            UnityEngine.UI.Image breathFill,
            UnityEngine.UI.Text badgeText,
            Font uiFont)
        {
            intimidationRingRoot = intimidationGroup;
            targetInnerRedRing = redInnerRing;
            outerGoldDeterrenceRing = goldOuterRing;
            skill2ParryAura = s2Aura;
            ultCirclesRoot = ultGroup;
            ultInnerCircleVisual = ultInnerVis;
            ultOuterCircleVisual = ultOuterVis;
            featherRainRoot = rainRoot;
            featherRainParticles = rainPs;
            headBarRoot = headBar;
            hpFillImage = hpFill;
            breathFillImage = breathFill;
            stateBadgeText = badgeText;
            popupFont = uiFont;
        }

        public void BindAudioAndAttackRangeReferences(
            Transform rangeIndicator,
            AudioSource audioSrc,
            AudioClip atkClip,
            AudioClip s1Clip,
            AudioClip s2Clip,
            AudioClip uFlyClip,
            AudioClip uSlamClip)
        {
            attackRangeIndicator = rangeIndicator;
            audioSource = audioSrc;
            attackSoundClip = atkClip;
            skill1SoundClip = s1Clip;
            skill2SoundClip = s2Clip;
            ultAscendSoundClip = uFlyClip;
            ultSlamSoundClip = uSlamClip;
        }

        private void PlayAudio(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void Awake()
        {
            heroMovement = GetComponent<MobaHeroMovement>();
            AutoResolveSceneChildren();
            SyncRingScales();
            SetInitialVisualActiveStates();
        }

        private void AutoResolveSceneChildren()
        {
            if (skill2ParryAura == null) skill2ParryAura = transform.Find("Skill2_ParryAura");
            if (ultCirclesRoot == null) ultCirclesRoot = transform.Find("Ultimate_Circles_Group");
            if (ultCirclesRoot != null)
            {
                if (ultInnerCircleVisual == null) ultInnerCircleVisual = ultCirclesRoot.Find("Ult_InnerCircle");
                if (ultOuterCircleVisual == null) ultOuterCircleVisual = ultCirclesRoot.Find("Ult_OuterCircle");
            }
            if (headBarRoot == null) headBarRoot = transform.Find("Player_HeadBar");

            if (intimidationRingRoot == null)
            {
                var found = GameObject.Find("Intimidation_Ring_Group");
                if (found != null) intimidationRingRoot = found.transform;
            }
            if (intimidationRingRoot != null)
            {
                if (targetInnerRedRing == null) targetInnerRedRing = intimidationRingRoot.Find("Target_InnerRedRing");
                if (outerGoldDeterrenceRing == null) outerGoldDeterrenceRing = intimidationRingRoot.Find("Outer_DeterrenceGoldRing");
            }

            if (featherRainRoot == null)
            {
                var foundRain = GameObject.Find("Ult_FeatherRain_Root");
                if (foundRain != null)
                {
                    featherRainRoot = foundRain.transform;
                    featherRainParticles = foundRain.GetComponentInChildren<ParticleSystem>(true);
                }
            }

            if (attackRangeIndicator == null)
            {
                var foundRange = transform.Find("Hero_AttackRange_Circle");
                if (foundRange != null) attackRangeIndicator = foundRange;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void SetInitialVisualActiveStates()
        {
            if (intimidationRingRoot != null) intimidationRingRoot.gameObject.SetActive(false);
            if (skill2ParryAura != null) skill2ParryAura.gameObject.SetActive(false);
            if (ultCirclesRoot != null) ultCirclesRoot.gameObject.SetActive(false);
            if (attackRangeIndicator != null) attackRangeIndicator.gameObject.SetActive(false);
            if (featherRainParticles != null) featherRainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void SyncRingScales()
        {
            // Ground quads/sprites are unit 1x1 before scaling, so world diameter = radius * 2
            if (targetInnerRedRing != null)
            {
                targetInnerRedRing.localScale = new Vector3(2.2f, 2.2f, 1f);
            }
            if (outerGoldDeterrenceRing != null)
            {
                float d = intimidationRadius * 2f;
                outerGoldDeterrenceRing.localScale = new Vector3(d, d, 1f);
            }
            if (ultInnerCircleVisual != null)
            {
                float d = ultInnerRadius * 2f;
                ultInnerCircleVisual.localScale = new Vector3(d, d, 1f);
            }
            if (ultOuterCircleVisual != null)
            {
                float d = ultOuterRadius * 2f;
                ultOuterCircleVisual.localScale = new Vector3(d, d, 1f);
            }
        }

        // ==================== 主动操控接口（供手机虚拟按键与键盘调试调用） ====================

        /// <summary>
        /// 普通攻击 / 补刀 / 推塔按钮触发：
        /// 若正处于一技能命中后的后跳倒飞阶段，立即取消倒飞使云中君留在威慑圈内！
        /// </summary>
        public bool TriggerBasicAttack(string actionLabel = "")
        {
            if (isInUltAscent) return false;

            // 核心机制1：点普通攻击立即取消一技能的后跳倒飞！
            bool cancelledRecoil = false;
            if (skill1State == Skill1State.RecoilingBackward)
            {
                CancelSkill1Recoil();
                cancelledRecoil = true;
                attackCdRemaining = 0f; // 允许取消后摇瞬间立即打出爪击
            }

            if (attackCdRemaining > 0f) return cancelledRecoil;
            attackCdRemaining = attackCooldown;
            PlayAudio(attackSoundClip);

            MobaTargetDummy target = FindNearestDummy(
                currentForm == YunZhongJunForm.Flying ? flyingAttackRange + 1.2f : groundedAttackRange);

            if (target != null)
            {
                FaceTowards(target.transform.position);
                combatRemaining = combatStateDuration;

                if (currentForm == YunZhongJunForm.Flying)
                {
                    // 飞鸡近战贴身微追击手感 + 叠1层抓击印记 + 回复少量气息维持飞行
                    float dist = HorizontalDistance(transform.position, target.transform.position);
                    if (dist > 1.6f && dist <= flyingAttackRange + 1.2f)
                    {
                        Vector3 stepDir = (target.transform.position - transform.position);
                        stepDir.y = 0f;
                        GetComponent<CharacterController>()?.Move(stepDir.normalized * Mathf.Min(dist - 1.4f, 1.8f));
                    }

                    currentBreath = Mathf.Clamp(currentBreath + flyingClawBreathGain, 0f, maxBreath);
                    string prefix = cancelledRecoil ? "断后摇爪击" : (string.IsNullOrEmpty(actionLabel) ? "爪击" : actionLabel);
                    target.TakeSkillOrAttackDamage(flyingAttackDamage, addClawStacks: 1, customPopupPrefix: prefix);
                }
                else
                {
                    // 走地鸡普攻（不叠抓击印记）
                    string prefix = string.IsNullOrEmpty(actionLabel) ? "羽箭" : actionLabel;
                    target.TakeSkillOrAttackDamage(groundedAttackDamage, addClawStacks: 0, customPopupPrefix: prefix);
                }
            }
            else if (!string.IsNullOrEmpty(actionLabel))
            {
                ShowHeroHint($"{actionLabel}挥击", 0.6f);
            }

            if (cancelledRecoil)
            {
                ShowHeroHint("普攻取消倒飞! 保留威慑30%减伤", 1.8f);
                SpawnHeroPopup("取消倒飞! 留圈减伤30%", new Color(0.35f, 1f, 0.65f));
            }

            return true;
        }

        public bool TriggerLastHitAttack() => TriggerBasicAttack("补刀");

        public bool TriggerPushTowerAttack() => TriggerBasicAttack("推塔");

        /// <summary>
        /// 一技能【天隙鸣】：仅飞行态可用，加速俯冲命中目标。
        /// </summary>
        public bool CastSkill1()
        {
            if (currentForm != YunZhongJunForm.Flying)
            {
                ShowHeroHint("走地鸡无法用一技能(需二技能+大招变飞鸡)", 1.6f);
                SpawnHeroPopup("折翼状态不可用!", new Color(1f, 0.55f, 0.25f));
                return false;
            }

            if (skill1CdRemaining > 0f || isInUltAscent || skill1State != Skill1State.Idle)
            {
                return false;
            }

            skill1CdRemaining = skill1Cooldown;
            combatRemaining = combatStateDuration;

            MobaTargetDummy nearest = FindNearestDummy(7.5f);
            if (nearest != null)
            {
                Vector3 dir = nearest.transform.position - transform.position;
                dir.y = 0f;
                skill1DiveDir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
            }
            else
            {
                skill1DiveDir = transform.forward;
            }

            skill1State = Skill1State.Diving;
            skill1StateTimer = 0f;
            PlayAudio(skill1SoundClip);
            heroMovement.SetExternalHorizontalOverride(skill1DiveDir * skill1DiveSpeed, skill1DiveDir);
            SpawnHeroPopup("天隙鸣·加速俯冲", new Color(0.45f, 0.92f, 1f));
            return true;
        }

        /// <summary>
        /// 二技能【若英·华彩】：解除控制并进入免控格挡 Buff 状态；配合大招可让走地鸡秒变飞鸡。
        /// </summary>
        public bool CastSkill2()
        {
            if (!CanCastSkill2) return false;

            skill2CdRemaining = skill2Cooldown;
            skill2BuffRemaining = skill2BuffDuration;
            PlayAudio(skill2SoundClip);

            if (skill2ParryAura != null)
            {
                skill2ParryAura.gameObject.SetActive(true);
            }

            MobaTargetDummy dummy = FindNearestDummy(skill2Radius);
            if (dummy != null)
            {
                combatRemaining = combatStateDuration;
                dummy.TakeSkillOrAttackDamage(skill2Damage, addClawStacks: 0, customPopupPrefix: "若英·华彩");
            }

            string msg = currentForm == YunZhongJunForm.Grounded
                ? "二技能格挡Buff开启! 接大招可秒变飞鸡"
                : "二技能免控格挡Buff开启!";
            ShowHeroHint(msg, skill2BuffDuration);
            SpawnHeroPopup("免控·格挡Buff!", new Color(0.4f, 1f, 0.85f));
            return true;
        }

        /// <summary>
        /// 三技能【风雷引】：冲天无法选中 + 显示内外双圈 + 落地内圈击飞叠印记/外圈不晕 + Unity粒子抓击雨。
        /// 走地鸡在二技能 Buff 期间（或格挡后）配合大招可秒变飞鸡！
        /// </summary>
        public bool CastSkill3()
        {
            if (!CanCastSkill3) return false;

            if (skill1State != Skill1State.Idle)
            {
                CancelSkill1Recoil();
            }

            skill3CdRemaining = skill3Cooldown;

            YunZhongJunForm prevForm = currentForm;
            bool hadSkill2Buff = IsSkill2BuffActive;
            currentForm = YunZhongJunLogic.EvaluateUltimateCastBreathAndForm(
                currentForm,
                currentBreath,
                maxBreath,
                hadSkill2Buff,
                ultBaseBreathGain,
                out currentBreath);

            if (prevForm == YunZhongJunForm.Grounded && currentForm == YunZhongJunForm.Flying)
            {
                ShowHeroHint("二技能配合大招：走地鸡秒变飞鸡！", 2.2f);
                SpawnHeroPopup("走地鸡秒变飞鸡!!", new Color(1f, 0.88f, 0.25f));
            }
            else if (currentForm == YunZhongJunForm.Grounded)
            {
                ShowHeroHint($"大招回复{Mathf.RoundToInt(ultBaseBreathGain)}气息 (先开二技能再接大招可秒变飞鸡)", 2.2f);
                SpawnHeroPopup($"+{Mathf.RoundToInt(ultBaseBreathGain)}气息(未满100)", new Color(0.95f, 0.75f, 0.3f));
            }
            else
            {
                SpawnHeroPopup("风雷引·无法选中!", new Color(0.55f, 0.9f, 1f));
            }

            isInUltAscent = true;
            isUntargetable = true;
            ultAscentTimer = 0f;
            PlayAudio(ultAscendSoundClip);

            SyncRingScales();
            if (ultCirclesRoot != null)
            {
                ultCirclesRoot.gameObject.SetActive(true);
            }

            return true;
        }

        /// <summary>
        /// 承受敌方/假人伤害与控制（完整结算：大招无法选中 / 二技能格挡回气 / 一技能威慑圈30%减伤）。
        /// </summary>
        public float ReceiveEnemyHit(MobaTargetDummy attacker, float rawDamage, bool applyControl)
        {
            if (isUntargetable)
            {
                SpawnHeroPopup("无法选中·免疫!", new Color(0.6f, 0.95f, 1f));
                return 0f;
            }

            // 二技能免控格挡判定
            if (IsSkill2BuffActive)
            {
                currentBreath = YunZhongJunLogic.ApplySkill2ParryBreath(currentBreath, maxBreath, skill2ParryBreathGain);
                currentForm = YunZhongJunLogic.EvaluateFormFromBreath(currentForm, currentBreath, maxBreath, out currentBreath);
                SpawnHeroPopup($"二技能格挡成功! +{Mathf.RoundToInt(skill2ParryBreathGain)}气息", new Color(0.35f, 1f, 0.75f));
                ShowHeroHint("成功格挡控制与伤害!", 1.5f);
                return 0f;
            }

            // 一技能威慑圈 30% 减伤判定
            bool fromIntimidated = isIntimidationActive && attacker != null && attacker == intimidatedTarget;
            float finalDamage = YunZhongJunLogic.CalculateDamageTakenWithIntimidation(
                rawDamage,
                isIntimidationActive,
                fromIntimidated,
                intimidationDamageReduction);

            currentHp = Mathf.Max(1f, currentHp - finalDamage);
            if (currentHp <= 1.5f) currentHp = maxHp;

            if (fromIntimidated)
            {
                SpawnHeroPopup($"威慑减伤30%! 承伤-{Mathf.RoundToInt(finalDamage)}(原{Mathf.RoundToInt(rawDamage)})", new Color(1f, 0.82f, 0.28f));
            }
            else
            {
                SpawnHeroPopup($"承伤 -{Mathf.RoundToInt(finalDamage)}", new Color(1f, 0.4f, 0.4f));
            }

            return finalDamage;
        }

        // ==================== 每帧状态更新 ====================

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. 冷却递减
            if (skill1CdRemaining > 0f) skill1CdRemaining = Mathf.Max(0f, skill1CdRemaining - dt);
            if (skill2CdRemaining > 0f) skill2CdRemaining = Mathf.Max(0f, skill2CdRemaining - dt);
            if (skill3CdRemaining > 0f) skill3CdRemaining = Mathf.Max(0f, skill3CdRemaining - dt);
            if (attackCdRemaining > 0f) attackCdRemaining = Mathf.Max(0f, attackCdRemaining - dt);

            // 2. 二技能 Buff 状态更新
            if (skill2BuffRemaining > 0f)
            {
                skill2BuffRemaining = Mathf.Max(0f, skill2BuffRemaining - dt);
                if (skill2ParryAura != null)
                {
                    skill2ParryAura.Rotate(0f, 0f, 280f * dt, Space.Self);
                    if (skill2BuffRemaining <= 0f)
                    {
                        skill2ParryAura.gameObject.SetActive(false);
                    }
                }
            }

            // 3. 一技能俯冲与倒飞状态机
            UpdateSkill1StateMachine(dt);

            // 4. 大招升空、内外圈落点与粒子羽毛抓击雨更新
            UpdateUltimateStateMachine(dt);

            // 5. 威慑圈边界实时判定（出圈立即失去威慑减伤！）
            UpdateIntimidationRing(dt);

            // 6. 气息自然增减与飞鸡/走地鸡形态切换
            UpdateBreathAndForm(dt);

            // 7. 同步模型高度（不改变原胶囊体模型，仅通过Y轴偏移呈现飞行/落地/一技能倒飞抬升/大招冲天）
            UpdateHeroVisualHeightAndSpeed();

            // 8. 提示与飘字更新
            if (transientHeroHintTimer > 0f)
            {
                transientHeroHintTimer = Mathf.Max(0f, transientHeroHintTimer - dt);
                if (transientHeroHintTimer <= 0f) transientHeroHint = string.Empty;
            }

            UpdateHeroPopups(dt);
            RefreshPlayerHeadBar();

            // 9. 王者荣耀风格：攻击范围地面金圈动态显示 + 敌人外层锁定 Shader 变色判定
            UpdateAttackRangeAndTargetLock();
        }

        private void UpdateAttackRangeAndTargetLock()
        {
            float currentRange = (currentForm == YunZhongJunForm.Flying) ? flyingAttackRange : groundedAttackRange;

            // 1. 攻击范围地面金圈：仿王者荣耀，攻击/战斗中动态呈现，未攻击时隐藏
            bool showAttackRange = YunZhongJunLogic.ShouldShowAttackRangeIndicator(combatRemaining, attackCdRemaining, skill1State == Skill1State.Diving);
            if (attackRangeIndicator != null)
            {
                attackRangeIndicator.gameObject.SetActive(showAttackRange);
                if (showAttackRange)
                {
                    float d = currentRange * 2f;
                    attackRangeIndicator.position = new Vector3(transform.position.x, 0.035f, transform.position.z);
                    attackRangeIndicator.rotation = Quaternion.Euler(90f, 0f, 0f);
                    attackRangeIndicator.localScale = new Vector3(d, d, 1f);
                }
            }

            // 2. 目标锁定外层 Shader / 锁定环判定：
            // 攻击范围外呈现红色 Shader / 环锁定；
            // 敌人进入当前普攻攻击范围内，Shader / 环立即变为黄色偏橘色，告知玩家已锁定并可实施打击。
            var dummies = FindObjectsByType<MobaTargetDummy>();
            MobaTargetDummy nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var d in dummies)
            {
                if (d == null) continue;
                float dist = HorizontalDistance(transform.position, d.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = d;
                }
            }

            foreach (var d in dummies)
            {
                if (d == null) continue;
                if (d == nearest && nearestDist <= 25f)
                {
                    bool inRange = nearestDist <= currentRange;
                    d.SetLockVisual(isLocked: true, isInAttackRange: inRange);
                }
                else
                {
                    d.SetLockVisual(isLocked: false, isInAttackRange: false);
                }
            }
        }

        private void LateUpdate()
        {
            if (headBarRoot != null && UnityEngine.Camera.main != null)
            {
                headBarRoot.rotation = UnityEngine.Camera.main.transform.rotation;
            }

            // 确保大招地面圈始终贴地水平，不随角色旋转歪斜
            if (ultCirclesRoot != null && ultCirclesRoot.gameObject.activeSelf)
            {
                ultCirclesRoot.position = new Vector3(transform.position.x, 0.04f, transform.position.z);
                ultCirclesRoot.rotation = Quaternion.identity;
            }
        }

        private void UpdateSkill1StateMachine(float dt)
        {
            if (skill1State == Skill1State.Diving)
            {
                skill1StateTimer += dt;
                MobaTargetDummy hitDummy = FindNearestDummy(1.75f);

                if (hitDummy != null)
                {
                    OnSkill1HitTarget(hitDummy);
                }
                else if (skill1StateTimer >= skill1DiveDuration)
                {
                    // 空放结束，不触发命中后跳与威慑圈
                    skill1State = Skill1State.Idle;
                    heroMovement.ClearExternalHorizontalOverride();
                }
            }
            else if (skill1State == Skill1State.RecoilingBackward)
            {
                skill1StateTimer += dt;
                float normTime = Mathf.Clamp01(skill1StateTimer / Mathf.Max(0.1f, recoilDuration));

                YunZhongJunLogic.EvaluateSkill1RecoilArc(
                    normTime,
                    recoilBackwardDir,
                    recoilDistance,
                    recoilArcHeight,
                    out Vector3 targetHorizOffset,
                    out recoilVerticalOffset);

                Vector3 frameDelta = targetHorizOffset - lastAppliedRecoilHoriz;
                lastAppliedRecoilHoriz = targetHorizOffset;

                // 通过外部速度驱动后跳位移，同时面朝被命中的目标
                Vector3 velocity = dt > 0.0001f ? (frameDelta / dt) : Vector3.zero;
                heroMovement.SetExternalHorizontalOverride(velocity, -recoilBackwardDir);

                if (normTime >= 1f)
                {
                    CancelSkill1Recoil();
                }
            }
        }

        private void OnSkill1HitTarget(MobaTargetDummy target)
        {
            // 1. 造成伤害并叠1层抓击印记
            target.TakeSkillOrAttackDamage(skill1Damage, addClawStacks: 1, customPopupPrefix: "天隙鸣");

            // 2. 保留旧版云中君 BUG：面板描述 0.5 秒眩晕，实际眩晕 1.0 秒（足足多一倍）！
            float stunDuration = YunZhongJunLogic.GetSkill1StunDuration(
                skill1DescribedStunDuration,
                enableLegacyDoubleStunBug,
                skill1ActualBugStunDuration);
            target.ApplyStun(stunDuration, "眩晕(描述0.5s实测)");

            // 3. 激活目标脚下红色小圈 + 外围金色威慑圈（圈内降低目标30%伤害）
            ActivateIntimidation(target);

            // 4. 进入向后倒飞阶段（Y轴向上抬升；若不点普攻取消，就会倒飞出威慑圈导致威慑消失！）
            skill1State = Skill1State.RecoilingBackward;
            skill1StateTimer = 0f;
            recoilStartPos = transform.position;
            lastAppliedRecoilHoriz = Vector3.zero;

            Vector3 awayDir = transform.position - target.transform.position;
            awayDir.y = 0f;
            recoilBackwardDir = awayDir.sqrMagnitude > 0.001f ? awayDir.normalized : -skill1DiveDir;

            ShowHeroHint("命中生成威慑圈(减伤30%)！快点普攻取消倒飞否则出圈失效", 1.8f);
        }

        private void CancelSkill1Recoil()
        {
            skill1State = Skill1State.Idle;
            skill1StateTimer = 0f;
            recoilVerticalOffset = 0f;
            lastAppliedRecoilHoriz = Vector3.zero;
            heroMovement.ClearExternalHorizontalOverride();
        }

        private void ActivateIntimidation(MobaTargetDummy target)
        {
            intimidatedTarget = target;
            isIntimidationActive = true;
            intimidationRemaining = intimidationDuration;

            SyncRingScales();
            if (intimidationRingRoot != null)
            {
                intimidationRingRoot.position = new Vector3(target.transform.position.x, 0.03f, target.transform.position.z);
                intimidationRingRoot.rotation = Quaternion.identity;
                intimidationRingRoot.gameObject.SetActive(true);
            }
        }

        private void UpdateIntimidationRing(float dt)
        {
            if (!isIntimidationActive) return;

            if (intimidatedTarget == null)
            {
                DeactivateIntimidation(false);
                return;
            }

            // 跟随目标位置（若目标在编辑器中被拖动）
            Vector3 ringCenter = new Vector3(intimidatedTarget.transform.position.x, 0.03f, intimidatedTarget.transform.position.z);
            if (intimidationRingRoot != null)
            {
                intimidationRingRoot.position = ringCenter;
            }

            // 核心机制：一旦云中君出了威慑圈（例如未用普攻取消一技能倒飞），威慑效果立即消失！
            bool inside = YunZhongJunLogic.IsInsideIntimidationRing(transform.position, ringCenter, intimidationRadius);
            if (!inside)
            {
                DeactivateIntimidation(exitedBounds: true);
                return;
            }

            intimidationRemaining -= dt;
            if (intimidationRemaining <= 0f)
            {
                DeactivateIntimidation(exitedBounds: false);
            }
        }

        private void DeactivateIntimidation(bool exitedBounds)
        {
            isIntimidationActive = false;
            intimidationRemaining = 0f;
            intimidatedTarget = null;

            if (intimidationRingRoot != null)
            {
                intimidationRingRoot.gameObject.SetActive(false);
            }

            if (exitedBounds)
            {
                ShowHeroHint("已出威慑圈！30%减伤威慑效果消失", 2.0f);
                SpawnHeroPopup("出圈! 威慑效果消失", new Color(1f, 0.42f, 0.35f));
            }
        }

        private void UpdateUltimateStateMachine(float dt)
        {
            if (isInUltAscent)
            {
                ultAscentTimer += dt;
                if (ultAscentTimer >= ultAscentDuration)
                {
                    ExecuteUltimateLanding();
                }
            }

            if (isFeatherRainActive)
            {
                featherRainTimer -= dt;
                featherRainTickTimer += dt;

                if (featherRainTickTimer >= 0.45f)
                {
                    featherRainTickTimer -= 0.45f;
                    var dummies = FindObjectsByType<MobaTargetDummy>();
                    foreach (var d in dummies)
                    {
                        if (HorizontalDistance(featherRainCenter, d.transform.position) <= ultOuterRadius)
                        {
                            d.TakeSkillOrAttackDamage(ultRainTickDamage, addClawStacks: 0, customPopupPrefix: "羽箭雨");
                        }
                    }
                }

                if (featherRainTimer <= 0f)
                {
                    isFeatherRainActive = false;
                    if (featherRainParticles != null)
                    {
                        featherRainParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }
        }

        private void ExecuteUltimateLanding()
        {
            isInUltAscent = false;
            isUntargetable = false;
            combatRemaining = combatStateDuration;

            PlayAudio(ultSlamSoundClip);

            // 手机物理震动（大招风雷引猛烈砸地冲击反馈）
            try { Handheld.Vibrate(); } catch { }

            if (ultCirclesRoot != null)
            {
                ultCirclesRoot.gameObject.SetActive(false);
            }

            Vector3 landingCenter = new Vector3(transform.position.x, 0f, transform.position.z);
            featherRainCenter = landingCenter;

            // 1. 判定大招内圈与外圈效果
            var dummies = FindObjectsByType<MobaTargetDummy>();
            foreach (var dummy in dummies)
            {
                UltimateHitZone zone = YunZhongJunLogic.EvaluateUltimateLandingZone(
                    landingCenter,
                    dummy.transform.position,
                    ultInnerRadius,
                    ultOuterRadius,
                    out bool canKnockup,
                    out int stacksToAdd);

                if (zone == UltimateHitZone.InnerRing)
                {
                    // 内圈：高额伤害 + 击飞 + 叠加1层抓击印记！
                    dummy.TakeSkillOrAttackDamage(ultInnerDamage, addClawStacks: stacksToAdd, customPopupPrefix: "大招内圈");
                    if (canKnockup)
                    {
                        dummy.ApplyKnockup(ultKnockupDuration);
                    }
                }
                else if (zone == UltimateHitZone.OuterRingOnly)
                {
                    // 外圈：砸不晕敌人，不击飞，不叠印记！
                    dummy.TakeSkillOrAttackDamage(ultOuterDamage, addClawStacks: 0, customPopupPrefix: "大招外圈(不晕)");
                    dummy.ShowTransientStatus("大招外圈命中：不击飞/不眩晕", 1.8f);
                }
            }

            // 2. 启动 Unity ParticleSystem 抓击下雨效果
            isFeatherRainActive = true;
            featherRainTimer = ultFeatherRainDuration;
            featherRainTickTimer = 0f;

            if (featherRainRoot != null)
            {
                featherRainRoot.position = new Vector3(landingCenter.x, 0.02f, landingCenter.z);
                featherRainRoot.gameObject.SetActive(true);
            }
            if (featherRainParticles != null)
            {
                var shape = featherRainParticles.shape;
                shape.radius = ultOuterRadius;
                featherRainParticles.Clear();
                featherRainParticles.Play(true);
            }
        }

        private void UpdateBreathAndForm(float dt)
        {
            if (isInUltAscent) return;

            if (combatRemaining > 0f)
            {
                combatRemaining = Mathf.Max(0f, combatRemaining - dt);
                if (currentForm == YunZhongJunForm.Flying)
                {
                    currentBreath -= combatBreathDrainPerSec * dt;
                }
            }
            else
            {
                currentBreath += outOfCombatBreathRegenPerSec * dt;
            }

            YunZhongJunForm prevForm = currentForm;
            currentForm = YunZhongJunLogic.EvaluateFormFromBreath(currentForm, currentBreath, maxBreath, out currentBreath);

            if (prevForm == YunZhongJunForm.Flying && currentForm == YunZhongJunForm.Grounded)
            {
                ShowHeroHint("气息耗尽变为【走地鸡】！可用 二技能+大招 秒变飞鸡", 2.8f);
                SpawnHeroPopup("折翼·走地鸡", new Color(1f, 0.6f, 0.25f));
            }
            else if (prevForm == YunZhongJunForm.Grounded && currentForm == YunZhongJunForm.Flying)
            {
                ShowHeroHint("气息盈满·展翅变为【飞鸡】！", 1.8f);
                SpawnHeroPopup("展翅·飞鸡!", new Color(0.4f, 0.95f, 1f));
            }
        }

        private void UpdateHeroVisualHeightAndSpeed()
        {
            if (heroMovement == null) return;

            // 速度随形态与大招升空变化
            if (isInUltAscent)
            {
                heroMovement.MoveSpeed = flyingMoveSpeed * 1.18f;
            }
            else
            {
                heroMovement.MoveSpeed = currentForm == YunZhongJunForm.Flying ? flyingMoveSpeed : groundedMoveSpeed;
            }

            // 模型 Y 轴高度偏移（保持原胶囊体模型不变）
            float targetYOffset = 0f;
            if (isInUltAscent)
            {
                float progress = Mathf.Clamp01(ultAscentTimer / 0.25f);
                targetYOffset = Mathf.Lerp(0.4f, ultSkyHeight, progress);
            }
            else if (skill1State == Skill1State.RecoilingBackward)
            {
                float flyBase = currentForm == YunZhongJunForm.Flying ? 0.35f : 0f;
                targetYOffset = flyBase + recoilVerticalOffset;
            }
            else if (currentForm == YunZhongJunForm.Flying)
            {
                targetYOffset = 0.35f + Mathf.Sin(Time.time * 4.2f) * 0.06f;
            }

            heroMovement.VisualExtraHeightOffset = targetYOffset;
        }

        private void RefreshPlayerHeadBar()
        {
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 1f;
            }

            if (breathFillImage != null)
            {
                breathFillImage.fillAmount = maxBreath > 0f ? Mathf.Clamp01(currentBreath / maxBreath) : 1f;
                breathFillImage.color = currentForm == YunZhongJunForm.Flying
                    ? new Color(0.25f, 0.92f, 1f, 1f)
                    : new Color(0.95f, 0.55f, 0.2f, 1f);
            }

            if (stateBadgeText != null)
            {
                string formTag = currentForm == YunZhongJunForm.Flying ? "飞鸡(展翅)" : "走地鸡(折翼)";
                if (!string.IsNullOrEmpty(transientHeroHint))
                {
                    stateBadgeText.text = transientHeroHint;
                    stateBadgeText.color = new Color(1f, 0.92f, 0.35f);
                }
                else if (isInUltAscent)
                {
                    stateBadgeText.text = $"{formTag} | 大招冲天(无法选中)";
                    stateBadgeText.color = new Color(0.5f, 0.95f, 1f);
                }
                else if (isIntimidationActive)
                {
                    stateBadgeText.text = $"{formTag} | 威慑圈内:减伤30% ({intimidationRemaining:0.1}s)";
                    stateBadgeText.color = new Color(1f, 0.85f, 0.25f);
                }
                else if (IsSkill2BuffActive)
                {
                    stateBadgeText.text = $"{formTag} | 二技能免控格挡Buff ({skill2BuffRemaining:0.1}s)";
                    stateBadgeText.color = new Color(0.4f, 1f, 0.8f);
                }
                else
                {
                    stateBadgeText.text = $"云中君 [{formTag}] 气息:{Mathf.RoundToInt(currentBreath)}/100";
                    stateBadgeText.color = currentForm == YunZhongJunForm.Flying
                        ? new Color(0.75f, 0.96f, 1f)
                        : new Color(1f, 0.72f, 0.42f);
                }
            }
        }

        private MobaTargetDummy FindNearestDummy(float maxRange)
        {
            var dummies = FindObjectsByType<MobaTargetDummy>();
            MobaTargetDummy best = null;
            float bestDist = maxRange;
            foreach (var d in dummies)
            {
                if (d == null) continue;
                float dist = HorizontalDistance(transform.position, d.transform.position);
                if (dist <= bestDist)
                {
                    bestDist = dist;
                    best = d;
                }
            }
            return best;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void FaceTowards(Vector3 worldTarget)
        {
            Vector3 flat = worldTarget - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
        }

        private void ShowHeroHint(string hint, float duration)
        {
            transientHeroHint = hint;
            transientHeroHintTimer = duration;
        }

        private void SpawnHeroPopup(string text, Color color)
        {
            if (headBarRoot == null || !Application.isPlaying) return;

            GameObject go = new GameObject("HeroPopup", typeof(RectTransform));
            go.transform.SetParent(headBarRoot, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector2 startPos = new Vector2(0f, 52f);
            rt.anchoredPosition = startPos;
            rt.sizeDelta = new Vector2(420f, 46f);

            var lbl = go.AddComponent<UnityEngine.UI.Text>();
            lbl.font = popupFont != null ? popupFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lbl.text = text;
            lbl.fontSize = 24;
            lbl.fontStyle = FontStyle.Bold;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = color;
            lbl.raycastTarget = false;

            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            heroPopups.Add(new HeroPopup
            {
                Rect = rt,
                Label = lbl,
                Elapsed = 0f,
                Duration = 0.95f,
                BaseColor = color,
                StartPos = startPos
            });
        }

        private void UpdateHeroPopups(float dt)
        {
            for (int i = heroPopups.Count - 1; i >= 0; i--)
            {
                var p = heroPopups[i];
                if (p.Rect == null)
                {
                    heroPopups.RemoveAt(i);
                    continue;
                }

                p.Elapsed += dt;
                float t = Mathf.Clamp01(p.Elapsed / p.Duration);
                p.Rect.anchoredPosition = p.StartPos + new Vector2(0f, 55f * t);
                Color c = p.BaseColor;
                c.a = 1f - t * t;
                p.Label.color = c;

                if (p.Elapsed >= p.Duration)
                {
                    Destroy(p.Rect.gameObject);
                    heroPopups.RemoveAt(i);
                }
                else
                {
                    heroPopups[i] = p;
                }
            }
        }
    }
}
