using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.RuneMage
{
    // Character 3 Q: launches every stored rune at the cursor point; each one explodes where it
    // lands (or on the first enemy it meets). With no runes it still throws a minimum volley.
    [CreateAssetMenu(menuName = "Skill/Rune Mage/Rune Burst")]
    public class RuneBurstSkillData : SkillData
    {
        [SerializeField] private float maxRange = 10f;
        [SerializeField] private float minRange = 2f;
        [SerializeField, Min(1)] private int minimumRunes = 1;
        [SerializeField] private float runeSpeed = 22f;
        [SerializeField] private float runeRadius = 0.35f;
        [SerializeField, Tooltip("Spread between runes, degrees.")] private float spreadAngle = 7f;
        [SerializeField, Tooltip("Delay between consecutive runes.")] private float launchInterval = 0.05f;
        [SerializeField] private float blastRadius = 2f;
        [SerializeField] private float blastDamage = 15f;
        [SerializeField] private float hitStop = 0.04f;
        [SerializeField] private float shake = 0.18f;

        [Header("Camera")]
        [SerializeField, Tooltip("Shake when the volley leaves (grows a little with rune count).")] private float castShake = 0.2f;
        [SerializeField, Tooltip("Camera pushed opposite the volley (world units).")] private float cameraKick = 0.3f;
        [SerializeField, Tooltip("Floor for each rune blast's shake, so misses still thump.")] private float blastShakeMin = 0.16f;

        protected override Color DefaultFxColor => new(0.7f, 0.4f, 1f);

        public override ISkillExecution Begin(ISkillContext context)
        {
            Vector3 direction = SkillAim.Resolve(context, minRange, maxRange, out float distance);
            Vector3 origin = context.Transform.position + Vector3.up;

            int stored = context.Passive is IRuneReservoir runes ? runes.ConsumeAll() : 0;
            int count = Mathf.Max(minimumRunes, stored);
            Color color = FxColor;
            Fx.RuneCast(origin + direction * 0.6f, direction, color, count);
            context.PlayShake(castShake * (1f + 0.1f * (count - 1)), 0.2f);
            context.PlayCameraKick(-direction * cameraKick, 0.18f);
            float blastShake = Mathf.Max(shake, blastShakeMin);

            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * 0.5f) * spreadAngle;
                var settings = new SkillProjectile.Settings
                {
                    Speed = runeSpeed,
                    MaxDistance = distance,
                    Radius = runeRadius,
                    Delay = i * launchInterval,
                    Color = color,
                    VisualSize = 0.4f
                };

                context.Run(new SkillProjectile(context, origin, Quaternion.Euler(0f, angle, 0f) * direction, settings,
                    onHit: null,
                    onFinish: point => SkillBlast.Explode(context, point, blastRadius, blastDamage, color, hitStop, blastShake)));
            }

            return null;
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 direction = SkillAim.Resolve(context, minRange, maxRange, out float distance);
            Vector3 start = context.Transform.position;
            Vector3 end = start + direction * distance;

            preview.RangeCircle(start, maxRange);
            preview.DirectionLine(start, end);
            preview.Circle(end, blastRadius);
        }
    }
}
