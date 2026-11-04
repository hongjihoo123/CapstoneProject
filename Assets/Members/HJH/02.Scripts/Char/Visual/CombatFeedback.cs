using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Audio;
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
        [SerializeField, Tooltip("평타가 맞았을 때 카메라가 때린 방향으로 툭 밀리는 거리 (마지막 타는 2.2배)")]
        private float swingKick = 0.07f;
        [SerializeField, Tooltip("마지막 타가 맞았을 때 카메라가 다가가는 정도 (0.04 = 4%)")]
        private float finisherZoom = 0.04f;
        [SerializeField, Tooltip("A jump longer than this in one frame (blink, scripted flight) draws a streak.")]
        private float teleportStreakDistance = 1.5f;

        private readonly WeaponVisualTracker _weapon = new();
        private CharacterSwitcher _switcher;
        private FxHandle _dashTrail;
        private Color _dashTrailColor;
        private Vector3 _lastPosition;
        private float _lastHitShakeTime;
        private bool _hitParticles = true;
        private bool _killBurst = true;
        private bool _wasDashing;

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

            // 원소 스킬로 맞았으면 불꽃·숫자를 그 원소 색으로 (아니면 캐릭터 색)
            Color sparkColor = _hitColor;
            Color normalColor = normalNumber;
            Color bigColor = bigNumber;
            if (HitTint.TryGet(info.Target, out Color elementColor))
            {
                sparkColor = elementColor;
                normalColor = Color.Lerp(normalNumber, elementColor, 0.35f);
                bigColor = Color.Lerp(bigNumber, elementColor, 0.6f);
            }

            bool big = info.Amount >= bigHitThreshold || info.IsWeakpoint;
            Color numberColor = info.Killed ? killNumber : big ? bigColor : normalColor;
            Fx.DamageNumber(body + Vector3.up * 0.7f, info.Amount, numberColor, big || info.Killed ? 1.35f : 1f);

            bool skillHit = sparkColor != _hitColor;
            // 원소 스킬 피격은 스킬 에셋의 PBR 피격 이펙트가 맡음 → 납작한 불꽃은 일반 공격에만
            if (_hitParticles && !skillHit)
                Fx.Hit(impact, direction, sparkColor, big);
            Fx.Punch(target, direction, Mathf.Clamp(info.Amount / 80f, 0.08f, 0.45f));

            // 맞은 적이 순간 번쩍 (하얀빛에 원소 색을 살짝)
            HitFlash.Flash(component, Color.Lerp(Color.white, sparkColor, 0.35f), big || info.Killed ? 0.16f : 0.1f);

            // 맞은 지점 섬광 (빛줄기 + 플레어). 큰 타격·원소 스킬일 때만 → 연사 무기로 도배되지 않게
            if (big || skillHit || info.Killed)
                SpawnFlare(impact, direction, sparkColor, big || info.Killed ? 1.3f : 0.9f);


            if (Time.unscaledTime - _lastHitShakeTime > 0.04f)
            {
                _lastHitShakeTime = Time.unscaledTime;
                HitFeel.Shake(Mathf.Clamp(info.Amount / 220f, 0.03f, 0.2f), 0.12f);
            }

            SfxBank sounds = Sfx.Bank;
            if (sounds != null)
            {
                Sfx.Play(info.IsWeakpoint ? sounds.weakpointHit : sounds.hit, impact, big ? 1f : 0.8f);
                if (info.Killed)
                    Sfx.Play(sounds.kill, impact);
            }

            if (info.Killed)
            {
                if (_killBurst)
                    Fx.Explosion(target.position, 1.6f, sparkColor, 1.3f);
                HitFeel.Play(killHitStop, killShake);
            }
        }

        // ------------------------------------------------ 섬광

        private const string FlarePath = "HJH/StylizedFx/FX_HitFlare";   // HJH > Element FX > 스타일 이펙트 만들기
        private static GameObject _flarePrefab;
        private static bool _flareLoaded;

        private static void SpawnFlare(Vector3 point, Vector3 direction, Color color, float scale)
        {
            if (!_flareLoaded)
            {
                _flareLoaded = true;
                _flarePrefab = Resources.Load<GameObject>(FlarePath);
            }
            if (_flarePrefab == null)
                return;

            Quaternion facing = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            Fx.Spawn(_flarePrefab, point, facing, scale, color);
        }

        private void HandleShot(ShotVisual shot)
        {
            Vector3 direction = shot.End - shot.Start;
            Fx.Muzzle(shot.Start, direction, shot.Color, shot.Empowered);
            if (Sfx.Bank != null)
                Sfx.Play(shot.Empowered ? Sfx.Bank.empoweredShot : Sfx.Bank.shot, shot.Start);

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

            // 휘두르는 소리는 칼날이 실제로 지나갈 때 (Delay 뒤). 마지막 타는 낮고 크게
            SfxBank sounds = Sfx.Bank;
            if (sounds == null || swing.Origin == null)
                return;
            bool finisher = swing.Step == swing.StepCount - 1;
            Transform origin = swing.Origin;
            Fx.After(swing.Delay, () =>
            {
                if (origin != null)
                    Sfx.Play(sounds.swing, origin.position, finisher ? 1f : 0.8f, finisher ? 0.85f : 1f + 0.05f * swing.Step);
            });
        }

        // 허공에 휘두르면 아무것도 없고, 맞았을 때만 멈칫 + 흔들림 + 때린 방향으로 카메라 툭. 마지막 타는 더 세게
        private void HandleSwingHit(SwingHitVisual hit)
        {
            bool finisher = hit.Step == hit.StepCount - 1;
            float shake = hit.ShakeForce * (finisher ? 0.7f : 0.4f);
            HitFeel.Play(finisher ? finisherHitStop : swingHitStop, shake);

            // 여러 명을 한 번에 맞히면 조금 더 세게 (최대 1.5배)
            float more = 1f + 0.1f * (Mathf.Min(hit.Targets, 6) - 1);
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                HitFeel.Kick(forward.normalized * swingKick * (finisher ? 2.2f : 1f) * more, finisher ? 0.22f : 0.14f);
            if (finisher)
                HitFeel.Zoom(finisherZoom, 0.26f);

            SfxBank sounds = Sfx.Bank;
            if (sounds != null)
                Sfx.Play(finisher ? sounds.finisherHit : sounds.swingHit, transform.position + forward, Mathf.Min(more, 1.2f));
        }

        private void UpdateMovementTrails()
        {
            _dashTrail ??= Fx.DashTrail(transform, themeColor);
            bool dashing = player != null && player.Mover != null && player.Mover.IsDashing;
            _dashTrail.SetValue(dashing ? 1f : 0f);
            if (dashing && !_wasDashing && Sfx.Bank != null)
                Sfx.Play(Sfx.Bank.dash, transform.position);
            _wasDashing = dashing;

            // 스킬 돌진이면 잔상을 원소 색으로 (아니면 캐릭터 테마색)
            Color trailColor = DashTint.TryGet(out Color skillColor) ? skillColor : themeColor;
            if (trailColor != _dashTrailColor)
            {
                _dashTrailColor = trailColor;
                _dashTrail.SetTint(trailColor);
            }

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
