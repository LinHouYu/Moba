using UnityEngine;

namespace Moba.Runtime.Hero
{
    /// <summary>
    /// 翼龙 3D 模型动画驱动与 Animation Event 桥接器：
    /// 1. 静止不移动 -> 播放 idle (3.0s Loop)
    /// 2. 拖动半截摇杆 -> 播放 walk (1.0s Loop)
    /// 3. 虚拟摇杆拉满 或 一技能冲刺 -> 播放 sprint (0.667s Loop)
    /// 4. 三技能大招 -> 播放 fly_slam (3.6s Once)，并在 T = 2.40s（第 72 帧）响应 OnSlamImpact() 触发地面砸击特效与震屏。
    /// </summary>
    [DisallowMultipleComponent]
    public class PterosaurAnimatorBridge : MonoBehaviour
    {
        private static readonly int MoveStateHash = Animator.StringToHash("MoveState");
        private static readonly int SpeedNormalizedHash = Animator.StringToHash("SpeedNormalized");
        private static readonly int IsFlyingSlamHash = Animator.StringToHash("IsFlyingSlam");
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
        private static readonly int AttackClawTriggerHash = Animator.StringToHash("AttackClaw");

        [Header("References")]
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private YunZhongJunSkillController skillController;

        [Header("Animation Thresholds (Inspector Tunable)")]
        [Tooltip("摇杆幅度低于此阈值时播放 idle 动画")]
        [SerializeField] [Range(0.01f, 0.3f)] private float walkThreshold = YunZhongJunLogic.DefaultWalkThreshold;
        [Tooltip("摇杆幅度达到或超过此阈值时切换为全速 sprint 动画，介于 walk 与 sprint 之间调用半速 walk 动画")]
        [SerializeField] [Range(0.4f, 0.95f)] private float sprintThreshold = YunZhongJunLogic.DefaultSprintThreshold;
        [Tooltip("动画切换平滑过渡时间（秒）")]
        [SerializeField] [Range(0.02f, 0.35f)] private float crossFadeDuration = 0.12f;

        [Header("Attack Animation Settings (Inspector Tunable)")]
        [Tooltip("利爪攻击动画总持续时长（秒，默认 0.42s 普攻动作周期）")]
        [SerializeField] [Range(0.2f, 1.0f)] private float attackAnimDuration = 0.42f;
        [Tooltip("攻击前摇保护时长（秒）：前摇期间确保撕抓动作展现，超过此阈值后可通过移动走A取消后摇")]
        [SerializeField] [Range(0.05f, 0.4f)] private float attackWindupDuration = 0.20f;

        [Header("Live Debug State (Read-Only)")]
        [SerializeField] private PterosaurAnimState currentState = PterosaurAnimState.Idle;
        [SerializeField] private string currentClipName = "idle";

        private bool hasInitializedState = false;
        private float attackAnimRemaining = 0f;

        public PterosaurAnimState CurrentState => currentState;
        public string CurrentClipName => currentClipName;
        public float WalkThreshold => walkThreshold;
        public float SprintThreshold => sprintThreshold;
        public float AttackAnimDuration => attackAnimDuration;
        public float AttackWindupDuration => attackWindupDuration;
        public bool IsAttackingAnimationPlaying => attackAnimRemaining > 0f;

        public void BindReferences(Animator animator, YunZhongJunSkillController controller)
        {
            targetAnimator = animator;
            skillController = controller;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            ForcePlayState(PterosaurAnimState.Idle, immediate: true);
        }

