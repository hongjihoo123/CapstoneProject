using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public sealed class TelegraphDemo : MonoBehaviour
    {
        public const string SessionKey = "HJH.TelegraphDemo";
        private const float ModeSeconds = 2.5f;
        private const float WarningSeconds = 2f;

        private AttackAreaVisualizer _visualizer;
        private Transform _player;
        private float _nextWarning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool(SessionKey, false))
                new GameObject("TelegraphDemo").AddComponent<TelegraphDemo>();
#endif
        }

        private void Start()
        {
            var go = new GameObject("TelegraphDemo_Visualizer");
            go.transform.SetParent(transform, false);
            _visualizer = go.AddComponent<AttackAreaVisualizer>();
            _visualizer.PreviewOverride = Describe;
        }

        private Vector3 Anchor()
        {
            if (_player == null)
            {
                PlayerAgent agent = FindFirstObjectByType<PlayerAgent>();
                if (agent != null)
                    _player = agent.transform;
            }
            if (_player != null)
                return _player.position;

            Camera cam = Camera.main;
            if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 200f))
                return hit.point;
            return Vector3.zero;
        }

        private static void ScreenAxes(out Vector3 right, out Vector3 up)
        {
            Camera cam = Camera.main;
            right = cam != null ? cam.transform.right : Vector3.right;
            right.y = 0f;
            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
            up = new Vector3(-right.z, 0f, right.x);
        }

        private void Describe(ISkillPreview preview)
        {
            Vector3 origin = Anchor();
            ScreenAxes(out Vector3 right, out Vector3 up);
            Vector3 aim = Quaternion.AngleAxis(15f, Vector3.up) * up;

            switch ((int)(Time.time / ModeSeconds) % 3)
            {
                case 0:
                    Vector3 landing = origin + aim * 2.2f;
                    preview.Path(origin, landing, 1f);
                    preview.Cone(landing, aim, 4.5f, 100f);
                    break;
                case 1:
                    preview.RangeCircle(origin, 7f);
                    preview.Circle(origin + aim * 4.5f, 2f);
                    break;
                default:
                    preview.Path(origin, origin + aim * 7f, 1.2f);
                    break;
            }
        }

        private void Update()
        {
            if (_visualizer == null || Time.time < _nextWarning)
                return;

            _nextWarning = Time.time + WarningSeconds + 0.3f;
            Vector3 origin = Anchor();
            ScreenAxes(out Vector3 right, out Vector3 up);

            Vector3 circleCenter = origin + right * 6.5f + up * 1.5f;
            _visualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning, circleCenter, 3f, WarningSeconds));

            Vector3 coneApex = origin + right * 5f - up * 3.5f;
            _visualizer.Show(AttackArea.Cone(AttackAreaKind.EnemyWarning, coneApex,
                Quaternion.LookRotation(-right), 3.5f, 70f, WarningSeconds));

            Vector3 boxCenter = origin - right * 6f;
            _visualizer.Show(AttackArea.Box(AttackAreaKind.EnemyWarning, boxCenter,
                Quaternion.LookRotation(up), new Vector3(1.6f, 0f, 6f), WarningSeconds));
        }
    }
}
