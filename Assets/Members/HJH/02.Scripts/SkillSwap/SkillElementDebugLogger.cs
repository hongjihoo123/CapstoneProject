using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.SkillSwap
{
    // Test helper: whenever a swappable (Q/E) skill is cast, logs the element it feeds into the combo.
    public class SkillElementDebugLogger : MonoBehaviour
    {
        [SerializeField] private PlayerAgent player;

        private SkillStateModule _skills;

        private void Awake()
        {
            if (player == null)
                player = GetComponentInParent<PlayerAgent>();
        }

        private void Start()
        {
            _skills = player != null ? player.SkillFsm : null;
            if (_skills != null)
                _skills.SkillUsed += HandleSkillUsed;
        }

        private void OnDestroy()
        {
            if (_skills != null)
                _skills.SkillUsed -= HandleSkillUsed;
        }

        private void HandleSkillUsed(SkillUsedInfo info)
        {
            if (!SkillSlots.IsSwappable(info.Slot) || info.Data == null)
                return;

            Debug.Log($"[스킬 사용] {player.GetSkillKeyLabel(info.Slot)} {info.Data.DisplayName} → {SkillPickup.ElementLabel(info.Data)}");
        }
    }
}
