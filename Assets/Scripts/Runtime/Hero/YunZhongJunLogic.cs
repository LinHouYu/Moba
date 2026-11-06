using UnityEngine;

namespace Moba.Runtime.Hero
{
    public enum YunZhongJunForm
    {
        Flying = 0,   // 飞鸡（展翅飞行）
        Grounded = 1  // 走地鸡（折翼行走）
    }

    public enum UltimateHitZone
    {
        None = 0,
        OuterRingOnly = 1, // 外圈：造成伤害，但砸不晕/不击飞，不叠印记
        InnerRing = 2      // 内圈：造成伤害 + 击飞 + 叠加1层抓击印记
    }

    public enum PterosaurAnimState
    {
        Idle = 0,        // 静止待机 (idle, 3.0s Loop)
        Walk = 1,        // 半截摇杆四足迈步 (walk, 1.0s Loop)
        Sprint = 2,      // 摇杆拉满/一技能疾跑冲刺 (sprint, 0.667s Loop)
        FlySlam = 3,     // 三技能飞天砸地 (fly_slam, 3.6s Once, T=2.40s 砸地暴击)
        AttackClaw = 4   // 普通攻击/利爪挥击 (attack_claw, 0.85s Once, 0.34s打击)
    }

    /// <summary>
    /// 云中君/翼龙核心机制纯逻辑计算（无 MonoBehaviour 耦合，支持完整单元测试验证）。
    /// 涵盖：
    /// 1. 飞鸡/走地鸡气息转换与“二技能配合大招秒变飞鸡”
    /// 2. 一技能命中威慑圈（圈内降低目标对云中君30%伤害、出圈失效）、倒飞Y轴抛物线、旧版1秒眩晕BUG（描述0.5秒实际1.0秒）
    /// 3. 大招内外圈判定（外圈不晕，内圈击飞+叠1层印记）
    /// 4. 五层抓击印记（叠层增伤，且必须叠满5层敌方移动才有撕裂额外伤害）
    /// 5. 翼龙动画状态机映射（idle / walk / sprint / fly_slam）与摄像机微震动衰减计算
    /// </summary>
    public static class YunZhongJunLogic
    {
        public const int MaxClawMarkStacks = 5;
        public const float DefaultIntimidationDamageReduction = 0.30f;
        public const float DescribedSkill1StunDuration = 0.5f;
        public const float LegacyBugActualSkill1StunDuration = 1.0f;
        public const float FlySlamImpactTimestamp = 2.40f;
        public const float FlySlamTotalDuration = 3.60f;
        public const float DefaultAttackClawDuration = 0.85f;
        public const float DefaultAttackClawStrikeTime = 0.34f;
        public const float DefaultWalkThreshold = 0.05f;
        public const float DefaultSprintThreshold = 0.75f;

        public static readonly Color TargetLockRedColor = new Color(1f, 0.18f, 0.18f, 1f);
        public static readonly Color TargetLockGoldenOrangeColor = new Color(1f, 0.62f, 0.08f, 1f);

        /// <summary>
        /// 判断目标是否处于云中君当前普攻射程之内（忽略Y轴高度，按地面平面距离计算）。
        /// </summary>
        public static bool IsTargetInAttackRange(Vector3 heroPos, Vector3 targetPos, float attackRange)
        {
            if (attackRange <= 0f) return false;
            float dx = heroPos.x - targetPos.x;
            float dz = heroPos.z - targetPos.z;
            return (dx * dx + dz * dz) <= (attackRange * attackRange);
        }

        /// <summary>
        /// 仿王者荣耀攻击范围显示规则：未攻击时隐藏攻击范围，攻击时或战斗中显示攻击范围。
        /// </summary>
        public static bool ShouldShowAttackRangeIndicator(float combatRemaining, float attackCdRemaining, bool isAttacking = false)
        {
            return isAttacking || combatRemaining > 0f || attackCdRemaining > 0f;
        }

