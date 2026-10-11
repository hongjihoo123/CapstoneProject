using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Chain
{
    [CreateAssetMenu(menuName = "Skill/Chain/Dash Strike")]
    public class DashStrikeSkillData : SkillData
    {
        [SerializeField] private float maxRange = 8f;
        [SerializeField] private float minRange = 1.5f;
        [SerializeField] private float dashDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float endSlowdown = 0.3f;
        [SerializeField, Min(1), Tooltip("Total hits. The last one always lands at the destination; the rest are spread over the dash.")]
        private int tickCount = 3;
        [SerializeField] private float strikeRadius = 3f;
        [SerializeField, FormerlySerializedAs("strikeDamage")] private float damagePerTick = 15f;
        [SerializeField] private float rangeFlashDuration = 0.2f;
        [SerializeField] private GameObject spinEffectPrefab;

        protected override Color DefaultFxColor => new(1f, 0.55f, 0.25f);

        public override ISkillExecution Begin(ISkillContext context) => new Execution(this, context);

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 direction = SkillAim.Resolve(context, minRange, maxRange, out float distance);
            Vector3 start = context.Transform.position;
            Vector3 end = start + direction * distance;

            preview.RangeCircle(start, maxRange);
            preview.DirectionLine(start, start + direction * maxRange);

            // The hit area follows the player, so the path is as wide as the strike circle.
            preview.Path(start, end, strikeRadius * 2f);
            preview.Circle(end, strikeRadius);
        }

        private sealed class Execution : ISkillExecution
        {
            private readonly DashStrikeSkillData _data;
            private readonly ISkillContext _context;
            private int _dashTicksDone;
            private bool _landed;
            private Vector3 _direction;
            private int _slashes;

            public Execution(DashStrikeSkillData data, ISkillContext context)
            {
                _data = data;
                _context = context;
                Vector3 direction = SkillAim.Resolve(context, data.minRange, data.maxRange, out float distance);
                _direction = direction;
                context.Dash(direction, distance / data.dashDuration, data.dashDuration, data.endSlowdown);
                context.SetSpinEffect(data.spinEffectPrefab, true, data.strikeRadius);
            }

            private int DashTickCount => _data.tickCount - 1;
            private float DashTickInterval => _data.dashDuration / Mathf.Max(1, DashTickCount);

            public void Tick(float elapsed)
            {
                if (_landed)
                    return;

                // Hits while travelling: 0s, interval, 2*interval ... (catch up so a long frame never skips one).
                while (_dashTicksDone < DashTickCount && elapsed >= _dashTicksDone * DashTickInterval && _context.IsDashing)
                {
                    DamageTick();
                    _dashTicksDone++;
                }

                // The final hit is tied to the dash actually stopping, so it always lands on the destination
                // even when stats stretch the dash.
                if (!_context.IsDashing)
                    Land();
            }

            public void OnAnimationEvent(SkillAnimationEvent animationEvent) { }

            public void End() => Land();

            private void Land()
            {
                if (_landed)
                    return;

                _landed = true;
                DamageTick();

                // Landing: two blades crossing in an X.
                Vector3 landing = _context.Transform.position;
                Vector3 chest = landing + Vector3.up * 0.9f;
                Fx.Slash(chest, _direction, _data.strikeRadius * 1.2f, false, 25f, 2.2f);
                Fx.Slash(chest, _direction, _data.strikeRadius * 1.2f, true, -25f, 2.2f);
                _context.PlayHitFeel(0.04f, 0.25f);
                _context.SetSpinEffect(_data.spinEffectPrefab, false, _data.strikeRadius);
            }

            // Centered on the player's position at this tick, so the hit area travels with the dash.
            // Every enemy inside is hit once per tick (no once-per-cast dedupe).
            private void DamageTick()
            {
                _context.FlashRange(_data.strikeRadius, _data.rangeFlashDuration);
                // A quick blade along the dash, alternating sides each tick.
                Fx.Slash(_context.Transform.position + Vector3.up * 0.9f, _direction, _data.strikeRadius, _slashes++ % 2 == 1, 0f, 2.6f);

                Vector3 center = _context.Transform.position + Vector3.up;
                foreach (var target in _context.OverlapSphere(center, _data.strikeRadius))
                    _context.DealDamage(target, _data.damagePerTick);
            }
        }
    }
}
