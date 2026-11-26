using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace RobotWeapons
{
    public class HitStagger : MonoBehaviour
    {
        [SerializeField, Tooltip("일반 타격 경직 (초)")] private float hitFlinch = 0.08f;
        [SerializeField, Tooltip("이 피해 이상이거나 약점이면 큰 경직")] private float bigHitDamage = 30f;
        [SerializeField, Tooltip("큰 타격 경직 (초)")] private float bigHitFlinch = 0.18f;

        private PlayerAgent _player;

        private void Awake() => _player = GetComponent<PlayerAgent>();

        private void OnEnable()
        {
            if (_player != null)
                _player.DamageDealt += HandleDamageDealt;
        }

        private void OnDisable()
        {
            if (_player != null)
                _player.DamageDealt -= HandleDamageDealt;
        }

        private void HandleDamageDealt(DamageDealtInfo info)
        {
            if (info.Killed || info.Amount <= 0f)
                return;

            if (CrowdControl.Of(info.Target) is not CrowdControlReceiver receiver)
                return;

            bool big = info.Amount >= bigHitDamage || info.IsWeakpoint;
            receiver.ApplyFlinch(big ? bigHitFlinch : hitFlinch);
        }
    }
}
