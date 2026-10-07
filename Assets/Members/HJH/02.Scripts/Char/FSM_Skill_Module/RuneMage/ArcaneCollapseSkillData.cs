using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.RuneMage
{
    // Character 3 R: draws a magic circle at the cursor, blinks into it, then pulls enemies to the
    // center before each of several explosions (the last one hits harder).
    [CreateAssetMenu(menuName = "Skill/Rune Mage/Arcane Collapse")]
    public class ArcaneCollapseSkillData : SkillData
    {
        [SerializeField] private float maxRange = 9f;
        [SerializeField] private float radius = 5f;
        [SerializeField] private float blinkDelay = 0.25f;
        [SerializeField] private float firstBlastTime = 0.45f;
        [SerializeField] private float blastInterval = 0.5f;
        [SerializeField, Min(1)] private int blastCount = 3;
        [SerializeField] private float pullDistance = 2.5f;
        [SerializeField] private float blastDamage = 30f;
        [SerializeField] private float finalBlastMultiplier = 1.5f;
        [SerializeField] private float hitStop = 0.05f;
        [SerializeField] private float shake = 0.3f;

        protected override Color DefaultFxColor => new(0.65f, 0.3f, 1f);

        public override ISkillExecution Begin(ISkillContext context) => new Execution(this, context);

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 direction = SkillAim.Resolve(context, 0f, maxRange, out float distance);
            Vector3 start = context.Transform.position;
            Vector3 end = start + direction * distance;

            preview.RangeCircle(start, maxRange);
            preview.DirectionLine(start, end);
            preview.Circle(end, radius);
        }

        private sealed class Execution : ISkillExecution
        {
            private readonly ArcaneCollapseSkillData _data;
            private readonly ISkillContext _context;
            private readonly Vector3 _center;
            private readonly FxHandle _circle;
            private readonly FxHandle _pull;
            private bool _blinked;
            private int _blastsDone;

            public Execution(ArcaneCollapseSkillData data, ISkillContext context)
            {
                _data = data;
                _context = context;

                Vector3 direction = SkillAim.Resolve(context, 0f, data.maxRange, out float distance);
                Vector3 flat = context.Transform.position + direction * distance;
                _center = context.TryFindGround(flat + Vector3.up * 3f, out Vector3 ground) ? ground : flat;

                _circle = Fx.MagicCircle(_center, data.radius, data.FxColor);
                _circle.SetSpeed(60f);
                _pull = Fx.Pull(_center, data.radius, data.FxColor);
            }

            private float LastBlastTime => _data.firstBlastTime + (_data.blastCount - 1) * _data.blastInterval;

            public void Tick(float elapsed)
            {
                // The circle spins faster as the collapse builds up.
                float fill = Mathf.Clamp01(elapsed / LastBlastTime);
                _circle.SetValue(fill);
                _circle.SetSpeed(Mathf.Lerp(60f, 420f, fill));

                if (!_blinked)
                {
                    AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.WeaponWindup, _center, _data.radius, 0.02f));
                    if (elapsed < _data.blinkDelay)
                        return;

                    _blinked = true;
                    _context.SetScriptedPosition(_center);
                    _context.CancelDash();
                    Fx.BlinkIn(_center, Vector3.zero, _data.FxColor);
                }

                while (_blastsDone < _data.blastCount && elapsed >= _data.firstBlastTime + _blastsDone * _data.blastInterval)
                    Blast();
            }

            public void OnAnimationEvent(SkillAnimationEvent animationEvent) { }

            public void End()
            {
                _circle.Kill();
                _pull.Kill();
                _context.CancelDash();
            }

            private void Blast()
            {
                _blastsDone++;
                bool last = _blastsDone == _data.blastCount;
                float strength = last ? _data.finalBlastMultiplier : 1f;

                _context.PullToward(_center, _data.radius, _data.pullDistance);
                Fx.PullBurst(_center, _data.radius, _data.FxColor);
                SkillBlast.Explode(_context, _center, _data.radius * (last ? 1f : 0.75f), _data.blastDamage * strength,
                    _data.FxColor, _data.hitStop * strength, _data.shake * strength, last ? 2.5f : 1.2f);

                if (last)
                {
                    _circle.Kill();
                    _pull.Kill();
                }
            }
        }
    }
}
