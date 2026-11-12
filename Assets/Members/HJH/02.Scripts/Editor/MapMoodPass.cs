using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HJH.EditorTools
{
    // "어둡지만 밝은" 분위기 (Shape of Dreams 참고). 전체를 밝히지 않고 명암 대비 + 국소 발광으로.
    //  진단(2026-11-12, Map_Bridge_Normal): VP_Dusk 노출 -0.25 · 채도 -20 · 비네트 0.36 → 발광물도 같이 죽음,
    //   Bloom 문턱 1.1 → 발광 꽃(Emission 1.6)이 거의 안 번짐, 태양 주황 → 바위가 갈보라색으로 탁함,
    //   안개 (0.2,0.28,0.31) 채도 없는 회청색.
    //  처리: 청색 달빛 태양 + 남청 환경광(실시간 오브젝트) + 채도 있는 청록 안개 + 노출 살짝↑ 채도↑ + Bloom 문턱↓,
    //   발광 꽃·덩굴 Emission↑.
    // 씬은 Backups/<씬>_before_mood.unity, 프로필은 VP_Dusk_before_mood.asset 으로 먼저 복사 (한 번만).
    public static class MapMoodPass
    {
        private const string ProfilePath = "Assets/Members/HJH/05.Data/Map/VP_Dusk.asset";
        private const string ProfileBackup = "Assets/Members/HJH/05.Data/Map/VP_Dusk_before_mood.asset";
        private const string BackupDir = "Assets/Members/HJH/Backups/";
        private static readonly string[] GlowMaterials =
        {
            "Assets/Members/HJH/05.Data/MapProps/Materials/GlowFlowers_Mat.mat",
            "Assets/Members/HJH/05.Data/MapProps/Materials/GlowVine_Mat.mat",
        };
        private const float GlowEmission = 2.6f;

        private const string BridgeScene = "Assets/Members/HJH/01.Scene/Map_Bridge_Normal.unity";

        // 다리 맵을 열어서 적용 → 저장 → 원래 씬으로 (열린 씬에 저장 안 된 변경이 있으면 중단)
        [MenuItem("HJH/맵/분위기/어둡지만 밝게 (SoD 식, 다리 맵 + VP_Dusk)")]
        private static void ApplyToBridge()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.GetActiveScene().isDirty) { Debug.LogError("[Mood] 저장 안 된 씬이 있어 중단"); return; }
            string original = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(BridgeScene, OpenSceneMode.Single);
            Apply();
            if (!string.IsNullOrEmpty(original) && original != BridgeScene) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        }

        [MenuItem("HJH/맵/분위기/어둡지만 밝게 (SoD 식, 지금 씬 + VP_Dusk)")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[Mood] 플레이 중에는 안 함"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path)) return;

            string sceneBackup = BackupDir + scene.name + "_before_mood.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneBackup) == null)
            {
                if (scene.isDirty) { Debug.LogError("[Mood] 저장 안 된 변경이 있어 백업을 못 함. 저장 후 다시"); return; }
                AssetDatabase.CopyAsset(scene.path, sceneBackup);
            }
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfileBackup) == null)
                AssetDatabase.CopyAsset(ProfilePath, ProfileBackup);

            // ── 태양: 주황 노을 → 푸른 달빛 (Mixed 라 직접광은 실시간으로 바로 바뀜)
            Light sun = RenderSettings.sun;
            if (sun != null)
            {
                Undo.RecordObject(sun, "Mood");
                sun.color = new Color(0.80f, 0.86f, 1f);   // 너무 파라면 캐릭터 고유색(빨강 갑옷 등)이 보라로 죽음
                sun.intensity = 1.05f;
            }

            // ── 환경광 / 안개: 채도 있는 남청·청록 (환경광은 캐릭터 같은 실시간 오브젝트에 바로, 지형은 다시 구우면)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.17f, 0.27f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.09f, 0.21f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.07f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.06f, 0.19f, 0.27f);
            RenderSettings.fogDensity = 0.009f;
            DynamicGI.UpdateEnvironment();

            ApplyProfile();
            ApplyGlow();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Mood] 적용 완료. 백업: " + sceneBackup + ", " + ProfileBackup);
        }

        [MenuItem("HJH/맵/분위기/어둡지만 밝게 되돌리기 (백업에서)")]
        private static void Revert()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            string sceneBackup = BackupDir + scene.name + "_before_mood.unity";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfileBackup) != null)
            {
                AssetDatabase.DeleteAsset(ProfilePath);
                AssetDatabase.CopyAsset(ProfileBackup, ProfilePath);
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneBackup) != null)
            {
                string path = scene.path;
                AssetDatabase.CopyAsset(sceneBackup, path);
                EditorSceneManager.OpenScene(path);
            }
            Debug.Log("[Mood] 백업으로 되돌림 (발광 머티리얼 Emission 은 git 으로 되돌리기)");
        }

        private static void ApplyProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null) { Debug.LogWarning("[Mood] VP_Dusk 없음"); return; }

            if (profile.TryGet(out ColorAdjustments color))
            {
                color.postExposure.Override(0.12f);    // -0.25 → 살짝 위로
                color.contrast.Override(20f);          // 12 → 대비↑ (어두운 곳은 그대로 어둡게)
                color.saturation.Override(4f);         // -20 → 발광 색이 살게 (너무 올리면 전부 파랗게)
            }
            if (profile.TryGet(out Bloom bloom))
            {
                bloom.threshold.Override(0.8f);        // 1.1 → 발광 꽃·버섯이 번지게
                bloom.intensity.Override(0.95f);
                bloom.scatter.Override(0.72f);
            }
            if (profile.TryGet(out SplitToning split))
            {
                split.shadows.Override(new Color(0.18f, 0.32f, 0.55f));   // 그림자 = 청색
                split.balance.Override(-12f);
            }
            if (profile.TryGet(out WhiteBalance wb))
                wb.temperature.Override(-8f);          // 태양이 이미 차가워서 덜
            if (profile.TryGet(out Vignette vignette))
            {
                vignette.intensity.Override(0.28f);
                vignette.color.Override(new Color(0.01f, 0.03f, 0.07f));
            }
            EditorUtility.SetDirty(profile);
        }

        private static void ApplyGlow()
        {
            foreach (string path in GlowMaterials)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
                Color c = mat.GetColor("_EmissionColor");
                float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                if (max <= 0.001f || max >= GlowEmission - 0.01f) continue;
                mat.SetColor("_EmissionColor", c * (GlowEmission / max));
                EditorUtility.SetDirty(mat);
            }
        }
    }
}
