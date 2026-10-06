using System;
using UnityEngine;

namespace RobotWeapons
{
    [CreateAssetMenu(menuName = "Weapon/Chain Weapon Data", fileName = "New ChainWeaponData")]
    public class ChainWeaponData : WeaponData
    {
        [Serializable]
        public class ComboStep
        {
            public string animId = "Chain_Attack1";
            public float damageMultiplier = 1f;
            public float duration = 0.55f;
            public float hitStart = 0.15f;
            public float hitEnd = 0.35f;
            public float inputBufferStart = 0.3f;
            public float moveSpeedMultiplier = 0.5f;
            public Vector3 hitCenter = new Vector3(0f, 0f, 1.1f);
            public Vector3 hitSize = new Vector3(2.4f, 1.6f, 2.2f);
        }

        public float baseDamage = 25f;
        public float comboResetDelay = 0.8f;
        public LayerMask hitMask = ~0;

        public ComboStep[] steps =
        {
            new ComboStep { animId = "Chain_Attack1" },
            new ComboStep { animId = "Chain_Attack2" },
            new ComboStep
            {
                animId = "Chain_Attack3",
                damageMultiplier = 2f,
                duration = 0.8f,
                hitStart = 0.3f,
                hitEnd = 0.5f,
                inputBufferStart = 0.8f,
                moveSpeedMultiplier = 0.3f,
                hitSize = new Vector3(3f, 1.8f, 3f)
            }
        };
    }
}
