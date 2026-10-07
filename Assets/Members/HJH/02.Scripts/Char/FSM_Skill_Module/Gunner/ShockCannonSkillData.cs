using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Gunner
{
    // Character 1 R: an energy barrel forms in front of the pistols and charges (effect only - the pistols
    // never transform, so no extra animation or model is needed), then fires one heavy shell from its tip.
    // On the first enemy it hits the shell deals big damage, then bursts into shrapnel flying out in every
    // direction (the first target is not hit again). Firing kicks the player a short step back.
    // The skill's Duration must be longer than chargeTime.
    [CreateAssetMenu(menuName = "Skill/Gunner/Shock Cannon")]
    public class ShockCannonSkillData : SkillData
    {
        [Header("Energy barrel (charge)")]
        [SerializeField] private float chargeTime = 0.3f;
        [SerializeField] private float barrelLength = 2.2f;
        [SerializeField] private float barrelWidth = 0.7f;
        [SerializeField, Tooltip("Barrel starts this far in front of the player (at the pistols).")]
        private float barrelOffset = 0.4f;

        [Header("Shell")]
        [SerializeField] private float shellSpeed = 35f;
        [SerializeField] private float shellRange = 22f;
        [SerializeField] private float shellRadius = 0.5f;
        [SerializeField] private float shellDamage = 120f;

        [Header("Shrapnel")]
        [SerializeField, Min(1)] private int shrapnelCount = 10;
        [SerializeField] private float shrapnelSpeed = 18f;
        [SerializeField] private float shrapnelRange = 7f;
        [SerializeField] private float shrapnelRadius = 0.35f;
        [SerializeField] private float shrapnelDamage = 30f;

        [Header("Recoil step")]
        [SerializeField] private float recoilDistance = 2.5f;
        [SerializeField] private float recoilDuration = 0.15f;

        [Header("Feel")]
        [SerializeField] private float hitStop = 0.12f;
        [SerializeField] private float shake = 0.55f;

        protected override Color DefaultFxColor => new(1f, 0.55f, 0.15f);

        public override ISkillExecution Begin(ISkillContext context) => new Execution(this, context);

        // Charge: the barrel grows and follows the player (direction locked at cast). Fire: shell from the tip.
        private sealed class Execution : ISkillExecution
        {
            private readonly ShockCannonSkillData _data;
            private readonly ISkillContext _context;
            private readonly Vector3 _direction;
            private FxHandle _barrel;
            private bool _fired;

            public Execution(ShockCannonSkillData data, ISkillContext context)
            {
                _data = data;
                _context = context;
                _direction = context.AimDirection;

                (Vector3 start, Vector3 tip) = Barrel();
                _barrel = Fx.EnergyCannon(start, tip, data.FxColor, data.barrelWidth);
            }

            public void Tick(float elapsed)
            {
                if (_fired)
                    return;

                (Vector3 start, Vector3 tip) = Barrel();
                if (elapsed < _data.chargeTime)
                {
                    float charge = elapsed / _data.chargeTime;
                    _barrel.SetPoints(start, tip);
                    _barrel.SetValue(Mathf.Lerp(0.35f, 1f, charge));
                    return;
                }

                Fire(tip);
            }

            public void OnAnimationEvent(SkillAnimationEvent animationEvent) { }

            // Cancelled before firing: the barrel just fades.
            public void End() => _barrel?.Kill();

            private (Vector3 start, Vector3 tip) Barrel()
            {
                Vector3 start = _context.Transform.position + Vector3.up + _direction * _data.barrelOffset;
                return (start, start + _direction * _data.barrelLength);
            }

            private void Fire(Vector3 tip)
            {
                _fired = true;
                _barrel.Kill();
                _barrel = null;

                Color color = _data.FxColor;
                var settings = new SkillProjectile.Settings
                {
                    Speed = _data.shellSpeed,
                    MaxDistance = _data.shellRange,
                    Radius = _data.shellRadius,
                    Color = color,
                    VisualSize = 0.7f
                };

                Vector3 direction = _direction;
                ISkillContext context = _context;
                ShockCannonSkillData data = _data;
                context.Run(new SkillProjectile(context, tip, direction, settings,
                    (target, point) => data.OnShellHit(context, target, point),
                    point => Fx.ProjectileImpact(point, direction, color, 0.7f, false)));

                Fx.CannonMuzzle(tip, direction, color);
                Fx.Shockwave(context.Transform.position, color, 2.2f);

                context.Dash(-direction, data.recoilDistance / data.recoilDuration, data.recoilDuration);
                context.PlayHitFeel(0.03f, data.shake * 0.5f);
            }
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 start = context.Transform.position;
            preview.Path(start, start + context.AimDirection * shellRange, shellRadius * 2f);
        }

        private void OnShellHit(ISkillContext context, IDamageable target, Vector3 point)
        {
            Color color = FxColor;
            context.DealDamage(target, shellDamage);
            context.PlayHitFeel(hitStop, shake);

            Vector3 ground = point - Vector3.up;
            Fx.Explosion(ground, 3.5f, color, 2f);
            AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.Skill, point, 1.5f, 0.2f));

            var settings = new SkillProjectile.Settings
            {
                Speed = shrapnelSpeed,
                MaxDistance = shrapnelRange,
                Radius = shrapnelRadius,
                Ignore = target,
                Color = Color.Lerp(color, Color.white, 0.3f),
                VisualSize = 0.25f
            };

            for (int i = 0; i < shrapnelCount; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, 360f * i / shrapnelCount, 0f) * Vector3.forward;
                context.Run(new SkillProjectile(context, point, direction, settings,
                    (hit, _) => context.DealDamage(hit, shrapnelDamage)));
            }
        }
    }
}