        /// <summary>
        /// 目标锁定外层 Shader / 指示器颜色判定：
        /// 攻击范围内为金色偏橘色让玩家知晓可以攻击；攻击范围外为红色锁定。
        /// </summary>
        public static Color GetTargetLockOutlineColor(bool isInAttackRange)
        {
            return isInAttackRange ? TargetLockGoldenOrangeColor : TargetLockRedColor;
        }

        /// <summary>
        /// 旧版云中君一技能眩晕时间计算：
        /// 技能描述写明眩晕 0.5 秒，开启保留旧版 BUG 时实际眩晕 1.0 秒（足足多一倍）。
        /// </summary>
        public static float GetSkill1StunDuration(
            float describedDuration = DescribedSkill1StunDuration,
            bool enableLegacyDoubleStunBug = true,
            float actualBugDuration = LegacyBugActualSkill1StunDuration)
        {
            return enableLegacyDoubleStunBug
                ? Mathf.Max(0f, actualBugDuration)
                : Mathf.Max(0f, describedDuration);
        }

        /// <summary>
        /// 判断云中君是否处于目标的威慑圈内（忽略Y轴高度，按地面水平距离判定，倒飞出圈即失效）。
        /// </summary>
        public static bool IsInsideIntimidationRing(Vector3 heroPosition, Vector3 ringCenterPosition, float ringRadius)
        {
            if (ringRadius <= 0f) return false;
            float dx = heroPosition.x - ringCenterPosition.x;
            float dz = heroPosition.z - ringCenterPosition.z;
            return (dx * dx + dz * dz) <= (ringRadius * ringRadius);
        }

        /// <summary>
        /// 计算威慑圈减伤后的最终承受伤害：
        /// 在威慑圈内降低被威慑目标对云中君 30% 的伤害；出圈或非威慑目标则无减伤。
        /// </summary>
        public static float CalculateDamageTakenWithIntimidation(
            float rawDamage,
            bool isIntimidationActive,
            bool isFromIntimidatedTarget,
            float reductionRatio = DefaultIntimidationDamageReduction)
        {
            float clampedDamage = Mathf.Max(0f, rawDamage);
            if (!isIntimidationActive || !isFromIntimidatedTarget)
            {
                return clampedDamage;
            }

            float clampedReduction = Mathf.Clamp01(reductionRatio);
            return clampedDamage * (1f - clampedReduction);
        }

        /// <summary>
        /// 计算一技能命中目标后，未用普攻取消时的后跳倒飞位移与 Y 轴抬升高度。
        /// </summary>
        /// <param name="normalizedTime">倒飞进度 [0, 1]</param>
        /// <param name="backwardDirXZ">水平后退单位方向</param>
        /// <param name="totalRecoilDistance">倒飞总水平距离（大于威慑圈半径时，不点普攻取消就会飞出威慑圈）</param>
        /// <param name="arcMaxHeight">倒飞时 Y 轴向上抬升的最大高度</param>
        /// <param name="horizontalDeltaFromStart">相对于起跳点的水平位移</param>
        /// <param name="verticalOffset">相对于基础高度的 Y 轴向上偏移</param>
        public static void EvaluateSkill1RecoilArc(
            float normalizedTime,
            Vector3 backwardDirXZ,
            float totalRecoilDistance,
            float arcMaxHeight,
            out Vector3 horizontalDeltaFromStart,
            out float verticalOffset)
        {
            float t = Mathf.Clamp01(normalizedTime);
            Vector3 flatDir = new Vector3(backwardDirXZ.x, 0f, backwardDirXZ.z);
            if (flatDir.sqrMagnitude > 0.0001f)
            {
                flatDir.Normalize();
            }

            horizontalDeltaFromStart = flatDir * (Mathf.Max(0f, totalRecoilDistance) * t);
            // 抛物线抬升：在倒飞过程中 Y 轴往上面移动一点，落地回落
            verticalOffset = 4f * Mathf.Max(0f, arcMaxHeight) * t * (1f - t);
        }

