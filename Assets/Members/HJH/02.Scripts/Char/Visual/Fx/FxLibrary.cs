using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Every effect in the game, one prefab each (Particle System / Line / Trail / Mesh + Shader Graph materials).
    // Swap a slot to change that effect everywhere. Lives in Resources so Fx finds it without scene setup.
    // "Authored for radius/size 1" = the prefab root is scaled by the runtime radius/size.
    [CreateAssetMenu(menuName = "HJH/FX Library")]
    public class FxLibrary : ScriptableObject
    {
        public const string ResourcePath = "HJH_FxLibrary";

        [Header("Combat (every character)")]
        [Tooltip("+Z = hit direction.")] public GameObject hit;
        [Tooltip("+Z = shot direction.")] public GameObject muzzle;
        [Tooltip("Authored for radius 1.")] public GameObject explosion;
        [Tooltip("Authored for radius 1.")] public GameObject shockwave;
        [Tooltip("Ring + dust + debris. Authored for radius 1.")] public GameObject groundSlam;
        [Tooltip("Text. Value = damage, Radius = size.")] public GameObject damageNumber;
        public GameObject characterSwap;

        [Header("Weapons and projectiles")]
        [Tooltip("FxLine. Instant shot line.")] public GameObject tracer;
        [Tooltip("Melee swing. Gets Follow + SwingShape (FxSlash mesh slash and/or FxSwipe ribbon).")] public GameObject swipe;
        [Tooltip("One-shot slash at a point (FxSlash). Radius = reach, Value = playback speed.")] public GameObject slash;
        [Tooltip("Held, moved every frame. Authored for size 1.")] public GameObject orb;
        [Tooltip("Where a projectile ends. Authored for size 1.")] public GameObject projectileImpact;

        [Header("Movement")]
        [Tooltip("FxFollow + FxTrailToggle on the player.")] public GameObject dashTrail;
        [Tooltip("FxLine between the old and new position.")] public GameObject teleportStreak;
        public GameObject blinkOut;
        public GameObject blinkIn;

        [Header("Skills")]
        [Tooltip("Held at the muzzle while charging. Value 0..1 = charge.")] public GameObject charge;
        [Tooltip("FxLine. Big piercing beam.")] public GameObject beam;
        [Tooltip("Held FxLine barrel in front of the pistols (R charge). Value 0..1 = charge (thickness), SetPoints follows.")]
        public GameObject energyCannon;
        [Tooltip("+Z = shot direction.")] public GameObject cannonMuzzle;
        [Tooltip("FxMagicCircleDriver. Value = fill, Speed = rotation. Authored for diameter 1.")] public GameObject magicCircle;
        [Tooltip("Held. FxReach Inward, Radius = pull radius.")] public GameObject pull;
        [Tooltip("One burst of pull particles. Radius = pull radius.")] public GameObject pullBurst;
        [Tooltip("+Z = cast direction. Scaled by rune count.")] public GameObject runeCast;
        public GameObject runeGain;

        [Header("Passives")]
        [Tooltip("FxReach Toward: Start = target, End = player.")] public GameObject lifesteal;
        public GameObject frenzyStack;
        [Tooltip("Held on the player. Value = stacks.")] public GameObject frenzyAura;

        private static FxLibrary _loaded;
        private static bool _searched;

        public static FxLibrary Loaded
        {
            get
            {
                if (!_searched)
                {
                    _searched = true;
                    _loaded = Resources.Load<FxLibrary>(ResourcePath);
                    if (_loaded == null)
                        Debug.LogWarning($"[Fx] Resources/{ResourcePath} 가 없습니다. 메뉴 HJH > Build Learn FX 를 실행하세요.");
                }

                return _loaded;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = null;
            _searched = false;
        }
    }
}
