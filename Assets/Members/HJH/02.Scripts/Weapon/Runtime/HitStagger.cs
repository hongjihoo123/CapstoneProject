using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace RobotWeapons
{
    // 플레이어가 적을 때릴 때마다 적에게 짧은 경직을 줌. 연타하면 경직이 이어짐 (최대 시간·면역은 CrowdControlReceiver 가 정함)
    // 무기·스킬 코드는 이걸 몰라도 됨: PlayerAgent.DamageDealt 만 듣고 있음.
    // CharacterModel.For 가 플레이어에 알아서 붙여줌
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

            // 적이 직접 CC 를 구현했으면(기본 Receiver 가 아니면) 경직 규칙을 모르니 건드리지 않음
            if (CrowdControl.Of(info.Target) is not CrowdControlReceiver receiver)
                return;

            bool big = info.Amount >= bigHitDamage || info.IsWeakpoint;
            receiver.ApplyFlinch(big ? bigHitFlinch : hitFlinch);
        }
    }
}
