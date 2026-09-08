namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public class DogEnemy : AbstractEnemy
    {
        public override void Attack()
        {
            Weapon?.PrimaryAttack();
        }
    }
}