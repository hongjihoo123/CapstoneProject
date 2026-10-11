using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.KYR._01_Scripts;
using Members.KYR._01_Scripts.Stats;
using RobotWeapons;
using UnityEngine;
using UnityEngine.AI;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // ISkillContext for the player: adapts PlayerAgent / PlayerMover / weapon / stats / HitFeel to the skill API.
    public sealed class PlayerSkillContext : ISkillContext
    {
        private static readonly IReadOnlyList<IDamageable> NoTargets = new List<IDamageable>();
        private static readonly Collider[] OverlapBuffer = new Collider[64];

        private readonly PlayerAgent _player;
        private readonly LayerMask _targetMask;
        private readonly LayerMask _environmentMask;
        private readonly HashSet<IDamageable> _hitThisActivation = new();

        public PlayerSkillContext(PlayerAgent player, LayerMask targetMask, LayerMask environmentMask)
        {
            _player = player;
            _targetMask = targetMask;
            _environmentMask = environmentMask;
        }

        public Transform Transform => _player.transform;
        public IWeapon Weapon => _player.Weapon.Weapon;

        public Vector3 MoveInputDirection
        {
            get
            {
                PlayerInputState input = _player.Input;
                return input.HasMoveInput ? _player.Mover.ToWorldMove(input.Move) : Vector3.zero;
            }
        }

        public Vector3 AimDirection
        {
            get
            {
                Vector3 direction = _player.AimDirection;
                direction.y = 0f;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            }
        }

        public Vector3 AimPoint => _player.AimPoint;

        public bool IsDashing => _player.Mover.IsDashing;

        public bool StatsReady => _player.Stats != null && _player.Stats.Tree != null;

        public IPassive Passive { get; set; }

        public void BeginActivation() => _hitThisActivation.Clear();

        public void Dash(Vector3 direction, float speed, float duration, float endSlowdown = 0f) =>
            _player.Mover.Dash(direction, speed, duration, endSlowdown);

        public void SetSpinEffect(GameObject prefab, bool active, float radius)
        {
            SkillSpinEffect effect = _player.GetComponent<SkillSpinEffect>();
            if (effect == null)
            {
                if (!active)
                    return;

                effect = _player.gameObject.AddComponent<SkillSpinEffect>();
            }

            if (active)
                effect.Play(prefab, radius);
            else
                effect.Stop();
        }

        public void FlashRange(float radius, float duration) =>
            AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.Skill, _player.transform.position + Vector3.up, radius, duration));

        public void Run(ISkillTimedEffect effect)
        {
            if (effect == null)
                return;

            if (!_player.TryGetComponent(out SkillEffectRunner runner))
                runner = _player.gameObject.AddComponent<SkillEffectRunner>();

            runner.Add(effect);
        }

        public void PlayHitFeel(float hitStop, float shake) => HitFeel.Play(hitStop, shake);

        public void PlayShake(float amplitude, float duration) => HitFeel.Shake(amplitude, duration);

        public void PlayCameraKick(Vector3 offset, float duration = 0.22f) => HitFeel.Kick(offset, duration);

        public void Heal(float amount)
        {
            if (amount > 0f)
                _player.Heal(amount);
        }

        public void SetScriptedMotion(Vector3 velocity) => _player.Mover.SetScriptedMotion(velocity);

        public void SetScriptedPosition(Vector3 position) => _player.Mover.SetScriptedPosition(position);

        public void CancelDash() => _player.Mover.CancelDash();

        // Capsule sweep against ground/walls only, so enemies and anything on other layers are ignored.
        public bool SweepEnvironment(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.0001f)
                return false;

            GetCapsule(from, out Vector3 bottom, out Vector3 top, out float radius);
            return Physics.CapsuleCast(bottom, top, radius, delta / distance, distance, _environmentMask, QueryTriggerInteraction.Ignore);
        }

        public bool TryFindGround(Vector3 from, out Vector3 ground)
        {
            if (Physics.Raycast(from + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 200f, _environmentMask, QueryTriggerInteraction.Ignore))
            {
                ground = hit.point;
                return true;
            }

            ground = from;
            return false;
        }

        public bool LineBlocked(Vector3 from, Vector3 to, out Vector3 hitPoint)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance > 0.0001f
                && Physics.Raycast(from, delta / distance, out RaycastHit hit, distance, _environmentMask, QueryTriggerInteraction.Ignore))
            {
                hitPoint = hit.point;
                return true;
            }

            hitPoint = to;
            return false;
        }

        public void PushAway(Vector3 center, float radius)
        {
            foreach (IDamageable target in OverlapSphere(center + Vector3.up, radius))
            {
                if (target is not Component component)
                    continue;

                Transform root = component.transform;
                Vector3 offset = root.position - center;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance >= radius)
                    continue;

                Vector3 direction = distance > 0.0001f ? offset / distance : -_player.transform.forward;
                MoveTarget(root, direction * (radius - distance));
            }
        }

        // Drags enemies toward the center by up to maxDistance, stopping just short of it.
        public void PullToward(Vector3 center, float radius, float maxDistance)
        {
            const float stopDistance = 0.8f;

            foreach (IDamageable target in OverlapSphere(center + Vector3.up, radius))
            {
                if (target is not Component component)
                    continue;

                Transform root = component.transform;
                Vector3 offset = center - root.position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance <= stopDistance)
                    continue;

                MoveTarget(root, offset / distance * Mathf.Min(maxDistance, distance - stopDistance));
            }
        }

        private static void MoveTarget(Transform root, Vector3 delta)
        {
            if (root.TryGetComponent(out NavMeshAgent agent) && agent.enabled && agent.isOnNavMesh)
                agent.Move(delta);
            else if (root.TryGetComponent(out CharacterController controller) && controller.enabled)
                controller.Move(delta);
            else if (root.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                body.MovePosition(body.position + delta);
            else
                root.position += delta;
        }

        private void GetCapsule(Vector3 position, out Vector3 bottom, out Vector3 top, out float radius)
        {
            CharacterController body = _player.Mover.Body;
            radius = body.radius * 0.9f;
            Vector3 center = position + body.center;
            float half = Mathf.Max(0f, body.height * 0.5f - body.radius);

            // Lifted slightly so standing on the ground at the destination does not count as a hit.
            bottom = center - Vector3.up * half + Vector3.up * 0.05f;
            top = center + Vector3.up * half;
        }

        public bool TryRegisterHit(IDamageable target) => _hitThisActivation.Add(target);

        public IReadOnlyList<IDamageable> OverlapMelee()
        {
            SkillOverlapHitbox hitbox = _player.Weapon.SkillOverlapHitbox;
            if (hitbox == null)
            {
                Debug.LogWarning($"{_player.name}의 PlayerWeapon에 Skill Overlap Hitbox가 연결돼있지 않습니다.");
                return NoTargets;
            }

            return hitbox.Overlap();
        }

        public IReadOnlyList<IDamageable> OverlapSphere(Vector3 center, float radius)
        {
            var results = new List<IDamageable>();
            int count = Physics.OverlapSphereNonAlloc(center, radius, OverlapBuffer, _targetMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                IDamageable target = OverlapBuffer[i].GetComponentInParent<IDamageable>();
                if (target != null && !ReferenceEquals(target, _player) && target.IsAlive && !results.Contains(target))
                    results.Add(target);
            }

            return results;
        }

        public IReadOnlyList<IDamageable> OverlapCapsule(Vector3 from, Vector3 to, float radius)
        {
            var results = new List<IDamageable>();
            int count = Physics.OverlapCapsuleNonAlloc(from, to, radius, OverlapBuffer, _targetMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                IDamageable target = OverlapBuffer[i].GetComponentInParent<IDamageable>();
                if (target != null && !ReferenceEquals(target, _player) && target.IsAlive && !results.Contains(target))
                    results.Add(target);
            }

            results.Sort((a, b) => DistanceFrom(from, a).CompareTo(DistanceFrom(from, b)));
            return results;
        }

        private static float DistanceFrom(Vector3 point, IDamageable target) =>
            target is Component component ? (component.transform.position - point).sqrMagnitude : float.MaxValue;

        public void DealDamage(IDamageable target, float amount, bool isWeakpoint = false) =>
            _player.ApplyDamageTo(target, amount, isWeakpoint);

        public void ApplyDamageOverTime(IDamageable target, float damagePerTick, float tickInterval, float duration)
        {
            StatusEffectModule status = _player.GetModule<StatusEffectModule>();
            status?.ApplyDamageOverTime(target, damagePerTick, tickInterval, duration, _player.gameObject);
        }

        public void AddStatModifier(PlayerStatId id, StatModifier modifier) => _player.Stats.AddModifier(id, modifier);

        public void RemoveStatModifiers(object source) => _player.Stats.RemoveModifiers(source);
    }
}
