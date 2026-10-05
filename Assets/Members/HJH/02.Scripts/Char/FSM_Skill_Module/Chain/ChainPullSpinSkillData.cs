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
        [SerializeField] private GameObject spinEffectPrefab;

        public override float CancelStartTime => riseDuration + diveDuration + CancelDelay;

        public override ISkillExecution Begin(ISkillContext context) => new Execution(this, context);

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 direction = SkillAim.Resolve(context, minRange, maxRange, out float distance);

            Vector3 start = context.Transform.position;
            Vector3 destination = start + direction * distance;

            preview.Path(start, destination, 0.6f);
            preview.Circle(destination, spinRadius);
        }

        private sealed class Execution : ISkillExecution
        {
            private readonly ChainPullSpinSkillData _data;
            private readonly ISkillContext _context;
            private readonly Vector3 _origin;
            private bool _diving;
            private bool _landed;
            private bool _spinning;
            private Vector3 _diveStart;
            private Vector3 _destination;
            private float _nextTick;

            public Execution(ChainPullSpinSkillData data, ISkillContext context)
            {
                _data = data;
                _context = context;
                _origin = context.Transform.position;
                _nextTick = data.riseDuration + data.diveDuration;
            }

            public void Tick(float elapsed)
            {
                float diveEnd = _data.riseDuration + _data.diveDuration;

                if (elapsed < _data.riseDuration)
                    Rise(elapsed / _data.riseDuration);
                else if (elapsed < diveEnd)
                    Dive((elapsed - _data.riseDuration) / _data.diveDuration);
                else if (!_landed)
                    Land();

                UpdateSpin(elapsed, diveEnd);
            }

            public void OnAnimationEvent(SkillAnimationEvent animationEvent) { }

            public void End()
            {
                StopSpin();
                _context.CancelDash();
            }

            private void Rise(float t)
            {
                float eased = 1f - (1f - t) * (1f - t);
                MoveTo(new Vector3(_origin.x, _origin.y + _data.riseHeight * eased, _origin.z));
            }

            private void Dive(float t)
            {
                if (!_diving)
                {
                    _diving = true;
                    _diveStart = _context.Transform.position;

                    Vector3 direction = SkillAim.Resolve(_context, _data.minRange, _data.maxRange, out float distance);
                    _destination = new Vector3(_origin.x, _origin.y, _origin.z) + direction * distance;
                }

                MoveTo(Vector3.Lerp(_diveStart, _destination, t * t));
            }

            private void Land()
            {
                _landed = true;

                if (!_diving)
                    return;

                MoveTo(_destination);
                _context.CancelDash();
            }

            private void MoveTo(Vector3 target)
            {
                Vector3 delta = target - _context.Transform.position;
                _context.SetScriptedMotion(delta / Mathf.Max(Time.deltaTime, 0.0001f));
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
                Vector3 center = _context.Transform.position + Vector3.up;
                foreach (var target in _context.OverlapSphere(center, _data.spinRadius))
                    _context.DealDamage(target, _data.spinDamagePerTick);
            }
        }
    }
}
