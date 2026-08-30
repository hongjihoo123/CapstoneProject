using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Player_1
{
    [CreateAssetMenu(menuName = "Skill/Dash")]
    public class DashSkillData : SkillData
    {
        [SerializeField] private float dashForce = 10f;

        public override void Execute(SkillStateModule owner)
        {
            Debug.Log("대쉬 실행");
            owner.Player.Mover.Dash(owner.transform.forward, dashForce, Duration);
        }
    }
}
