using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Assets.Members.HJH.Environment.GraphGen
{
    // 메뉴: HJH > Jungle Water > 셰이더 전환 (HLSL ↔ 노드)
    // 열린 씬의 Water / GroundFog 와 물보라 프리팹의 머티리얼을 HLSL 버전 ↔ 노드 버전으로 바꾼다.
    public static class JungleWaterShaderSwitcher
    {
        private const string MatDir = "Assets/Members/HJH/09.JungleWater/Generated/Materials";
        private const string SplashPrefabPath = "Assets/Members/HJH/09.JungleWater/Generated/Prefabs/WaterSplash.prefab";

        private static readonly (string objectName, string material)[] Targets =
        {
            ("Water", "M_JungleWater"),
            ("GroundFog", "M_JungleFog"),
        };

        [MenuItem("HJH/Jungle Water/셰이더 전환 (HLSL ↔ 노드)")]
        public static void Toggle()
        {
            GameObject water = GameObject.Find("Water");
            if (water == null)
            {
                Debug.LogWarning("[JungleWater] 열린 씬에 'Water' 오브젝트가 없습니다. JungleWaterTest 씬을 열어 주세요.");
                return;
            }

            bool toNode = !water.GetComponent<Renderer>().sharedMaterial.name.EndsWith("_Node");
            string suffix = toNode ? "_Node" : "";

            foreach (var (objectName, material) in Targets)
            {
                Material mat = Load(material + suffix);
                GameObject go = GameObject.Find(objectName);
                if (mat == null || go == null)
                    continue;

                var renderer = go.GetComponent<Renderer>();
                Undo.RecordObject(renderer, "Switch Jungle Water Shader");
                renderer.sharedMaterial = mat;
            }

            Material splash = Load("M_WaterSplash" + suffix);
            if (splash != null && AssetDatabase.LoadAssetAtPath<GameObject>(SplashPrefabPath) != null)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(SplashPrefabPath);
                foreach (var r in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    r.sharedMaterial = splash;
                PrefabUtility.SaveAsPrefabAsset(root, SplashPrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            EditorSceneManager.MarkSceneDirty(water.scene);
            Debug.Log($"[JungleWater] 지금 셰이더: {(toNode ? "노드(Shader Graph) 버전" : "HLSL 버전")}");
        }

        private static Material Load(string name)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");
            if (mat == null)
                Debug.LogWarning($"[JungleWater] 머티리얼 없음: {name} (노드 버전이면 먼저 '노드 버전 셰이더 그래프 만들기' 실행)");
            return mat;
        }
    }
}
