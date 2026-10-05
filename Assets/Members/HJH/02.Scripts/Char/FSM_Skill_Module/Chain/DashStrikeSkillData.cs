using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Chain
{
    [CreateAssetMenu(menuName = "Skill/Chain/Dash Strike")]
    public class DashStrikeSkillData : SkillData
    {
        [SerializeField] private float dashDistance = 5f;
        [SerializeField] private float dashDuration = 0.25f;
        [SerializeField] private float strikeRadius = 3f;
        [SerializeField] private float strikeDamage = 45f;
        [SerializeField] private float strikeDelay = 0.1f;
        [SerializeField] private float followUpDistance = 3f;
        [SerializeField] private float followUpDuration = 0.25f;

        public override ISkillExecution Begin(ISkillContext context) => new Execution(this, context);

        private static Vector3 FlatForward(ISkillContext context)
        {
            Vector3 forward = context.Transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private sealed class Execution : ISkillExecution
        {
            private readonly DashStrikeSkillData _data;
            private readonly ISkillContext _context;
            private bool _struck;
            private bool _followedUp;

            public Execution(DashStrikeSkillData data, ISkillContext context)
            {
                _data = data;
                _context = context;
                context.Dash(FlatForward(context), data.dashDistance / data.dashDuration, data.dashDuration);
            }

            public void Tick(float elapsed)
            {
                if (!_struck && elapsed >= _data.dashDuration)
                {
                    _struck = true;
                    Strike();
                }

                if (!_followedUp && elapsed >= _data.dashDuration + _data.strikeDelay)
                {
                    _followedUp = true;
                    _context.Dash(FlatForward(_context), _data.followUpDistance / _data.followUpDuration, _data.followUpDuration);
                }
            }

            public void End() { }

            private void Strike()
            {
                Vector3 center = _context.Transform.position + Vector3.up;
                foreach (var target in _context.OverlapSphere(center, _data.strikeRadius))
                    _context.DealDamage(target, _data.strikeDamage);
            }
        }
    }
}
