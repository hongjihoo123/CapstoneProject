using System.Collections.Generic;
using Members.KYR._01.Scripts.Modules;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Assets.Members.HJH._02.Scripts.Char.Movement
{
    // 대시 중에는 적을 뚫고 지나감 (적이 옆으로 살짝 밀림). 대시가 끝났을 때 적과 겹쳐 있으면 가까운 빈자리로 빼 줌.
    //  - 레이어 충돌 매트릭스는 안 건드림: 플레이어 CharacterController 의 excludeLayers 에 적 레이어를 대시 동안만 넣었다 뺌
    //  - 대시 자체(속도·시간·방향)는 기존 PlayerMover / 스킬 쪽 그대로. 여기는 IsDashing 을 보기만 함
    [DefaultExecutionOrder(50)]
    public sealed class DashPassThrough : MonoBehaviour
    {
        [SerializeField, Tooltip("대시 중 통과할 레이어 (적)")] private LayerMask passThroughLayers;

        [Header("대시 끝 위치 보정")]
        [SerializeField, Tooltip("빈자리를 찾을 최대 거리 (m)")] private float maxResolveDistance = 3f;
        [SerializeField, Tooltip("찾을 때 거리 간격 (m)")] private float resolveStep = 0.2f;
        [SerializeField, Range(0f, 1f), Tooltip("빈자리가 여러 개면 대시 진행 방향 쪽을 얼마나 더 선호할지 (1 = 앞쪽 우선)")]
        private float preferForward = 0.6f;
        [SerializeField, Tooltip("빈자리 판정 때 몸 반지름에 더하는 여유 (m)")] private float clearance = 0.05f;

        [Header("지나가는 적 옆으로 밀기 (연출)")]
        [SerializeField] private bool shoveEnemies = true;
        [SerializeField, Tooltip("옆으로 밀리는 거리 (m). 약하게")] private float shoveDistance = 0.6f;
        [SerializeField, Tooltip("밀리는 데 걸리는 시간 (초)")] private float shoveTime = 0.15f;
        [SerializeField, Tooltip("몸 반지름에 더해 이만큼 가까운 적을 밀기 (m)")] private float shoveRadiusPadding = 0.35f;

        private struct Shove
        {
            public NavMeshAgent Agent;
            public Transform Root;
            public Vector3 Total;
            public float Elapsed;
        }

        private static readonly Collider[] Overlaps = new Collider[32];
        private readonly HashSet<Transform> _shovedThisDash = new();
        private readonly List<Shove> _shoves = new();

        private PlayerAgent _player;
        private CharacterController _body;
        private bool _wasDashing;
        private int _originalExclude;
        private Vector3 _lastPosition;
        private Vector3 _dashDirection;
        private int _blockingMask;

        private void Awake()
        {
            _player = GetComponent<PlayerAgent>();
            if (passThroughLayers.value == 0)
                passThroughLayers = LayerMask.GetMask("Enemy");
        }

        private void Reset() => passThroughLayers = LayerMask.GetMask("Enemy");

        private PlayerMover Mover => _player != null ? _player.Mover : null;

        private void Update()
        {
            PlayerMover mover = Mover;
            if (mover == null || mover.Body == null)
                return;

            if (_body != mover.Body)
            {
                _body = mover.Body;
                _blockingMask = BlockingMask(_body.gameObject.layer) & ~passThroughLayers.value;
            }

            bool dashing = mover.IsDashing;
            if (dashing && !_wasDashing)
                BeginDash();
            else if (!dashing && _wasDashing)
                EndDash();

            if (dashing)
            {
                Vector3 step = transform.position - _lastPosition;
                step.y = 0f;
                if (step.sqrMagnitude > 0.000001f)
                    _dashDirection = step.normalized;
                if (shoveEnemies)
                    FindEnemiesToShove();
            }

            _lastPosition = transform.position;
            TickShoves(Time.deltaTime);
            _wasDashing = dashing;
        }

        private void OnDisable()
        {
            if (_wasDashing && _body != null)
                _body.excludeLayers = _originalExclude;
            _wasDashing = false;
        }

        private void BeginDash()
        {
            _originalExclude = _body.excludeLayers.value;
            _body.excludeLayers = _originalExclude | passThroughLayers.value;
            _shovedThisDash.Clear();
            _dashDirection = transform.forward;
            _lastPosition = transform.position;
        }

        private void EndDash()
        {
            _body.excludeLayers = _originalExclude;
            ResolveOverlap();
        }

        // ------------------------------------------------ 대시 끝: 겹침 풀기

        private void ResolveOverlap()
        {
            Vector3 origin = transform.position;
            if (IsFree(origin, passThroughLayers.value))
                return;

            // 가까운 거리부터 16방향을 훑어서, 적·벽과 안 겹치고 발밑에 땅이 있는 첫 자리
            Vector3 forward = _dashDirection.sqrMagnitude > 0.0001f ? _dashDirection : transform.forward;
            const int Directions = 16;
            for (float distance = resolveStep; distance <= maxResolveDistance + 0.001f; distance += resolveStep)
            {
                Vector3 best = Vector3.zero;
                float bestScore = float.MaxValue;
                for (int i = 0; i < Directions; i++)
                {
                    float angle = i * (360f / Directions);
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
                    Vector3 candidate = origin + dir * distance;
                    if (!IsFree(candidate, passThroughLayers.value | _blockingMask) || !HasGround(candidate))
                        continue;

                    float score = -Vector3.Dot(dir, forward) * preferForward;   // 같은 거리면 앞쪽
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }

                if (bestScore < float.MaxValue)
                {
                    Mover.Teleport(best);
                    return;
                }
            }
        }

        private bool IsFree(Vector3 position, int mask)
        {
            GetCapsule(position, out Vector3 bottom, out Vector3 top, out float radius);
            return !Physics.CheckCapsule(bottom, top, radius + clearance, mask, QueryTriggerInteraction.Ignore);
        }

        private bool HasGround(Vector3 position)
        {
            float half = _body.height * 0.5f;
            Vector3 from = position + _body.center;
            return Physics.Raycast(from, Vector3.down, half + _body.stepOffset + 0.6f, _blockingMask, QueryTriggerInteraction.Ignore);
        }

        private void GetCapsule(Vector3 position, out Vector3 bottom, out Vector3 top, out float radius)
        {
            radius = _body.radius;
            Vector3 center = position + _body.center;
            float half = Mathf.Max(0f, _body.height * 0.5f - radius);
            bottom = center - Vector3.up * half + Vector3.up * (_body.stepOffset + 0.02f);   // 발밑 땅은 안 걸리게
            top = center + Vector3.up * half;
            if (top.y < bottom.y)
                top = bottom;
        }

        // 플레이어 레이어와 충돌하는 레이어 (충돌 매트릭스 기준) = 대시 끝 자리를 막는 것들
        private static int BlockingMask(int layer)
        {
            int mask = 0;
            for (int i = 0; i < 32; i++)
                if (!Physics.GetIgnoreLayerCollision(layer, i))
                    mask |= 1 << i;
            return mask & ~(1 << layer);
        }

        // ------------------------------------------------ 지나가는 적 밀기

        private void FindEnemiesToShove()
        {
            GetCapsule(transform.position, out Vector3 bottom, out Vector3 top, out float radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius + shoveRadiusPadding, Overlaps, passThroughLayers.value, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform root = Overlaps[i].attachedRigidbody != null ? Overlaps[i].attachedRigidbody.transform : Overlaps[i].transform;
                NavMeshAgent agent = root.GetComponentInParent<NavMeshAgent>();
                if (agent != null)
                    root = agent.transform;
                if (!_shovedThisDash.Add(root))
                    continue;

                // 대시 방향에 수직으로, 적이 있는 쪽으로 (정면이면 오른쪽)
                Vector3 offset = root.position - transform.position;
                offset.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, _dashDirection);
                float sign = Vector3.Dot(offset, side) >= 0f ? 1f : -1f;
                _shoves.Add(new Shove { Agent = agent, Root = root, Total = side * (sign * shoveDistance) });
            }
        }

        private void TickShoves(float deltaTime)
        {
            for (int i = _shoves.Count - 1; i >= 0; i--)
            {
                Shove shove = _shoves[i];
                if (shove.Root == null)
                {
                    _shoves.RemoveAt(i);
                    continue;
                }

                float before = shoveTime > 0f ? EaseOut(shove.Elapsed / shoveTime) : 1f;
                shove.Elapsed += deltaTime;
                float after = shoveTime > 0f ? EaseOut(Mathf.Clamp01(shove.Elapsed / shoveTime)) : 1f;
                Vector3 delta = shove.Total * (after - before);

                if (shove.Agent != null && shove.Agent.enabled && shove.Agent.isOnNavMesh)
                    shove.Agent.Move(delta);   // 내비메시 밖으로는 안 나감
                else if (shove.Agent == null)
                    shove.Root.position += delta;

                if (after >= 1f)
                    _shoves.RemoveAt(i);
                else
                    _shoves[i] = shove;
            }
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
