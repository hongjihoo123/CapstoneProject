using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Gunner
{
    // 건슬링어 기본스킬(우클릭): 마우스 방향으로 대쉬한 다음, 그 방향 앞으로 부채꼴 충격포를 쏴서 적들을 뒤로 밀어냄.
    // 대쉬 끝나는 순간(벽에 막혀도) 충격포가 나감.
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

            // 대쉬 경로 + 도착 지점에서 나갈 부채꼴 범위
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
                context.Dash(_direction, data.dashSpeed, data.dashTime);   // 대쉬 잔상은 CombatFeedback 이 알아서 그림
            }

            public void Tick(float elapsed)
            {
                // 대쉬 끝나면(시간 다 됐거나 벽에 막혀서 멈췄으면) 바로 쏨
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
                Color color = _data.FxColor;

                Fx.ConeBlast(origin, _direction, _data.blastRange, _data.blastAngle, color);
                AttackAreaBus.Raise(AttackArea.Cone(AttackAreaKind.Skill, origin, Quaternion.LookRotation(_direction), _data.blastRange, _data.blastAngle, 0.2f));

                float halfAngle = _data.blastAngle * 0.5f;
                int hits = 0;
                foreach (IDamageable target in _context.OverlapSphere(origin + Vector3.up, _data.blastRange))
                {
                    if (target is not Component component)
                        continue;

                    Vector3 toTarget = component.transform.position - origin;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(_direction, toTarget) > halfAngle)
                        continue;   // 부채꼴 밖

                    _context.DealDamage(target, _data.damage);
                    // 정면 방향 + 옆으로 벌어지는 방향 섞어서 밀어냄 (부채꼴 모양으로 퍼지게)
                    Vector3 push = toTarget.sqrMagnitude > 0.01f ? (_direction + toTarget.normalized).normalized : _direction;
                    _context.Knockback(target, push, _data.knockback);
                    hits++;
                }

                _context.PlayHitFeel(hits > 0 ? _data.hitStop : 0f, _data.shake);
                _context.PlayCameraKick(-_direction * _data.cameraKick);
            }
        }
    }
}
