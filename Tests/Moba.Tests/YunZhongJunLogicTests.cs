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
    }
}
