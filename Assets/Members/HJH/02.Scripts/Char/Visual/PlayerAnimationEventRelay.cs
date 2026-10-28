using Members.KYR._01_Scripts;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 애니메이션 이벤트는 Animator 붙은 오브젝트(캐릭터 모델)로만 와서, 여기서 플레이어 쪽으로 넘겨줌.
    // CharacterModel 이 모델 생성할 때 자동으로 붙임
    public class PlayerAnimationEventRelay : MonoBehaviour
    {
        private PlayerAgent _player;

        private void Awake() => _player = GetComponentInParent<PlayerAgent>();

        public void Anim_SkillHitBegin() => _player.Anim_SkillHitBegin();

        public void Anim_SkillHitEnd() => _player.Anim_SkillHitEnd();

        // 발사 클립(평타 Attack, 패시브 PassiveShot)에서 총구 불 나가는 프레임에 넣으면 그 순간 총알/충격포가 나감
        public void Anim_Fire()
        {
            if (_player != null && _player.Weapon != null && _player.Weapon.Weapon is IAnimationTimedShots gun)
                gun.OnFireFrame();
        }
    }
}
