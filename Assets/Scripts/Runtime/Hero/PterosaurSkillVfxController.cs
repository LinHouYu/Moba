using UnityEngine;

namespace Moba.Runtime.Hero
{
    /// <summary>
    /// 翼龙专属 3D 立体技能特效控制器：
    /// 1. 技能1【天隙鸣】：蓝色冲刺过去 3D 立体破空锥、双翼 3D 流光拖尾、3D 冲刺粒子束、撞击敌人 3D 蓝色爆裂球
    /// 2. 技能3【风雷引 (fly_slam)】：高空俯冲 3D 蓝色天降能量漏斗 + T=2.40s 砸地瞬间 3D 蓝色立体冲击波穹顶、
    ///    3D 破地能量冰晶尖刺簇（12根立体斜插晶柱）、3D 双层立体气浪柱、3D 通天蓝色光柱与 3D 立体碎晶粒子爆发。
    /// </summary>
    [DisallowMultipleComponent]
    public class PterosaurSkillVfxController : MonoBehaviour
    {
        [Header("Skill 1: 蓝色冲刺 3D 立体特效引用")]
        [SerializeField] private Transform skill1DashVfxRoot;
        [SerializeField] private Transform dashSpearCone3D;
        [SerializeField] private Transform dashInnerCore3D;
        [SerializeField] private Transform dashVortexRingFront;
        [SerializeField] private Transform dashVortexRingRear;
        [SerializeField] private ParticleSystem dashBlueParticles;
        [SerializeField] private TrailRenderer leftWingTrail;
        [SerializeField] private TrailRenderer rightWingTrail;
        [SerializeField] private Transform skill1HitBurstRoot;
        [SerializeField] private Transform skill1HitSphere3D;
        [SerializeField] private ParticleSystem skill1HitParticles;

        [Header("Skill 3: 蓝色砸向地面 3D 立体特效引用")]
        [SerializeField] private Transform diveEnergyFunnel3D;
        [SerializeField] private Transform skill3SlamVfxRoot;
        [SerializeField] private Transform slamShockwaveDome3D;
        [SerializeField] private Transform slamInnerWaveCylinder3D;
        [SerializeField] private Transform slamOuterWaveCylinder3D;
        [SerializeField] private Transform slamSkyBeamPillar3D;
        [SerializeField] private Transform slamCrystalSpikesRoot;
        [SerializeField] private ParticleSystem slamBurstParticles3D;

        [Header("3D VFX Timing (Inspector Tunable)")]
        [SerializeField] [Range(0.2f, 1.2f)] private float skill1HitBurstDuration = 0.42f;
        [SerializeField] [Range(0.4f, 2.0f)] private float skill3SlamVfxDuration = 1.15f;

        private bool isSkill1Dashing = false;
        private float skill1DashElapsed = 0f;

        private bool isBasicAttackDashing = false;
        private float basicAttackDashElapsed = 0f;
        private float basicAttackDashDuration = 0.22f;

        private bool isSkill1HitBurstActive = false;
        private float skill1HitBurstElapsed = 0f;

        private bool isSkill3Diving = false;
        private bool isSkill3SlamActive = false;
        private float skill3SlamElapsed = 0f;
        private float activeInnerRadius = 2.2f;
        private float activeOuterRadius = 4.8f;

        private MaterialPropertyBlock propBlock;

        public bool IsSkill1Dashing => isSkill1Dashing;
        public bool IsBasicAttackDashing => isBasicAttackDashing;
        public bool IsSkill3SlamActive => isSkill3SlamActive;

        public void BindSkill1VfxReferences(
            Transform dashRoot,
            Transform spearCone,
            Transform innerCore,
            Transform vortexFront,
            Transform vortexRear,
            ParticleSystem dashPs,
            TrailRenderer trailL,
            TrailRenderer trailR,
            Transform hitRoot,
            Transform hitSphere,
            ParticleSystem hitPs)
        {
            skill1DashVfxRoot = dashRoot;
            dashSpearCone3D = spearCone;
            dashInnerCore3D = innerCore;
            dashVortexRingFront = vortexFront;
            dashVortexRingRear = vortexRear;
            dashBlueParticles = dashPs;
            leftWingTrail = trailL;
            rightWingTrail = trailR;
            skill1HitBurstRoot = hitRoot;
            skill1HitSphere3D = hitSphere;
            skill1HitParticles = hitPs;
        }

