using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 데미지 숫자 종류 확인용 데모. 에디터 메뉴 HJH/디버그/데미지 숫자 샘플 (플레이 시) 로 켜 두면
    // 다음 플레이부터 플레이어 앞에 2초마다 5종류가 한 줄로 떠서 모양을 비교할 수 있음 (빌드에는 안 생김).
    //   왼쪽부터: 물리(주황) / 마법(파랑) / 약함(흰색) / 크리(빨강) / 받은 피해
    public sealed class DamageNumberDemo : MonoBehaviour
    {
        public const string SessionKey = "HJH.DamageNumberDemo";

        private Transform _player;
        private float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool(SessionKey, false))
                new GameObject("DamageNumberDemo").AddComponent<DamageNumberDemo>();
#endif
        }

        private void Update()
        {
            if (Time.time < _next)
                return;

            if (_player == null)
            {
                PlayerAgent agent = FindFirstObjectByType<PlayerAgent>();
                if (agent != null)
                    _player = agent.transform;
            }
            if (_player == null)
                return;

            _next = Time.time + 2f;

            Camera cam = Camera.main;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            right.y = 0f;
            right.Normalize();
            Vector3 up = cam != null ? cam.transform.up : Vector3.forward;
            up.y = 0f;
            up.Normalize();

            Vector3 origin = _player.position + up * 3.2f + Vector3.up * 1.6f;
            const float gap = 2.1f;
            Show(origin - right * gap * 2f, 58f, DamageNumberStyle.Normal);
            Show(origin - right * gap, 92f, DamageNumberStyle.Magic);
            Show(origin, 24f, DamageNumberStyle.Weak);
            Show(origin + right * gap, 138f, DamageNumberStyle.Crit);
            Show(origin + right * gap * 2f, 31f, DamageNumberStyle.Taken);
        }

        private static void Show(Vector3 at, float amount, DamageNumberStyle style)
        {
            float scale = style == DamageNumberStyle.Crit ? 1.3f : style == DamageNumberStyle.Weak ? 0.8f : 0.9f;
            Fx.DamageNumber(at, amount, Color.white, scale, style);
        }
    }
}
