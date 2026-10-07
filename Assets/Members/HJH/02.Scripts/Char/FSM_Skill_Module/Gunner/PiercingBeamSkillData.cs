using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Gunner
{
    // Character 1 Q: straight beam that pierces everything in line after a short charge.
    // The player keeps moving (slowed) while it charges; the direction is locked at cast.
    [CreateAssetMenu(menuName = "Skill/Gunner/Piercing Beam")]
    public class PiercingBeamSkillData : SkillData
    {
        [SerializeField] private float range = 16f;
        [SerializeField] private float width = 1.2f;
        [SerializeField] private float windup = 0.12f;
        [SerializeField] private float damage = 45f;
        [SerializeField] private float hitStop = 0.06f;
        [SerializeField] private float shake = 0.22f;
        [SerializeField, Tooltip("Kick back on fire.")] private float recoilDistance = 0.7f;

        protected override Color DefaultFxColor => new(0.3f, 1f, 0.85f);

        public override ISkillExecution Begin(ISkillContext context) => new Execution(this, context);

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 start = context.Transform.position;
            Vector3 end = start + context.AimDirection * range;
            preview.Path(start, end, width);
        }

        private sealed class Execution : ISkillExecution
        {
            private readonly PiercingBeamSkillData _data;
            private readonly ISkillContext _context;
            private readonly Vector3 _direction;
            private bool _fired;
            private FxHandle _charge;
            private FxHandle _aimLine;

            public Execution(PiercingBeamSkillData data, ISkillContext context)
            {
                _data = data;
                _context = context;
                _direction = context.AimDirection;
            }

            public void Tick(float elapsed)
            {
                if (_fired)
                    return;

                Vector3 start = _context.Transform.position + Vector3.up + _direction * 0.6f;
                Vector3 end = start + _direction * _data.range;

                if (elapsed < _data.windup)
                {
                    // Charge-up: energy gathering at the muzzle + faint aim line, both follow the player.
                    Color color = _data.FxColor;
                    _charge ??= Fx.Charge(start, color);
                    _aimLine ??= Fx.AimLine(start, end, color, 0.04f);
                    _charge.MoveTo(start);
                    _charge.SetValue(elapsed / _data.windup);
                    _aimLine.SetPoints(start, end);
                    return;
                }

                Fire(start, end);
            }

            public void OnAnimationEvent(SkillAnimationEvent animationEvent) { }

            public void End() => StopCharge();

            private void StopCharge()
            {
                _charge?.Kill();
                _aimLine?.Kill();
                _charge = null;
                _aimLine = null;
            }

            private void Fire(Vector3 start, Vector3 end)
            {
                _fired = true;
                StopCharge();
                Color color = _data.FxColor;

                AttackAreaBus.Raise(AttackArea.Box(AttackAreaKind.Skill, (start + end) * 0.5f, Quaternion.LookRotation(end - start),
                    new Vector3(_data.width, 0.05f, _data.range), 0.15f));

                // The beam prefab has the body + white core lines, a muzzle flash and sparks spread along its length.
                Fx.Beam(start, end, color, _data.width * 1.1f, 0.3f);
                Fx.Shockwave(start - Vector3.up * 0.9f, color, 1.4f);

                int hits = 0;
                foreach (IDamageable target in _context.OverlapCapsule(start, end, _data.width * 0.5f))
                {
                    _context.DealDamage(target, _data.damage);
                    hits++;
                }

                _context.Dash(-_direction, _data.recoilDistance / 0.08f, 0.08f);
                _context.PlayHitFeel(hits > 0 ? _data.hitStop : 0f, _data.shake);
            }
        }
    }
}
