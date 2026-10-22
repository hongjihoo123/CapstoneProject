using Members.KYR._01_Scripts;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public class PlayerAnimationEventRelay : MonoBehaviour
    {
        private PlayerAgent _player;

        private void Awake() => _player = GetComponentInParent<PlayerAgent>();

        public void Anim_SkillHitBegin() => _player.Anim_SkillHitBegin();

        public void Anim_SkillHitEnd() => _player.Anim_SkillHitEnd();
    }
}
