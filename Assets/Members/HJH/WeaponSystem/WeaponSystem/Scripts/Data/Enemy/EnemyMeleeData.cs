using RobotWeapons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Members.HJH.WeaponSystem.WeaponSystem.Scripts.Data.Enemy
{
    [CreateAssetMenu(menuName = "Weapon/Enemy Melee Data")]
    public class EnemyMeleeData : WeaponData
    {
        public float meleeDamage = 10f;
        public float attackCooldown = 1.5f; // 이건 공격 한번 할 떄,
        // 데미지 2번 들어갈까봐 임시 조치로 한거고, 완전히 없애진 말고 수치 조절해가면서 버그없을 정도로까지만 테스트 해서 사용하면 될 듯
    }
}
