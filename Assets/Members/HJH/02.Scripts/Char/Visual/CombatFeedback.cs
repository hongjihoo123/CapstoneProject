using Assets.Members.HJH._02.Scripts.Char.Character;
using Members.KYR._01_Scripts;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Player-side presentation layer. Listens to what the player does and draws it:
    // every damage dealt (numbers, sparks, knockback, kill burst, shake), weapon shots/swings,
    // dash trails and teleport streaks. Gameplay code never needs to know this exists.
    public class CombatFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerAgent player;
        [SerializeField] private Color themeColor = new(1f, 0.7f, 0.3f);
        private Color _hitColor = new(1f, 0.7f, 0.3f);   // 맞았을 때 색 (캐릭터별, 없으면 테마색)

        [Header("Damage numbers")]
        [SerializeField] private float bigHitThreshold = 30f;
        [SerializeField] private Color normalNumber = Color.white;
        [SerializeField] private Color bigNumber = new(1f, 0.85f, 0.2f);
        [SerializeField] private Color killNumber = new(1f, 0.35f, 0.25f);

        [Header("Feel")]
        [SerializeField] private float shotShake = 0.025f;
        [SerializeField] private float killHitStop = 0.05f;
        [SerializeField] private float killShake = 0.3f;
        [SerializeField, Tooltip("근접 평타가 맞았을 때 멈추는 시간 (마지막 타는 finisherHitStop)")]
        private float swingHitStop = 0.04f;
        [SerializeField] private float finisherHitStop = 0.08f;
        [SerializeField, Tooltip("A jump longer than this in one frame (blink, scripted flight) draws a streak.")]
        private float teleportStreakDistance = 1.5f;

        private readonly WeaponVisualTracker _weapon = new();
        private CharacterSwitcher _switcher;
        private FxHandle _dashTrail;
        private Vector3 _lastPosition;
        private float _lastHitShakeTime;
        private bool _hitParticles = true;
        private bool _killBurst = true;

        private void Awake()
        {
            if (player == null)
                player = GetComponent<PlayerAgent>();
            _hitColor = themeColor;   // 캐릭터 교체기가 없는 씬에서도 테마색으로 시작

            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            if (player != null)
                player.DamageDealt += HandleDamageDealt;

            _weapon.ShotFired += HandleShot;
            _weapon.ProjectileLaunched += HandleProjectileLaunched;
            _weapon.Swung += HandleSwing;
            _weapon.SwingHit += HandleSwingHit;

            _switcher = FindFirstObjectByType<CharacterSwitcher>();
            if (_switcher != null)
            {
                _switcher.CharacterChanged += HandleCharacterChanged;
                if (_switcher.Current != null)
                    HandleCharacterChanged(_switcher.Current);
            }
        }

        private void OnDisable()
        {
            if (player != null)
                player.DamageDealt -= HandleDamageDealt;

            if (_switcher != null)
                _switcher.CharacterChanged -= HandleCharacterChanged;

            _weapon.ShotFired -= HandleShot;
            _weapon.ProjectileLaunched -= HandleProjectileLaunched;
            _weapon.Swung -= HandleSwing;
            _weapon.SwingHit -= HandleSwingHit;
            _weapon.Clear();
            _dashTrail?.Kill();
            _dashTrail = null;
        }

        private void Update()
        {
            _weapon.Track(player);
            UpdateMovementTrails();
        }

        private void HandleDamageDealt(DamageDealtInfo info)
        {
            if (info.Target is not Component component)
                return;

            Transform target = component.transform;
            Vector3 body = target.position + Vector3.up * 1.1f;
            Vector3 direction = target.position - transform.position;
            direction.y = 0f;

            // Weapons that know the exact impact (bullets) mark it; otherwise use the target's body.
            Vector3 impact = body;
            if (HitPoint.TryTake(info.Target, out Vector3 markedPoint, out Vector3 markedDirection))
            {
                impact = markedPoint;
                direction = markedDirection;
            }

            bool big = info.Amount >= bigHitThreshold || info.IsWeakpoint;
            Color numberColor = info.Killed ? killNumber : big ? bigNumber : normalNumber;
            Fx.DamageNumber(body + Vector3.up * 0.7f, info.Amount, numberColor, big || info.Killed ? 1.35f : 1f);

            if (_hitParticles)
                Fx.Hit(impact, direction, _hitColor, big);
            Fx.Punch(target, direction, Mathf.Clamp(info.Amount / 80f, 0.08f, 0.45f));

            if (Time.unscaledTime - _lastHitShakeTime > 0.04f)
            {
                _lastHitShakeTime = Time.unscaledTime;
                HitFeel.Shake(Mathf.Clamp(info.Amount / 220f, 0.03f, 0.2f), 0.12f);
            }

            if (info.Killed)
            {
                if (_killBurst)
                    Fx.Explosion(target.position, 1.6f, _hitColor, 1.3f);
                HitFeel.Play(killHitStop, killShake);
            }
        }

        private void HandleShot(ShotVisual shot)
        {
            Vector3 direction = shot.End - shot.Start;
            Fx.Muzzle(shot.Start, direction, shot.Color, shot.Empowered);

            // Travelling shots get their orb from HandleProjectileLaunched; instant shots get a tracer.
            if (!shot.Projectile)
            {
                Fx.Tracer(shot.Start, shot.End, shot.Color, shot.Empowered ? 0.24f : 0.09f, shot.Empowered ? 0.12f : 0.06f);
                if (!shot.Hit)
                    Fx.ProjectileImpact(shot.End, direction, shot.Color, 0.4f, false);
            }

            if (shot.Empowered)
            {
                Fx.Shockwave(shot.Start - Vector3.up * 0.9f, shot.Color, 0.9f);
                HitFeel.Shake(0.08f, 0.1f);
            }
            else
            {
                HitFeel.Shake(shotShake, 0.06f);
            }
        }

        private static void HandleProjectileLaunched(ShotProjectile projectile) =>
            FxRunner.Instance.Add(new ProjectileFollowFx(projectile));

        // Orb prefab that rides a weapon projectile, then an impact where it lands.
        private sealed class ProjectileFollowFx : FxInstance
        {
            private readonly ShotProjectile _projectile;
            private readonly FxHandle _orb;

            public ProjectileFollowFx(ShotProjectile projectile)
            {
                _projectile = projectile;
                _orb = Fx.Orb(projectile.Position, projectile.Color, projectile.Size);
            }

            public override bool Tick(float deltaTime)
            {
                _orb.MoveTo(_projectile.Position);
                if (_projectile.Alive)
                    return true;

                _orb.Kill();
                Fx.ProjectileImpact(_projectile.Position, _projectile.Direction, _projectile.Color, _projectile.Size, _projectile.Hit);
                return false;
            }
        }

        private static void HandleSwing(SwingVisual swing)
        {
            SwingShape shape = swing.Shape ?? new SwingShape();
            Fx.Swipe(swing.Origin, swing.Reach, shape, swing.Color, swing.Delay, swing.Duration);
        }

        // 허공에 휘두르면 아무것도 없고, 맞았을 때만 멈칫 + 흔들림. 마지막 타는 더 세게
        private void HandleSwingHit(SwingHitVisual hit)
        {
            bool finisher = hit.Step == hit.StepCount - 1;
            float shake = hit.ShakeForce * (finisher ? 0.7f : 0.4f);
            HitFeel.Play(finisher ? finisherHitStop : swingHitStop, shake);
        }

        private void UpdateMovementTrails()
        {
            _dashTrail ??= Fx.DashTrail(transform, themeColor);
            bool dashing = player != null && player.Mover != null && player.Mover.IsDashing;
            _dashTrail.SetValue(dashing ? 1f : 0f);

            Vector3 position = transform.position;
            Vector3 jump = position - _lastPosition;
            if (jump.sqrMagnitude >= teleportStreakDistance * teleportStreakDistance)
            {
                Vector3 up = Vector3.up * 0.9f;
                Fx.TeleportStreak(_lastPosition + up, position + up, themeColor);
            }

            _lastPosition = position;
        }

        private void HandleCharacterChanged(CharacterData character)
        {
            themeColor = character.ThemeColor;
            _hitColor = character.HitColor;
            _hitParticles = character.HitParticles;
            _killBurst = character.KillBurst;
            _dashTrail?.SetTint(themeColor);
            Fx.CharacterSwap(transform.position, themeColor);
        }
    }
}
