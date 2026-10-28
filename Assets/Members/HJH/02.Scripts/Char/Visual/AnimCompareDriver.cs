using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 애니 비교 씬용. 나란히 세운 캐릭터들한테 똑같은 이동 입력(MoveX/MoveY + Idle/Run)을 동시에 넣어줌.
    // 캐릭터는 제자리에서 뛰고, 대신 바닥 격자가 반대로 흘러서 발 미끄러짐도 볼 수 있음.
    //
    //   자동 모드 : 서 있기 -> 앞 -> 앞오른 -> 오른 -> 뒤오른 -> 뒤 -> 뒤왼 -> 왼 -> 앞왼 순서로 계속 돎
    //   수동 모드 : WASD 로 이동 방향, 마우스로 보는 방향 (게임이랑 똑같이 "보는 방향 기준" 으로 바뀜)
    //
    //   Tab  : 자동/수동 바꾸기
    //   1~4  : 재생 속도 1 / 0.5 / 0.25 / 0.1 배
    //   Space: 일시정지
    //   F    : 수동 모드에서 마우스 따라 보기 켜기/끄기 (끄면 전부 카메라 반대쪽(+Z)만 봄)
    public class AnimCompareDriver : MonoBehaviour
    {
        [Serializable]
        public class Actor
        {
            public string label;
            public Animator animator;
            [Tooltip("돌릴 대상 (비우면 animator 를 돌림). 모델 자체 회전값은 안 건드리려고 따로 둠")]
            public Transform root;

            public Transform Pivot => root != null ? root : animator != null ? animator.transform : null;
        }

        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");
        private static readonly int Idle = Animator.StringToHash("Idle");
        private static readonly int Run = Animator.StringToHash("Run");

        private static readonly (string name, Vector2 dir)[] Steps =
        {
            ("서 있기", Vector2.zero),
            ("앞", new Vector2(0f, 1f)),
            ("앞-오른쪽", new Vector2(1f, 1f).normalized),
            ("오른쪽", new Vector2(1f, 0f)),
            ("뒤-오른쪽", new Vector2(1f, -1f).normalized),
            ("뒤", new Vector2(0f, -1f)),
            ("뒤-왼쪽", new Vector2(-1f, -1f).normalized),
            ("왼쪽", new Vector2(-1f, 0f)),
            ("앞-왼쪽", new Vector2(-1f, 1f).normalized),
        };

        [SerializeField] private Actor[] actors = Array.Empty<Actor>();
        [SerializeField, Tooltip("자동 모드에서 방향 하나 보여주는 시간(초)")] private float stepTime = 2.5f;
        [SerializeField, Tooltip("바닥이 흐르는 속도 (m/s). 게임 이동 속도(PlayerMover walkSpeed 4.5)랑 같음. 발 미끄러짐 비교용")] private float groundSpeed = 4.5f;
        [SerializeField] private Renderer ground;
        [SerializeField] private float fade = 0.15f;
        [SerializeField] private float moveDirDamp = 0.1f;

        private bool _auto = true;
        private bool _faceMouse = true;
        private bool _paused;
        private float _speed = 1f;
        private float _stepTimer;
        private int _step;
        private Vector2 _worldMove;
        private Vector2 _groundOffset;
        private Vector2 _uvSign;
        private readonly bool[] _running = new bool[16];

        private void Start()
        {
            foreach (Actor actor in actors)
            {
                if (actor.animator == null)
                    continue;
                actor.animator.applyRootMotion = false;
                actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // 상체 레이어 있는 컨트롤러(현재 KM 거)는 게임처럼 상체도 같이 켬
                int upper = actor.animator.GetLayerIndex("Upper");
                if (upper >= 0)
                    actor.animator.SetLayerWeight(upper, 1f);
            }
            Play(false, true);
        }

        private void Update()
        {
            ReadKeys();
            Time.timeScale = _paused ? 0f : _speed;

            if (_auto)
            {
                _stepTimer += Time.deltaTime;
                if (_stepTimer >= stepTime)
                {
                    _stepTimer = 0f;
                    _step = (_step + 1) % Steps.Length;
                }
                // 자동 모드는 다들 +Z 보고, 방향은 그 기준
                _worldMove = Steps[_step].dir;
                Face(Vector3.forward);
            }
            else
            {
                Keyboard kb = Keyboard.current;
                Vector2 input = kb == null ? Vector2.zero : new Vector2(
                    (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                    (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
                _worldMove = input.sqrMagnitude > 0f ? input.normalized : Vector2.zero;
                if (_faceMouse)
                    FaceMouse();
                else
                    Face(Vector3.forward);
            }

            bool moving = _worldMove.sqrMagnitude > 0.01f;
            for (int i = 0; i < actors.Length; i++)
            {
                Animator animator = actors[i].animator;
                if (animator == null)
                    continue;
                if (moving)
                {
                    // 월드 이동 방향 -> 캐릭터 기준 방향 (CharacterAnimator 랑 같은 계산)
                    Vector3 local = animator.transform.InverseTransformDirection(new Vector3(_worldMove.x, 0f, _worldMove.y));
                    Vector2 dir = new Vector2(local.x, local.z).normalized;
                    animator.SetFloat(MoveX, dir.x, moveDirDamp, Time.deltaTime);
                    animator.SetFloat(MoveY, dir.y, moveDirDamp, Time.deltaTime);
                }
                if (i < _running.Length && _running[i] != moving)
                    PlayOne(i, moving);
            }

            ScrollGround();
        }

        private void ReadKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
                return;
            if (kb.tabKey.wasPressedThisFrame)
            {
                _auto = !_auto;
                _step = 0;
                _stepTimer = 0f;
            }
            if (kb.fKey.wasPressedThisFrame)
                _faceMouse = !_faceMouse;
            if (kb.spaceKey.wasPressedThisFrame)
                _paused = !_paused;
            if (kb.digit1Key.wasPressedThisFrame) _speed = 1f;
            if (kb.digit2Key.wasPressedThisFrame) _speed = 0.5f;
            if (kb.digit3Key.wasPressedThisFrame) _speed = 0.25f;
            if (kb.digit4Key.wasPressedThisFrame) _speed = 0.1f;
        }

        private void Play(bool run, bool instant)
        {
            for (int i = 0; i < actors.Length; i++)
                PlayOne(i, run, instant);
        }

        private void PlayOne(int index, bool run, bool instant = false)
        {
            Animator animator = actors[index].animator;
            if (animator == null)
                return;
            int state = run ? Run : Idle;
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                if (!animator.HasState(layer, state))
                    continue;
                animator.CrossFadeInFixedTime(state, instant ? 0f : fade, layer, 0f);
            }
            if (index < _running.Length)
                _running[index] = run;
        }

        private void Face(Vector3 forward)
        {
            foreach (Actor actor in actors)
            {
                if (actor.Pivot != null)
                    actor.Pivot.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        // 마우스가 가리키는 바닥 쪽을 봄. 캐릭터마다 자기 위치 기준이라 셋 다 거의 같은 방향을 봄
        private void FaceMouse()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance))
                return;
            Vector3 point = ray.GetPoint(distance);
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (Actor actor in actors)
            {
                if (actor.animator == null)
                    continue;
                center += actor.animator.transform.position;
                count++;
            }
            if (count == 0)
                return;
            Vector3 look = point - center / count;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                Face(look.normalized);
        }

        // 캐릭터는 제자리라 바닥을 반대로 흘림 (격자 1칸 = 1m)
        private void ScrollGround()
        {
            if (ground == null)
                return;
            if (_uvSign == Vector2.zero)
                _uvSign = UvSign(ground);
            _groundOffset -= _worldMove * (groundSpeed * Time.deltaTime);
            Material material = ground.material;
            Vector2 tiling = material.HasProperty("_BaseMap") ? material.GetTextureScale("_BaseMap") : material.mainTextureScale;
            float size = ground.bounds.size.x;
            Vector2 offset = size > 0f ? Vector2.Scale(_groundOffset, _uvSign) * (tiling.x / size) : Vector2.zero;
            if (material.HasProperty("_BaseMap"))
                material.SetTextureOffset("_BaseMap", -offset);
            material.mainTextureOffset = -offset;
        }

        // 바닥 메쉬의 UV 가 월드 x/z 랑 같은 방향인지 반대인지 (Plane 은 뒤집혀 있어서 확인함)
        private static Vector2 UvSign(Renderer renderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.uv.Length != mesh.vertexCount)
                return Vector2.one;
            Vector3[] v = mesh.vertices;
            Vector2[] uv = mesh.uv;
            int minX = 0, maxX = 0, minZ = 0, maxZ = 0;
            for (int i = 1; i < v.Length; i++)
            {
                if (v[i].x < v[minX].x) minX = i;
                if (v[i].x > v[maxX].x) maxX = i;
                if (v[i].z < v[minZ].z) minZ = i;
                if (v[i].z > v[maxZ].z) maxZ = i;
            }
            return new Vector2(uv[maxX].x >= uv[minX].x ? 1f : -1f, uv[maxZ].y >= uv[minZ].y ? 1f : -1f);
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft };
            string mode = _auto ? $"자동  ·  지금: {Steps[_step].name}" : $"수동 (WASD, 마우스로 보기 {(_faceMouse ? "켬" : "끔")})";
            string text = $"{mode}\n재생 속도 {_speed:0.##}x{(_paused ? "  (멈춤)" : "")}\n\nTab 자동/수동 · 1~4 속도 · Space 멈춤 · F 마우스 보기";
            GUI.Box(new Rect(12, 12, 560, 110), text, style);

            // 캐릭터 머리 위 이름 (TextMesh 는 한글 폰트 문제 있어서 OnGUI 로)
            Camera cam = Camera.main;
            if (cam == null)
                return;
            var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = Color.white;
            foreach (Actor actor in actors)
            {
                if (actor.Pivot == null)
                    continue;
                Vector3 screen = cam.WorldToScreenPoint(actor.Pivot.position + Vector3.up * 2.3f);
                if (screen.z <= 0f)
                    continue;
                var rect = new Rect(screen.x - 130f, Screen.height - screen.y - 24f, 260f, 48f);
                GUI.Label(rect, actor.label, labelStyle);
            }
        }

        private void OnDisable() => Time.timeScale = 1f;
    }
}
