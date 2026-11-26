using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Audio;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01.Scripts.UI;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public class CombatFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerAgent player;
        [SerializeField] private Color themeColor = new(1f, 0.7f, 0.3f);
        private Color _hitColor = new(1f, 0.7f, 0.3f);

        [Header("Damage numbers")]
        [SerializeField] private float bigHitThreshold = 30f;
        [Header("데미지 숫자 종류 (SoD 영상 기준)")]
        [SerializeField, Tooltip("이 값 미만이면 흰색 '약한 피해' 숫자")] private float weakNumberThreshold = 40f;
        [SerializeField, Tooltip("이 값 이상이면 크고 붉은 '크리' 숫자. 약점/처치도 크리 숫자")] private float critNumberThreshold = 120f;
        [SerializeField] private Color normalNumberColor = new(1f, 0.62f, 0.22f);
        [SerializeField] private Color critNumberColor = new(1f, 0.22f, 0.16f);
        [SerializeField, Tooltip("맞은 번쩍임 다음 프레임에 숫자가 뜨게 (SoD: 번쩍 → 숫자 순서)")]
        private float numberDelay = 0.033f;
        [SerializeField, Tooltip("데미지 숫자 시작 높이: 몸통 중심에서 얼마나 아래(m). 클수록 배/다리 쪽")] private float numberBelowBody = 0.25f;
        [SerializeField, Tooltip("숫자가 올라가다 멈추는 높이: 체력바 밑면에서 얼마나 떨어뜨릴지(m). 글자 크기까지 고려해서 넉넉히")] private float numberBarGap = 0.45f;

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
            _hitColor = themeColor;

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

        private void NumberSpot(Component target, Vector3 body, out Vector3 start, out float riseCap)
        {
            start = body - Vector3.up * numberBelowBody;
            riseCap = 0.5f;

            ModuleOwner owner = target.GetComponentInParent<ModuleOwner>();
            Transform scope = owner != null ? owner.transform : target.transform;
            HeadHealthBar bar = scope.GetComponentInChildren<HeadHealthBar>(true);
            if (bar == null)
                return;

            float half = 0f;
            if (bar.transform is RectTransform rect)
                half = rect.rect.height * Mathf.Abs(rect.lossyScale.y) * 0.5f;
            float barBottom = bar.transform.position.y - half;
            riseCap = Mathf.Clamp(barBottom - start.y - numberBarGap, 0.1f, 0.9f);
        }

        private void HandleDamageDealt(DamageDealtInfo info)
        {
            if (info.Target is not Component component)
                return;

            Transform target = component.transform;
            Vector3 body = target.position + Vector3.up * 1.1f;
            Vector3 direction = target.position - transform.position;
            direction.y = 0f;

            Vector3 impact = body;
            if (HitPoint.TryTake(info.Target, out Vector3 markedPoint, out Vector3 markedDirection))
            {
                impact = markedPoint;
                direction = markedDirection;
            }

            Color sparkColor = _hitColor;
            bool skillHit = HitTint.TryGet(info.Target, out Color elementColor);
            if (skillHit)
                sparkColor = elementColor;

            bool crit = info.Amount >= bigHitThreshold || info.IsWeakpoint || info.Killed;

            Color flashColor = Color.Lerp(Color.white, sparkColor, skillHit ? elementFlashTint : 0.35f);
            HitFlash.Flash(component, flashColor, crit ? enemyFlashTime.y : enemyFlashTime.x);

            Color numberColor = crit ? critNumberColor
                : skillHit ? Color.Lerp(normalNumberColor, elementColor, 0.35f) : normalNumberColor;
            NumberSpot(component, body, out Vector3 numberAt, out float numberRise);
            float amount = info.Amount;
            bool numberCrit = info.IsWeakpoint || info.Killed || info.Amount >= critNumberThreshold;
            bool weakHit = !numberCrit && info.Amount < weakNumberThreshold;
            DamageNumberStyle style = numberCrit ? DamageNumberStyle.Crit
                : weakHit ? DamageNumberStyle.Weak
                : skillHit ? DamageNumberStyle.Magic
                : DamageNumberStyle.Normal;
            float numberScale = numberCrit ? 1.3f : weakHit ? 0.8f : 0.9f;
            if (numberDelay > 0f)
                Fx.After(numberDelay, () => Fx.DamageNumber(numberAt, amount, numberColor, numberScale, style, numberRise));
            else
                Fx.DamageNumber(numberAt, amount, numberColor, numberScale, style, numberRise);

            bool big = crit;
            if (_hitParticles && !skillHit)
                Fx.Hit(impact, direction, sparkColor, big);
            Fx.Punch(target, direction, Mathf.Clamp(info.Amount / 80f, 0.08f, 0.45f), knockbackMove);

            if (big || skillHit || info.Killed)
                SpawnFlare(impact, direction, sparkColor, big || info.Killed ? 1.3f : 0.9f);


            HitSourceKind source = HitSource.Current;
            float hitShake = Mathf.Clamp(info.Amount / 220f, 0.03f, 0.2f);
            if (source == HitSourceKind.Passive && Time.unscaledTime - _lastPassiveShakeTime >= passiveHitInterval)
            {
                _lastPassiveShakeTime = Time.unscaledTime;
                HitFeel.Shake(Mathf.Max(passiveHitShake, hitShake * 1.5f), passiveHitShakeTime);
                if (direction.sqrMagnitude > 0.0001f)
                    HitFeel.Kick(direction.normalized * passiveHitKick, 0.2f);
            }
            else if (Time.unscaledTime - _lastHitShakeTime > 0.04f)
            {
                _lastHitShakeTime = Time.unscaledTime;
                HitFeel.Shake(hitShake, 0.12f);
            }

            bool strong = IsStrongKill(info, source);

            SoundLibrary sounds = SoundManager.Library;
            if (sounds != null)
            {
                SoundManager.Play(info.IsWeakpoint ? sounds.weakpointHit : sounds.hit, impact, big ? 1f : 0.8f);
                if (info.Killed && !strong)
                    SoundManager.Play(sounds.kill, impact);
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
            else if (strong)
            {
                PlayStrongKill(target.position, impact, sparkColor);
            }
        }

        [System.Serializable]
        private class StrongKillSettings
        {
            public bool enabled = true;

            [Header("발동 조건: 스킬 1회 시전이 대상 최대 체력의 일정 비율 이상을 깎았을 때")]
            [Range(0f, 1f), Tooltip("스킬 한 번 시전으로 한 대상에게 준 피해 합계 / 대상 최대 체력. 이 값 이상이면 발동 (0.7 = 70%). " +
                                    "여러 틱·파편으로 나눠 맞는 스킬도 같은 시전이면 합쳐서 셈")]
            public float minCastDamageRatio = 0.7f;
            [Tooltip("켜면 그 타격으로 처치했을 때만 발동 (강한 '처치' 연출). 끄면 안 죽어도 비율만 넘으면 발동")]
            public bool requireKill = true;
            [Tooltip("패시브(충전 관통탄 등) 한 방도 같은 비율 기준으로 셀지. 패시브는 시전이 없어서 한 방 피해만 봄")]
            public bool includePassive;
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

        private const int RememberedCasts = 8;
        private readonly List<(int castId, Dictionary<IDamageable, float> damage)> _castDamage = new();
        private readonly HashSet<(int, IDamageable)> _strongFired = new();

        private float RecordCastDamage(int castId, IDamageable target, float amount)
        {
            Dictionary<IDamageable, float> perTarget = null;
            foreach ((int id, Dictionary<IDamageable, float> damage) entry in _castDamage)
                if (entry.id == castId)
                    perTarget = entry.damage;

            if (perTarget == null)
            {
                if (_castDamage.Count >= RememberedCasts)
                {
                    int dropped = _castDamage[0].castId;
                    _castDamage.RemoveAt(0);
                    _strongFired.RemoveWhere(k => k.Item1 == dropped);
                }
                perTarget = new Dictionary<IDamageable, float>();
                _castDamage.Add((castId, perTarget));
            }

            perTarget.TryGetValue(target, out float total);
            total += amount;
            perTarget[target] = total;
            return total;
        }

        private bool IsStrongKill(DamageDealtInfo info, HitSourceKind source)
        {
            StrongKillSettings s = strongKill;
            if (s == null || !s.enabled)
                return false;

            float dealt;
            int castId = HitSource.CurrentCastId;
            if (source == HitSourceKind.Skill && castId != 0)
                dealt = RecordCastDamage(castId, info.Target, info.Amount);
            else if (source == HitSourceKind.Passive && s.includePassive)
                dealt = info.Amount;
            else
                return false;

            if (s.requireKill && !info.Killed)
                return false;
            if (Time.unscaledTime - _lastStrongKillTime < s.cooldown)
                return false;
            if (!Health.HealthReader.TryGetMax(info.Target, out float maxHp))
                return false;
            if (dealt < maxHp * s.minCastDamageRatio)
                return false;

            return castId == 0 || _strongFired.Add((castId, info.Target));
        }

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

            SoundManager.Punch(s.volumeGain, s.volumeHold, s.volumeRelease);
            SoundLibrary sounds = SoundManager.Library;
            if (sounds == null)
                return;
            SoundManager.Play(sounds.kill, impact, s.killVolume, s.killPitch);
            if (s.layerHeavyImpact)
                SoundManager.Play(sounds.heavyImpact, impact, 1f, 0.8f);
        }

        private const string FlarePath = "HJH/StylizedFx/FX_HitFlare";
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
            GunnerFx.Shot(shot.Start, direction, shot.Color, shot.Empowered);
            if (SoundManager.Library != null)
                SoundManager.Play(shot.Empowered ? SoundManager.Library.empoweredShot : SoundManager.Library.shot, shot.Start);

            if (!shot.Projectile)
            {
                Fx.Tracer(shot.Start, shot.End, shot.Color, shot.Empowered ? 0.24f : 0.11f, shot.Empowered ? 0.12f : 0.07f);
                Fx.Tracer(shot.Start, shot.End, GunnerFx.Hot(shot.Color, 0.8f), shot.Empowered ? 0.1f : 0.04f, shot.Empowered ? 0.08f : 0.045f);
                if (!shot.Hit)
                {
                    Fx.ProjectileImpact(shot.End, direction, shot.Color, 0.4f, false);
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
                HitFeel.Shake(0.08f, 0.1f);
            }
            else
            {
                HitFeel.Shake(shotShake, 0.06f);
            }
        }

        private static void HandleProjectileLaunched(ShotProjectile projectile) =>
            FxRunner.Instance.Add(new ProjectileFollowFx(projectile));

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

            SoundLibrary sounds = SoundManager.Library;
            if (sounds == null || swing.Origin == null)
                return;
            bool finisher = swing.Step == swing.StepCount - 1;
            Transform origin = swing.Origin;
            Fx.After(swing.Delay, () =>
            {
                if (origin != null)
                    SoundManager.Play(sounds.swing, origin.position, finisher ? 1f : 0.8f, finisher ? 0.85f : 1f + 0.05f * swing.Step);
            });
        }

        private void HandleSwingHit(SwingHitVisual hit)
        {
            bool finisher = hit.Step == hit.StepCount - 1;
            float shake = hit.ShakeForce * (finisher ? 0.7f : 0.4f);
            HitFeel.Play(finisher ? finisherFreeze : swingHitStop, shake);

            float more = 1f + 0.1f * (Mathf.Min(hit.Targets, 6) - 1);
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                HitFeel.Kick(forward.normalized * swingKick * (finisher ? 2.2f : 1f) * more, finisher ? 0.22f : 0.14f);
            if (finisher)
                HitFeel.Zoom(finisherZoom, 0.26f);

            SoundLibrary sounds = SoundManager.Library;
            if (sounds != null)
                SoundManager.Play(finisher ? sounds.finisherHit : sounds.swingHit, transform.position + forward, Mathf.Min(more, 1.2f));
        }

        private void UpdateMovementTrails()
        {
            _dashTrail ??= Fx.DashTrail(transform, themeColor);
            bool dashing = player != null && player.Mover != null && player.Mover.IsDashing;
            _dashTrail.SetValue(dashing ? 1f : 0f);
            if (dashing && !_wasDashing && SoundManager.Library != null)
                SoundManager.Play(SoundManager.Library.dash, transform.position);
            _wasDashing = dashing;

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
