using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyDashSkill : MonoBehaviour, IEnemySkill
    {
        public bool IsUsing { get; private set; }

        public void Initialize(AbstractEnemy owner)
        {

        }

        public void UseSkill()
        {
            if (IsUsing)
                return;

            StartCoroutine(UseSkillCoroutine());
        }

        private IEnumerator UseSkillCoroutine()
        {
            IsUsing = true;


            yield return new WaitForSeconds(2f);

            IsUsing = false;
        }
    }
}