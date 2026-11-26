using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
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