        public void BindSkill3VfxReferences(
            Transform diveFunnel,
            Transform slamRoot,
            Transform dome3D,
            Transform innerCyl3D,
            Transform outerCyl3D,
            Transform skyBeam3D,
            Transform spikesRoot,
            ParticleSystem slamPs)
        {
            diveEnergyFunnel3D = diveFunnel;
            skill3SlamVfxRoot = slamRoot;
            slamShockwaveDome3D = dome3D;
            slamInnerWaveCylinder3D = innerCyl3D;
            slamOuterWaveCylinder3D = outerCyl3D;
            slamSkyBeamPillar3D = skyBeam3D;
            slamCrystalSpikesRoot = spikesRoot;
            slamBurstParticles3D = slamPs;
        }

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            AutoResolveSceneReferences();
            ResetAllVfxToIdle();
        }

        private void AutoResolveSceneReferences()
        {
            if (skill1DashVfxRoot == null)
            {
                skill1DashVfxRoot = transform.Find("Skill1_BlueDash_3DVFX");
            }
            if (skill1DashVfxRoot != null)
            {
                if (dashSpearCone3D == null) dashSpearCone3D = skill1DashVfxRoot.Find("Dash_3D_SpearCone");
                if (dashInnerCore3D == null) dashInnerCore3D = skill1DashVfxRoot.Find("Dash_3D_InnerCore");
                if (dashVortexRingFront == null) dashVortexRingFront = skill1DashVfxRoot.Find("Dash_3D_VortexRing_Front");
                if (dashVortexRingRear == null) dashVortexRingRear = skill1DashVfxRoot.Find("Dash_3D_VortexRing_Rear");
                if (dashBlueParticles == null) dashBlueParticles = skill1DashVfxRoot.GetComponentInChildren<ParticleSystem>(true);
            }

            if (leftWingTrail == null || rightWingTrail == null)
            {
                var trails = GetComponentsInChildren<TrailRenderer>(true);
                if (trails.Length >= 2)
                {
                    leftWingTrail = trails[0];
                    rightWingTrail = trails[1];
                }
            }

            if (diveEnergyFunnel3D == null)
            {
                diveEnergyFunnel3D = transform.Find("Skill3_Dive_3DFunnel");
            }

            if (skill1HitBurstRoot == null)
            {
                var foundHit = GameObject.Find("Skill1_BlueHit_3DVFX_Root");
                if (foundHit != null) skill1HitBurstRoot = foundHit.transform;
            }
            if (skill1HitBurstRoot != null)
            {
                if (skill1HitSphere3D == null) skill1HitSphere3D = skill1HitBurstRoot.Find("Hit_3D_ShockSphere");
                if (skill1HitParticles == null) skill1HitParticles = skill1HitBurstRoot.GetComponentInChildren<ParticleSystem>(true);
            }

            if (skill3SlamVfxRoot == null)
            {
                var foundSlam = GameObject.Find("Skill3_BlueSlam_3DVFX_Root");
                if (foundSlam != null) skill3SlamVfxRoot = foundSlam.transform;
            }
            if (skill3SlamVfxRoot != null)
            {
                if (slamShockwaveDome3D == null) slamShockwaveDome3D = skill3SlamVfxRoot.Find("Slam_3D_ShockwaveDome");
                if (slamInnerWaveCylinder3D == null) slamInnerWaveCylinder3D = skill3SlamVfxRoot.Find("Slam_3D_InnerWaveCylinder");
                if (slamOuterWaveCylinder3D == null) slamOuterWaveCylinder3D = skill3SlamVfxRoot.Find("Slam_3D_OuterWaveCylinder");
                if (slamSkyBeamPillar3D == null) slamSkyBeamPillar3D = skill3SlamVfxRoot.Find("Slam_3D_SkyBeamPillar");
                if (slamCrystalSpikesRoot == null) slamCrystalSpikesRoot = skill3SlamVfxRoot.Find("Slam_3D_CrystalSpikes_Group");
                if (slamBurstParticles3D == null) slamBurstParticles3D = skill3SlamVfxRoot.GetComponentInChildren<ParticleSystem>(true);
            }
        }

