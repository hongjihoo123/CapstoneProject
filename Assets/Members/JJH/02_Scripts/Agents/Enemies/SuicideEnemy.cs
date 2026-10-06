using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public class SuicideEnemy : AbstractEnemy
    {
        [SerializeField] private GameObject deadParticle;

        public override void Attack()
        {
            base.Attack();
            Weapon?.PrimaryAttack();
            Instantiate(deadParticle, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}