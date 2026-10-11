using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Chain
{
    [CreateAssetMenu(menuName = "Skill/Chain/Chain Pull Spin")]
    public class ChainPullSpinSkillData : SkillData
    {
        [SerializeField] private float maxRange = 8f;
        [SerializeField] private float minRange = 2f;
        [SerializeField] private float riseHeight = 6f;
        [SerializeField] private float riseDuration = 0.35f;
        [SerializeField] private float diveDuration = 0.3f;
        [SerializeField] private float spinDuration = 2.5f;
        [SerializeField] private float spinTickInterval = 0.25f;
        [SerializeField] private float spinRadius = 3.2f;
        [SerializeField] private float spinDamagePerTick = 15f;
        [SerializeField, Tooltip("Enemies closer than this are pushed out on landing so the player never ends up inside one.")]
        private float landingPushRadius = 1.2f;
        [SerializeField] private GameObject spinEffectPrefab;

        protected override Color DefaultFxColor => new(1f, 0.5f, 0.2f);

        public override float CancelStartTime => riseDuration + diveDuration + CancelDelay;

        public override ISkillExecution Begin(ISkillContext context) => new Execution(this, context);

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 direction = SkillAim.Resolve(context, minRange, maxRange, out float distance);

            Vector3 start = context.Transform.position;
            Vector3 destination = start + direction * distance;

            preview.RangeCircle(start, maxRange);
            preview.DirectionLine(start, start + direction * maxRange);
            preview.Path(start, destination, 0.6f);
            preview.Circle(destination, spinRadius);
        }

        // Flight: Rise -> Dive -> (Drop if ground/wall blocks the path) -> Landed -> Spin.
        // Flight moves ignore collisions, so enemies and props are passed through;
        // only the environment layers (ground/walls) are checked with a sweep.
        // Landing always happens at riseDuration + diveDuration, so cancel and spin timing stay fixed.
        private sealed class Execution : ISkillExecution
        {
            private enum Phase { Rise, Dive, Drop, Landed }

            private readonly ChainPullSpinSkillData _data;
            private readonly ISkillContext _context;
            private readonly Vector3 _origin;
            private Phase _phase = Phase.Rise;
            private Vector3 _diveStart;
            private Vector3 _destination;
            private Vector3 _dropStart;
            private Vector3 _dropEnd;
            private float _dropStartTime;
            private bool _spinning;
            private int _slashes;
            private float _nextTick;

            public Execution(ChainPullSpinSkillData data, ISkillContext context)
            {
                _data = data;
                _context = context;
                _origin = context.Transform.position;

                // Lock the landing point to the cursor at cast time; moving the mouse or camera mid-air must not change it.
                Vector3 direction = SkillAim.Resolve(context, data.minRange, data.maxRange, out float distance);
                Vector3 flat = _origin + direction * distance;
                _destination = context.TryFindGround(flat + Vector3.up * data.riseHeight, out Vector3 ground) ? ground : flat;
                _nextTick = LandTime;
            }

            private float LandTime => _data.riseDuration + _data.diveDuration;

            public void Tick(float elapsed)
            {
                if (_phase != Phase.Landed)
                {
                    if (elapsed >= LandTime)
                        Land();
                    else
                        Fly(elapsed);
                }

                UpdateSpin(elapsed, LandTime);
            }

            public void OnAnimationEvent(SkillAnimationEvent animationEvent) { }

            public void End()
            {
                StopSpin();
                _context.CancelDash();
            }

            private void Fly(float elapsed)
            {
                Vector3 current = _context.Transform.position;

                if (_phase == Phase.Drop)
                {
                    float t = Mathf.InverseLerp(_dropStartTime, LandTime, elapsed);
                    _context.SetScriptedPosition(Vector3.Lerp(_dropStart, _dropEnd, t * t));
                    return;
                }

                Vector3 target = elapsed < _data.riseDuration
                    ? RisePoint(elapsed / _data.riseDuration)
                    : DivePoint((elapsed - _data.riseDuration) / _data.diveDuration);

                // Hit ground or a wall: stop where we are and fall straight down for the rest of the flight.
                if (_context.SweepEnvironment(current, target))
                {
                    BeginDrop(current, elapsed);
                    target = current;
                }

                _context.SetScriptedPosition(target);
            }

            private Vector3 RisePoint(float t)
            {
                float eased = 1f - (1f - t) * (1f - t);
                return _origin + Vector3.up * (_data.riseHeight * eased);
            }

            private Vector3 DivePoint(float t)
            {
                if (_phase != Phase.Dive)
                {
                    _phase = Phase.Dive;
                    _diveStart = _context.Transform.position;
                }

                return Vector3.Lerp(_diveStart, _destination, t * t);
            }

            private void BeginDrop(Vector3 from, float elapsed)
            {
                _phase = Phase.Drop;
                _dropStart = from;
                _dropStartTime = elapsed;
                _context.TryFindGround(from, out _dropEnd);
            }

            private void Land()
            {
                Vector3 landing = _phase switch
                {
                    Phase.Drop => _dropEnd,
                    Phase.Dive => _destination,
                    _ => _context.TryFindGround(_context.Transform.position, out Vector3 ground) ? ground : _origin
                };

                _phase = Phase.Landed;
                _context.SetScriptedPosition(landing);
                _context.CancelDash();
                _context.PushAway(landing, _data.landingPushRadius);

                // Landing: a big blade X cutting through the landing point.
                Vector3 forward = _destination - _origin;
                Vector3 chest = landing + Vector3.up * 0.9f;
                Fx.Slash(chest, forward, _data.spinRadius * 1.3f, false, 30f, 1.8f);
                Fx.Slash(chest, forward, _data.spinRadius * 1.3f, true, -30f, 1.8f);
                _context.PlayHitFeel(0.08f, 0.5f);
            }

            private void UpdateSpin(float elapsed, float spinStart)
            {
                float spinEnd = spinStart + _data.spinDuration;

                if (!_spinning && elapsed >= spinStart && elapsed <= spinEnd)
                {
                    _spinning = true;
                    _context.FlashRange(_data.spinRadius, 0.25f);
                    _context.SetSpinEffect(_data.spinEffectPrefab, true, _data.spinRadius);
                }
                else if (_spinning && elapsed > spinEnd)
                {
                    StopSpin();
                }

                while (elapsed >= _nextTick && _nextTick <= spinEnd)
                {
                    SpinTick();
                    _nextTick += _data.spinTickInterval;
                }
            }

            private void StopSpin()
            {
                if (!_spinning)
                    return;

                _spinning = false;
                _context.SetSpinEffect(_data.spinEffectPrefab, false, _data.spinRadius);
            }

            private void SpinTick()
            {
                // A blade flurry on top of the spinning blades: each tick cuts at a new angle.
                float yaw = _slashes * 137.5f;
                Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                Fx.Slash(_context.Transform.position + Vector3.up * 0.9f, direction, _data.spinRadius, _slashes++ % 2 == 1, (_slashes % 3 - 1) * 20f, 3f);
                Vector3 center = _context.Transform.position + Vector3.up;
                foreach (var target in _context.OverlapSphere(center, _data.spinRadius))
                    _context.DealDamage(target, _data.spinDamagePerTick);
            }
        }
    }
}
