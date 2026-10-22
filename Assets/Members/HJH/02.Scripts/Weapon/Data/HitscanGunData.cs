using UnityEngine;

namespace RobotWeapons
{
    // Instant-hit gun for the top-down characters: fires along the flat aim direction,
    // damages the first target in line, auto-reloads when the magazine runs dry.
    [CreateAssetMenu(menuName = "Weapon/Hitscan Gun Data", fileName = "New HitscanGunData")]
    public class HitscanGunData : WeaponData
    {
        public float damagePerShot = 8f;
        [Tooltip("Shots per second at attack speed 1.")]
        public float fireRate = 8f;
        public float range = 14f;
        [Tooltip("Shot thickness. A little width keeps top-down aiming forgiving.")]
        public float shotRadius = 0.25f;
        public LayerMask hitMask = ~0;

        [Header("Muzzles")]
        [Tooltip("One entry per hand/barrel, fired in turn. Shots start at the MuzzleSocket with this id under the player.")]
        public string[] muzzleIds = { "Pistol_L", "Pistol_R" };
        [Tooltip("Fallback when no matching MuzzleSocket exists: sideways offset from the aim origin (same order).")]
        public float[] muzzleSideOffsets = { -0.25f, 0.25f };
        [Tooltip("Animation id per muzzle (same order). The animator plays the matching clip.")]
        public string[] fireAnimIds = { "Pistol_FireL", "Pistol_FireR" };

        [Header("Feedback")]
        public float tracerWidth = 0.08f;
        public float empoweredTracerWidth = 0.25f;
        public float tracerDuration = 0.05f;
        public Color fxColor = new Color(1f, 0.8f, 0.35f);
        public Color empoweredFxColor = new Color(0.3f, 1f, 0.85f);

        [Header("Projectile")]
        [Tooltip("> 0: shots are real projectiles that travel at this speed and deal damage on contact. 0 = instant hit.")]
        public float projectileSpeed;
        public float projectileSize = 0.45f;
    }
}
