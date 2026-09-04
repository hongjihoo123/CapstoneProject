namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public class DronEnemy : AbstractEnemy
    {
        public override void Attack()
        {
            Weapon?.PrimaryAttack();
        }
    }
}