        private void ResolveReferences()
        {
            if (targetAnimator == null)
            {
                targetAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
            }

            if (skillController == null)
            {
                skillController = GetComponentInParent<YunZhongJunSkillController>()
                    ?? FindAnyObjectByType<YunZhongJunSkillController>();
            }

            if (targetAnimator != null)
            {
                targetAnimator.applyRootMotion = false;
                targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        /// <summary>
        /// 每帧由角色控制器调用，根据摇杆输入幅度与技能状态驱动 idle / walk / sprint / fly_slam / attack_claw 动画。
        /// </summary>
        public void UpdateLocomotionAnimation(float joystickMagnitude, bool isDashingSkill1, bool isPlayingFlySlam)
        {
            if (targetAnimator == null)
            {
                ResolveReferences();
                if (targetAnimator == null) return;
            }

            if (attackAnimRemaining > 0f)
            {
                attackAnimRemaining -= Time.deltaTime;
            }

            // 走A取消后摇逻辑：在前摇保护期（attackWindupDuration）后，若玩家输入大幅移动，则允许提前取消后摇切入走路/冲刺
            bool inWindup = (attackAnimDuration - attackAnimRemaining) < attackWindupDuration;
            bool isAttacking = attackAnimRemaining > 0f && (inWindup || joystickMagnitude < walkThreshold);

            PterosaurAnimState desiredState = YunZhongJunLogic.EvaluatePterosaurAnimationState(
                joystickMagnitude,
                isDashingSkill1,
                isPlayingFlySlam,
                isAttacking,
                walkThreshold,
                sprintThreshold);

            if (!hasInitializedState || desiredState != currentState)
            {
                TransitionToState(desiredState, immediate: !hasInitializedState);
            }

            // 同步更新 Animator 参数（供 Animator 窗口检视与状态机条件过渡）
            if (targetAnimator.runtimeAnimatorController != null && targetAnimator.isActiveAndEnabled)
            {
                targetAnimator.SetInteger(MoveStateHash, (int)desiredState);
                targetAnimator.SetFloat(SpeedNormalizedHash, Mathf.Clamp01(joystickMagnitude));
                targetAnimator.SetBool(IsFlyingSlamHash, isPlayingFlySlam);
                targetAnimator.SetBool(IsAttackingHash, isAttacking);
            }
        }

        /// <summary>
        /// 触发普攻利爪撕抓攻击动画（attack_claw）。
        /// 无论原地普通攻击、补刀、推塔或近战爪击，均能立刻播放挥爪撕抓动作。
        /// </summary>
        public void PlayAttackClaw()
        {
            if (targetAnimator == null)
            {
                ResolveReferences();
            }

            attackAnimRemaining = attackAnimDuration;
            currentState = PterosaurAnimState.AttackClaw;
            currentClipName = YunZhongJunLogic.GetAnimationClipName(PterosaurAnimState.AttackClaw);
            hasInitializedState = true;

            if (targetAnimator != null && targetAnimator.runtimeAnimatorController != null && targetAnimator.isActiveAndEnabled)
            {
                targetAnimator.SetInteger(MoveStateHash, (int)PterosaurAnimState.AttackClaw);
                targetAnimator.SetBool(IsAttackingHash, true);
                targetAnimator.SetTrigger(AttackClawTriggerHash);
                targetAnimator.CrossFadeInFixedTime(currentClipName, 0.05f, 0);
            }
        }

        /// <summary>
        /// 立即从第 0 帧触发三技能 fly_slam 动画（3.6s Once）。
        /// </summary>
        public void PlayFlySlamFromStart()
        {
            if (targetAnimator == null)
            {
                ResolveReferences();
            }

            attackAnimRemaining = 0f;
            currentState = PterosaurAnimState.FlySlam;
            currentClipName = YunZhongJunLogic.GetAnimationClipName(PterosaurAnimState.FlySlam);
            hasInitializedState = true;

            if (targetAnimator != null && targetAnimator.runtimeAnimatorController != null && targetAnimator.isActiveAndEnabled)
            {
                targetAnimator.SetInteger(MoveStateHash, (int)PterosaurAnimState.FlySlam);
                targetAnimator.SetBool(IsFlyingSlamHash, true);
                targetAnimator.SetBool(IsAttackingHash, false);
                targetAnimator.Play(currentClipName, 0, 0f);
            }
        }

        private void TransitionToState(PterosaurAnimState nextState, bool immediate)
        {
            currentState = nextState;
            currentClipName = YunZhongJunLogic.GetAnimationClipName(nextState);
            hasInitializedState = true;

            if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null || !targetAnimator.isActiveAndEnabled)
            {
                return;
            }

            if (immediate || crossFadeDuration <= 0.001f)
            {
                targetAnimator.Play(currentClipName, 0, 0f);
            }
            else
            {
                targetAnimator.CrossFadeInFixedTime(currentClipName, crossFadeDuration, 0);
            }
        }

        private void ForcePlayState(PterosaurAnimState state, bool immediate)
        {
            TransitionToState(state, immediate);
        }

        /// <summary>
        /// Animation Event 回调（绑定在 fly_slam 动画 T = 2.40s / 第 72 帧）：
        /// 巨爪与双臂暴力砸地瞬间触发地面 3D 蓝色冲击特效、内外圈伤害判定与摄像机微震动。
        /// </summary>
        public void OnSlamImpact()
        {
            if (skillController == null)
            {
                ResolveReferences();
            }

            if (skillController != null)
            {
                skillController.OnSlamImpact();
            }
        }
    }
}
