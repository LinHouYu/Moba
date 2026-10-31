using UnityEngine;
using Moba.Runtime.Hero;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Moba.Runtime.UI
{
    /// <summary>
    /// 移动端云中君战斗虚拟按键 HUD 控制器（参考 NKGMobaBasedOnET 冷却条与状态显示逻辑，适配手机拇指弧形按键布局）。
    /// 仅包含：左下虚拟摇杆（独立组件）+ 右下普攻/补刀/推塔 + 一/二/三技能虚拟按键（不含回城、恢复、疾跑等无关按键）。
    /// </summary>
    [DisallowMultipleComponent]
    public class YunZhongJunBattleHUD : MonoBehaviour
    {
        [Header("Target Hero Reference")]
        [SerializeField] private YunZhongJunSkillController heroController;

        [Header("Attack Virtual Buttons (普攻 / 补刀 / 推塔)")]
        [SerializeField] private UnityEngine.UI.Button basicAttackButton;
        [SerializeField] private UnityEngine.UI.Button lastHitButton;
        [SerializeField] private UnityEngine.UI.Button pushTowerButton;

        [Header("Skill 1 UI (天隙鸣 · 加速 / 描述0.5s实测1.0s眩晕)")]
        [SerializeField] private UnityEngine.UI.Button skill1Button;
        [SerializeField] private UnityEngine.UI.Image skill1CdMask;
        [SerializeField] private UnityEngine.UI.Text skill1CdText;
        [SerializeField] private GameObject skill1LockedOverlay;

        [Header("Skill 2 UI (若英·华彩 · 免控格挡Buff)")]
        [SerializeField] private UnityEngine.UI.Button skill2Button;
        [SerializeField] private UnityEngine.UI.Image skill2CdMask;
        [SerializeField] private UnityEngine.UI.Text skill2CdText;
        [SerializeField] private UnityEngine.UI.Image skill2BuffHighlightRing;

        [Header("Skill 3 UI (风雷引 · 大招内外圈)")]
        [SerializeField] private UnityEngine.UI.Button skill3Button;
        [SerializeField] private UnityEngine.UI.Image skill3CdMask;
        [SerializeField] private UnityEngine.UI.Text skill3CdText;

        [Header("Keyboard Debug Shortcuts (PC Editor 调试快捷键开关)")]
        [SerializeField] private bool enableKeyboardShortcuts = true;

        public void SetReferences(
            YunZhongJunSkillController hero,
            UnityEngine.UI.Button btnAttack,
            UnityEngine.UI.Button btnLastHit,
            UnityEngine.UI.Button btnPushTower,
            UnityEngine.UI.Button btnS1,
            UnityEngine.UI.Image s1Mask,
            UnityEngine.UI.Text s1Text,
            GameObject s1LockObj,
            UnityEngine.UI.Button btnS2,
            UnityEngine.UI.Image s2Mask,
            UnityEngine.UI.Text s2Text,
            UnityEngine.UI.Image s2BuffRing,
            UnityEngine.UI.Button btnS3,
            UnityEngine.UI.Image s3Mask,
            UnityEngine.UI.Text s3Text)
        {
            heroController = hero;
            basicAttackButton = btnAttack;
            lastHitButton = btnLastHit;
            pushTowerButton = btnPushTower;
            skill1Button = btnS1;
            skill1CdMask = s1Mask;
            skill1CdText = s1Text;
            skill1LockedOverlay = s1LockObj;
            skill2Button = btnS2;
            skill2CdMask = s2Mask;
            skill2CdText = s2Text;
            skill2BuffHighlightRing = s2BuffRing;
            skill3Button = btnS3;
            skill3CdMask = s3Mask;
            skill3CdText = s3Text;
        }

        private void Awake()
        {
            ResolveHeroIfMissing();
            WireButtons();
            RefreshSkillCDVisuals();
        }

        private void ResolveHeroIfMissing()
        {
            if (heroController == null)
            {
                heroController = FindAnyObjectByType<YunZhongJunSkillController>();
            }
        }

        private void WireButtons()
        {
            if (basicAttackButton != null)
            {
                basicAttackButton.onClick.RemoveListener(OnBasicAttackClicked);
                basicAttackButton.onClick.AddListener(OnBasicAttackClicked);
            }
            if (lastHitButton != null)
            {
                lastHitButton.onClick.RemoveListener(OnLastHitClicked);
                lastHitButton.onClick.AddListener(OnLastHitClicked);
            }
            if (pushTowerButton != null)
            {
                pushTowerButton.onClick.RemoveListener(OnPushTowerClicked);
                pushTowerButton.onClick.AddListener(OnPushTowerClicked);
            }
            if (skill1Button != null)
            {
                skill1Button.onClick.RemoveListener(OnSkill1Clicked);
                skill1Button.onClick.AddListener(OnSkill1Clicked);
            }
            if (skill2Button != null)
            {
                skill2Button.onClick.RemoveListener(OnSkill2Clicked);
                skill2Button.onClick.AddListener(OnSkill2Clicked);
            }
            if (skill3Button != null)
            {
                skill3Button.onClick.RemoveListener(OnSkill3Clicked);
                skill3Button.onClick.AddListener(OnSkill3Clicked);
            }
        }

        private void OnBasicAttackClicked()
        {
            ResolveHeroIfMissing();
            heroController?.TriggerBasicAttack();
        }

        private void OnLastHitClicked()
        {
            ResolveHeroIfMissing();
            heroController?.TriggerLastHitAttack();
        }

        private void OnPushTowerClicked()
        {
            ResolveHeroIfMissing();
            heroController?.TriggerPushTowerAttack();
        }

        private void OnSkill1Clicked()
        {
            ResolveHeroIfMissing();
            heroController?.CastSkill1();
        }

        private void OnSkill2Clicked()
        {
            ResolveHeroIfMissing();
            heroController?.CastSkill2();
        }

        private void OnSkill3Clicked()
        {
            ResolveHeroIfMissing();
            heroController?.CastSkill3();
        }

        private void Update()
        {
            if (heroController == null)
            {
                ResolveHeroIfMissing();
                if (heroController == null) return;
            }

            if (enableKeyboardShortcuts)
            {
                PollKeyboardShortcuts();
            }

            RefreshSkillCDVisuals();
        }

        private void PollKeyboardShortcuts()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.jKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                heroController.TriggerBasicAttack();
            if (kb.uKey.wasPressedThisFrame)
                heroController.TriggerLastHitAttack();
            if (kb.iKey.wasPressedThisFrame)
                heroController.TriggerPushTowerAttack();
            if (kb.digit1Key.wasPressedThisFrame || kb.qKey.wasPressedThisFrame)
                heroController.CastSkill1();
            if (kb.digit2Key.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                heroController.CastSkill2();
            if (kb.digit3Key.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame)
                heroController.CastSkill3();
#endif
        }

        private void RefreshSkillCDVisuals()
        {
            if (heroController == null) return;

            // 参考 NKGMobaBasedOnET FUI_BattleComponentSystems 的冷却更新逻辑
            UpdateSingleSkillSlot(
                heroController.Skill1CdRemaining,
                heroController.Skill1Cooldown,
                skill1CdMask,
                skill1CdText);

            if (skill1LockedOverlay != null)
            {
                bool isGroundedLocked = heroController.CurrentForm == YunZhongJunForm.Grounded;
                if (skill1LockedOverlay.activeSelf != isGroundedLocked)
                {
                    skill1LockedOverlay.SetActive(isGroundedLocked);
                }
            }

            UpdateSingleSkillSlot(
                heroController.Skill2CdRemaining,
                heroController.Skill2Cooldown,
                skill2CdMask,
                skill2CdText);

            if (skill2BuffHighlightRing != null)
            {
                bool buffOn = heroController.IsSkill2BuffActive;
                if (skill2BuffHighlightRing.gameObject.activeSelf != buffOn)
                {
                    skill2BuffHighlightRing.gameObject.SetActive(buffOn);
                }
            }

            UpdateSingleSkillSlot(
                heroController.Skill3CdRemaining,
                heroController.Skill3Cooldown,
                skill3CdMask,
                skill3CdText);
        }

        private static void UpdateSingleSkillSlot(
            float remainCd,
            float totalCd,
            UnityEngine.UI.Image cdMask,
            UnityEngine.UI.Text cdLabel)
        {
            bool onCd = remainCd > 0.01f && totalCd > 0.01f;
            if (cdMask != null)
            {
                cdMask.gameObject.SetActive(onCd);
                if (onCd)
                {
                    cdMask.fillAmount = Mathf.Clamp01(remainCd / totalCd);
                }
            }

            if (cdLabel != null)
            {
                cdLabel.gameObject.SetActive(onCd);
                if (onCd)
                {
                    cdLabel.text = remainCd >= 1f
                        ? Mathf.CeilToInt(remainCd).ToString()
                        : remainCd.ToString("0.0");
                }
            }
        }
    }
}
