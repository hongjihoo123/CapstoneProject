using System.IO;
using System.Linq;
using Assets.Members.HJH.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HJH.EditorTools
{
    public static class TrainingGroundDawn
    {
        private const string ScenePath = "Assets/Members/HJH/01.Scene/Map_Training.unity";
        private const string DataDir = "Assets/Members/HJH/05.Data/Training";
        private const string ProfilePath = DataDir + "/VP_Training_Dawn.asset";

        private const float SunElevation = 27f;
        private const float SunScreenYaw = -55f;
        private const float CameraYaw = 45f;

        [MenuItem("HJH/맵/새 맵/훈련장 새벽 분위기 적용 (Map_Training)")]
        private static void Menu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[Dawn] 플레이 중에는 적용하지 않습니다."); return; }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Dawn] 저장 안 된 씬이 있습니다. 저장한 뒤 다시 실행하세요."); return; }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            string backup = "_RemovedBackup/" + System.DateTime.Now.ToString("yyyy-MM-dd") + "/Assets/Members/HJH/01.Scene/";
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, backup + "Map_Training_beforeDawn" + System.DateTime.Now.ToString("_HHmmss") + ".unity", true);

            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Dawn] 새벽 분위기 적용 완료");
        }

        internal static void Apply(Scene scene)
        {
            Transform root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Map_Training")?.transform;
            if (root == null) { Debug.LogError("[Dawn] Map_Training 루트가 없습니다."); return; }
            Transform lights = root.Find("Lights");
            if (lights == null)
            {
                lights = new GameObject("Lights").transform;
                lights.SetParent(root, false);
            }

            Sun(lights);
            Sky();
            Materials();
            Volume(lights);
            Braziers(lights);
            DisableOcclusionFade(scene);
        }

        private static void Sun(Transform lights)
        {
            Light sun = lights.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun == null) { Debug.LogWarning("[Dawn] 해(Directional Light)가 없습니다."); return; }
            sun.color = new Color(1f, 0.77f, 0.68f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.35f;
            sun.transform.rotation = Quaternion.Euler(SunElevation, CameraYaw + SunScreenYaw, 0f);
            RenderSettings.sun = sun;
        }

        private static void Sky()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.62f, 0.90f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.44f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.20f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.56f, 0.64f, 0.82f);
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 80f;
        }

        private static void Materials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { DataDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m.name.EndsWith("_Flame")) m.SetColor("_BaseColor", new Color(1.7f, 0.34f, 0.03f));
                else if (m.name.EndsWith("_FlameCore")) m.SetColor("_BaseColor", new Color(2.1f, 0.85f, 0.12f));
                else if (m.name.EndsWith("_MapFloor")) Unfogged(m, new Color(0.36f, 0.3f, 0.2f));
                else if (m.name.EndsWith("_MapWall")) Unfogged(m, new Color(0.78f, 0.62f, 0.5f));
                else if (m.shader.name == "HJH/Map/Toon")
                {
                    m.SetColor("_ShadowColor", new Color(0.58f, 0.58f, 0.86f, 1f));
                    m.SetFloat("_ShadowSoftness", 0.08f);
                    m.SetFloat("_ReceiveShadow", m.name.Contains("_Leaf") ? 0.6f : 1f);
                    m.SetColor("_RimColor", new Color(1f, 0.76f, 0.6f, 1f));
                    m.SetFloat("_RimStrength", 0.12f);
                }
                EditorUtility.SetDirty(m);
            }
        }

        private static void Unfogged(Material m, Color color)
        {
            m.shader = Shader.Find("Sprites/Default");
            m.SetColor("_Color", color);
            m.renderQueue = -1;
        }

        private static void Volume(Transform lights)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            Tonemapping tone = Part<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);

            Bloom bloom = Part<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(new Color(1f, 0.85f, 0.72f));

            ColorAdjustments color = Part<ColorAdjustments>(profile);
            color.postExposure.Override(0.1f);
            color.contrast.Override(12f);
            color.saturation.Override(-8f);

            WhiteBalance balance = Part<WhiteBalance>(profile);
            balance.temperature.Override(-16f);
            balance.tint.Override(6f);

            SplitToning split = Part<SplitToning>(profile);
            split.shadows.Override(new Color(0.34f, 0.42f, 0.72f));
            split.highlights.Override(new Color(0.68f, 0.52f, 0.50f));
            split.balance.Override(-10f);

            Vignette vignette = Part<Vignette>(profile);
            vignette.color.Override(new Color(0.07f, 0.06f, 0.16f));
            vignette.intensity.Override(0.27f);
            vignette.smoothness.Override(0.55f);
            EditorUtility.SetDirty(profile);

            Transform holder = lights.Find("Volume_Dawn");
            if (holder == null)
            {
                holder = new GameObject("Volume_Dawn").transform;
                holder.SetParent(lights, false);
            }
            Volume volume = holder.GetComponent<Volume>();
            if (volume == null)
                volume = holder.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        private static T Part<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T found))
                return found;
            T part = profile.Add<T>(false);
            part.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(part, profile);
            return part;
        }

        private static void Braziers(Transform lights)
        {
            Material glow = GlowMaterial();
            foreach (Light light in lights.GetComponentsInChildren<Light>(true))
            {
                if (light.type != LightType.Point)
                    continue;
                light.color = new Color(1f, 0.52f, 0.2f);
                light.range = 8f;
                light.intensity = 5f;

                Transform pool = light.transform.Find("GroundGlow");
                if (pool == null)
                {
                    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "GroundGlow";
                    Object.DestroyImmediate(quad.GetComponent<Collider>());
                    pool = quad.transform;
                    pool.SetParent(light.transform, false);
                }
                Vector3 p = light.transform.position;
                pool.position = new Vector3(p.x, 0.05f, p.z);
                pool.rotation = Quaternion.Euler(90f, 0f, 0f);
                pool.localScale = new Vector3(6.5f, 6.5f, 1f);
                var renderer = pool.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = glow;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Material GlowMaterial()
        {
            string texturePath = DataDir + "/T_Glow.png";
            if (!File.Exists(texturePath))
            {
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude * 2f;
                        float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            string path = DataDir + "/M_Training_Glow.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            m.SetColor("_BaseColor", new Color(1f, 0.4f, 0.08f, 0.7f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void DisableOcclusionFade(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                foreach (Camera camera in go.GetComponentsInChildren<Camera>(true))
                {
                    if (!camera.CompareTag("MainCamera"))
                        continue;
                    var fader = camera.GetComponent<JungleOcclusionFader>();
                    if (fader == null)
                        fader = camera.gameObject.AddComponent<JungleOcclusionFader>();
                    fader.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(fader);
                }
        }
    }
}
