using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Chain
{
    [CreateAssetMenu(menuName = "Skill/Chain/Chain Pull Spin")]
    public class ChainPullSpinSkillData : SkillData
    {
        [SerializeField] private float throwDistance = 8f;
        [SerializeField] private float throwDuration = 0.4f;
        [SerializeField] private float pullDuration = 0.35f;
        [SerializeField] private float spinDuration = 2.5f;
        [SerializeField] private float spinTickInterval = 0.25f;
        [SerializeField] private float spinRadius = 3.2f;
        [SerializeField] private float spinDamagePerTick = 15f;
        [SerializeField] private GameObject spinEffectPrefab;

        public override ISkillExecution Begin(ISkillContext context) => new Execution(this, context);

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 forward = context.AimDirection;

            Vector3 start = context.Transform.position;
            Vector3 destination = start + forward * throwDistance;

            preview.Path(start, destination, 0.6f);
            preview.Circle(destination, spinRadius);
        }

        private sealed class Execution : ISkillExecution
        {
            private readonly ChainPullSpinSkillData _data;
            private readonly ISkillContext _context;
            private readonly Vector3 _direction;
            private bool _pulled;
            private bool _spinning;
            private float _nextTick;

            public Execution(ChainPullSpinSkillData data, ISkillContext context)
            {
                _data = data;
                _context = context;

                _direction = context.AimDirection;
                _nextTick = data.throwDuration + data.pullDuration;
            }

            public void Tick(float elapsed)
            {
                if (!_pulled && elapsed >= _data.throwDuration)
                {
                    _pulled = true;
                    _context.Dash(_direction, _data.throwDistance / _data.pullDuration, _data.pullDuration);
                }

                float spinStart = _data.throwDuration + _data.pullDuration;
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

            public void OnAnimationEvent(SkillAnimationEvent animationEvent) { }

            public void End()
            {
                StopSpin();
                _context.CancelDash();
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
