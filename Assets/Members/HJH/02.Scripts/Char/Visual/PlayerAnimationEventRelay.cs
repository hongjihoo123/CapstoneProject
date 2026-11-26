using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public class PlayerAnimationEventRelay : MonoBehaviour
    {
        private PlayerAgent _player;

        private void Awake() => _player = GetComponentInParent<PlayerAgent>();

        public void Anim_SkillHitBegin() => _player.Anim_SkillHitBegin();

        public void Anim_SkillHitEnd() => _player.Anim_SkillHitEnd();

        public void Anim_Fire()
        {
            if (_player != null && _player.Weapon != null && _player.Weapon.Weapon is IAnimationTimedShots gun)
                gun.OnFireFrame();
        }

        public void PlaySkillSound(string eventName) =>
            Audio.SkillSoundPlayer.OnAnimationEvent(_player != null ? _player.SkillFsm : null, eventName);
    }
}
