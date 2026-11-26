using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    public sealed class DelayedImpact : ISkillTimedEffect
    {
        private readonly ElementSkillCast _cast;
        private readonly Vector3? _center;
        private readonly float _delay;
        private readonly float _radius;
        private readonly FxHandle _circle;
        private readonly FxHandle _windup;
        private float _elapsed;

        public DelayedImpact(ElementSkillCast cast, Vector3? center, float delay, float radius, FxSlot windupFx = null)
        {
            _cast = cast;
            _center = center;
            _delay = delay;
            _radius = radius;

            if (center.HasValue)
            {
                Vector3 ground = cast.Ground(center.Value);
                if (!cast.Skill.HideMagicCircle)
                {
                    _circle = Fx.MagicCircle(ground, radius, cast.Color);
                    _circle.SetSpeed(160f);
                }
                _windup = cast.Hold(windupFx, ground, Vector3.forward);
            }
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed < _delay)
            {
                _circle?.SetValue(_elapsed / _delay);
                if (_center.HasValue)
                    AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.WeaponWindup, _center.Value, _radius, 0.02f));
                return true;
            }

            _circle?.Kill();
            _windup?.Kill();
            _cast.Impact(_center ?? _cast.Position, _radius);
            return false;
        }
    }
}