        /// <summary>
        /// 常规气息变化与形态判定：
        /// - 飞行态（飞鸡）气息归零时折翼变为走地鸡；
        /// - 走地鸡必须气息回满（>= maxBreath）才能重新展翅变回飞鸡。
        /// </summary>
        public static YunZhongJunForm EvaluateFormFromBreath(
            YunZhongJunForm currentForm,
            float currentBreath,
            float maxBreath,
            out float clampedBreath)
        {
            float safeMax = Mathf.Max(1f, maxBreath);
            clampedBreath = Mathf.Clamp(currentBreath, 0f, safeMax);

            if (currentForm == YunZhongJunForm.Flying)
            {
                return clampedBreath <= 0.001f ? YunZhongJunForm.Grounded : YunZhongJunForm.Flying;
            }
            else
            {
                return clampedBreath >= safeMax - 0.001f ? YunZhongJunForm.Flying : YunZhongJunForm.Grounded;
            }
        }

        /// <summary>
        /// 二技能成功格挡控制/攻击时的气息回复。
        /// </summary>
        public static float ApplySkill2ParryBreath(float currentBreath, float maxBreath, float parryBreathGain = 50f)
        {
            return Mathf.Clamp(currentBreath + Mathf.Max(0f, parryBreathGain), 0f, Mathf.Max(1f, maxBreath));
        }

        /// <summary>
        /// 释放大招时的气息与形态结算：
        /// - 若单独开大（未开二技能 Buff 且未格挡攒气），走地鸡（0气息）仅回复 ultBaseBreathGain（如50点），不足以秒变飞鸡；
        /// - 若二技能处于 Buff 状态配合大招释放（或二技能格挡回复气息后再接大招使气息满100），走地鸡立即秒变飞鸡！
        /// </summary>
        public static YunZhongJunForm EvaluateUltimateCastBreathAndForm(
            YunZhongJunForm currentForm,
            float currentBreath,
            float maxBreath,
            bool isSkill2BuffActive,
            float ultBaseBreathGain,
            out float resultingBreath)
        {
            float safeMax = Mathf.Max(1f, maxBreath);
            if (isSkill2BuffActive)
            {
                resultingBreath = safeMax;
                return YunZhongJunForm.Flying;
            }

            float nextBreath = Mathf.Clamp(currentBreath + Mathf.Max(0f, ultBaseBreathGain), 0f, safeMax);
            return EvaluateFormFromBreath(currentForm, nextBreath, safeMax, out resultingBreath);
        }

        /// <summary>
        /// 大招内外圈命中判定：
        /// - 内圈（<= innerRadius）：可击飞敌人，并叠加 1 层抓击印记；
        /// - 外圈（innerRadius < dist <= outerRadius）：砸不晕/不击飞敌人，不叠加印记；
        /// - 圈外（> outerRadius）：未命中。
        /// </summary>
        public static UltimateHitZone EvaluateUltimateLandingZone(
            Vector3 ultCenter,
            Vector3 targetPosition,
            float innerRadius,
            float outerRadius,
            out bool canKnockup,
            out int clawMarkStacksToAdd)
        {
            float safeInner = Mathf.Max(0f, innerRadius);
            float safeOuter = Mathf.Max(safeInner, outerRadius);

            float dx = targetPosition.x - ultCenter.x;
            float dz = targetPosition.z - ultCenter.z;
            float distSq = dx * dx + dz * dz;

            if (distSq <= safeInner * safeInner)
            {
                canKnockup = true;
                clawMarkStacksToAdd = 1;
                return UltimateHitZone.InnerRing;
            }

            if (distSq <= safeOuter * safeOuter)
            {
                canKnockup = false;
                clawMarkStacksToAdd = 0;
                return UltimateHitZone.OuterRingOnly;
            }

            canKnockup = false;
            clawMarkStacksToAdd = 0;
            return UltimateHitZone.None;
        }

