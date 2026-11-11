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

        // SoD 기준 (StudyNotes/SoD_타격감_분석.md): 일반 = 작은 주황, 크리(큰 타격·약점·처치) = 큰 빨강 + 흰 외곽선
        [Header("Damage numbers")]
        [SerializeField] private float bigHitThreshold = 30f;
        [SerializeField] private Color normalNumberColor = new(1f, 0.62f, 0.22f);
        [SerializeField] private Color critNumberColor = new(1f, 0.22f, 0.16f);
        [SerializeField, Tooltip("맞은 번쩍임 다음 프레임에 숫자가 뜨게 (SoD: 번쩍 → 숫자 순서)")]
        private float numberDelay = 0.033f;

        [Header("Hit flash / knockback")]
        [SerializeField, Tooltip("적 몸 번쩍 시간: 일반 / 크리 (1~2프레임)")]
        private Vector2 enemyFlashTime = new(0.05f, 0.07f);
        [SerializeField, Range(0f, 1f), Tooltip("원소 스킬로 맞으면 번쩍임에 원소 색을 얼마나 섞을지")]
        private float elementFlashTint = 0.65f;
        [SerializeField, Range(0f, 1f), Tooltip("적이 실제로 밀리는 거리 배율. SoD 는 이동보다 '움찔' 자세 위주")]
        private float knockbackMove = 0.4f;

        [Header("Feel")]
        [SerializeField] private float shotShake = 0.025f;
        [SerializeField] private float killHitStop = 0.05f;
        [SerializeField] private float killShake = 0.3f;
        [SerializeField, Tooltip("근접 평타가 맞았을 때 멈추는 시간 (마지막 타는 finisherFreeze)")]
        private float swingHitStop = 0.04f;
        [SerializeField, Tooltip("마지막 타 히트스톱. SoD 는 3프레임(0.066초) 이하")]
        private float finisherFreeze = 0.06f;
        [SerializeField, Tooltip("평타가 맞았을 때 카메라가 때린 방향으로 툭 밀리는 거리 (마지막 타는 2.2배)")]
        private float swingKick = 0.07f;
        [SerializeField, Tooltip("마지막 타가 맞았을 때 카메라가 다가가는 정도 (0.04 = 4%)")]
        private float finisherZoom = 0.04f;
        [SerializeField, Tooltip("A jump longer than this in one frame (blink, scripted flight) draws a streak.")]
        private float teleportStreakDistance = 1.5f;

        [Header("패시브 적중 (충전 관통탄·룬 공명 등)")]
        [SerializeField, Tooltip("패시브가 맞혔을 때 흔들림 세기 (최소값. 큰 피해면 더 셈). 일반 총알 적중은 0.03~0.2")]
        private float passiveHitShake = 0.4f;
        [SerializeField, Tooltip("흔들림 시간 (실제 초)")] private float passiveHitShakeTime = 0.28f;
        [SerializeField, Tooltip("이 시간(실제 초) 안에 또 맞히면 세게 흔들지 않음 (룬 공명처럼 계속 닿는 패시브가 화면을 계속 흔들지 않게)")]
        private float passiveHitInterval = 0.3f;
        [SerializeField, Tooltip("맞은 방향으로 카메라가 툭 밀리는 거리")] private float passiveHitKick = 0.25f;

        [Header("강한 처치 (SoD식: 줌인 + 슬로모션 + 소리 펀치 → 부드럽게 복귀)")]
        [SerializeField] private StrongKillSettings strongKill = new();

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
            bool skillHit = HitTint.TryGet(info.Target, out Color elementColor);
            if (skillHit)
                sparkColor = elementColor;

            bool crit = info.Amount >= bigHitThreshold || info.IsWeakpoint || info.Killed;

            // 1) 맞은 적이 1~2프레임 번쩍. 원소 스킬이면 원소 색 위주 (SoD: 불꽃탄 → 노랑/주황 전신 플래시)
            Color flashColor = Color.Lerp(Color.white, sparkColor, skillHit ? elementFlashTint : 0.35f);
            HitFlash.Flash(component, flashColor, crit ? enemyFlashTime.y : enemyFlashTime.x);

            // 2) 다음 프레임에 숫자. 크리는 고정 빨강(크리라는 게 바로 보이게), 일반은 주황에 원소 색을 조금
            Color numberColor = crit ? critNumberColor
                : skillHit ? Color.Lerp(normalNumberColor, elementColor, 0.35f) : normalNumberColor;
            Vector3 numberAt = body + Vector3.up * 0.7f;
            float amount = info.Amount;
            DamageNumberStyle style = crit ? DamageNumberStyle.Crit : DamageNumberStyle.Normal;
            float numberScale = crit ? 1.3f : 0.9f;
            if (numberDelay > 0f)
                Fx.After(numberDelay, () => Fx.DamageNumber(numberAt, amount, numberColor, numberScale, style));
            else
                Fx.DamageNumber(numberAt, amount, numberColor, numberScale, style);

            bool big = crit;
            // 원소 스킬 피격은 스킬 에셋의 PBR 피격 이펙트가 맡음 → 납작한 불꽃은 일반 공격에만
            if (_hitParticles && !skillHit)
                Fx.Hit(impact, direction, sparkColor, big);
            // 움찔(찌그러짐)은 그대로, 실제로 밀리는 거리만 줄임
            Fx.Punch(target, direction, Mathf.Clamp(info.Amount / 80f, 0.08f, 0.45f), knockbackMove);

            // 맞은 지점 섬광 (빛줄기 + 플레어). 큰 타격·원소 스킬일 때만 → 연사 무기로 도배되지 않게
            if (big || skillHit || info.Killed)
                SpawnFlare(impact, direction, sparkColor, big || info.Killed ? 1.3f : 0.9f);


            HitSourceKind source = HitSource.Current;
            float hitShake = Mathf.Clamp(info.Amount / 220f, 0.03f, 0.2f);
            if (source == HitSourceKind.Passive && Time.unscaledTime - _lastPassiveShakeTime >= passiveHitInterval)
            {
                _lastPassiveShakeTime = Time.unscaledTime;
                // 패시브는 자주 안 나가는 한 방이라 확실히 세게 (센 쪽이 이기니 여러 명 맞혀도 쌓이진 않음)
                HitFeel.Shake(Mathf.Max(passiveHitShake, hitShake * 1.5f), passiveHitShakeTime);
                if (direction.sqrMagnitude > 0.0001f)
                    HitFeel.Kick(direction.normalized * passiveHitKick, 0.2f);
            }
            else if (Time.unscaledTime - _lastHitShakeTime > 0.04f)
            {
                _lastHitShakeTime = Time.unscaledTime;
                HitFeel.Shake(hitShake, 0.12f);
            }

            bool strong = info.Killed && IsStrongKill(info, source);

            SfxBank sounds = Sfx.Bank;
            if (sounds != null)
            {
                Sfx.Play(info.IsWeakpoint ? sounds.weakpointHit : sounds.hit, impact, big ? 1f : 0.8f);
                if (info.Killed && !strong)
                    Sfx.Play(sounds.kill, impact);
            }

            if (info.Killed)
            {
                if (_killBurst)
                    Fx.Explosion(target.position, strong ? 2.2f : 1.6f, sparkColor, strong ? 1.6f : 1.3f);

                if (strong)
                    PlayStrongKill(target.position, impact, sparkColor);
                else
                    HitFeel.Play(killHitStop, killShake);
            }
        }

        // ------------------------------------------------ 강한 처치

        [System.Serializable]
        private class StrongKillSettings
        {
            public bool enabled = true;

            [Header("발동 조건 (하나라도 맞으면)")]
            [Tooltip("이 피해 이상으로 처치하면 (마지막 한 방 기준)")] public float minDamage = 40f;
            [Tooltip("스킬로 처치하면 피해와 상관없이")] public bool skillKills = true;
            [Tooltip("패시브로 처치하면 피해와 상관없이. 룬 공명처럼 계속 닿는 패시브가 있어 기본은 끔 (충전 관통탄은 피해 조건으로 걸림)")]
            public bool passiveKills;
            [Tooltip("약점으로 처치하면")] public bool weakpointKills;
            [Tooltip("한 번 나온 뒤 이 시간(실제 초) 동안은 다시 안 나옴. 광역 스킬로 여럿 잡아도 한 번만")]
            public float cooldown = 0.8f;

            [Header("멈칫 + 흔들림")]
            [Tooltip("처치 순간 멈추는 시간 (실제 초)")] public float hitStop = 0.07f;
            public float shake = 0.4f;

            [Header("줌인")]
            [Tooltip("카메라가 다가가는 비율 (0.15 = 거리 15% 가까이)")] public float zoom = 0.15f;
            [Tooltip("다가간 채로 유지하는 시간 (실제 초)")] public float zoomHold = 0.25f;
            [Tooltip("다가갔다 돌아올 때까지 전체 시간 (실제 초)")] public float zoomTime = 0.8f;

            [Header("포스트 프로세싱 (줌인과 같이)")]
            public KillPostFx postFx = new();
            [Tooltip("처치 순간 2~3프레임 흑백 고대비 번쩍 (임팩트 프레임)")] public bool impactFrame = true;
            [Tooltip("임팩트 프레임 시간 (실제 초)")] public float impactFrameTime = 0.05f;

            [Header("슬로모션")]
            [Range(0.05f, 1f), Tooltip("느려지는 배속 (0.2 = 5배 느리게)")] public float slowScale = 0.2f;
            [Tooltip("슬로모션 전체 시간 (실제 초). 끝나면 원래 속도로 정확히 복귀")] public float slowTime = 0.6f;
            [Range(0.05f, 1f), Tooltip("전체 시간 중 마지막 이 비율 동안 부드럽게 원래 속도로")] public float slowRecover = 0.5f;

            [Header("소리 펀치")]
            [Tooltip("효과음 전체 볼륨 배율 (2 = 두 배)")] public float volumeGain = 2f;
            [Tooltip("크게 유지하는 시간 (실제 초)")] public float volumeHold = 0.12f;
            [Tooltip("원래 볼륨으로 돌아오는 시간 (실제 초)")] public float volumeRelease = 0.5f;
            [Tooltip("처치 소리 크기 배율")] public float killVolume = 1.6f;
            [Tooltip("처치 소리 피치 (낮을수록 묵직)")] public float killPitch = 0.85f;
            [Tooltip("묵직한 충격음(heavyImpact)을 한 겹 더")] public bool layerHeavyImpact = true;
        }

        private float _lastStrongKillTime = -999f;
        private float _lastPassiveShakeTime = -999f;

        private bool IsStrongKill(DamageDealtInfo info, HitSourceKind source)
        {
            StrongKillSettings s = strongKill;
            if (s == null || !s.enabled || Time.unscaledTime - _lastStrongKillTime < s.cooldown)
                return false;

            return info.Amount >= s.minDamage
                   || (s.skillKills && source == HitSourceKind.Skill)
                   || (s.passiveKills && source == HitSourceKind.Passive)
                   || (s.weakpointKills && info.IsWeakpoint);
        }

        // 멈칫 → 슬로모션 + 줌인(유지) → 부드럽게 원래대로. 소리는 확 커졌다 돌아옴.
        // 시간·줌·볼륨 모두 HitFeel / Sfx 가 실제 시간 기준으로 되돌림 (timeScale 은 시작 전 값으로 복구)
        private void PlayStrongKill(Vector3 position, Vector3 impact, Color color)
        {
            StrongKillSettings s = strongKill;
            _lastStrongKillTime = Time.unscaledTime;

            HitFeel.Play(s.hitStop, s.shake);
            HitFeel.SlowMo(s.slowScale, s.slowTime, s.slowRecover);
            HitFeel.Zoom(s.zoom, s.zoomTime, s.zoomHold);
            ScreenPulse.KillPunch(s.postFx);
            if (s.impactFrame)
                ScreenPulse.ImpactFrame(s.impactFrameTime);
            Fx.Shockwave(position + Vector3.up * 0.1f, color, 1.6f);

            Sfx.Punch(s.volumeGain, s.volumeHold, s.volumeRelease);
            SfxBank sounds = Sfx.Bank;
            if (sounds == null)
                return;
            Sfx.Play(sounds.kill, impact, s.killVolume, s.killPitch);
            if (s.layerHeavyImpact)
                Sfx.Play(sounds.heavyImpact, impact, 1f, 0.8f);
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
            GunnerFx.Shot(shot.Start, direction, shot.Color, shot.Empowered);   // 총구 섬광 + 하얀 심 + 순간 조명 (강화탄은 파동·화면 펄스)
            if (Sfx.Bank != null)
                Sfx.Play(shot.Empowered ? Sfx.Bank.empoweredShot : Sfx.Bank.shot, shot.Start);

            // Travelling shots get their orb from HandleProjectileLaunched; instant shots get a tracer.
            if (!shot.Projectile)
            {
                Fx.Tracer(shot.Start, shot.End, shot.Color, shot.Empowered ? 0.24f : 0.11f, shot.Empowered ? 0.12f : 0.07f);
                Fx.Tracer(shot.Start, shot.End, GunnerFx.Hot(shot.Color, 0.8f), shot.Empowered ? 0.1f : 0.04f, shot.Empowered ? 0.08f : 0.045f);   // 하얀 심
                if (!shot.Hit)
                {
                    Fx.ProjectileImpact(shot.End, direction, shot.Color, 0.4f, false);
                    // 바위·벽에 막힌 탄: 맞은 자리에서 쏜 방향 반대로 튀는 불꽃 + 짧은 조명
                    if (shot.Blocked && direction.sqrMagnitude > 0.0001f)
                    {
                        Vector3 back = -direction.normalized;
                        Fx.Hit(shot.End, back, GunnerFx.Hot(shot.Color, 0.4f), shot.Empowered);
                        GunnerFx.Flash(shot.End + back * 0.2f, shot.Color, shot.Empowered ? 3.5f : 1.6f, 2.2f, 0.06f);
                    }
                }
            }

            if (shot.Empowered)
            {
                // 파동은 GunnerFx.Shot 이 그림
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
            HitFeel.Play(finisher ? finisherFreeze : swingHitStop, shake);

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
