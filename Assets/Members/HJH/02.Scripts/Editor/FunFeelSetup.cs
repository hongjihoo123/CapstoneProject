using System.IO;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.KYR._01_Scripts;
using RobotWeapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // Scene + data setup for the fun-test pass: combat feedback on the player, bloom/vignette
    // post-processing so the HDR effects glow, and effect colors on the character weapons.
    public static class FunFeelSetup
    {
        private const string ProfilePath = "Assets/Members/HJH/Data/Fx/FunTestVolume.asset";
        private const string GunnerWeaponPath = "Assets/Members/HJH/Data/Weapon/Gunner_DualPistols.asset";
        private const string RuneWeaponPath = "Assets/Members/HJH/Data/Weapon/RuneMage_Bolt.asset";

        public static void ApplyToScene(PlayerAgent player)
        {
            if (!player.TryGetComponent(out CombatFeedback feedback))
                feedback = player.gameObject.AddComponent<CombatFeedback>();

            var so = new SerializedObject(feedback);
            so.FindProperty("player").objectReferenceValue = player;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetupPostProcessing();
        }

        // Only touches values still at their code defaults, so later hand tuning is kept.
        public static void ApplyWeaponVisualDefaults()
        {
            var rune = AssetDatabase.LoadAssetAtPath<HitscanGunData>(RuneWeaponPath);
            if (rune != null && rune.projectileSpeed <= 0f)
            {
                rune.projectileSpeed = 26f;
                rune.projectileSize = 0.45f;
                rune.fxColor = new Color(0.7f, 0.4f, 1f);
                EditorUtility.SetDirty(rune);
            }

            var gunner = AssetDatabase.LoadAssetAtPath<HitscanGunData>(GunnerWeaponPath);
            if (gunner != null && gunner.empoweredTracerWidth < 0.26f)
            {
                gunner.fxColor = new Color(1f, 0.8f, 0.35f);
                gunner.empoweredFxColor = new Color(0.3f, 1f, 0.85f);
                gunner.empoweredTracerWidth = 0.3f;
                EditorUtility.SetDirty(gunner);
            }
        }

        private static void SetupPostProcessing()
        {
            Camera camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (camera != null)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                camera.allowHDR = true;
                EditorUtility.SetDirty(data);
                EditorUtility.SetDirty(camera);
            }

            Volume volume = null;
            foreach (Volume candidate in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (candidate.isGlobal)
                {
                    volume = candidate;
                    break;
                }
            }

            if (volume == null)
            {
                volume = new GameObject("Global Volume (Fun Test)").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
            }

            if (volume.sharedProfile == null)
                volume.sharedProfile = LoadOrCreateProfile();

            EditorUtility.SetDirty(volume);
        }

        private static VolumeProfile LoadOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null)
                return profile;

            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.7f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            // Components added through Add are sub-assets; they must be saved with the profile.
            foreach (VolumeComponent component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
