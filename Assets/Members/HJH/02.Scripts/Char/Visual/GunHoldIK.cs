using System;
using Assets.Members.HJH._02.Scripts.Char.Character;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 총 잡는 양손 IK. Generic 리그라 유니티 기본 IK(Humanoid 전용)가 안 돼서 팔을 직접 2본 IK 로 풂.
    //
    //   - 총을 "어디에 어떤 각도로 들지"(자세)만 정함 -> 오른손은 권총 손잡이, 왼손은 앞 수직 손잡이를 정확히 잡음
    //   - 이 캐릭터는 팔이 짧아서(어깨~손목 ≈ 총 길이의 40%) 두 손이 닿는 범위 안으로 총 위치를 자동으로 끌어당김
    //   - 오른손 검지는 방아쇠 쪽으로 펴고 나머지 손가락은 모션 그대로(쥔 채)
    //   - 가슴/몸통이 흔들리면 총도 따라옴 (몸통이 평소 자세에서 돈 만큼)
    //   - 쏜 직후엔 견착 클립 첫 프레임 자세로 올림 (CharacterAnimator.Hold == Aim)
    //
    // 자세 숫자 단위: 모델 원본 미터(캐릭터 키 ≈ 0.85). 캐릭터 앞 기준 x=오른쪽, y=위, z=앞.
    // 손 위치는 "오른쪽 어깨 뼈" 기준. 총 방향은 pitch(총구 아래로)/yaw(+오른쪽)/roll(총 기울임)
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public class GunHoldIK : MonoBehaviour
    {
        [Serializable]
        public class HoldPose
        {
            [Tooltip("오른손(권총 손잡이) 목표 위치. 오른쪽 어깨 기준, 캐릭터 앞 축 (x 오른쪽, y 위, z 앞)")] public Vector3 hand;
            [Tooltip("총구를 아래로 내리는 각도(도)")] public float pitch;
            [Tooltip("총구를 좌우로 돌리는 각도(도). + 는 오른쪽")] public float yaw;
            [Tooltip("총 기울임(도)")] public float roll;
        }

        [Header("자세 (낮춰 든 자세)")]
        [SerializeField] private HoldPose idle = new() { hand = new Vector3(-0.02f, -0.07f, 0.08f), pitch = 22f, yaw = -10f, roll = 0f };
        [SerializeField] private HoldPose run = new() { hand = new Vector3(-0.02f, -0.06f, 0.09f), pitch = 14f, yaw = -8f, roll = 0f };

        [Header("손잡이")]
        [SerializeField, Tooltip("왼손이 잡을 앞 손잡이 위치. 총 기준 (앞, 위, 오른쪽). 기본은 수직 그립 중심")] private Vector3 leftGrip = new Vector3(0.177f, -0.035f, 0f);
        [SerializeField, Range(0f, 1f), Tooltip("오른손 검지를 방아쇠 쪽으로 펴는 정도")] private float indexExtend = 0.8f;
        [SerializeField, Range(0.6f, 1f), Tooltip("팔을 끝까지 안 펴고 이 비율까지만 뻗도록 총을 끌어당김")] private float reachFraction = 0.94f;

        [SerializeField, Range(0f, 70f), Tooltip("두 손이 총에 닿게 상체를 오른쪽으로 비트는 최대 각도 (팔이 짧아서 필요). 머리는 정면 유지")] private float maxTwist = 55f;

        [Header("팔꿈치 방향 (각 어깨 기준, 캐릭터 앞 축)")]
        [SerializeField] private Vector3 rightElbowHint = new Vector3(0.12f, -0.12f, -0.08f);
        [SerializeField] private Vector3 leftElbowHint = new Vector3(-0.12f, -0.12f, -0.03f);

        [Header("부드러움")]
        [SerializeField, Tooltip("자세 바뀔 때 따라가는 속도 (클수록 빠름)")] private float poseSharpness = 10f;
        [SerializeField, Tooltip("IK 켜고 끄는 데 걸리는 시간")] private float blendTime = 0.12f;
        [SerializeField, Range(0f, 1f), Tooltip("전체 강도")] private float weight = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("몸통이 흔들리고 기울 때 총이 따라가는 정도")] private float bodyFollow = 0.85f;

        private const string TemplateClip = "AimIdle";
        private const float BlenderLeftGripAlong = 0.177f;   // 견착 클립에서 왼손이 오른손 앞 0.177 에 있음 (Blender 측정) -> 크기 환산 기준

        private CharacterModel _model;
        private CharacterAnimator _anim;
        private Animator _bound;

        private Transform _chest, _rUpper, _rFore, _rHand, _lUpper, _lFore, _lHand, _rLeg, _lLeg;
        private Transform _idx1, _idx2, _idx3, _idx4;
        private Transform _spine1, _neck, _head;
        private float _twist;     // 지금 적용 중인 상체 비틀기 각도
        private bool _ready;

        // 견착 클립에서 읽은 기준 (월드 스케일로 나눠 크기에 상관없게)
        private Vector3 _aimHandPos;          // 오른쪽 어깨 기준, 캐릭터 앞 축
        private Quaternion _aimHandRot;       // 캐릭터 앞 기준 손 회전
        private Quaternion _lHandRotInRHand;
        private Vector3 _axLocal = Vector3.forward;   // 오른손 뼈 기준 총구 방향
        private Vector3 _upLocal = Vector3.up;        // 오른손 뼈 기준 총 위쪽
        private Quaternion _gunBasisLocal = Quaternion.identity;
        private float _unit = 1f;             // Blender 미터 -> 이 모델 단위
        private Vector3 _forwardLocal = Vector3.forward;
        private Quaternion _chestRefInFrame;

        // 지금 적용 중인 값
        private HoldPose _cur = new();
        private float _aim;      // 0 = 낮춰 듦, 1 = 견착
        private float _blend;
        private bool _poseInit;

        private void Awake()
        {
            _model = GetComponent<CharacterModel>();
            _anim = GetComponent<CharacterAnimator>();
        }

        private void OnDisable()
        {
            _bound = null;
            _ready = false;
        }

        private void LateUpdate()
        {
            Animator animator = _model != null ? _model.Animator : null;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                _bound = null;
                _ready = false;
                return;
            }
            if (animator != _bound)
                Bind(animator);
            if (!_ready || _anim == null)
                return;

            CharacterAnimator.GunHold hold = _anim.Hold;
            bool lowered = hold == CharacterAnimator.GunHold.Idle || hold == CharacterAnimator.GunHold.Run;
            bool aiming = hold == CharacterAnimator.GunHold.Aim || hold == CharacterAnimator.GunHold.Shoot;
            float target = lowered || hold == CharacterAnimator.GunHold.Aim ? weight : 0f;
            _blend = Mathf.MoveTowards(_blend, target, blendTime > 0f ? Time.deltaTime / blendTime : 1f);

            HoldPose wanted = hold == CharacterAnimator.GunHold.Run ? run : idle;
            if (!_poseInit)
            {
                Copy(wanted, _cur);
                _aim = aiming ? 1f : 0f;
                _poseInit = true;
            }
            float k = 1f - Mathf.Exp(-poseSharpness * Time.deltaTime);
            if (lowered)
                Lerp(_cur, wanted, k);
            _aim = Mathf.Lerp(_aim, aiming ? 1f : 0f, k);

            if (_blend <= 0.001f)
                return;
            Apply(animator.transform, _blend);
        }

        private static void Copy(HoldPose from, HoldPose to)
        {
            to.hand = from.hand; to.pitch = from.pitch; to.yaw = from.yaw; to.roll = from.roll;
        }

        private static void Lerp(HoldPose cur, HoldPose to, float k)
        {
            cur.hand = Vector3.Lerp(cur.hand, to.hand, k);
            cur.pitch = Mathf.Lerp(cur.pitch, to.pitch, k);
            cur.yaw = Mathf.Lerp(cur.yaw, to.yaw, k);
            cur.roll = Mathf.Lerp(cur.roll, to.roll, k);
        }

        // ---------- 준비: 뼈 찾고, 견착 클립에서 기준 읽기 ----------
        private void Bind(Animator animator)
        {
            _bound = animator;
            _ready = false;
            _poseInit = false;
            _blend = 0f;

            Transform root = animator.transform;
            _chest = Find(root, "Spine2");
            _rUpper = Find(root, "RightArm");
            _rFore = Find(root, "RightForeArm");
            _rHand = Find(root, "RightHand");
            _lUpper = Find(root, "LeftArm");
            _lFore = Find(root, "LeftForeArm");
            _lHand = Find(root, "LeftHand");
            _rLeg = Find(root, "RightUpLeg");
            _lLeg = Find(root, "LeftUpLeg");
            _spine1 = Find(root, "Spine1");
            _neck = Find(root, "Neck");
            _head = Find(root, "Head");
            _twist = 0f;
            _idx1 = Find(root, "RightHandIndex1");
            _idx2 = Find(root, "RightHandIndex2");
            _idx3 = Find(root, "RightHandIndex3");
            _idx4 = Find(root, "RightHandIndex4");
            if (_chest == null || _rUpper == null || _rFore == null || _rHand == null || _lUpper == null || _lFore == null || _lHand == null || _rLeg == null || _lLeg == null)
            {
                Debug.LogWarning("[GunHoldIK] 믹사모 뼈(Spine2/팔/손/다리)를 못 찾아 IK 를 끕니다: " + animator.name);
                return;
            }

            AnimationClip aimClip = null, carryClip = null;
            foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
            {
                if (c == null) continue;
                if (aimClip == null && c.name.Contains(TemplateClip)) aimClip = c;
                if (carryClip == null && c.name.Contains("CarryIdle")) carryClip = c;
            }
            if (aimClip == null)
            {
                Debug.LogWarning("[GunHoldIK] 컨트롤러에 '" + TemplateClip + "' 클립이 없어 IK 를 끕니다. GunnerV2 세팅을 다시 돌려 주세요.");
                return;
            }

            // 견착 자세를 한 번 입혀서 기준을 읽고, 바로 원래 자세로 되돌림 (Update(0) = 시간 안 흐르게 다시 계산)
            aimClip.SampleAnimation(root.gameObject, 0f);
            float s = Mathf.Max(0.0001f, root.lossyScale.y);

            // 앞 방향 = (왼다리 -> 오른다리) 가로 방향에서 구함. 왼쪽이 -X 일 때 +Z 를 봄
            Vector3 right = _rLeg.position - _lLeg.position;
            right.y = 0f;
            if (right.sqrMagnitude < 1e-6f)
                right = root.right;
            Vector3 forward = Vector3.Cross(right.normalized, Vector3.up);
            // 견착 클립은 총이 캐릭터 정면을 곧게 겨눔 -> 총구(오른손->왼손) 수평 방향이 곧 앞. 다리 뼈로 구한 값은 부호가 뒤집힌 적이 있어 이걸 우선함
            Vector3 barrelFlat = _lHand.position - _rHand.position;
            barrelFlat.y = 0f;
            if (barrelFlat.sqrMagnitude > 1e-6f)
            {
                if (Vector3.Dot(barrelFlat.normalized, forward) < 0f)
                    Debug.Log("[GunHoldIK] 다리 기준 앞 방향이 총구와 반대여서 총구 기준으로 보정했습니다.");
                forward = barrelFlat.normalized;
            }
            _forwardLocal = root.InverseTransformDirection(forward);
            Quaternion frame = Quaternion.LookRotation(forward, Vector3.up);
            Quaternion frameInv = Quaternion.Inverse(frame);

            _aimHandPos = frameInv * (_rHand.position - _rUpper.position) / s;
            _aimHandRot = frameInv * _rHand.rotation;
            _chestRefInFrame = frameInv * _chest.rotation;

            // 총 축: 견착 자세에서 왼손은 총구 쪽(앞) 일직선 0.177 위치 -> 그 방향이 총구, 월드 위쪽이 총 위쪽
            Quaternion rInv = Quaternion.Inverse(_rHand.rotation);
            Vector3 lInR = rInv * (_lHand.position - _rHand.position) / s;
            _lHandRotInRHand = rInv * _lHand.rotation;
            _axLocal = lInR.normalized;
            _unit = Mathf.Max(0.0001f, lInR.magnitude / BlenderLeftGripAlong);
            Vector3 upL = rInv * Vector3.up;
            upL = Vector3.ProjectOnPlane(upL, _axLocal);
            _upLocal = upL.sqrMagnitude > 1e-6f ? upL.normalized : Vector3.ProjectOnPlane(Vector3.up, _axLocal).normalized;
            _gunBasisLocal = Quaternion.LookRotation(_axLocal, _upLocal);

            // 평소 상체(Carry)의 가슴 회전을 "흔들림 0" 기준으로 씀
            if (carryClip != null)
            {
                carryClip.SampleAnimation(root.gameObject, 0f);
                _chestRefInFrame = frameInv * _chest.rotation;
            }

            animator.Update(0f);
            _ready = true;
        }

        private static Transform Find(Transform root, string boneName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == boneName || t.name.EndsWith(":" + boneName, StringComparison.Ordinal))
                    return t;
            }
            return null;
        }

        // ---------- 매 프레임: 총 위치 정하고 -> 양손 목표 -> 팔 IK ----------
        private void Apply(Transform root, float w)
        {
            float s = Mathf.Max(0.0001f, root.lossyScale.y);
            float u = _unit * s;   // Blender 미터 -> 월드
            Vector3 forward = root.TransformDirection(_forwardLocal);
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                forward = root.forward;
            Quaternion frame = Quaternion.LookRotation(forward.normalized, Vector3.up);

            // 몸통 흔들림 = 평소 자세 대비 가슴이 더 돈 만큼 (비틀기 넣기 전에 잼)
            Quaternion sway = _chest.rotation * Quaternion.Inverse(frame * _chestRefInFrame);
            sway = Quaternion.Slerp(Quaternion.identity, sway, bodyFollow);

            // 팔 길이(고정) 와 왼손이 잡을 앞 손잡이 (오른손 뼈 기준 벡터)
            float rReach = (Vector3.Distance(_rUpper.position, _rFore.position) + Vector3.Distance(_rFore.position, _rHand.position)) * reachFraction;
            float lReach = (Vector3.Distance(_lUpper.position, _lFore.position) + Vector3.Distance(_lFore.position, _lHand.position)) * reachFraction;
            Vector3 gunRight = Vector3.Cross(_upLocal, _axLocal);
            Vector3 leftLocal = (_axLocal * leftGrip.x + _upLocal * leftGrip.y + gunRight * leftGrip.z) * u;

            // (1) 두 손이 닿을 때까지 상체를 오른쪽으로 비틀 각도 찾기 (팔이 짧아서 정면 상태로는 앞 손잡이에 못 닿음)
            Vector3 pivot = _chest.position;
            float needed = 0f;
            for (float a = 0f; a <= maxTwist + 0.01f; a += 4f)
            {
                Quaternion q = Quaternion.AngleAxis(a, Vector3.up);
                Vector3 rs = pivot + q * (_rUpper.position - pivot);
                Vector3 ls = pivot + q * (_lUpper.position - pivot);
                needed = a;
                Targets(rs, ls, frame, sway, u, s, leftLocal, rReach, lReach, false, out Vector3 g, out Quaternion rr, out Vector3 lp);
                if ((lp - ls).magnitude <= lReach * 1.01f && (g - rs).magnitude <= rReach * 1.01f)
                    break;
            }
            _twist = Mathf.Lerp(_twist, needed, 1f - Mathf.Exp(-poseSharpness * Time.deltaTime));

            // (2) 비틀기 적용: 척추 둘에 나눠서, 머리는 정면 유지
            float ang = _twist * w;
            if (ang > 0.01f)
            {
                if (_spine1 != null) _spine1.rotation = Quaternion.AngleAxis(ang * 0.4f, Vector3.up) * _spine1.rotation;
                _chest.rotation = Quaternion.AngleAxis(ang * 0.6f, Vector3.up) * _chest.rotation;
                if (_neck != null) _neck.rotation = Quaternion.AngleAxis(-ang * 0.5f, Vector3.up) * _neck.rotation;
                if (_head != null) _head.rotation = Quaternion.AngleAxis(-ang * 0.5f, Vector3.up) * _head.rotation;
            }

            // (3) 비튼 뒤 어깨 위치로 총 위치·양손 목표 다시 계산
            Targets(_rUpper.position, _lUpper.position, frame, sway, u, s, leftLocal, rReach, lReach, true, out Vector3 grip, out Quaternion rRot, out Vector3 lPos);
            Quaternion lRot = rRot * _lHandRotInRHand;
            Vector3 rPole = _rUpper.position + frame * (rightElbowHint * u);
            Vector3 lPole = _lUpper.position + frame * (leftElbowHint * u);

            Solve(_rUpper, _rFore, _rHand, grip, rRot, rPole, w);
            Solve(_lUpper, _lFore, _lHand, lPos, lRot, lPole, w);
            ExtendIndex(rRot * _axLocal, w);
        }

        // 어깨 위치가 주어졌을 때 총 손잡이(오른손) 위치/회전과 왼손 위치. 두 손이 닿는 범위 밖이면 총을 끌어당김
        private void Targets(Vector3 rShoulder, Vector3 lShoulder, Quaternion frame, Quaternion sway, float u, float s, Vector3 leftLocal,
            float rReach, float lReach, bool pull, out Vector3 grip, out Quaternion rRot, out Vector3 lPos)
        {
            // 낮춰 든 자세: 지정한 위치/방향
            Vector3 gripLow = rShoulder + frame * (_cur.hand * u);
            Quaternion gunLow = sway * frame * Quaternion.Euler(_cur.pitch, _cur.yaw, _cur.roll);
            Quaternion rotLow = gunLow * Quaternion.Inverse(_gunBasisLocal);
            // 견착 자세: 클립에서 읽은 것
            Vector3 gripAim = rShoulder + frame * (_aimHandPos * s);
            Quaternion rotAim = sway * frame * _aimHandRot;

            grip = Vector3.Lerp(gripLow, gripAim, _aim);
            rRot = Quaternion.Slerp(rotLow, rotAim, _aim);

            // 왼손 먼저, 방아쇠 손이 마지막이라 오른손이 우선. (비틀기 각도 탐색 땐 끌어당기지 않고 닿는지만 봄)
            for (int i = 0; pull && i < 4; i++)
            {
                Vector3 lw = grip + rRot * leftLocal;
                Vector3 toL = lw - lShoulder;
                if (toL.magnitude > lReach)
                    grip += lShoulder + toL.normalized * lReach - lw;
                Vector3 toR = grip - rShoulder;
                if (toR.magnitude > rReach)
                    grip = rShoulder + toR.normalized * rReach;
            }
            lPos = grip + rRot * leftLocal;
        }

        // 오른손 검지를 총구 방향(약간 아래 = 방아쇠 위)으로 폄. 나머지 손가락은 모션대로 쥠
        private void ExtendIndex(Vector3 barrel, float w)
        {
            if (_idx1 == null || _idx2 == null || _idx3 == null || _idx4 == null || indexExtend <= 0f)
                return;
            float t = indexExtend * w;
            Vector3 dir = barrel.normalized;
            Straighten(_idx1, _idx2, dir, t);
            Straighten(_idx2, _idx3, dir, t);
            Straighten(_idx3, _idx4, dir, t);
        }

        private static void Straighten(Transform bone, Transform child, Vector3 dir, float t)
        {
            Vector3 cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-10f)
                return;
            bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, dir), t) * bone.rotation;
        }

        // 어깨-팔꿈치-손목 2본 IK. 손 위치·회전을 목표에 맞추고 팔꿈치는 pole 쪽으로 꺾음. w 로 원래 모션과 섞음
        private static void Solve(Transform upper, Transform fore, Transform hand, Vector3 target, Quaternion handRot, Vector3 pole, float w)
        {
            Quaternion localUpper = upper.localRotation, localFore = fore.localRotation, localHand = hand.localRotation;

            Vector3 a = upper.position, b = fore.position, c = hand.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            if (l1 < 1e-4f || l2 < 1e-4f)
                return;

            Vector3 toTarget = target - a;
            float dist = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 0.001f, l1 + l2 - 0.001f);   // 팔 길이 밖은 최대한 뻗기만
            Vector3 dir = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : (c - a).normalized;

            float cosA = Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);

            Vector3 side = Vector3.ProjectOnPlane(pole - a, dir);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(b - a, dir);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(Vector3.down, dir);
            side.Normalize();

            Vector3 newElbow = a + (dir * cosA + side * sinA) * l1;
            upper.rotation = Quaternion.FromToRotation(b - a, newElbow - a) * upper.rotation;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, target - fore.position) * fore.rotation;
            hand.rotation = handRot;

            if (w < 0.999f)
            {
                upper.localRotation = Quaternion.Slerp(localUpper, upper.localRotation, w);
                fore.localRotation = Quaternion.Slerp(localFore, fore.localRotation, w);
                hand.localRotation = Quaternion.Slerp(localHand, hand.localRotation, w);
            }
        }
    }
}
