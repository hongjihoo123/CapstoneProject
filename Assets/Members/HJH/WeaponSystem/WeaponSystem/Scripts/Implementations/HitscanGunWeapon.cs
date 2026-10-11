using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotWeapons
{
    public class HitscanGunWeapon : WeaponBase, IAttackSpeedScalable, IAmmoDisplay, IAttackFeedbackSource,
        IEmpowerableShots, IShotHitSource, IShotVisualSource
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

        public float AttackSpeedMultiplier { get; set; } = 1f;
        public int EmpoweredShots { get; private set; }
        public override bool PrimaryIsHeld => true;

        public event Action<IDamageable, bool> ShotHit;
        public event Action<ShotVisual> ShotFired;
        public event Action<ShotProjectile> ProjectileLaunched;

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
            TickReload(dt);
            TickProjectiles(dt);
        }

        public override void PrimaryAttack()
        {
            if (owner == null || IsReloading || fireCooldown > 0f)
                return;

            if (CurrentResource <= 0f)
            {
                Reload();
                return;
            }

            fireCooldown = 1f / (Mathf.Max(0.01f, data.fireRate) * Mathf.Max(0.01f, AttackSpeedMultiplier));
            CurrentResource -= 1f;

            bool empowered = EmpoweredShots > 0;
            if (empowered)
                EmpoweredShots--;

            Fire(empowered);

            // No reload key in the top-down build: an empty magazine reloads by itself.
            if (CurrentResource <= 0f)
                Reload();
        }

        private void Fire(bool empowered)
        {
            Transform origin = owner.AimOrigin;
            Vector3 facing = origin.forward;
            facing.y = 0f;
            facing = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.forward;

            int muzzleCount = Mathf.Max(1, Mathf.Max(data.muzzleIds.Length, data.muzzleSideOffsets.Length));
            int muzzle = muzzleIndex % muzzleCount;
            muzzleIndex = (muzzleIndex + 1) % muzzleCount;

            Vector3 start = MuzzlePosition(origin, facing, muzzle);
            Vector3 direction = AimDirection(start, facing);
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

        // From the muzzle toward the cursor point (both guns converge on it). Flat, so shots stay at muzzle height.
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

        // ------------------------------------------------------------- travelling shots

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