        public void ResetAllVfxToIdle()
        {
            isSkill1Dashing = false;
            isBasicAttackDashing = false;
            isSkill1HitBurstActive = false;
            isSkill3Diving = false;
            isSkill3SlamActive = false;

            if (skill1DashVfxRoot != null) skill1DashVfxRoot.gameObject.SetActive(false);
            if (leftWingTrail != null) { leftWingTrail.emitting = false; leftWingTrail.Clear(); }
            if (rightWingTrail != null) { rightWingTrail.emitting = false; rightWingTrail.Clear(); }
            if (skill1HitBurstRoot != null) skill1HitBurstRoot.gameObject.SetActive(false);
            if (diveEnergyFunnel3D != null) diveEnergyFunnel3D.gameObject.SetActive(false);
            if (skill3SlamVfxRoot != null) skill3SlamVfxRoot.gameObject.SetActive(false);
        }

        // ==================== 技能 1 & 普攻：透明淡蓝色冲刺过去 3D 立体特效 ====================

        public void StartSkill1BlueDashVfx()
        {
            isSkill1Dashing = true;
            isBasicAttackDashing = false;
            skill1DashElapsed = 0f;

            if (skill1DashVfxRoot != null)
            {
                skill1DashVfxRoot.gameObject.SetActive(true);
                SetRendererAlpha(dashSpearCone3D, new Color(0.25f, 0.82f, 1f, 0.20f));
                SetRendererAlpha(dashInnerCore3D, new Color(0.45f, 0.92f, 1f, 0.24f));
                SetRendererAlpha(dashVortexRingFront, new Color(0.35f, 0.88f, 1f, 0.22f));
                SetRendererAlpha(dashVortexRingRear, new Color(0.20f, 0.75f, 1f, 0.18f));
            }

            if (dashBlueParticles != null)
            {
                dashBlueParticles.Clear();
                dashBlueParticles.Play(true);
            }

            if (leftWingTrail != null)
            {
                leftWingTrail.Clear();
                leftWingTrail.emitting = true;
            }
            if (rightWingTrail != null)
            {
                rightWingTrail.Clear();
                rightWingTrail.emitting = true;
            }
        }

        /// <summary>
        /// 普通攻击 / 爪击冲刺时触发淡蓝色透明 3D 冲刺破空与双翼流光特效（透明度0.2左右，轻快迅猛且不挡模型）
        /// </summary>
        public void PlayBasicAttackBlueDashVfx(float duration = 0.22f)
        {
            if (isSkill1Dashing) return;

            isBasicAttackDashing = true;
            basicAttackDashElapsed = 0f;
            basicAttackDashDuration = Mathf.Max(0.12f, duration);

            if (skill1DashVfxRoot != null)
            {
                skill1DashVfxRoot.gameObject.SetActive(true);
                SetRendererAlpha(dashSpearCone3D, new Color(0.32f, 0.85f, 1f, 0.20f));
                SetRendererAlpha(dashInnerCore3D, new Color(0.52f, 0.95f, 1f, 0.24f));
                SetRendererAlpha(dashVortexRingFront, new Color(0.35f, 0.88f, 1f, 0.20f));
                SetRendererAlpha(dashVortexRingRear, new Color(0.20f, 0.75f, 1f, 0.16f));
            }

            if (leftWingTrail != null)
            {
                leftWingTrail.Clear();
                leftWingTrail.emitting = true;
            }
            if (rightWingTrail != null)
            {
                rightWingTrail.Clear();
                rightWingTrail.emitting = true;
            }

            if (dashBlueParticles != null)
            {
                dashBlueParticles.Clear();
                dashBlueParticles.Play(true);
            }
        }

