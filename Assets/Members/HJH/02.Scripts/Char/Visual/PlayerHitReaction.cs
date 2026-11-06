using Assets.Members.HJH._02.Scripts.Char.Audio;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Members.KYR._01_Scripts;
using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 플레이어가 맞았을 때 "몸"이 반응하는 거 (화면 빨개지는 건 PlayerDamageScreenFx 가 함)
    //  - 몸이 빨갛게 번쩍
    //  - 때린 쪽 반대로 몸(Visual)만 툭 밀렸다 돌아옴 + 찌그러짐. 실제 위치(판정)는 안 움직임
    //  - 머리 위에 흰색 피해 숫자 (내가 받은 피해)
    //  - 카메라가 맞은 방향으로 살짝 밀림. 세게 맞으면 아주 짧게 멈칫
    //  - 컨트롤러에 "Hit" 상태 있으면 맞는 모션 (CharacterAnimator)
    // CharacterModel.For 가 알아서 붙여줌. 씬에 따로 안 붙여도 됨
    public class PlayerHitReaction : MonoBehaviour
    {
        [SerializeField] private Color flashColor = new(1f, 0.2f, 0.15f);
        [SerializeField] private float flashTime = 0.14f;

        [Header("몸 밀림")]
        [SerializeField, Tooltip("약하게 맞았을 때 / 세게 맞았을 때 밀리는 세기")]
        private Vector2 punch = new(0.12f, 0.3f);
        [SerializeField, Tooltip("이 시간 안에 또 맞으면 밀림·모션은 건너뜀 (연타에 덜덜 떨지 않게). 번쩍임은 매번")]
        private float minInterval = 0.2f;

        [Header("세게 맞음")]
        [SerializeField, Range(0f, 1f), Tooltip("최대 HP의 이 비율 이상 한 번에 맞으면 세게 맞은 걸로")]
        private float heavyRatio = 0.12f;
        [SerializeField, Tooltip("세게 맞았을 때 멈칫하는 시간")] private float heavyHitStop = 0.05f;
        [SerializeField] private float cameraKick = 0.12f;

        [Header("화면 흔들림 (맞음 / 경직·기절)")]
        [SerializeField, Tooltip("맞았을 때 흔들림: x = 약하게, y = 세게 맞았을 때. PlayerDamageScreenFx 흔들림(0.06~0.15)보다 세게 → 센 쪽이 이김")]
        private Vector2 hitShake = new(0.22f, 0.42f);
        [SerializeField, Tooltip("맞았을 때 흔들림 시간 (실제 초)")] private float hitShakeTime = 0.26f;
        [SerializeField, Tooltip("기절(묶임)이 걸리는 순간 흔들림")] private float stunShake = 0.45f;
        [SerializeField, Tooltip("기절 흔들림 시간 (실제 초)")] private float stunShakeTime = 0.35f;
        [SerializeField, Tooltip("기절 순간 카메라가 다가가는 정도 (0.05 = 5%)")] private float stunZoom = 0.05f;

        [Header("죽음")]
        [SerializeField, Tooltip("죽는 순간 이 배속으로 느려짐")] private float deathSlowMo = 0.25f;
        [SerializeField, Tooltip("느려지는 시간 (실제 초)")] private float deathSlowTime = 1.2f;
        [SerializeField] private float deathShake = 0.35f;
        [SerializeField, Tooltip("카메라가 다가가는 정도")] private float deathZoom = 0.12f;

        [Header("피해 숫자")]
        [SerializeField] private bool showNumber = true;
        [SerializeField, Tooltip("받은 피해는 흰색 (SoD: 주는 피해 = 주황/빨강, 받는 피해 = 흰색)")]
        private Color numberColor = Color.white;

        private PlayerAgent _player;
        private CharacterModel _model;
        private CharacterAnimator _animator;
        private float _lastReactTime = -999f;
        private bool _wasAlive = true;
        private bool _wasStunned;

        private void Awake()
        {
            _player = GetComponent<PlayerAgent>();
            _model = GetComponent<CharacterModel>();
            _animator = GetComponent<CharacterAnimator>();
        }

        private void OnEnable()
        {
            if (_player != null)
                _player.DamageTaken += HandleDamageTaken;
        }

        private void OnDisable()
        {
            if (_player != null)
                _player.DamageTaken -= HandleDamageTaken;
        }

        // 죽는 순간 한 번: 확 느려지고 + 크게 흔들리고 + 카메라가 다가감. 쓰러지는 건 CharacterAnimator 의 Dead,
        // 화면 회색+빨간 테두리는 PlayerDamageScreenFx 가 함
        private void Update()
        {
            if (_player == null || _player.Health == null)
                return;

            bool alive = _player.IsAlive;
            if (_wasAlive && !alive)
                PlayDeath();
            _wasAlive = alive;

            // 기절이 막 걸린 순간 한 번 (PlayerHealth 가 맞을 때 거는 경직 기절 포함)
            bool stunned = alive && _player.Health.IsStunned;
            if (stunned && !_wasStunned)
            {
                HitFeel.Shake(stunShake, stunShakeTime);
                HitFeel.Zoom(stunZoom, 0.3f);
            }
            _wasStunned = stunned;
        }

        private void PlayDeath()
        {
            Component body = _model != null && _model.Current != null ? _model.Current.transform : transform;
            HitFlash.Flash(body, Color.white, 0.3f);
            HitFeel.SlowMo(deathSlowMo, deathSlowTime);
            HitFeel.Shake(deathShake, 0.4f);
            HitFeel.Zoom(deathZoom, deathSlowTime);

            SfxBank sounds = Sfx.Bank;
            if (sounds != null)
                Sfx.Play(sounds.heavyImpact, transform.position, 1f, 0.7f);
        }

        private void HandleDamageTaken(float amount, GameObject source)
        {
            if (amount <= 0f || _player == null)
                return;

            float maxHp = _player.Health != null ? Mathf.Max(1f, _player.Health.MaxHp) : 100f;
            float ratio = amount / maxHp;
            bool heavy = ratio >= heavyRatio;

            // 몸 번쩍 (모델 없으면 캡슐째로)
            Component body = _model != null && _model.Current != null ? _model.Current.transform : transform;
            HitFlash.Flash(body, flashColor, heavy ? flashTime * 1.4f : flashTime);

            if (showNumber)
                Fx.DamageNumber(transform.position + Vector3.up * 2.1f, amount, numberColor, heavy ? 1.15f : 0.9f, DamageNumberStyle.Taken);

            // 흔들림은 매번 (센 쪽이 이겨서 연타에도 쌓이지 않음)
            float shakeWeight = heavy ? 1f : Mathf.Clamp01(ratio / Mathf.Max(0.01f, heavyRatio));
            HitFeel.Shake(Mathf.Lerp(hitShake.x, hitShake.y, shakeWeight), hitShakeTime);

            if (Time.unscaledTime - _lastReactTime < minInterval && !heavy)
                return;
            _lastReactTime = Time.unscaledTime;

            Vector3 away = AwayFrom(source);

            // 몸만 밀기 (Visual 이 플레이어 자식이라 판정·이동은 그대로)
            Transform visual = _model != null ? _model.Visual : null;
            if (visual != null && visual != transform)
                Fx.Punch(visual, away, heavy ? punch.y : Mathf.Lerp(punch.x, punch.y, ratio / Mathf.Max(0.01f, heavyRatio)));

            HitFeel.Kick(away * cameraKick * (heavy ? 1.6f : 1f), heavy ? 0.2f : 0.14f);
            if (heavy)
                HitFeel.Play(heavyHitStop, 0f);   // 흔들림은 위에서 이미 줌

            if (_animator != null)
                _animator.PlayHit();

            SfxBank sounds = Sfx.Bank;
            if (sounds != null)
            {
                SfxCue light = sounds.playerHurt.HasClips ? sounds.playerHurt : sounds.hit;
                Sfx.Play(heavy ? sounds.heavyImpact : light, transform.position, heavy ? 1f : 0.7f, 0.8f);
            }
        }

        // 때린 쪽 반대 방향 (모르면 뒤로)
        private Vector3 AwayFrom(GameObject source)
        {
            Vector3 away = source != null ? transform.position - source.transform.position : -transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = -transform.forward;
            away.y = 0f;
            return away.normalized;
        }
    }
}
