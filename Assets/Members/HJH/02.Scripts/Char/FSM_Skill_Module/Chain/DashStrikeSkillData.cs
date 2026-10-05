using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Chain
{
    [CreateAssetMenu(menuName = "Skill/Chain/Dash Strike")]
    public class DashStrikeSkillData : SkillData
    {
        [SerializeField] private float dashDistance = 8f;
        [SerializeField] private float dashDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float endSlowdown = 0.3f;
        [SerializeField] private float strikeTime = 0.2f;
        [SerializeField] private float strikeDuration = 0.25f;
        [SerializeField] private float strikeRadius = 3f;
        [SerializeField] private float strikeDamage = 45f;
        [SerializeField] private float rangeFlashDuration = 0.2f;
        [SerializeField] private bool useAnimationEvents;
        [SerializeField] private GameObject spinEffectPrefab;

        public override ISkillExecution Begin(ISkillContext context) => new Execution(this, context);

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 direction = context.AimDirection;
            Vector3 start = context.Transform.position;
            float strikeMiddle = (strikeTime + strikeDuration * 0.5f) / dashDuration;

            preview.Path(start, start + direction * dashDistance, 1.2f);
            preview.Circle(start + direction * dashDistance * Mathf.Clamp01(strikeMiddle), strikeRadius);
        }

        private sealed class Execution : ISkillExecution
        {
            private readonly DashStrikeSkillData _data;
            private readonly ISkillContext _context;
            private bool _hitting;

            public Execution(DashStrikeSkillData data, ISkillContext context)
            {
                _data = data;
                _context = context;
                context.Dash(context.AimDirection, data.dashDistance / data.dashDuration, data.dashDuration, data.endSlowdown);
            }

            public void Tick(float elapsed)
            {
                if (!_data.useAnimationEvents)
                {
                    bool inWindow = elapsed >= _data.strikeTime && elapsed <= _data.strikeTime + _data.strikeDuration;
                    if (inWindow && !_hitting)
                        BeginHit();
                    else if (!inWindow && _hitting)
                        EndHit();
                }

                if (_hitting)
                    DamageTick();
            }

            public void OnAnimationEvent(SkillAnimationEvent animationEvent)
            {
                if (!_data.useAnimationEvents)
                    return;

                if (animationEvent == SkillAnimationEvent.HitBegin)
                    BeginHit();
                else
                    EndHit();
            }

            public void End() => EndHit();

            private void BeginHit()
            {
                if (_hitting)
                    return;

                _hitting = true;
                _context.FlashRange(_data.strikeRadius, _data.rangeFlashDuration);
                _context.SetSpinEffect(_data.spinEffectPrefab, true, _data.strikeRadius);
            }

            private void EndHit()
            {
                if (!_hitting)
                    return;

                _hitting = false;
                _context.SetSpinEffect(_data.spinEffectPrefab, false, _data.strikeRadius);
            }

            private void DamageTick()
            {
                Vector3 center = _context.Transform.position + Vector3.up;
                foreach (var target in _context.OverlapSphere(center, _data.strikeRadius))
                {
                    if (_context.TryRegisterHit(target))
                        _context.DealDamage(target, _data.strikeDamage);
                }
            }
        }
    }
}