        public void StopSkill1BlueDashVfx()
        {
            isSkill1Dashing = false;
            isBasicAttackDashing = false;

            if (skill1DashVfxRoot != null)
            {
                skill1DashVfxRoot.gameObject.SetActive(false);
            }

            if (dashBlueParticles != null)
            {
                dashBlueParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (leftWingTrail != null) leftWingTrail.emitting = false;
            if (rightWingTrail != null) rightWingTrail.emitting = false;
        }

        public void PlaySkill1HitBurstVfx(Vector3 impactWorldPosition)
        {
            StopSkill1BlueDashVfx();

            isSkill1HitBurstActive = true;
            skill1HitBurstElapsed = 0f;

            if (skill1HitBurstRoot != null)
            {
                skill1HitBurstRoot.position = new Vector3(impactWorldPosition.x, 1.0f, impactWorldPosition.z);
                skill1HitBurstRoot.gameObject.SetActive(true);
            }

            // 保持透明度在 0.2 左右，既能看到半圆/球体冲击波，又能清晰透视角色
            if (skill1HitSphere3D != null)
            {
                skill1HitSphere3D.localScale = Vector3.one * 0.4f;
                SetRendererAlpha(skill1HitSphere3D, new Color(0.22f, 0.78f, 1f, 0.20f));
            }

            if (skill1HitParticles != null)
            {
                skill1HitParticles.Clear();
                skill1HitParticles.Play(true);
            }
        }

        // ==================== 技能 3：fly_slam 俯冲与蓝色砸向地面 3D 立体特效 ====================

        public void SetSkill3DiveFunnelActive(bool active)
        {
            if (isSkill3Diving == active) return;
            isSkill3Diving = active;

            if (diveEnergyFunnel3D != null)
            {
                if (active)
                {
                    SetRendererAlpha(diveEnergyFunnel3D, new Color(0.20f, 0.75f, 1f, 0.20f));
                }
                diveEnergyFunnel3D.gameObject.SetActive(active);
            }
        }

        public void PlaySkill3BlueSlamImpactVfx(Vector3 slamCenter, float innerRadius, float outerRadius)
        {
            SetSkill3DiveFunnelActive(false);

            isSkill3SlamActive = true;
            skill3SlamElapsed = 0f;
            activeInnerRadius = Mathf.Max(1f, innerRadius);
            activeOuterRadius = Mathf.Max(activeInnerRadius + 1f, outerRadius);

            if (skill3SlamVfxRoot != null)
            {
                skill3SlamVfxRoot.position = new Vector3(slamCenter.x, 0.02f, slamCenter.z);
                skill3SlamVfxRoot.rotation = Quaternion.identity;
                skill3SlamVfxRoot.gameObject.SetActive(true);
            }

            // 初始设置半圆特效穹顶为透明 0.2
            if (slamShockwaveDome3D != null)
            {
                slamShockwaveDome3D.localScale = new Vector3(1.2f, 0.8f, 1.2f);
                SetRendererAlpha(slamShockwaveDome3D, new Color(0.18f, 0.78f, 1f, 0.20f));
            }

            if (slamBurstParticles3D != null)
            {
                slamBurstParticles3D.Clear();
                slamBurstParticles3D.Play(true);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (isSkill1Dashing)
            {
                UpdateSkill1Dash3DVfx(dt);
            }
            else if (isBasicAttackDashing)
            {
                basicAttackDashElapsed += dt;
                UpdateSkill1Dash3DVfx(dt);
                if (basicAttackDashElapsed >= basicAttackDashDuration)
                {
                    isBasicAttackDashing = false;
                    StopSkill1BlueDashVfx();
                }
            }

            if (isSkill1HitBurstActive)
            {
                UpdateSkill1HitBurst3DVfx(dt);
            }

            if (isSkill3Diving && diveEnergyFunnel3D != null)
            {
                diveEnergyFunnel3D.Rotate(0f, 540f * dt, 0f, Space.Self);
            }

            if (isSkill3SlamActive)
            {
                UpdateSkill3Slam3DVfx(dt);
            }
        }

        private void UpdateSkill1Dash3DVfx(float dt)
        {
            skill1DashElapsed += dt;
            float pulse = 1f + Mathf.Sin(skill1DashElapsed * 38f) * 0.12f;

            if (dashSpearCone3D != null)
            {
                dashSpearCone3D.localScale = new Vector3(1.35f * pulse, 2.2f, 1.35f * pulse);
                dashSpearCone3D.Rotate(0f, 720f * dt, 0f, Space.Self);
                SetRendererAlpha(dashSpearCone3D, new Color(0.25f, 0.82f, 1f, 0.20f));
            }

            if (dashInnerCore3D != null)
            {
                dashInnerCore3D.localScale = new Vector3(0.75f * pulse, 2.4f, 0.75f * pulse);
                dashInnerCore3D.Rotate(0f, -900f * dt, 0f, Space.Self);
                SetRendererAlpha(dashInnerCore3D, new Color(0.45f, 0.92f, 1f, 0.24f));
            }

            if (dashVortexRingFront != null)
            {
                dashVortexRingFront.Rotate(0f, 0f, 840f * dt, Space.Self);
                SetRendererAlpha(dashVortexRingFront, new Color(0.35f, 0.88f, 1f, 0.22f));
            }

            if (dashVortexRingRear != null)
            {
                dashVortexRingRear.Rotate(0f, 0f, -680f * dt, Space.Self);
                SetRendererAlpha(dashVortexRingRear, new Color(0.20f, 0.75f, 1f, 0.18f));
            }
        }

        private void UpdateSkill1HitBurst3DVfx(float dt)
        {
            skill1HitBurstElapsed += dt;
            float t = Mathf.Clamp01(skill1HitBurstElapsed / Mathf.Max(0.05f, skill1HitBurstDuration));

            // 核心要求：半圆/球体冲击波特效透明度在 0.2 左右，透视角色完全可见
            if (skill1HitSphere3D != null)
            {
                float scale = Mathf.Lerp(0.5f, 3.8f, 1f - (1f - t) * (1f - t));
                skill1HitSphere3D.localScale = new Vector3(scale, scale, scale);
                float alpha = (1f - t) * 0.20f;
                SetRendererAlpha(skill1HitSphere3D, new Color(0.18f, 0.82f, 1f, alpha));
            }

            if (t >= 1f)
            {
                isSkill1HitBurstActive = false;
                if (skill1HitBurstRoot != null)
                {
                    skill1HitBurstRoot.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateSkill3Slam3DVfx(float dt)
        {
            skill3SlamElapsed += dt;
            float duration = Mathf.Max(0.2f, skill3SlamVfxDuration);
            float t = Mathf.Clamp01(skill3SlamElapsed / duration);
            float easeOut = 1f - (1f - t) * (1f - t);

            // 1. 核心要求：3D 蓝色立体半圆/穹顶冲击波 (Slam_3D_ShockwaveDome) 透明度在 0.2 左右，能看到冲击波也能看到角色！
            if (slamShockwaveDome3D != null)
            {
                float domeProgress = Mathf.Clamp01(skill3SlamElapsed / (duration * 0.55f));
                float domeEase = 1f - (1f - domeProgress) * (1f - domeProgress);
                float diameter = Mathf.Lerp(1.2f, activeOuterRadius * 2.05f, domeEase);
                float height = Mathf.Lerp(0.8f, 3.4f, domeEase);
                slamShockwaveDome3D.localScale = new Vector3(diameter, height, diameter);
                float domeAlpha = Mathf.Clamp01(1f - domeProgress) * 0.20f;
                SetRendererAlpha(slamShockwaveDome3D, new Color(0.18f, 0.78f, 1f, domeAlpha));
            }

            // 2. 3D 内圈立体气浪圆筒 (Slam_3D_InnerWaveCylinder)
            if (slamInnerWaveCylinder3D != null)
            {
                float innerProgress = Mathf.Clamp01(skill3SlamElapsed / (duration * 0.65f));
                float d = Mathf.Lerp(0.8f, activeInnerRadius * 2.0f, Mathf.Sqrt(innerProgress));
                float h = Mathf.Lerp(1.8f, 0.25f, innerProgress);
                slamInnerWaveCylinder3D.localPosition = new Vector3(0f, h * 0.5f, 0f);
                slamInnerWaveCylinder3D.localScale = new Vector3(d, h * 0.5f, d);
                float alpha = (1f - innerProgress) * 0.22f;
                SetRendererAlpha(slamInnerWaveCylinder3D, new Color(0.38f, 0.92f, 1f, alpha));
            }

            // 3. 3D 外圈立体气浪圆筒 (Slam_3D_OuterWaveCylinder)
            if (slamOuterWaveCylinder3D != null)
            {
                float outerProgress = Mathf.Clamp01(skill3SlamElapsed / (duration * 0.8f));
                float d = Mathf.Lerp(activeInnerRadius * 1.2f, activeOuterRadius * 2.0f, Mathf.Sqrt(outerProgress));
                float h = Mathf.Lerp(1.3f, 0.18f, outerProgress);
                slamOuterWaveCylinder3D.localPosition = new Vector3(0f, h * 0.5f, 0f);
                slamOuterWaveCylinder3D.localScale = new Vector3(d, h * 0.5f, d);
                float alpha = (1f - outerProgress) * 0.18f;
                SetRendererAlpha(slamOuterWaveCylinder3D, new Color(0.12f, 0.62f, 1f, alpha));
            }

            // 4. 3D 通天蓝色光柱 (Slam_3D_SkyBeamPillar)
            if (slamSkyBeamPillar3D != null)
            {
                float beamProgress = Mathf.Clamp01(skill3SlamElapsed / (duration * 0.45f));
                float width = Mathf.Lerp(2.6f, 0.15f, beamProgress);
                slamSkyBeamPillar3D.localPosition = new Vector3(0f, 4.5f, 0f);
                slamSkyBeamPillar3D.localScale = new Vector3(width, 4.5f, width);
                float beamAlpha = (1f - beamProgress) * 0.22f;
                SetRendererAlpha(slamSkyBeamPillar3D, new Color(0.48f, 0.95f, 1f, beamAlpha));
            }

            // 5. 3D 破地蓝色能量冰晶刺簇 (12根立体斜插晶柱从地面破出，透明蓝色 ~0.25)
            if (slamCrystalSpikesRoot != null)
            {
                float spikeScaleY;
                if (t < 0.15f)
                {
                    spikeScaleY = Mathf.Clamp01(t / 0.15f);
                }
                else if (t < 0.55f)
                {
                    spikeScaleY = 1f + Mathf.Sin((t - 0.15f) * 25f) * 0.06f;
                }
                else
                {
                    spikeScaleY = Mathf.Clamp01(1f - (t - 0.55f) / 0.45f);
                }

                slamCrystalSpikesRoot.localScale = new Vector3(1f, spikeScaleY, 1f);
                float spikeAlpha = Mathf.Clamp01(spikeScaleY) * 0.25f;
                Color spikeCol = new Color(0.22f, 0.78f, 1f, spikeAlpha);
                foreach (Transform child in slamCrystalSpikesRoot)
                {
                    SetRendererAlpha(child, spikeCol);
                }
            }

            if (t >= 1f)
            {
                isSkill3SlamActive = false;
                if (skill3SlamVfxRoot != null)
                {
                    skill3SlamVfxRoot.gameObject.SetActive(false);
                }
            }
        }

        private void SetRendererAlpha(Transform target, Color color)
        {
            if (target == null) return;
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", color);
            propBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(propBlock);
        }
    }
}
