using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 플레이어가 움직일 때 발밑 바닥에 따라: 흙먼지 / 진흙 튐 / 물방울 튐 / 풀·이끼 조각 / 낙엽 + 발자국·풀잎 자국.
    //  - 바닥 판별은 MoveSurfaceProbe (터레인 페인트 + MoveFxSurface + 물 높이)
    //  - 종류별 설정은 profiles (인스펙터). 파티클 모양은 05.Data/Fx/MoveFx 프리팹에서
    //  - 한 걸음(stride m)마다 왼발/오른발 번갈아 한 번. 대시는 발자국 없이 튐만 촘촘히
    //  - 이동은 위치 변화로 읽음 (CharacterController.Move 든 대시 순간이동이든 똑같이)
    //  - 수면 파동은 WaterRippleEmitter 담당이라 여기선 안 함
    [RequireComponent(typeof(CharacterController))]
    public class MoveFootFx : MonoBehaviour
    {
        [Serializable]
        public class Profile
        {
            public MoveSurfaceKind kind;
            [Tooltip("튀는 파티클 (흙먼지·진흙·물방울·풀 조각). 비우면 없음")] public ParticleSystem spray;
            [Min(0)] public int sprayPerStep = 1;
            [Tooltip("멈췄다 출발할 때 한 번에")] [Min(0)] public int sprayStartBurst = 3;
            [Tooltip("튀는 세기: x = 뒤로(이동 속도에 곱함), y = 위로(m/s)")] public Vector2 sprayKick = new(0.18f, 0.5f);
            public Color sprayColor = Color.white;
            [Tooltip("발자국 색 (알파 = 진하기). 알파 0 이면 발자국 없음")] public Color printColor = new(0f, 0f, 0f, 0f);
            [Tooltip("발자국 남는 시간 (초)")] public float printLife = 6f;
            [Tooltip("몇 걸음마다 바닥에 잎 자국 하나 (0 = 없음)")] [Min(0)] public int leafEvery;
            public Color leafColor = Color.white;
            [Tooltip("0 = 위 색 그대로, 1 = 바닥 텍스처 색으로 (밝기는 groundBright)")] [Range(0f, 1f)] public float groundTint = 0.6f;
            public float groundBright = 1.6f;
        }

        [SerializeField] private MoveSurfaceTable table;
        [SerializeField] private Profile[] profiles = Array.Empty<Profile>();
        [SerializeField, Tooltip("발자국 (바닥에 눕는 파티클)")] private ParticleSystem footprints;
        [SerializeField, Tooltip("바닥에 떨어진 잎 자국")] private ParticleSystem leafDecals;

        [SerializeField, Tooltip("이 속도(m/s)보다 빠르면 걷는 중")] private float minSpeed = 2f;
        [SerializeField, Tooltip("이 속도보다 빠르면 대시")] private float dashSpeed = 11f;
        [SerializeField, Tooltip("한 걸음 거리 (m)")] private float stride = 1.05f;
        [SerializeField, Tooltip("대시 때 튐 간격 (m)")] private float dashStride = 0.45f;
        [SerializeField, Tooltip("왼발·오른발 사이 반 폭 (m)")] private float footSpread = 0.14f;
        [SerializeField] private float footprintSize = 0.6f;
        [SerializeField, Tooltip("발밑에서 이만큼 아래까지 땅이 있으면 땅 위")] private float groundProbe = 0.35f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Tooltip("발자국을 바닥에서 띄우는 높이 (깜빡임 방지)")] private float printLift = 0.025f;
        [SerializeField, Tooltip("발자국 그림이 진행 방향과 어긋나면 여기서 돌림 (도)")] private float printYawOffset;

        private CharacterController _body;
        private MoveSurfaceProbe _probe;
        private Vector3 _lastPosition;
        private float _travelled;
        private bool _wasMoving;
        private bool _leftFoot;
        private int _stepCount;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            _probe = new MoveSurfaceProbe(table);
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _wasMoving = false;
        }

        private void LateUpdate()
        {
            if (!_body.enabled || Time.deltaTime <= 0f)
                return;
#if UNITY_EDITOR
            DebugAutoWalk();
#endif
            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;
            delta.y = 0f;
            float moved = delta.magnitude;
            if (moved > 4f)   // 순간이동·스폰
                return;

            float speed = moved / Time.deltaTime;
            bool moving = speed >= minSpeed;
            if (!moving)
            {
                _wasMoving = false;
                _travelled = 0f;
                return;
            }

            Vector3 heading = delta / moved;
            bool dash = speed >= dashSpeed;
            if (!_wasMoving)
            {
                _wasMoving = true;
                _travelled = 0f;
                if (TryStep(heading, out MoveSurfaceHit hit, out Profile p))
                    Spray(p, hit, heading, speed, p.sprayStartBurst);
            }

            _travelled += moved;
            float step = dash ? dashStride : stride;
            while (_travelled >= step)
            {
                _travelled -= step;
                _leftFoot = !_leftFoot;
                if (!TryStep(heading, out MoveSurfaceHit hit, out Profile p))
                    continue;
                Spray(p, hit, heading, speed, dash ? Mathf.Max(1, p.sprayPerStep) : p.sprayPerStep);
                if (dash)
                    continue;
                _stepCount++;
                Print(p, hit, heading);
                if (p.leafEvery > 0 && _stepCount % p.leafEvery == 0)
                    Leaf(p, hit, heading);
            }
        }

        private bool TryStep(Vector3 heading, out MoveSurfaceHit hit, out Profile profile)
        {
            hit = default;
            profile = null;
            var side = new Vector3(heading.z, 0f, -heading.x) * (_leftFoot ? -footSpread : footSpread);
            Vector3 bottom = transform.TransformPoint(_body.center) + Vector3.down * (_body.height * 0.5f - _body.radius) + side;
            if (!Physics.SphereCast(bottom + Vector3.up * 0.05f, _body.radius * 0.3f, Vector3.down, out RaycastHit ray,
                    _body.radius + groundProbe, groundMask, QueryTriggerInteraction.Ignore))
                return false;
            if (ray.collider.transform.IsChildOf(transform))
                return false;

            hit = _probe.Classify(ray);
#if UNITY_EDITOR
            // 확인용: HJH/FX/이동 이펙트/바닥 강제 메뉴 (-1 = 끔)
            int forced = UnityEditor.EditorPrefs.GetInt("HJH.MoveFx.ForceKind", -1);
            if (forced >= 0)
                hit.Kind = (MoveSurfaceKind)forced;
#endif
            profile = Find(hit.Kind);
            return profile != null;
        }

        private Profile Find(MoveSurfaceKind kind)
        {
            if (kind == MoveSurfaceKind.None)
                return null;
            foreach (Profile p in profiles)
                if (p.kind == kind)
                    return p;
            return null;
        }

        private static Color Mix(Profile p, Color baseColor, Color ground, float bright)
        {
            if (ground == Color.white || p.groundTint <= 0f)
                return baseColor;
            Color g = ground * bright;
            Color c = Color.Lerp(baseColor, g, p.groundTint);
            c.a = baseColor.a;
            return c;
        }

        private void Spray(Profile p, MoveSurfaceHit hit, Vector3 heading, float speed, int count)
        {
            if (p.spray == null || count <= 0)
                return;
            Vector3 back = -heading;
            var side = new Vector3(-back.z, 0f, back.x);
            Color color = Mix(p, p.sprayColor, hit.Tint, p.groundBright);
            var param = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                param.position = hit.Point + Vector3.up * 0.08f + side * UnityEngine.Random.Range(-0.15f, 0.15f) + back * UnityEngine.Random.Range(0.05f, 0.25f);
                param.velocity = back * (speed * p.sprayKick.x * UnityEngine.Random.Range(0.6f, 1.1f))
                                 + Vector3.up * (p.sprayKick.y * UnityEngine.Random.Range(0.6f, 1.3f))
                                 + side * UnityEngine.Random.Range(-0.5f, 0.5f);
                p.spray.Emit(param, 1);
            }
        }

        private void Print(Profile p, MoveSurfaceHit hit, Vector3 heading)
        {
            if (footprints == null || p.printColor.a <= 0.001f)
                return;
            Color color = Mix(p, p.printColor, hit.Tint, 0.6f);
            EmitFlat(footprints, hit, heading, color, footprintSize, p.printLife, 0f);
        }

        private void Leaf(Profile p, MoveSurfaceHit hit, Vector3 heading)
        {
            if (leafDecals == null)
                return;
            Color color = Mix(p, p.leafColor, hit.Tint, 1.2f);
            Vector3 offset = new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0f, UnityEngine.Random.Range(-0.3f, 0.3f));
            hit.Point += offset;
            float yaw = UnityEngine.Random.Range(0f, 360f);
            EmitFlat(leafDecals, hit, Quaternion.Euler(0f, yaw, 0f) * heading, color, UnityEngine.Random.Range(0.3f, 0.45f), p.printLife * 0.8f, 0f);
        }

        // 바닥에 눕는 사각형 (Horizontal Billboard): 수직축 회전 하나만 줌 → 그림 위쪽(발끝)이 진행 방향.
        //  (예전엔 3D 회전을 오일러로 줬는데 파티클의 회전 순서가 Transform 과 달라 방향에 따라 발자국이 엉뚱하게 돌았음)
        private void EmitFlat(ParticleSystem system, MoveSurfaceHit hit, Vector3 heading, Color color, float size, float life, float roll)
        {
            Vector3 normal = hit.Normal.sqrMagnitude > 0.5f ? hit.Normal : Vector3.up;
            float yaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg + printYawOffset + roll;
            var param = new ParticleSystem.EmitParams
            {
                position = hit.Point + normal * printLift,
                rotation = yaw,
                startColor = color,
                startSize = size,
                startLifetime = life,
                velocity = Vector3.zero,
                applyShapeToPosition = false
            };
            system.Emit(param, 1);
        }

