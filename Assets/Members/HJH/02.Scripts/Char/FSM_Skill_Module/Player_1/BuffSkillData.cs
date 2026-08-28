using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Player_1
{
    [CreateAssetMenu(menuName = "Skill/Buff")]
    public class BuffSkillData : SkillData
    {
        [SerializeField] private BuffType type;
        [SerializeField] private float multiplier = 1.3f;
        [SerializeField] private float buffDuration = 5f;

        public override void Execute(SkillStateModule owner)
        {
            owner.Player.GetModule<StatusEffectModule>()?.Apply(type, multiplier, buffDuration);
        }
    }
}
