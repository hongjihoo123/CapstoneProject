using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 잠깐 뒤에 터지는 거 (메테오, 낙뢰, 바위 방패 끝 폭발)
    //  - 자리 정해져 있으면: 바닥에 원이 차오르고 예고 이펙트 → 시간 되면 거기서 터짐
    //  - 자리 null 이면: 그때 플레이어가 서 있는 자리에서 터짐
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
                _circle = Fx.MagicCircle(ground, radius, cast.Color);
                _circle.SetSpeed(160f);
                _windup = cast.Hold(windupFx, ground, Vector3.forward);
            }
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed < _delay)
            {
                _circle?.SetValue(_elapsed / _delay);   // 원이 차오름
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
