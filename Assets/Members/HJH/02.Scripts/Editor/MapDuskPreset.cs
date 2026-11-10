using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HJH.EditorTools
{
    // 저녁 7시 무렵의 음산한 분위기 프리셋. 메뉴: HJH/맵/분위기/...
    //  - 태양: 낮고 식어 가는 주황, 약하게 / 환경광: 어두운 남보라 / 안개: 가라앉은 청회색
    //  - 이 씬 전용 Volume(Volume_Dusk)으로 채도↓, 차가운 화이트밸런스, 그림자 남청·하이라이트 주황, 비네트, Bloom 으로 빛나는 소품 강조
    //  - 스카이박스는 공용 머티리얼을 건드리지 않고 이 맵 전용 복사본을 만들어 어둡게 한다
    //  값을 바꾼 뒤에는 라이트맵을 다시 구워야 합니다.
    public static class MapDuskPreset
    {
        private const string Dir = "Assets/Members/HJH/05.Data/Map/";
        private const string VolumeName = "Volume_Dusk";

        private static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        [MenuItem("HJH/맵/분위기/저녁 7시 음산 프리셋 적용")]
        private static void ApplyDusk()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Dusk] 플레이 중에는 못 합니다."); return; }

            var sun = FindSun();
            if (sun != null)
            {
                Undo.RecordObject(sun, "Dusk"); Undo.RecordObject(sun.transform, "Dusk");
                sun.transform.rotation = Quaternion.Euler(18f, -35f, 0f);   // 낮은 해
                sun.color = new Color(1.0f, 0.62f, 0.45f);                  // 식어 가는 주황
                sun.intensity = 0.55f;
                sun.shadows = LightShadows.Soft;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.24f, 0.40f);
            RenderSettings.ambientEquatorColor = new Color(0.11f, 0.17f, 0.19f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.07f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.20f, 0.28f, 0.31f);
            RenderSettings.fogDensity = 0.009f;

            DuskSkybox();
            BuildVolume();

            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Dusk] 저녁 프리셋 적용: 태양 18°·주황·0.55, 환경광 남보라, 안개 청회색 0.009, Volume_Dusk 추가. 라이트맵을 다시 구워야 반영됩니다.");
        }

        private static void DuskSkybox()
        {
            var sky = RenderSettings.skybox;
            if (sky == null) return;
            string copyPath = Dir + "MapL_Skybox_Dusk.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(copyPath);
            if (m == null)
            {
                m = new Material(sky) { name = "MapL_Skybox_Dusk" };
                AssetDatabase.CreateAsset(m, copyPath);
            }
            if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", 0.45f);
            if (m.HasProperty("_SkyTint")) m.SetColor("_SkyTint", new Color(0.45f, 0.40f, 0.62f));
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", new Color(0.55f, 0.5f, 0.7f));
            if (m.HasProperty("_GroundColor")) m.SetColor("_GroundColor", new Color(0.08f, 0.1f, 0.12f));
            if (m.HasProperty("_AtmosphereThickness")) m.SetFloat("_AtmosphereThickness", 1.2f);
            EditorUtility.SetDirty(m);
            RenderSettings.skybox = m;
        }

        private static void BuildVolume()
        {
            var old = GameObject.Find(VolumeName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            string path = Dir + "VP_Dusk.asset";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var ca = profile.Add<ColorAdjustments>(true);
            ca.saturation.Override(-20f);
            ca.postExposure.Override(-0.25f);
            ca.contrast.Override(12f);

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(-14f);
            wb.tint.Override(4f);

            var st = profile.Add<SplitToning>(true);
            st.shadows.Override(new Color(0.22f, 0.32f, 0.55f));      // 그림자 남청
            st.highlights.Override(new Color(0.85f, 0.55f, 0.35f));   // 하이라이트 탁한 주황
            st.balance.Override(-10f);

            var vg = profile.Add<Vignette>(true);
            vg.color.Override(new Color(0.02f, 0.03f, 0.05f));
            vg.intensity.Override(0.36f);
            vg.smoothness.Override(0.5f);

            var bl = profile.Add<Bloom>(true);
            bl.threshold.Override(1.0f);
            bl.intensity.Override(1.1f);
            bl.scatter.Override(0.65f);

            foreach (var c in new VolumeComponent[] { ca, wb, st, vg, bl })
            {
                c.name = c.GetType().Name;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject(VolumeName);
            Undo.RegisterCreatedObjectUndo(go, "Dusk volume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 20f;
            vol.weight = 1f;
            vol.sharedProfile = profile;
        }

        // 원래 낮 분위기(맵 빌더 기본값)로 되돌림
        [MenuItem("HJH/맵/분위기/낮 프리셋으로 되돌리기")]
        private static void RestoreDay()
        {
            if (EditorApplication.isPlaying) return;
            var sun = FindSun();
            if (sun != null)
            {
                Undo.RecordObject(sun, "Day"); Undo.RecordObject(sun.transform, "Day");
                sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
                sun.color = new Color(1f, 0.93f, 0.8f);
                sun.intensity = 1.1f;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.62f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.4f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.17f, 0.12f);
            RenderSettings.fogColor = new Color(0.55f, 0.74f, 0.68f);
            RenderSettings.fogDensity = 0.005f;
            var vol = GameObject.Find(VolumeName);
            if (vol != null) Undo.DestroyObjectImmediate(vol);
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Dusk] 낮 프리셋으로 되돌렸습니다 (스카이박스는 복사본 그대로: 필요하면 씬의 Lighting 창에서 원래 스카이박스를 다시 지정). 라이트맵을 다시 구워야 합니다.");
        }
    }
}
