using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;
using UnityEngine.AI;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 몇 초 동안 남는 벽/장막. 적 투사체를 막고, 켜면 적 길찾기도 막음 (NavMesh 구멍)
    //  - Front: 내 앞에 커서 쪽을 보고 섬 (물 장막)
    //  - Cursor: 커서 자리에 커서 쪽을 보고 섬 (바위 벽)
    //  - AroundSelf: 내 주변 원, 나를 따라다님 (바람 장막)
    [CreateAssetMenu(menuName = "Skill/Element/Barrier (벽·장막)")]
    public class BarrierSkill : ElementSkillBase
    {
        public enum Placement { Front, Cursor, AroundSelf }

        [Header("벽·장막")]
        [SerializeField] private Placement placement = Placement.Front;
        [SerializeField, Tooltip("Front = 내 앞 거리, Cursor = 커서 최대 거리")] private float distance = 2f;
        [SerializeField, Tooltip("벽 폭 (AroundSelf 면 무시)")] private float width = 5f;
        [SerializeField, Tooltip("벽 두께 (AroundSelf 면 무시)")] private float thickness = 0.8f;
        [SerializeField, Tooltip("AroundSelf 원 반지름")] private float radius = 2.5f;
        [SerializeField, Tooltip("남아 있는 시간")] private float lifetime = 4f;
        [SerializeField, Tooltip("적 투사체 막기")] private bool blockProjectiles = true;
        [SerializeField, Tooltip("적이 못 지나가게 (NavMesh 에 구멍)")] private bool blockMovement;
        [SerializeField, Tooltip("벽 모양 이펙트. 벽 기준 앞 = +Z, 폭 = X")] private FxSlot barrierFx = new();
        [SerializeField, Tooltip("투사체 막을 때 그 자리에서")] private FxSlot blockFx = new();

        protected override ISkillExecution Cast(ElementSkillCast cast)
        {
            Vector3 aim = cast.Aim(distance, out float reach);
            Vector3 center = placement switch
            {
                Placement.Front => cast.Position + aim * distance,
                Placement.Cursor => cast.Position + aim * reach,
                _ => cast.Position
            };

            cast.PlayCast(aim);
            cast.Context.PlayShake(0.1f, 0.15f);
            cast.Context.Run(new Wall(this, cast, cast.Ground(center), aim));
            return null;
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 position = context.Transform.position;
            if (placement == Placement.AroundSelf)
            {
                preview.Circle(position, radius);
                return;
            }

            Vector3 aim = SkillAim.Resolve(context, 0f, distance, out float reach);
            Vector3 center = position + aim * (placement == Placement.Front ? distance : reach);
            Vector3 side = Vector3.Cross(Vector3.up, aim).normalized * (width * 0.5f);
            if (placement == Placement.Cursor)
                preview.RangeCircle(position, distance);
            preview.Path(center - side, center + side, thickness);
        }

        protected override void DescribeShape(List<SkillStat> stats)
        {
            stats.Add(placement == Placement.AroundSelf
                ? new SkillStat("범위", $"{radius:0.#}m (따라다님)")
                : new SkillStat("벽 폭", $"{width:0.#}m"));
            stats.Add(new SkillStat("유지", $"{lifetime:0.#}초"));
            if (blockProjectiles)
                stats.Add(new SkillStat("투사체", "막음"));
            if (blockMovement)
                stats.Add(new SkillStat("적 이동", "막음"));
        }

        private sealed class Wall : ISkillTimedEffect
        {
            private readonly BarrierSkill _skill;
            private readonly ElementSkillCast _cast;
            private readonly Quaternion _facing;
            private readonly FxHandle _fx;
            private readonly GameObject _obstacle;
            private Vector3 _center;
            private float _left;

            public Wall(BarrierSkill skill, ElementSkillCast cast, Vector3 center, Vector3 aim)
            {
                _skill = skill;
                _cast = cast;
                _center = center;
                _facing = Quaternion.LookRotation(aim);
                _left = skill.lifetime;
                // 벽 프리팹은 폭 5m, 둥근 장막은 반지름 2m 기준
                _fx = cast.Hold(skill.barrierFx, center, aim,
                    skill.placement == Placement.AroundSelf ? skill.radius / ElementSkillCast.AuthoredRadius : skill.width / 5f);
                cast.PlayImpact(center, Round ? skill.radius : skill.width * 0.35f);

                if (skill.blockMovement && !Round)
                {
                    _obstacle = new GameObject("SkillBarrier");
                    _obstacle.transform.SetPositionAndRotation(center, _facing);
                    var nav = _obstacle.AddComponent<NavMeshObstacle>();
                    nav.shape = NavMeshObstacleShape.Box;
                    nav.size = new Vector3(skill.width, 3f, skill.thickness);
                    nav.center = Vector3.up * 1.5f;
                    nav.carving = true;
                    nav.carveOnlyStationary = false;
                }
            }

            private bool Round => _skill.placement == Placement.AroundSelf;

            public bool Tick(float deltaTime)
            {
                _left -= deltaTime;
                if (Round)
                {
                    _center = _cast.Ground(_cast.Position);
                    _fx?.MoveTo(_center);
                    AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.Skill, _center, _skill.radius, 0.02f));
                }
                else
                {
                    AttackAreaBus.Raise(AttackArea.Box(AttackAreaKind.Skill, _center + Vector3.up * 1.5f, _facing,
                        new Vector3(_skill.width, 3f, _skill.thickness), 0.02f));
                }

                if (_skill.blockProjectiles)
                    BlockProjectiles();

                if (_left > 0f)
                    return true;

                _fx?.Kill();
                if (_obstacle != null)
                    Object.Destroy(_obstacle);
                Fx.Shockwave(_center, _cast.Color, Round ? _skill.radius : _skill.width * 0.4f);
                return false;
            }

            private void BlockProjectiles()
            {
                IReadOnlyList<Projectile> active = Projectile.Active;
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    Projectile projectile = active[i];
                    if (projectile == null || IsMine(projectile) || !Inside(projectile.transform.position))
                        continue;

                    Vector3 point = projectile.transform.position;
                    projectile.Block();
                    _cast.Play(_skill.blockFx, point, -projectile.transform.forward);
                    Fx.Hit(point, -projectile.transform.forward, _cast.Color, false);
                }
            }

            private bool IsMine(Projectile projectile) =>
                projectile.Shooter is Component shooter && shooter.transform == _cast.Context.Transform;

            private bool Inside(Vector3 point)
            {
                Vector3 offset = point - _center;
                if (offset.y < -0.5f || offset.y > 3.5f)
                    return false;
                offset.y = 0f;

                if (Round)
                    return offset.magnitude <= _skill.radius;

                Vector3 local = Quaternion.Inverse(_facing) * offset;
                return Mathf.Abs(local.x) <= _skill.width * 0.5f && Mathf.Abs(local.z) <= _skill.thickness * 0.5f + 0.4f;
            }
        }
    }
}
