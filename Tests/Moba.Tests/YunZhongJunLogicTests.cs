using UnityEngine;
using Moba.Runtime.Hero;

namespace Moba.Tests
{
    [TestFixture]
    public class YunZhongJunLogicTests
    {
        [Test]
        public void Skill1StunBug_LegacyEnabled_ReturnsOneSecondDoubleOfDescribedHalfSecond()
        {
            float described = 0.5f;
            float actualWithBug = YunZhongJunLogic.GetSkill1StunDuration(described, enableLegacyDoubleStunBug: true, actualBugDuration: 1.0f);
            float actualFixed = YunZhongJunLogic.GetSkill1StunDuration(described, enableLegacyDoubleStunBug: false, actualBugDuration: 1.0f);

            Assert.That(actualWithBug, Is.EqualTo(1.0f).Within(0.0001f));
            Assert.That(actualWithBug, Is.EqualTo(described * 2f).Within(0.0001f));
            Assert.That(actualFixed, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void IntimidationRing_InsideReducesDamageBy30Percent_OutsideLosesEffect()
        {
            Vector3 targetCenter = new Vector3(0f, 0f, 5f);
            float radius = 4.2f;

            Vector3 insidePos = new Vector3(0f, 0f, 2.0f); // dist = 3.0m <= 4.2m
            Vector3 outsidePos = new Vector3(0f, 0f, -0.5f); // dist = 5.5m > 4.2m

            Assert.That(YunZhongJunLogic.IsInsideIntimidationRing(insidePos, targetCenter, radius), Is.True);
            Assert.That(YunZhongJunLogic.IsInsideIntimidationRing(outsidePos, targetCenter, radius), Is.False);

            float reducedDmg = YunZhongJunLogic.CalculateDamageTakenWithIntimidation(1000f, isIntimidationActive: true, isFromIntimidatedTarget: true, 0.30f);
            float unreducedOutside = YunZhongJunLogic.CalculateDamageTakenWithIntimidation(1000f, isIntimidationActive: false, isFromIntimidatedTarget: true, 0.30f);

            Assert.That(reducedDmg, Is.EqualTo(700f).Within(0.01f));
            Assert.That(unreducedOutside, Is.EqualTo(1000f).Within(0.01f));
        }

        [Test]
        public void Skill1Recoil_WithoutBasicAttackCancel_LiftsYAndExitsIntimidationRing()
        {
            Vector3 targetPos = new Vector3(0f, 0f, 4.5f);
            Vector3 hitPos = new Vector3(0f, 0f, 3.3f); // 1.2m from target when hit
            Vector3 backwardDir = Vector3.back;
            float ringRadius = 4.2f;
            float recoilDistance = 4.7f;
            float arcHeight = 1.15f;

            // Mid-recoil (t = 0.5): Y-axis lifts upward!
            YunZhongJunLogic.EvaluateSkill1RecoilArc(0.5f, backwardDir, recoilDistance, arcHeight, out Vector3 midHoriz, out float midY);
            Assert.That(midY, Is.EqualTo(arcHeight).Within(0.001f));
            Assert.That(midY, Is.GreaterThan(1.0f));

            // Early cancel via Basic Attack (t = 0.2): stays inside intimidation ring!
            YunZhongJunLogic.EvaluateSkill1RecoilArc(0.2f, backwardDir, recoilDistance, arcHeight, out Vector3 earlyHoriz, out _);
            Vector3 earlyCancelPos = hitPos + earlyHoriz;
            Assert.That(YunZhongJunLogic.IsInsideIntimidationRing(earlyCancelPos, targetPos, ringRadius), Is.True);

            // Uncancelled full recoil (t = 1.0): lands outside intimidation ring (1.2 + 4.7 = 5.9m > 4.2m)!
            YunZhongJunLogic.EvaluateSkill1RecoilArc(1.0f, backwardDir, recoilDistance, arcHeight, out Vector3 fullHoriz, out float endY);
            Vector3 endPos = hitPos + fullHoriz;
            Assert.That(endY, Is.EqualTo(0f).Within(0.001f));
            Assert.That(YunZhongJunLogic.IsInsideIntimidationRing(endPos, targetPos, ringRadius), Is.False);
        }

        [Test]
        public void UltimateZones_InnerKnocksUpAndAddsMark_OuterDoesNotStunOrAddMark()
        {
            Vector3 ultCenter = Vector3.zero;
            float innerR = 2.2f;
            float outerR = 4.8f;

            // 1. Inner circle target (1.5m)
            var innerZone = YunZhongJunLogic.EvaluateUltimateLandingZone(
                ultCenter, new Vector3(0f, 0f, 1.5f), innerR, outerR, out bool innerKnockup, out int innerStacks);
            Assert.That(innerZone, Is.EqualTo(UltimateHitZone.InnerRing));
            Assert.That(innerKnockup, Is.True);
            Assert.That(innerStacks, Is.EqualTo(1));

            // 2. Outer circle target (3.5m) -> 外圈砸不晕敌人，不击飞，不叠印记
            var outerZone = YunZhongJunLogic.EvaluateUltimateLandingZone(
                ultCenter, new Vector3(0f, 0f, 3.5f), innerR, outerR, out bool outerKnockup, out int outerStacks);
            Assert.That(outerZone, Is.EqualTo(UltimateHitZone.OuterRingOnly));
            Assert.That(outerKnockup, Is.False);
            Assert.That(outerStacks, Is.EqualTo(0));

            // 3. Outside target (6.0m)
            var missZone = YunZhongJunLogic.EvaluateUltimateLandingZone(
                ultCenter, new Vector3(0f, 0f, 6.0f), innerR, outerR, out bool missKnockup, out int missStacks);
            Assert.That(missZone, Is.EqualTo(UltimateHitZone.None));
            Assert.That(missKnockup, Is.False);
            Assert.That(missStacks, Is.EqualTo(0));
        }

        [Test]
        public void ClawMarks_FiveStacksCap_ProgressivelyIncreasesDamage()
        {
            int stacks = 0;
            float prevDmg = YunZhongJunLogic.CalculateDamageWithClawMarkBonus(100f, stacks, 0.12f);

            for (int i = 1; i <= 5; i++)
            {
                stacks = YunZhongJunLogic.AddClawMarkStacks(stacks, 1);
                Assert.That(stacks, Is.EqualTo(i));
                float curDmg = YunZhongJunLogic.CalculateDamageWithClawMarkBonus(100f, stacks, 0.12f);
                Assert.That(curDmg, Is.GreaterThan(prevDmg));
                prevDmg = curDmg;
            }

            // Cap at 5 stacks
            stacks = YunZhongJunLogic.AddClawMarkStacks(stacks, 3);
            Assert.That(stacks, Is.EqualTo(5));
        }

        [TestCase(0, 0f)]
        [TestCase(1, 0f)]
        [TestCase(2, 0f)]
        [TestCase(3, 0f)]
        [TestCase(4, 0f)]
        [TestCase(5, 150f)]
        public void ClawMarks_MovementExtraDamage_OnlyTriggersAtFullFiveStacks(int stacks, float expectedDamage)
        {
            // 严格验证：不叠满5层印记（0~4层），敌方移动绝对没有额外伤害；满5层移动3米触发 3 * 50 = 150 额外伤害
            float extraDmg = YunZhongJunLogic.CalculateMovementExtraDamage(stacks, distanceMoved: 3.0f, extraDamagePerMeter: 50f);
            Assert.That(extraDmg, Is.EqualTo(expectedDamage).Within(0.01f));
        }

        [Test]
        public void GroundedToFlying_Skill2BuffWithUltimate_InstantlyTransformsIntoFlying()
        {
            // 1. Flying losing all breath becomes Grounded (走地鸡)
            var formAfterDrain = YunZhongJunLogic.EvaluateFormFromBreath(YunZhongJunForm.Flying, 0f, 100f, out float breath0);
            Assert.That(formAfterDrain, Is.EqualTo(YunZhongJunForm.Grounded));
            Assert.That(breath0, Is.EqualTo(0f));

            // 2. Grounded casting Ultimate ALONE without Skill 2 Buff only recovers 50 breath -> still Grounded
            var formUltAlone = YunZhongJunLogic.EvaluateUltimateCastBreathAndForm(
                YunZhongJunForm.Grounded, 0f, 100f, isSkill2BuffActive: false, ultBaseBreathGain: 50f, out float breath50);
            Assert.That(formUltAlone, Is.EqualTo(YunZhongJunForm.Grounded));
            Assert.That(breath50, Is.EqualTo(50f));

            // 3. Grounded casting Skill 2 Buff + Ultimate -> instantly hits 100 breath & becomes Flying (秒变飞鸡)
            var formCombo = YunZhongJunLogic.EvaluateUltimateCastBreathAndForm(
                YunZhongJunForm.Grounded, 0f, 100f, isSkill2BuffActive: true, ultBaseBreathGain: 50f, out float breathFull);
            Assert.That(formCombo, Is.EqualTo(YunZhongJunForm.Flying));
            Assert.That(breathFull, Is.EqualTo(100f));

            // 4. Grounded parrying with Skill 2 (+50 breath) then Ultimate (+50 breath) -> also becomes Flying (秒变飞鸡)
            float afterParry = YunZhongJunLogic.ApplySkill2ParryBreath(0f, 100f, 50f);
            var formParryUlt = YunZhongJunLogic.EvaluateUltimateCastBreathAndForm(
                YunZhongJunForm.Grounded, afterParry, 100f, isSkill2BuffActive: false, ultBaseBreathGain: 50f, out float breathParryFull);
            Assert.That(formParryUlt, Is.EqualTo(YunZhongJunForm.Flying));
            Assert.That(breathParryFull, Is.EqualTo(100f));
        }

        [Test]
        public void AttackRangeAndTargetLock_InAndOutRange_CorrectlyEvaluated()
        {
            Vector3 heroPos = new Vector3(0f, 0f, 0f);
            Vector3 insideTarget = new Vector3(0f, 0f, 3.0f); // 3m <= 3.4m flying range
            Vector3 outsideTarget = new Vector3(0f, 0f, 4.5f); // 4.5m > 3.4m flying range

            float flyingRange = 3.4f;
            Assert.That(YunZhongJunLogic.IsTargetInAttackRange(heroPos, insideTarget, flyingRange), Is.True);
            Assert.That(YunZhongJunLogic.IsTargetInAttackRange(heroPos, outsideTarget, flyingRange), Is.False);

            // Grounded range (5.8m)
            float groundedRange = 5.8f;
            Assert.That(YunZhongJunLogic.IsTargetInAttackRange(heroPos, outsideTarget, groundedRange), Is.True);
        }

        [Test]
        public void AttackRangeIndicatorVisibility_HonorOfKingsStyle_OnlyShowsWhenAttackingOrInCombat()
        {
            // Idle and not in combat -> Hidden
            Assert.That(YunZhongJunLogic.ShouldShowAttackRangeIndicator(combatRemaining: 0f, attackCdRemaining: 0f, isAttacking: false), Is.False);

            // In combat -> Shown
            Assert.That(YunZhongJunLogic.ShouldShowAttackRangeIndicator(combatRemaining: 3.5f, attackCdRemaining: 0f, isAttacking: false), Is.True);

            // Attacking / windup -> Shown
            Assert.That(YunZhongJunLogic.ShouldShowAttackRangeIndicator(combatRemaining: 0f, attackCdRemaining: 0.3f, isAttacking: false), Is.True);
            Assert.That(YunZhongJunLogic.ShouldShowAttackRangeIndicator(combatRemaining: 0f, attackCdRemaining: 0f, isAttacking: true), Is.True);
        }

        [Test]
        public void TargetLockOutlineColor_RedWhenOutside_GoldenOrangeWhenInsideRange()
        {
            Color outsideColor = YunZhongJunLogic.GetTargetLockOutlineColor(isInAttackRange: false);
            Color insideColor = YunZhongJunLogic.GetTargetLockOutlineColor(isInAttackRange: true);

            // Outside range -> Red (high R, low G)
            Assert.That(outsideColor.r, Is.GreaterThan(0.8f));
            Assert.That(outsideColor.g, Is.LessThan(0.35f));

            // Inside range -> Golden-Orange (high R, medium G ~0.6)
            Assert.That(insideColor.r, Is.GreaterThan(0.8f));
            Assert.That(insideColor.g, Is.GreaterThan(0.5f));
            Assert.That(insideColor.b, Is.LessThan(0.2f));
        }

        [Test]
        public void PterosaurLocomotion_AnimationStateTransitions_MatchesJoystickAndSkillInputs()
        {
            float walkThresh = YunZhongJunLogic.DefaultWalkThreshold; // 0.05
            float sprintThresh = YunZhongJunLogic.DefaultSprintThreshold; // 0.75

            // 1. No input -> idle
            var idle = YunZhongJunLogic.EvaluatePterosaurAnimationState(0f, false, false, walkThresh, sprintThresh);
            Assert.That(idle, Is.EqualTo(PterosaurAnimState.Idle));
            Assert.That(YunZhongJunLogic.GetAnimationClipName(idle), Is.EqualTo("idle"));

            // Below walk threshold -> idle
            Assert.That(YunZhongJunLogic.EvaluatePterosaurAnimationState(0.04f, false, false, walkThresh, sprintThresh), Is.EqualTo(PterosaurAnimState.Idle));

            // 2. Half drag (0.45) -> walk
            var walk = YunZhongJunLogic.EvaluatePterosaurAnimationState(0.45f, false, false, walkThresh, sprintThresh);
            Assert.That(walk, Is.EqualTo(PterosaurAnimState.Walk));
            Assert.That(YunZhongJunLogic.GetAnimationClipName(walk), Is.EqualTo("walk"));

            // 3. Full drag (0.85) -> sprint
            var sprint = YunZhongJunLogic.EvaluatePterosaurAnimationState(0.85f, false, false, walkThresh, sprintThresh);
            Assert.That(sprint, Is.EqualTo(PterosaurAnimState.Sprint));
            Assert.That(YunZhongJunLogic.GetAnimationClipName(sprint), Is.EqualTo("sprint"));

            // 4. Skill 1 dash overrides zero input to sprint
            var dashSprint = YunZhongJunLogic.EvaluatePterosaurAnimationState(0f, isDashingSkill1: true, isPlayingFlySlam: false, walkThresh, sprintThresh);
            Assert.That(dashSprint, Is.EqualTo(PterosaurAnimState.Sprint));

            // 5. Ultimate fly_slam overrides all other states
            var flySlam = YunZhongJunLogic.EvaluatePterosaurAnimationState(1.0f, isDashingSkill1: true, isPlayingFlySlam: true, walkThresh, sprintThresh);
            Assert.That(flySlam, Is.EqualTo(PterosaurAnimState.FlySlam));
            Assert.That(YunZhongJunLogic.GetAnimationClipName(flySlam), Is.EqualTo("fly_slam"));
        }

        [Test]
        public void FlySlam_HeightTrajectory_AscendsHoversAndHitsGroundAt2Point4Seconds()
        {
            float impactTime = YunZhongJunLogic.FlySlamImpactTimestamp; // 2.40s
            float apex = 2.4f;

            // Start (t = 0): at ground (0)
            float h0 = YunZhongJunLogic.EvaluateFlySlamHeightOffset(0f, impactTime, apex);
            Assert.That(h0, Is.EqualTo(0f).Within(0.001f));

            // Ascent peak (t = 0.8s): reached apex
            float hPeak = YunZhongJunLogic.EvaluateFlySlamHeightOffset(0.8f, impactTime, apex);
            Assert.That(hPeak, Is.EqualTo(apex).Within(0.01f));

            // Hovering at high altitude (t = 1.3s): stays near apex
            float hHover = YunZhongJunLogic.EvaluateFlySlamHeightOffset(1.3f, impactTime, apex);
            Assert.That(hHover, Is.GreaterThan(apex - 0.2f));

            // Dive impact instant (t = 2.40s): violently lands at ground (0)
            float hImpact = YunZhongJunLogic.EvaluateFlySlamHeightOffset(impactTime, impactTime, apex);
            Assert.That(hImpact, Is.EqualTo(0f).Within(0.001f));

            // Ground recovery phase (t = 3.0s): stays at ground (0)
            float hRecovery = YunZhongJunLogic.EvaluateFlySlamHeightOffset(3.0f, impactTime, apex);
            Assert.That(hRecovery, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void CameraMicroShake_ActiveAndFinished_BoundedAndDamped()
        {
            float duration = 0.25f;
            float amplitude = 0.15f;

            // When elapsed >= duration, offset must be exactly Vector3.zero
            Vector3 finishedOffset = YunZhongJunLogic.EvaluateCameraShakeOffset(0.26f, duration, amplitude);
            Assert.That(finishedOffset, Is.EqualTo(Vector3.zero));

            // When elapsed < duration, offset must be bounded within amplitude
            Vector3 activeOffset = YunZhongJunLogic.EvaluateCameraShakeOffset(0.05f, duration, amplitude);
            Assert.That(activeOffset.x, Is.InRange(-amplitude, amplitude));
            Assert.That(activeOffset.y, Is.InRange(-amplitude, amplitude));
            Assert.That(activeOffset.z, Is.InRange(-amplitude, amplitude));
        }

        [Test]
        public void ShockwaveAndDashVfx_TransparencyAround0Point2_EnsuresVisibilityOfCharacter()
        {
            // 1. 冲击波/半圆穹顶特效峰值透明度在 0.20 左右，绝不遮挡角色
            float startAlpha = YunZhongJunLogic.EvaluateShockwaveDomeAlpha(0f, 0.20f);
            Assert.That(startAlpha, Is.EqualTo(0.20f).Within(0.001f));

            float midAlpha = YunZhongJunLogic.EvaluateShockwaveDomeAlpha(0.5f, 0.20f);
            Assert.That(midAlpha, Is.EqualTo(0.10f).Within(0.001f));

            float endAlpha = YunZhongJunLogic.EvaluateShockwaveDomeAlpha(1.0f, 0.20f);
            Assert.That(endAlpha, Is.EqualTo(0f).Within(0.001f));

            // 2. 普通攻击与技能冲刺透明淡蓝色颜色计算 (透明度 0.2 左右，淡蓝色)
            Color vfxColor = YunZhongJunLogic.EvaluateTransparentVfxColor(0.20f, isLightBlue: true);
            Assert.That(vfxColor.a, Is.EqualTo(0.20f).Within(0.01f));
            Assert.That(vfxColor.b, Is.GreaterThan(0.9f));
            Assert.That(vfxColor.r, Is.LessThan(0.35f));
        }

        [Test]
        public void ShouldPlayFlyingAttackDashVfx_GroundedNeverPlays_FlyingOnlyPlaysWhenDashingIntoTarget()
        {
            // 规则：
            // 1. 走地鸡状态下（Grounded），无论是否有目标、无论距离远近，绝不播放任何冲刺特效
            Assert.That(YunZhongJunLogic.ShouldPlayFlyingAttackDashVfx(
                YunZhongJunForm.Grounded, hasTarget: true, distanceToTarget: 2.5f), Is.False);
            Assert.That(YunZhongJunLogic.ShouldPlayFlyingAttackDashVfx(
                YunZhongJunForm.Grounded, hasTarget: false, distanceToTarget: 0f), Is.False);
            Assert.That(YunZhongJunLogic.ShouldPlayFlyingAttackDashVfx(
                YunZhongJunForm.Grounded, hasTarget: true, distanceToTarget: 3.5f, isCancellingRecoil: true), Is.False);

            // 2. 展翅状态下（Flying），无目标时不出特效
            Assert.That(YunZhongJunLogic.ShouldPlayFlyingAttackDashVfx(
                YunZhongJunForm.Flying, hasTarget: false, distanceToTarget: 3.0f), Is.False);

            // 3. 展翅状态下（Flying），有目标但为贴脸原地攻击（<= 1.2f），不播放冲刺特效
            Assert.That(YunZhongJunLogic.ShouldPlayFlyingAttackDashVfx(
                YunZhongJunForm.Flying, hasTarget: true, distanceToTarget: 0.8f), Is.False);

            // 4. 展翅状态下（Flying），有目标且距离 > 1.2m 触发冲刺位移撞击敌人时，必须播放淡蓝色炫酷冲刺特效
            Assert.That(YunZhongJunLogic.ShouldPlayFlyingAttackDashVfx(
                YunZhongJunForm.Flying, hasTarget: true, distanceToTarget: 2.5f), Is.True);
            Assert.That(YunZhongJunLogic.ShouldPlayFlyingAttackDashVfx(
                YunZhongJunForm.Flying, hasTarget: true, distanceToTarget: 4.2f), Is.True);

            // 5. 展翅状态下（Flying），取消倒飞强行冲刺贴身时，必须播放特效
            Assert.That(YunZhongJunLogic.ShouldPlayFlyingAttackDashVfx(
                YunZhongJunForm.Flying, hasTarget: true, distanceToTarget: 0.5f, isCancellingRecoil: true), Is.True);
        }

        [Test]
        public void AttackClawAnimation_StateMappingAndPriority_Verified()
        {
            // 1. Clip 名字映射验证
            Assert.That(YunZhongJunLogic.GetAnimationClipName(PterosaurAnimState.AttackClaw), Is.EqualTo("attack_claw"));

            // 2. 常量定义验证
            Assert.That(YunZhongJunLogic.DefaultAttackClawDuration, Is.EqualTo(0.85f).Within(0.001f));
            Assert.That(YunZhongJunLogic.DefaultAttackClawStrikeTime, Is.EqualTo(0.34f).Within(0.001f));

            // 3. 普攻激活状态下，进入 AttackClaw
            var attackState = YunZhongJunLogic.EvaluatePterosaurAnimationState(
                joystickMagnitude: 0f,
                isDashingSkill1: false,
                isPlayingFlySlam: false,
                isAttacking: true);
            Assert.That(attackState, Is.EqualTo(PterosaurAnimState.AttackClaw));

            // 4. 优先级检查：三技能大招（fly_slam）优先级最高，压制普攻动画
            var slamOverride = YunZhongJunLogic.EvaluatePterosaurAnimationState(
                joystickMagnitude: 0f,
                isDashingSkill1: false,
                isPlayingFlySlam: true,
                isAttacking: true);
            Assert.That(slamOverride, Is.EqualTo(PterosaurAnimState.FlySlam));

            // 5. 优先级检查：一技能蓝色冲刺（sprint）优先于普攻
            var sprintOverride = YunZhongJunLogic.EvaluatePterosaurAnimationState(
                joystickMagnitude: 0f,
                isDashingSkill1: true,
                isPlayingFlySlam: false,
                isAttacking: true);
            Assert.That(sprintOverride, Is.EqualTo(PterosaurAnimState.Sprint));

            // 6. 非攻击状态下（isAttacking: false），正常根据摇杆输入判断
            var normalIdle = YunZhongJunLogic.EvaluatePterosaurAnimationState(
                joystickMagnitude: 0f,
                isDashingSkill1: false,
                isPlayingFlySlam: false,
                isAttacking: false);
            Assert.That(normalIdle, Is.EqualTo(PterosaurAnimState.Idle));
        }
    }
}