        /// <summary>
        /// 叠加抓击印记层数（上限 5 层）。
        /// </summary>
        public static int AddClawMarkStacks(int currentStacks, int addCount, int maxStacks = MaxClawMarkStacks)
        {
            int safeMax = Mathf.Max(1, maxStacks);
            return Mathf.Clamp(currentStacks + Mathf.Max(0, addCount), 0, safeMax);
        }

        /// <summary>
        /// 抓击印记持续增伤计算：叠加印记层数越高，造成的伤害持续增加。
        /// </summary>
        public static float CalculateDamageWithClawMarkBonus(
            float baseDamage,
            int currentStacks,
            float bonusPerStackRatio = 0.12f)
        {
            int clampedStacks = Mathf.Clamp(currentStacks, 0, MaxClawMarkStacks);
            float multiplier = 1f + clampedStacks * Mathf.Max(0f, bonusPerStackRatio);
            return Mathf.Max(0f, baseDamage) * multiplier;
        }

        /// <summary>
        /// 抓击印记周期持续流血伤害（随印记层数递增）。
        /// </summary>
        public static float CalculatePeriodicClawBleedDamage(int currentStacks, float damagePerStackTick = 12f)
        {
            int clampedStacks = Mathf.Clamp(currentStacks, 0, MaxClawMarkStacks);
            if (clampedStacks <= 0) return 0f;
            return clampedStacks * Mathf.Max(0f, damagePerStackTick);
        }

        /// <summary>
        /// 敌方移动额外撕裂伤害计算：
        /// 严格遵守：不叠满 5 层印记（0~4层），敌方移动绝对不会有额外伤害（返回 0）；
        /// 只有叠满 5 层印记时，敌方每移动一定距离才触发额外撕裂伤害。
        /// </summary>
        public static float CalculateMovementExtraDamage(
            int currentStacks,
            float distanceMoved,
            float extraDamagePerMeter = 45f,
            int requiredFullStacks = MaxClawMarkStacks)
        {
            if (currentStacks < requiredFullStacks || distanceMoved <= 0.001f)
            {
                return 0f;
            }

            return distanceMoved * Mathf.Max(0f, extraDamagePerMeter);
        }

        /// <summary>
        /// 翼龙动作状态判定：
        /// - 正在释放三技能大招（fly_slam）时，最高优先级播放 FlySlam；
        /// - 正在释放一技能蓝色冲刺时，优先播放 Sprint 冲刺动画；
        /// - 处于普攻攻击前摇/撕抓期（isAttacking）时，播放 AttackClaw 利爪攻击动画；
        /// - 虚拟摇杆拉满（>= sprintThreshold）时，调用 Sprint 动画；
        /// - 玩家拖动半截摇杆（>= walkThreshold 且 < sprintThreshold）时，调用 Walk 动画；
        /// - 英雄不移动（< walkThreshold）时，采用 Idle 动画。
        /// </summary>
        public static PterosaurAnimState EvaluatePterosaurAnimationState(
            float joystickMagnitude,
            bool isDashingSkill1,
            bool isPlayingFlySlam,
            float walkThreshold,
            float sprintThreshold)
        {
            return EvaluatePterosaurAnimationState(
                joystickMagnitude,
                isDashingSkill1,
                isPlayingFlySlam,
                isAttacking: false,
                walkThreshold,
                sprintThreshold);
        }

