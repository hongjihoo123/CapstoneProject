using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotWeapons
{
    public class HitscanGunWeapon : WeaponBase, IAttackSpeedScalable, IAmmoDisplay, IAttackFeedbackSource,
        IEmpowerableShots, IShotHitSource, IShotVisualSource, IShotInterceptable, IMuzzleSource, IAnimationTimedShots
    {
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];
        private static readonly int WeakpointLayer = LayerMask.NameToLayer("Weakpoint");
        private static readonly IComparer<RaycastHit> DistanceComparer =
            Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        private readonly HitscanGunData data;
        private float fireCooldown;
        private int muzzleIndex;
        private float empoweredMultiplier = 1f;
        private readonly List<ShotProjectile> inFlight = new();
        private IAimPointProvider aimSource;

        // A shot that already spent its ammo and waits for the fire clip's Anim_Fire event.
        private bool shotQueued;
        private bool queuedEmpowered;
        private float queuedTimer;

        // A passive replaced the bullet and waits for its own clip's Anim_Fire: no new shots until then.
        private bool interceptHeld;
        private float interceptTimer;

        public float AttackSpeedMultiplier { get; set; } = 1f;
        public int EmpoweredShots { get; private set; }
        public override bool PrimaryIsHeld => true;

        public event Action<IDamageable, bool> ShotHit;
        public event Action<ShotVisual> ShotFired;
        public event Action<ShotProjectile> ProjectileLaunched;
        public event Action ShotIntercepted;
        public event Action ShotQueued;
        public event Action FireFrame;

        public bool WaitsForAnimation => data.fireOnAnimationEvent;

        public Func<Vector3, Vector3, bool> ShotInterceptor { get; set; }

        // The muzzle the next shot will use, at this moment.
        public Vector3 CurrentMuzzle
        {
            get
            {
                if (owner == null)
                    return Vector3.zero;

                Transform origin = owner.AimOrigin;
                return MuzzlePosition(origin, Facing(origin), muzzleIndex % MuzzleCount);
            }
        }

        public HitscanGunWeapon(HitscanGunData d) : base(d) { data = d; }

        public bool TryGetAmmoText(out string text)
        {
            text = AmmoText.ForResource(this);
            return true;
        }

        public AttackFeedback DescribeAttack(string animId) =>
            Array.IndexOf(data.fireAnimIds, animId) >= 0 ? new AttackFeedback { IsFire = true } : default;

        public void Empower(int shots, float damageMultiplier)
        {
            EmpoweredShots = Mathf.Max(EmpoweredShots, shots);
            empoweredMultiplier = damageMultiplier;
        }

        public override void Tick(float dt)
        {
            fireCooldown -= dt;
            TickQueuedShot(dt);
            TickReload(dt);
            TickProjectiles(dt);
        }

        public override void PrimaryAttack()
        {
            if (owner == null || IsReloading || fireCooldown > 0f || interceptHeld)
                return;

            if (CurrentResource <= 0f)
            {
                Reload();
                return;
            }

            fireCooldown = 1f / (Mathf.Max(0.01f, data.fireRate) * Mathf.Max(0.01f, AttackSpeedMultiplier));
            CurrentResource -= 1f;

            // A shot still waiting for its animation goes out before the next one is queued.
            ReleaseShot();

            bool empowered = EmpoweredShots > 0;
            if (empowered)
                EmpoweredShots--;

            if (WaitsForAnimation)
                QueueShot(empowered);
            else
                Fire(empowered, canIntercept: true);

            // No reload key in the top-down build: an empty magazine reloads by itself.
            if (CurrentResource <= 0f)
                Reload();
        }

        // Called by the fire clip's Anim_Fire event (normal fire clip or the passive's clip).
        public void OnFireFrame()
        {
            interceptHeld = false;
            FireFrame?.Invoke();
            ReleaseShot();
        }

        // Sends out the queued bullet: on the fire frame, or as a fallback on timeout / the next trigger pull.
        private void ReleaseShot()
        {
            if (!shotQueued)
                return;

            shotQueued = false;
            if (owner != null)
                Fire(queuedEmpowered, canIntercept: false);
        }

        private void QueueShot(bool empowered)
        {
            // The passive decides right at the trigger, so its own clip (PassiveShot) plays instead of the normal fire clip.
            Aim(muzzleIndex % MuzzleCount, out Vector3 start, out Vector3 direction);
            if (TryIntercept(start, direction))
            {
                muzzleIndex = (muzzleIndex + 1) % MuzzleCount;
                interceptHeld = true;
                interceptTimer = Mathf.Max(0f, data.interceptedShotTimeout);
                return;
            }

            shotQueued = true;
            queuedEmpowered = empowered;
            queuedTimer = Mathf.Max(0f, data.animationEventTimeout);
            ShotQueued?.Invoke();
        }

        private void TickQueuedShot(float dt)
        {
            // The passive's clip never reached Anim_Fire (interrupted, missing event): send the frame ourselves.
            if (interceptHeld)
            {
                interceptTimer -= dt;
                if (interceptTimer <= 0f)
                    OnFireFrame();
            }

            if (!shotQueued)
                return;

            queuedTimer -= dt;
            if (queuedTimer <= 0f)
                ReleaseShot();
        }

        private bool TryIntercept(Vector3 start, Vector3 direction)
        {
            // A passive can replace this shot with its own (e.g. the gunner's charged piercing shot).
            if (ShotInterceptor == null || !ShotInterceptor(start, direction))
                return false;

            ShotIntercepted?.Invoke();
            return true;
        }

        private int MuzzleCount => Mathf.Max(1, Mathf.Max(data.muzzleIds.Length, data.muzzleSideOffsets.Length));

        private static Vector3 Facing(Transform origin)
        {
            Vector3 facing = origin.forward;
            facing.y = 0f;
            return facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.forward;
        }

        // Muzzle position and flat aim direction for the given muzzle, read from the model right now.
        private void Aim(int muzzle, out Vector3 start, out Vector3 direction)
        {
            Transform origin = owner.AimOrigin;
            Vector3 facing = Facing(origin);
            start = MuzzlePosition(origin, facing, muzzle);
            direction = AimDirection(start, facing);
        }

        // canIntercept is false for a queued shot: the passive already had its say when the trigger was pulled.
        private void Fire(bool empowered, bool canIntercept)
        {
            int muzzle = muzzleIndex % MuzzleCount;
            muzzleIndex = (muzzleIndex + 1) % MuzzleCount;

            Aim(muzzle, out Vector3 start, out Vector3 direction);
            if (canIntercept && TryIntercept(start, direction))
                return;

            Color color = empowered ? data.empoweredFxColor : data.fxColor;

            if (muzzle < data.fireAnimIds.Length)
                RaiseAttackTriggered(data.fireAnimIds[muzzle]);

            if (data.projectileSpeed > 0f)
            {
                Launch(start, direction, empowered, color);
                return;
            }

            Vector3 end = start + direction * data.range;
            if (TryFindTarget(start, direction, data.range, out IDamageable target, out Vector3 point, out bool weakpoint, out bool blocked))
            {
                end = point;
                ApplyHit(target, weakpoint, empowered, point, direction);
            }
            else if (blocked)
            {
                end = point;
            }

            DrawTracer(start, end, empowered);
            ShotFired?.Invoke(new ShotVisual
            {
                Start = start,
                End = end,
                Hit = target != null,
                Empowered = empowered,
                Color = color
            });
        }

        // From the muzzle toward the cursor point. Flat, so shots stay at muzzle height.
        // Falls back to the facing when the cursor is on top of the character.
        private Vector3 AimDirection(Vector3 start, Vector3 facing)
        {
            if (aimSource == null && owner is Component component)
                aimSource = component.GetComponentInChildren<IAimPointProvider>();

            if (aimSource == null)
                return facing;

            Vector3 toAim = aimSource.AimPoint - start;
            toAim.y = 0f;
            return toAim.sqrMagnitude > 1f ? toAim.normalized : facing;
        }

        // The shot leaves the gun on the model and travels along the aim direction at the muzzle's height.
        private Vector3 MuzzlePosition(Transform origin, Vector3 direction, int muzzle)
        {
            if (muzzle < data.muzzleIds.Length && MuzzleSocket.TryFind(owner, data.muzzleIds[muzzle], out Transform socket))
                return socket.position;

            float side = muzzle < data.muzzleSideOffsets.Length ? data.muzzleSideOffsets[muzzle] : 0f;
            return origin.position + Vector3.Cross(Vector3.up, direction) * side;
        }

        private void ApplyHit(IDamageable target, bool weakpoint, bool empowered, Vector3 point, Vector3 direction)
        {
            HitPoint.Mark(target, point, direction);
            float damage = (data.damagePerShot + bonusDamage) * DamageMultiplier * (empowered ? empoweredMultiplier : 1f);
            owner.ApplyDamageTo(target, damage, weakpoint);
            RaiseDamage(damage);
            ShotHit?.Invoke(target, empowered);
        }

        private void Launch(Vector3 start, Vector3 direction, bool empowered, Color color)
        {
            var projectile = new ShotProjectile
            {
                Position = start,
                Direction = direction,
                Empowered = empowered,
                Color = color,
                Size = data.projectileSize * (empowered ? 1.4f : 1f)
            };

            inFlight.Add(projectile);
            ShotFired?.Invoke(new ShotVisual
            {
                Start = start,
                End = start + direction * data.range,
                Empowered = empowered,
                Color = color,
                Projectile = true
            });
            ProjectileLaunched?.Invoke(projectile);
        }

        private void TickProjectiles(float dt)
        {
            for (int i = inFlight.Count - 1; i >= 0; i--)
            {
                ShotProjectile projectile = inFlight[i];
                float step = Mathf.Min(data.projectileSpeed * dt, data.range - projectile.Travelled);

                if (owner == null)
                {
                    projectile.Alive = false;
                }
                else if (TryFindTarget(projectile.Position, projectile.Direction, step,
                        out IDamageable target, out Vector3 point, out bool weakpoint, out bool blocked))
                {
                    projectile.Position = point;
                    projectile.Hit = true;
                    projectile.Alive = false;
                    ApplyHit(target, weakpoint, projectile.Empowered, point, projectile.Direction);
                }
                else if (blocked)
                {
                    projectile.Position = point;
                    projectile.Alive = false;
                }
                else
                {
                    projectile.Position += projectile.Direction * step;
                    projectile.Travelled += step;
                    projectile.Alive = owner != null && projectile.Travelled < data.range - 0.001f;
                }

                if (!projectile.Alive)
                    inFlight.RemoveAt(i);
            }
        }

        public override void Unequip()
        {
            shotQueued = false;
            interceptHeld = false;
            foreach (ShotProjectile projectile in inFlight)
                projectile.Alive = false;
            inFlight.Clear();
            base.Unequip();
        }

        // Nearest thing along the shot that is either a living target or solid geometry.
        private bool TryFindTarget(Vector3 start, Vector3 direction, float distance, out IDamageable target, out Vector3 point,
            out bool weakpoint, out bool blocked)
        {
            target = null;
            point = start;
            weakpoint = false;
            blocked = false;

            if (distance <= 0f)
                return false;

            int mask = data.hitMask.value & ~AimUtility.IgnoreLayerMask.value;
            int count = Physics.SphereCastNonAlloc(start, data.shotRadius, direction, HitBuffer, distance, mask, QueryTriggerInteraction.Collide);
            Array.Sort(HitBuffer, 0, count, DistanceComparer);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = HitBuffer[i];
                IDamageable candidate = hit.collider.GetComponentInParent<IDamageable>();

                if (candidate != null)
                {
                    if (ReferenceEquals(candidate, owner) || !candidate.IsAlive)
                        continue;

                    target = candidate;
                    // distance 0 = already overlapping at the muzzle; hit.point is not valid then.
                    point = hit.distance > 0f ? hit.point : hit.collider.ClosestPoint(start);
                    weakpoint = hit.collider.gameObject.layer == WeakpointLayer;
                    return true;
                }

                if (hit.collider.isTrigger)
                    continue;

                blocked = true;
                point = hit.distance > 0f ? hit.point : start;
                return false;
            }

            return false;
        }

        private void DrawTracer(Vector3 start, Vector3 end, bool empowered)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.01f)
                return;

            float width = empowered ? data.empoweredTracerWidth : data.tracerWidth;
            AttackAreaBus.Raise(AttackArea.Box(
                empowered ? AttackAreaKind.Skill : AttackAreaKind.WeaponHit,
                start + delta * 0.5f,
                Quaternion.LookRotation(delta),
                new Vector3(width, 0.05f, length),
                data.tracerDuration));
        }
    }
}
