using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Gunner
{
    // 건슬링어 궁극기(R) 화염포: 총구 앞에 포신이 충전됐다가 일직선으로 큰 탄을 쏨.
    // 첫 적한테 맞으면 큰 피해 + 사방으로 파편이 퍼지고, 파편이 주변 적한테 맞으면 그 자리에서 또 터짐(추가 폭발).
    // 쏘면 반동으로 플레이어가 살짝 뒤로 밀림. 스킬 Duration 은 chargeTime 보다 길어야 함
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
        [SerializeField, Tooltip("파편이 적한테 맞으면 그 자리에서 작게 한 번 더 터짐")] private float shrapnelBlastRadius = 1.6f;
        [SerializeField] private float shrapnelBlastDamage = 15f;

        [Header("Recoil step")]
        [SerializeField] private float recoilDistance = 2.5f;
        [SerializeField] private float recoilDuration = 0.15f;

        [Header("Feel")]
        [SerializeField] private float hitStop = 0.12f;
        [SerializeField] private float shake = 0.55f;
        [SerializeField, Tooltip("Rumble while the barrel charges, grows to this.")] private float chargeRumble = 0.16f;
        [SerializeField, Tooltip("Shake when the shell leaves the barrel.")] private float fireShake = 0.5f;
        [SerializeField] private float fireShakeDuration = 0.32f;
        [SerializeField, Tooltip("Camera pushed opposite the shot (world units).")] private float cameraKick = 0.9f;
        [SerializeField, Tooltip("Shake when the shell bursts without hitting anyone.")] private float missShake = 0.3f;

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
                    _context.PlayShake(_data.chargeRumble * charge * charge, 0.06f);
                    GunnerFx.CannonCharging(tip, _data.FxColor, charge);
                    return;
                }

                Fire(tip);
            }

            public void OnAnimationEvent(SkillAnimationEvent animationEvent) { }

            // Cancelled before firing: the barrel just fades.
            public void End() => _barrel?.Kill();

            private (Vector3 start, Vector3 tip) Barrel()
            {
                // 포신도 경사를 따라 눕힘: 오르막에서 수평으로 뻗으면 포신 끝(탄 나가는 곳)이 땅속에 들어가서 탄이 바로 사라짐
                Vector3 origin = _context.Transform.position + Vector3.up;
                GroundTrace.Trace(_context, origin, _direction, _data.barrelOffset, _path);
                Vector3 start = _path[_path.Count - 1];
                GroundTrace.Trace(_context, start, _direction, _data.barrelLength, _path);
                return (start, _path[_path.Count - 1]);
            }

            private readonly System.Collections.Generic.List<Vector3> _path = new();

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
                    point =>
                    {
                        Fx.ProjectileImpact(point, direction, color, 0.7f, false);
                        context.PlayShake(data.missShake, 0.2f);
                    }));

                GunnerFx.CannonFire(tip, context.Transform.position, direction, color);

                context.Dash(-direction, data.recoilDistance / data.recoilDuration, data.recoilDuration, 0f, faceDirection: false);   // 반동이라 조준 방향 유지
                context.PlayHitFeel(0.04f, 0f);
                context.PlayShake(data.fireShake, data.fireShakeDuration);
                context.PlayCameraKick(-direction * data.cameraKick, 0.3f);
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
            context.DealDamage(target, shellDamage, this);
            context.PlayHitFeel(hitStop, shake);
            context.PlayCameraKick(Vector3.down * 0.5f, 0.25f);

            Vector3 ground = point - Vector3.up;
            GunnerFx.CannonExplode(ground, color);
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

            // 스킬 트리: 파편 수 증가
            int count = shrapnelCount + Mathf.RoundToInt(SkillUpgrades.Exact(SkillUpgradeKind.ShrapnelCount, SkillUpgradeScope.Ultimate));
            // 집속탄: 파편 수 2배, 한 발 피해 -35%
            bool dense = SkillUpgrades.Exact(SkillUpgradeKind.ShrapnelDense, SkillUpgradeScope.Ultimate) > 0f;
            float shrapnelHit = dense ? shrapnelDamage * 0.65f : shrapnelDamage;
            if (dense)
                count *= 2;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward;
                context.Run(new SkillProjectile(context, point, direction, settings,
                    (hit, hitPoint) =>
                    {
                        context.DealDamage(hit, shrapnelHit, this);
                        SkillBlast.Explode(context, hitPoint, shrapnelBlastRadius, shrapnelBlastDamage, color, 0.02f, 0.15f, 0.8f);
                    }));
            }
        }
    }
}