#if UNITY_EDITOR
        // 캡처 도구는 이동 입력을 못 넣으니, HJH/FX/이동 이펙트 자동 걷기 를 켜면 플레이 중 제자리 원을 그리며 걸음 (확인용)
        private float _walkAngle;
        private float _printTest;

        private void DebugAutoWalk()
        {
            // 방향 시험: 발밑 네 방향(+Z, +X, -Z, -X)에 그 방향을 향한 발자국 → 맞으면 발끝이 전부 바깥쪽
            if (UnityEditor.EditorPrefs.GetBool("HJH.MoveFx.PrintTest", false) && footprints != null && (_printTest -= Time.deltaTime) <= 0f)
            {
                _printTest = 1f;
                foreach (Vector3 d in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                {
                    var test = new MoveSurfaceHit { Point = transform.position + d * 1.2f, Normal = Vector3.up };
                    EmitFlat(footprints, test, d, new Color(0f, 0f, 0f, 0.9f), 0.8f, 1.5f, 0f);
                }
            }
            if (!UnityEditor.EditorPrefs.GetBool("HJH.MoveFx.AutoWalk", false))
                return;
            _walkAngle += Time.deltaTime * 1.1f;
            var dir = new Vector3(Mathf.Cos(_walkAngle), 0f, Mathf.Sin(_walkAngle));
            _body.Move((dir * 5.5f + Vector3.down * 2f) * Time.deltaTime);
        }
#endif

        [ContextMenu("이번 발밑 바닥 종류 찍기 (테스트)")]
        private void LogSurface()
        {
            if (_body == null)
                Awake();
            if (TryStep(transform.forward, out MoveSurfaceHit hit, out _))
                Debug.Log($"[MoveFootFx] {hit.Kind} tint={hit.Tint} at {hit.Point}");
            else
                Debug.Log("[MoveFootFx] 발밑에 땅 없음 / 프로필 없음");
        }
    }
}
