using System;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // The one place gameplay code asks for effects. Every effect is a prefab in FxLibrary;
    // this class only decides where it goes, how big, which color, and passes runtime values (FxParams).
    // An empty library slot just plays nothing.
    public static class Fx
    {
        private static FxLibrary Library => FxLibrary.Loaded;

        // ------------------------------------------------------------- combat

        public static void Hit(Vector3 position, Vector3 direction, Color color, bool big) =>
            Play(Library?.hit, position, Facing(direction), big ? 1.5f : 1f, color);

        public static void Muzzle(Vector3 position, Vector3 direction, Color color, bool big) =>
            Play(Library?.muzzle, position, Facing(direction), big ? 1.6f : 1f, color);

        // power > 1 makes the blast a bit larger than its damage radius reads.
        public static void Explosion(Vector3 center, float radius, Color color, float power = 1f) =>
            Play(Library?.explosion, center, Quaternion.identity, radius * Mathf.Lerp(1f, 1.25f, Mathf.Clamp01(power - 1f)), color);

        public static void Shockwave(Vector3 center, Color color, float radius) =>
            Play(Library?.shockwave, center, Quaternion.identity, radius, color);

        public static void GroundSlam(Vector3 center, Color color, float radius) =>
            Play(Library?.groundSlam, center, Quaternion.identity, radius, color);

        public static void DamageNumber(Vector3 position, float amount, Color color, float scale = 1f) =>
            Play(Library?.damageNumber, position, Quaternion.identity, 1f, color, new FxParams { Value = amount, Radius = scale });

        public static void CharacterSwap(Vector3 center, Color color) =>
            Play(Library?.characterSwap, center, Quaternion.identity, 1f, color);

        // ------------------------------------------------------------- weapons and projectiles

        public static void Tracer(Vector3 from, Vector3 to, Color color, float width, float duration = 0.07f) =>
            Play(Library?.tracer, from, Quaternion.identity, 1f, color, new FxParams { Start = from, End = to, Width = width, Duration = duration });

        // Held thin line (aim guides). Re-aim with SetPoints, Kill when done.
        public static FxHandle AimLine(Vector3 from, Vector3 to, Color color, float width) =>
            Hold(Library?.tracer, from, Quaternion.identity, 1f, color, new FxParams { Start = from, End = to, Width = width, Duration = 0f });

        public static void Swipe(Transform origin, float radius, SwingShape shape, Color color, float delay, float duration) =>
            Play(Library?.swipe, origin.position, Quaternion.identity, 1f, color,
                new FxParams { Follow = origin, Radius = radius, Shape = shape, Delay = delay, Duration = duration });

        // A blade slash on its own (skills): faces forward, tilt rolls the slash plane, mirror flips the sweep direction.
        public static void Slash(Vector3 position, Vector3 forward, float radius, bool mirror = false, float tilt = 0f, float speed = 1f)
        {
            Quaternion rotation = Facing(new Vector3(forward.x, 0f, forward.z)) * Quaternion.Euler(0f, 0f, tilt);
            Play(Library?.slash, position, rotation, 1f, Color.white,
                new FxParams { Radius = radius, Value = speed, Shape = new SwingShape { clockwise = !mirror } });
        }

        public static FxHandle Orb(Vector3 position, Color color, float size) =>
            Hold(Library?.orb, position, Quaternion.identity, size, color);

        public static void ProjectileImpact(Vector3 position, Vector3 direction, Color color, float size, bool hit) =>
            Play(Library?.projectileImpact, position, Facing(-direction), size * (hit ? 1.4f : 0.8f), color);

        // ------------------------------------------------------------- movement

        // Value 1 = drawing, 0 = paused.
        public static FxHandle DashTrail(Transform target, Color color) =>
            Hold(Library?.dashTrail, target.position, Quaternion.identity, 1f, color,
                new FxParams { Follow = target, Offset = Vector3.up * 0.9f, Value = 0f });

        public static void TeleportStreak(Vector3 from, Vector3 to, Color color) =>
            Play(Library?.teleportStreak, from, Quaternion.identity, 1f, color, new FxParams { Start = from, End = to, Width = 0.5f, Duration = 0.2f });

        public static void BlinkOut(Vector3 position, Color color) => Play(Library?.blinkOut, position, Quaternion.identity, 1f, color);

        public static void BlinkIn(Vector3 position, Vector3 direction, Color color) =>
            Play(Library?.blinkIn, position, Facing(direction), 1f, color);

        // ------------------------------------------------------------- skills

        // Held charge-up at a point. SetValue(0..1) as it charges, MoveTo to follow the muzzle.
        public static FxHandle Charge(Vector3 position, Color color) =>
            Hold(Library?.charge, position, Quaternion.identity, 1f, color);

        public static void Beam(Vector3 from, Vector3 to, Color color, float width, float duration = 0.3f) =>
            Play(Library?.beam, from, Quaternion.identity, 1f, color, new FxParams { Start = from, End = to, Width = width, Duration = duration });

        // Held energy barrel (gunner R). SetPoints to follow, SetValue(0..1) as it charges, Kill when it fires.
        public static FxHandle EnergyCannon(Vector3 start, Vector3 tip, Color color, float width) =>
            Hold(Library?.energyCannon, start, Quaternion.identity, 1f, color, new FxParams { Start = start, End = tip, Width = width, Duration = 0f, Value = 0.35f });

        public static void CannonMuzzle(Vector3 position, Vector3 direction, Color color) =>
            Play(Library?.cannonMuzzle, position, Facing(direction), 1f, color);

        // Held circle. SetValue = fill 0..1, SetSpeed = rotation (deg/s).
        public static FxHandle MagicCircle(Vector3 center, float radius, Color color) =>
            Hold(Library?.magicCircle, center + Vector3.up * 0.07f, Quaternion.identity, radius * 2f, color);

        public static FxHandle Pull(Vector3 center, float radius, Color color) =>
            Hold(Library?.pull, center, Quaternion.identity, 1f, color, new FxParams { Radius = radius });

        public static void PullBurst(Vector3 center, float radius, Color color) =>
            Play(Library?.pullBurst, center, Quaternion.identity, 1f, color, new FxParams { Radius = radius });

        public static void RuneCast(Vector3 position, Vector3 direction, Color color, int runes) =>
            Play(Library?.runeCast, position, Facing(direction), 1f + runes * 0.2f, color);

        public static void RuneGain(Vector3 position, Color color) => Play(Library?.runeGain, position, Quaternion.identity, 1f, color);

        // ------------------------------------------------------------- passives

        public static void Lifesteal(Vector3 from, Vector3 to, Color color) =>
            Play(Library?.lifesteal, from, Facing(to - from), 1f, color, new FxParams { Start = from, End = to });

        public static void FrenzyStack(Vector3 center, Color color, int stacks) =>
            Play(Library?.frenzyStack, center, Quaternion.identity, 1f + stacks * 0.25f, color);

        // Held on the player. SetValue(stacks).
        public static FxHandle FrenzyAura(Transform target, Color color, int stacks) =>
            Hold(Library?.frenzyAura, target.position, Quaternion.identity, 1f, color, new FxParams { Follow = target, Value = stacks });

        // ------------------------------------------------------------- not effects, but timing helpers

        public static void Punch(Transform target, Vector3 direction, float strength) => PunchFx.Play(target, direction, strength);

        // Runs an action after a delay on scaled time (frozen during hit stop like the effects).
        public static void After(float delay, Action action) => FxRunner.Instance.Add(new DelayFx(delay, action));

        // ------------------------------------------------------------- plumbing

        private static void Play(GameObject prefab, Vector3 position, Quaternion rotation, float scale, Color color, FxParams parameters = default)
        {
            if (prefab == null)
                return;

            parameters.Tint = color;
            FxPrefabInstance.Spawn(prefab, position, rotation, scale, parameters);
        }

        private static FxHandle Hold(GameObject prefab, Vector3 position, Quaternion rotation, float scale, Color color, FxParams parameters = default)
        {
            parameters.Tint = color;
            FxPrefabInstance instance = prefab != null ? FxPrefabInstance.Spawn(prefab, position, rotation, scale, parameters, held: true) : null;
            return new FxHandle(instance, position);
        }

        private static Quaternion Facing(Vector3 direction) =>
            direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;

        private sealed class DelayFx : FxInstance
        {
            private readonly Action _action;
            private float _remaining;

            public DelayFx(float delay, Action action)
            {
                _remaining = delay;
                _action = action;
            }

            public override bool Tick(float deltaTime)
            {
                _remaining -= deltaTime;
                if (_remaining > 0f)
                    return true;

                _action?.Invoke();
                return false;
            }
        }
    }
}
