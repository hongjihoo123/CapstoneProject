using Members.KYR._01.Scripts.Player;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Gunner
{
    [CreateAssetMenu(menuName = "Skill/Gunner/Dash Blast")]
    public class DashBlastSkillData : SkillData
    {
        [Header("대쉬")]
        [SerializeField] private float dashSpeed = 24f;
        [SerializeField] private float dashTime = 0.18f;

        [Header("부채꼴 충격포")]
        [SerializeField] private float blastRange = 7f;
        [SerializeField, Range(10f, 180f), Tooltip("부채꼴 전체 각도")] private float blastAngle = 120f;
        [SerializeField] private float damage = 25f;
        [SerializeField, Tooltip("맞은 적 밀어내는 거리")] private float knockback = 4.5f;

        [Header("타격감")]
        [SerializeField] private float hitStop = 0.05f;
        [SerializeField] private float shake = 0.3f;
        [SerializeField] private float cameraKick = 0.35f;

        protected override Color DefaultFxColor => new(0.3f, 1f, 0.85f);

        public override ISkillExecution Begin(ISkillContext context) => new Execution(this, context);

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 direction = SkillAim.Resolve(context, 0f, 1f, out _);
            Vector3 start = context.Transform.position;
            Vector3 landing = start + direction * (dashSpeed * dashTime);

            preview.Path(start, landing, 1f);
            preview.Cone(landing, direction, blastRange, blastAngle);
        }

        public override void DescribeStats(System.Collections.Generic.List<SkillStat> stats)
        {
            stats.Add(new SkillStat("피해량", $"{damage:0}"));
            stats.Add(new SkillStat("대쉬 거리", $"{dashSpeed * dashTime:0.#}m"));
            stats.Add(new SkillStat("충격포", $"{blastRange:0.#}m / {blastAngle:0}°"));
            base.DescribeStats(stats);
        }

        private sealed class Execution : ISkillExecution
        {
            private readonly DashBlastSkillData _data;
            private readonly ISkillContext _context;
            private readonly Vector3 _direction;
            private bool _fired;

            public Execution(DashBlastSkillData data, ISkillContext context)
            {
                _data = data;
                _context = context;
                _direction = SkillAim.Resolve(context, 0f, 1f, out _);
                context.Dash(_direction, data.dashSpeed, data.dashTime);
                GunnerFx.DashStart(context.Transform, _direction, data.FxColor, data.dashTime);
            }

            public void Tick(float elapsed)
            {
                if (!_fired && (elapsed >= _data.dashTime || (elapsed > 0.02f && !_context.IsDashing)))
                    Fire();
            }

            public void OnAnimationEvent(SkillAnimationEvent animationEvent) { }

            public void End()
            {
                if (!_fired)
                    Fire();
            }

            private void Fire()
            {
                _fired = true;
                Vector3 origin = _context.Transform.position;
                float blastRange = _data.blastRange * SkillUpgrades.Multiplier(SkillUpgradeKind.Range, _data);

                int crits = 0;
                void OnLanded(SkillUpgradeScope slot)
                {
                    if (slot == SkillUpgradeScope.Shock)
                        crits++;
                }
                CritSystem.Landed += OnLanded;
                int hits;
                try
                {
                    hits = Blast(origin, _direction, blastRange, ref crits);
                    if (SkillUpgrades.Exact(SkillUpgradeKind.DoubleBlast, SkillUpgradeScope.Shock) > 0f)
                        hits += Blast(origin, -_direction, blastRange, ref crits);
                }
                finally
                {
                    CritSystem.Landed -= OnLanded;
                }

                float refund = SkillUpgrades.Exact(SkillUpgradeKind.HitRefundSelf, SkillUpgradeScope.Shock) * hits;
                if (refund > 0f)
                    _context.ReduceCooldown(SkillSlotId.Weapon, refund);
                if (crits >= 3 && _context is PlayerSkillContext)
                    CritSystem.TryChainReset(_player(), crits);

                _context.PlayHitFeel(hits > 0 ? _data.hitStop : 0f, _data.shake);
                _context.PlayCameraKick(-_direction * _data.cameraKick);
            }

            private PlayerAgent _player() =>
                (_context.Transform != null) ? _context.Transform.GetComponentInParent<PlayerAgent>() : null;

            private int Blast(Vector3 origin, Vector3 direction, float blastRange, ref int crits)
            {
                Color color = _data.FxColor;
                GunnerFx.DashBlast(origin, direction, blastRange, _data.blastAngle, color);
                AttackAreaBus.Raise(AttackArea.Cone(AttackAreaKind.Skill, origin, Quaternion.LookRotation(direction), blastRange, _data.blastAngle, 0.2f));

                float halfAngle = _data.blastAngle * 0.5f;
                float chain = SkillUpgrades.Exact(SkillUpgradeKind.ChainShock, SkillUpgradeScope.Shock);
                int hits = 0;
                foreach (IDamageable target in _context.OverlapSphere(origin + Vector3.up, blastRange))
                {
                    if (target is not Component component)
                        continue;

                    Vector3 toTarget = component.transform.position - origin;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(direction, toTarget) > halfAngle)
                        continue;
                    if (!_struck.Add(target))
                        continue;

                    CritSystem.Mark(target);
                    int before = crits;
                    _context.DealDamage(target, _data.damage);
                    if (chain > 0f && crits > before)
                        Chain(component.transform.position, target, _data.damage * chain);

                    GunnerFx.BlastHit(component.transform.position + Vector3.up, toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : direction, color);
                    Vector3 push = toTarget.sqrMagnitude > 0.01f ? (direction + toTarget.normalized).normalized : direction;
                    _context.Knockback(target, push, _data.knockback);
                    hits++;
                }

                return hits;
            }

            private readonly System.Collections.Generic.HashSet<IDamageable> _struck = new();

            private void Chain(Vector3 center, IDamageable source, float damage)
            {
                foreach (IDamageable other in _context.OverlapSphere(center + Vector3.up, ChainRadius))
                {
                    if (other == source || other is not Component part || !_struck.Add(other))
                        continue;

                    GunnerFx.BlastHit(part.transform.position + Vector3.up, (part.transform.position - center).normalized, _data.FxColor);
                    _context.DealDamage(other, damage);
                }
            }

            private const float ChainRadius = 5f;
        }
    }
}
