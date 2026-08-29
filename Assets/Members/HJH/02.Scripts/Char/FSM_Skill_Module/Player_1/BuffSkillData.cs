using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Player_1
{
    [Serializable]
    public struct BuffEntry
    {
        public BuffType type;
        public float multiplier;
        public float duration;
    }

    [CreateAssetMenu(menuName = "Skill/Buff")]
    public class BuffSkillData : SkillData
    {
        [SerializeField] private BuffEntry[] buffs;

        public override void Execute(SkillStateModule owner)
        {
            var status = owner.Player.GetModule<StatusEffectModule>();
            if (status == null) return;

            foreach (var buff in buffs)
                status.Apply(buff.type, buff.multiplier, buff.duration);
        }
    }
}
