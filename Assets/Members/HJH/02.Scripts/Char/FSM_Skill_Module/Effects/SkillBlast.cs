using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects
{
    // One-shot circular blast: damage everything in the radius once, play the explosion and the hit feel.
    public static class SkillBlast
    {
        // Damages everything in the circle once and plays the explosion. Returns the number of targets hit.
        public static int Explode(ISkillContext context, Vector3 center, float radius, float damage, Color color,
            float hitStop = 0f, float shake = 0f, float power = 1f)
        {
            AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.Skill, center, radius, 0.2f));
            Fx.Explosion(GroundOf(context, center), radius, color, power);

            int hits = 0;
            foreach (IDamageable target in context.OverlapSphere(center + Vector3.up * 0.5f, radius))
            {
                context.DealDamage(target, damage);
                hits++;
            }

            // A blast always shakes a little; landing hits adds the freeze.
            context.PlayHitFeel(hits > 0 ? hitStop : 0f, hits > 0 ? shake : shake * 0.4f);
            return hits;
        }

        private static Vector3 GroundOf(ISkillContext context, Vector3 point) =>
            context.TryFindGround(point + Vector3.up, out Vector3 ground) ? ground : point;
    }

    // Telegraphed magic circle that fills up and explodes after a delay (traps, delayed strikes).
    public sealed class DelayedBlast : ISkillTimedEffect
    {
        private readonly ISkillContext _context;
        private readonly Vector3 _center;
        private readonly float _delay;
        private readonly float _radius;
        private readonly float _damage;
        private readonly Color _color;
        private readonly float _hitStop;
        private readonly float _shake;
        private readonly FxHandle _circle;
        private float _elapsed;

        public DelayedBlast(ISkillContext context, Vector3 center, float delay, float radius, float damage, Color color,
            float hitStop = 0f, float shake = 0f)
        {
            _context = context;
            _center = center;
            _delay = delay;
            _radius = radius;
            _damage = damage;
            _color = color;
            _hitStop = hitStop;
            _shake = shake;
            _circle = Fx.MagicCircle(center, radius, color);
            _circle.SetSpeed(160f);
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed < _delay)
            {
                _circle.SetValue(_elapsed / _delay);
                AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.WeaponWindup, _center, _radius, 0.02f));
                return true;
            }

            _circle.Kill();
            SkillBlast.Explode(_context, _center, _radius, _damage, _color, _hitStop, _shake);
            return false;
        }
    }
}
