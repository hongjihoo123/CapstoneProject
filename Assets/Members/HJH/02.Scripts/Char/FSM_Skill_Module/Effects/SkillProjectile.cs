using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects
{
    // Code-only projectile for skills: flies flat, hits damageables (once each), stops on walls.
    // Drawn through AttackAreaBus until real VFX exist. Run it with ISkillContext.Run.
    public sealed class SkillProjectile : ISkillTimedEffect
    {
        public struct Settings
        {
            public float Speed;
            public float MaxDistance;
            public float Radius;
            public bool Pierce;
            public float Delay;
            public IDamageable Ignore;
            // Visual: VisualSize 0 = invisible (only the debug circle). Otherwise an FxLibrary orb of this size rides it.
            public Color Color;
            public float VisualSize;
        }

        private readonly ISkillContext _context;
        private readonly Settings _settings;
        private readonly Vector3 _direction;
        private readonly Action<IDamageable, Vector3> _onHit;
        private readonly Action<Vector3> _onFinish;
        private readonly HashSet<IDamageable> _hit = new();
        private Vector3 _position;
        private float _travelled;
        private float _waited;
        private FxHandle _orb;

        public SkillProjectile(ISkillContext context, Vector3 origin, Vector3 direction, Settings settings,
            Action<IDamageable, Vector3> onHit, Action<Vector3> onFinish = null)
        {
            _context = context;
            _settings = settings;
            _onHit = onHit;
            _onFinish = onFinish;
            _position = origin;
            direction.y = 0f;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        public bool Tick(float deltaTime)
        {
            if (_waited < _settings.Delay)
            {
                _waited += deltaTime;
                return true;
            }

            float step = Mathf.Min(_settings.Speed * deltaTime, _settings.MaxDistance - _travelled);
            Vector3 next = _position + _direction * step;
            bool blocked = _context.LineBlocked(_position, next, out Vector3 wallPoint);
            if (blocked)
                next = wallPoint;

            foreach (IDamageable target in _context.OverlapCapsule(_position, next, _settings.Radius))
            {
                if (ReferenceEquals(target, _settings.Ignore) || !_hit.Add(target))
                    continue;

                Vector3 point = PointOf(target, next);
                _onHit?.Invoke(target, point);

                if (!_settings.Pierce)
                    return Finish(point);
            }

            _position = next;
            _travelled += step;
            UpdateOrb();
            AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.Skill, _position, _settings.Radius, 0.02f));

            return blocked || _travelled >= _settings.MaxDistance - 0.0001f ? Finish(_position) : true;
        }

        private void UpdateOrb()
        {
            if (_settings.VisualSize <= 0f)
                return;

            if (_orb == null)
                _orb = Fx.Orb(_position, _settings.Color, _settings.VisualSize);
            else
                _orb.MoveTo(_position);
        }

        private bool Finish(Vector3 point)
        {
            if (_orb != null)
            {
                _orb.MoveTo(point);
                _orb.Kill();
            }

            _onFinish?.Invoke(point);
            return false;
        }

        private Vector3 PointOf(IDamageable target, Vector3 fallback)
        {
            if (target is not Component component)
                return fallback;

            Vector3 point = component.transform.position;
            point.y = _position.y;
            return point;
        }
    }
}
