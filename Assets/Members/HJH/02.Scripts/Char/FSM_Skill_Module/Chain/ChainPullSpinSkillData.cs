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

        public override ISkillExecution Begin(ISkillContext context) => new Execution(this, context);

        private sealed class Execution : ISkillExecution
        {
            private readonly ChainPullSpinSkillData _data;
            private readonly ISkillContext _context;
            private readonly Vector3 _direction;
            private bool _pulled;
            private float _nextTick;

            public Execution(ChainPullSpinSkillData data, ISkillContext context)
            {
                _data = data;
                _context = context;

                Vector3 forward = context.Transform.forward;
                forward.y = 0f;
                _direction = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
                _nextTick = data.throwDuration + data.pullDuration;
            }

            public void Tick(float elapsed)
            {
                if (!_pulled && elapsed >= _data.throwDuration)
                {
                    _pulled = true;
                    _context.Dash(_direction, _data.throwDistance / _data.pullDuration, _data.pullDuration);
                }

                float spinEnd = _data.throwDuration + _data.pullDuration + _data.spinDuration;
                while (elapsed >= _nextTick && _nextTick <= spinEnd)
                {
                    SpinTick();
                    _nextTick += _data.spinTickInterval;
                }
            }

            public void End() { }

            private void SpinTick()
            {
                Vector3 center = _context.Transform.position + Vector3.up;
                foreach (var target in _context.OverlapSphere(center, _data.spinRadius))
                    _context.DealDamage(target, _data.spinDamagePerTick);
            }
        }
    }
}