        public static PterosaurAnimState EvaluatePterosaurAnimationState(
            float joystickMagnitude,
            bool isDashingSkill1,
            bool isPlayingFlySlam,
            bool isAttacking = false,
            float walkThreshold = DefaultWalkThreshold,
            float sprintThreshold = DefaultSprintThreshold)
        {
            if (isPlayingFlySlam)
            {
                return PterosaurAnimState.FlySlam;
            }

            if (isDashingSkill1)
            {
                return PterosaurAnimState.Sprint;
            }

            if (isAttacking)
            {
                return PterosaurAnimState.AttackClaw;
            }

            float safeWalk = Mathf.Clamp(walkThreshold, 0.001f, 0.9f);
            float safeSprint = Mathf.Clamp(sprintThreshold, safeWalk + 0.05f, 0.99f);
            float mag = Mathf.Max(0f, joystickMagnitude);

            if (mag >= safeSprint)
            {
                return PterosaurAnimState.Sprint;
            }

            if (mag >= safeWalk)
            {
                return PterosaurAnimState.Walk;
            }

            return PterosaurAnimState.Idle;
        }

        /// <summary>
        /// 获取翼龙动画状态对应的标准 FBX Clip 名称。
        /// </summary>
        public static string GetAnimationClipName(PterosaurAnimState state)
        {
            switch (state)
            {
                case PterosaurAnimState.Walk:
                    return "walk";
                case PterosaurAnimState.Sprint:
                    return "sprint";
                case PterosaurAnimState.FlySlam:
                    return "fly_slam";
                case PterosaurAnimState.AttackClaw:
                    return "attack_claw";
                case PterosaurAnimState.Idle:
                default:
                    return "idle";
            }
        }

        /// <summary>
        /// 计算三技能 fly_slam（总长3.6s，T=2.40s巨爪砸地）期间的额外高度曲线：
        /// - 0.00 ~ 0.25s：地面蓄力下蹲（0m）
        /// - 0.25 ~ 0.80s：起飞蓄力冲天（平滑升至 maxSkyHeight）
        /// - 0.80 ~ 1.80s：空中扇翼盘旋（保持在 maxSkyHeight 附近微幅扇翼起伏）
        /// - 1.80 ~ 2.40s：收翼俯冲砸向地面（在 T=2.40s 砸地暴击瞬间精准归零 0m）
        /// - 2.40 ~ 3.60s：砸地暴击与收翼挺身回归待机（0m）
        /// </summary>
        public static float EvaluateFlySlamHeightOffset(
            float elapsed,
            float slamImpactTime = FlySlamImpactTimestamp,
            float maxSkyHeight = 2.2f)
        {
            if (elapsed <= 0.25f || elapsed >= slamImpactTime || maxSkyHeight <= 0f)
            {
                return 0f;
            }

            float takeoffEnd = Mathf.Min(0.80f, slamImpactTime * 0.35f);
            float diveStart = Mathf.Clamp(1.80f, takeoffEnd + 0.1f, slamImpactTime - 0.1f);

            if (elapsed < takeoffEnd)
            {
                float t = Mathf.InverseLerp(0.25f, takeoffEnd, elapsed);
                float smooth = t * t * (3f - 2f * t);
                return maxSkyHeight * smooth;
            }

            if (elapsed < diveStart)
            {
                float flapWave = Mathf.Sin((elapsed - takeoffEnd) * Mathf.PI * 4f) * 0.12f;
                return Mathf.Max(0f, maxSkyHeight + flapWave);
            }

            // 1.80s ~ 2.40s 急速俯冲砸地
            float diveT = Mathf.InverseLerp(diveStart, slamImpactTime, elapsed);
            float diveCurve = 1f - (diveT * diveT);
            return Mathf.Max(0f, maxSkyHeight * diveCurve);
        }

        /// <summary>
        /// 计算摄像机微震动位移偏移（用于一技能撞击敌人微震与三技能砸地微震，二次包络平滑衰减至0）。
        /// </summary>
        public static Vector3 EvaluateCameraShakeOffset(
            float elapsed,
            float duration,
            float magnitude,
            float frequency = 32f)
        {
            if (duration <= 0.0001f || elapsed < 0f || elapsed >= duration || magnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float damper = (1f - normalizedTime) * (1f - normalizedTime);
            float phase = elapsed * Mathf.Max(1f, frequency) * Mathf.PI * 2f;

            float offsetX = Mathf.Sin(phase) * magnitude * damper;
            float offsetY = Mathf.Cos(phase * 1.35f) * (magnitude * 0.85f) * damper;
            float offsetZ = Mathf.Sin(phase * 0.75f + 1.1f) * (magnitude * 0.55f) * damper;

            return new Vector3(offsetX, offsetY, offsetZ);
        }

        /// <summary>
        /// 技能1与技能3冲击波/半圆穹顶特效透明度计算（保持在 0.20 左右，玩家既能看清冲击波又能看清角色）。
        /// </summary>
        public static float EvaluateShockwaveDomeAlpha(float progress, float peakAlpha = 0.20f)
        {
            float norm = Mathf.Clamp01(progress);
            return (1f - norm) * Mathf.Clamp01(peakAlpha);
        }

        /// <summary>
        /// 普通攻击与技能冲刺特效透明淡蓝色计算（透明度 0.2 左右，透光不遮挡角色）。
        /// </summary>
        public static Color EvaluateTransparentVfxColor(float alpha = 0.20f, bool isLightBlue = true)
        {
            float safeAlpha = Mathf.Clamp(alpha, 0.05f, 0.35f);
            return isLightBlue 
                ? new Color(0.25f, 0.82f, 1f, safeAlpha)
                : new Color(0.18f, 0.72f, 1f, safeAlpha);
        }

        /// <summary>
        /// 判断是否应该播放展翅冲刺攻击特效：
        /// 规则：走地鸡形态下不显示任何特效；只有在展翅形态下，且攻击行为判定为冲刺贴身撞击目标时才显示特效。
        /// </summary>
        public static bool ShouldPlayFlyingAttackDashVfx(
            YunZhongJunForm form,
            bool hasTarget,
            float distanceToTarget,
            float minDashDistance = 1.2f,
            float maxDashDistance = 4.6f,
            bool isCancellingRecoil = false)
        {
            if (form != YunZhongJunForm.Flying)
            {
                return false;
            }

            if (!hasTarget)
            {
                return false;
            }

            if (isCancellingRecoil)
            {
                return true;
            }

            return distanceToTarget > minDashDistance && distanceToTarget <= maxDashDistance;
        }

        /// <summary>
        /// 判断是否应该锁定面向目标：
        /// 规则：攻击期间若动画未播放完毕（isAttackAnimPlaying == true）且存在目标时，角色必须持续锁定准星看向敌人；
        /// 只有动画完全结束后（isAttackAnimPlaying == false）才能解除准星锁定，恢复跟随移动方向自由转向。
        /// </summary>
        public static bool ShouldLockFacingToTarget(bool isAttackAnimPlaying, bool hasTarget)
        {
            return isAttackAnimPlaying && hasTarget;
        }

        /// <summary>
        /// 计算锁定敌人的平面 Y 轴朝向角度（0~360度）。
        /// </summary>
        public static float CalculateLockedFacingAngle(Vector3 heroPos, Vector3 targetPos)
        {
            Vector3 diff = targetPos - heroPos;
            diff.y = 0f;
            if (diff.sqrMagnitude < 0.0001f) return 0f;
            float angle = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// 计算 MOBA 角色移动水平位移向量：
        /// 当准星锁定敌人时，角色位移方向必须跟随摇杆输入方向（inputMoveDirection），而非被锁定的身体朝向强制拉扯。
        /// </summary>
        public static Vector3 CalculateMobaMovementVelocity(
            Vector3 inputMoveDirection,
            float horizontalSpeed,
            bool hasExternalOverride,
            Vector3 externalVelocity)
        {
            if (hasExternalOverride)
            {
                return externalVelocity;
            }

            Vector3 flatDir = new Vector3(inputMoveDirection.x, 0f, inputMoveDirection.z);
            if (flatDir.sqrMagnitude > 0.0001f)
            {
                flatDir.Normalize();
            }

            return flatDir * Mathf.Max(0f, horizontalSpeed);
        }
    }
}
