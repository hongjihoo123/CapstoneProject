using System.Linq;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Assets.Members.HJH.Environment.EditorTools
{
    // 메뉴: HJH > Jungle Water > 프랍 정리 (바위 콜라이더 + 나무 반투명)
    //  - 바위(Rock.fbx 메시): 콜라이더 추가 → 이동·대시가 막힘
    //  - 나무·잎: 콜라이더 제거 (대시가 나무에 걸려 멈추는 게 거슬린다는 피드백) + 카메라를 가리면 반투명
    //    · 줄기 머티리얼 M_Tree → M_JungleBark (같은 텍스처 + 반투명 지원)
    //    · 메인 카메라에 JungleOcclusionFader
    //  - NavMesh 다시 굽기
    public static class JungleVegetationSetup
    {
        private const string RockModel = "Assets/Members/KM/Assets/Props/Natural/Rock/Rock.fbx";
        private const string TreeModel = "Assets/Members/KM/Assets/Props/Natural/Tree/Tree.fbx";
        private const string TreeMaterial = "Assets/Members/KM/Assets/Props/Natural/Tree/M_Tree.mat";
        private const string BarkMaterial = "Assets/Members/HJH/09.JungleWater/Generated/Materials/M_JungleBark.mat";
        private const string LeafMaterial = "Assets/Members/HJH/09.JungleWater/Generated/Materials/M_JungleLeaf.mat";
        private const string BarkMenu = "HJH/Jungle Water/나무 줄기 셰이더 그래프 만들기";
        private const string LeafMenu = "HJH/Jungle Water/잎 셰이더 그래프 만들기";

        [MenuItem("HJH/Jungle Water/프랍 정리 (바위 콜라이더 + 나무 반투명)")]
        public static void Setup()
        {
            // 셰이더에 반투명(_Fade)이 없으면 먼저 만든다
            var leaf = AssetDatabase.LoadAssetAtPath<Material>(LeafMaterial);
            if (leaf == null || !leaf.HasProperty("_Fade"))
                EditorApplication.ExecuteMenuItem(LeafMenu);
            var bark = AssetDatabase.LoadAssetAtPath<Material>(BarkMaterial);
            if (bark == null)
            {
                EditorApplication.ExecuteMenuItem(BarkMenu);
                bark = AssetDatabase.LoadAssetAtPath<Material>(BarkMaterial);
            }

            var treeMaterial = AssetDatabase.LoadAssetAtPath<Material>(TreeMaterial);
            int rockColliders = 0, removedColliders = 0, barkSwaps = 0;

            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                string meshPath = mf.sharedMesh != null ? AssetDatabase.GetAssetPath(mf.sharedMesh) : null;

                if (meshPath == RockModel && mf.GetComponent<Collider>() == null)
                {
                    Undo.AddComponent<BoxCollider>(mf.gameObject);   // 메시 크기에 자동으로 맞춰짐
                    rockColliders++;
                }

                bool isTree = meshPath == TreeModel;
                bool isLeaf = mf.TryGetComponent(out Renderer leafRenderer) && leafRenderer.sharedMaterial != null
                              && leafRenderer.sharedMaterial.name.StartsWith("M_JungleLeaf");
                if (isTree || isLeaf)
                {
                    foreach (Collider c in mf.GetComponents<Collider>())
                    {
                        Undo.DestroyObjectImmediate(c);
                        removedColliders++;
                    }
                }

                if (bark != null && treeMaterial != null && mf.TryGetComponent(out Renderer renderer)
                    && renderer.sharedMaterials.Contains(treeMaterial))
                {
                    Undo.RecordObject(renderer, "Swap Bark Material");
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m == treeMaterial ? bark : m).ToArray();
                    barkSwaps++;
                }
            }

            // 나무 뿌리 둔덕 전체에 붙은 캡슐(예전 강가 꾸미기)도 제거
            foreach (CapsuleCollider capsule in Object.FindObjectsByType<CapsuleCollider>(FindObjectsSortMode.None))
            {
                if (!capsule.name.StartsWith("PF_JungleTree"))
                    continue;
                Undo.DestroyObjectImmediate(capsule);
                removedColliders++;
            }

            bool fader = false;
            Camera cam = Camera.main;
            if (cam != null && cam.GetComponent<JungleOcclusionFader>() == null)
            {
                Undo.AddComponent<JungleOcclusionFader>(cam.gameObject);
                fader = true;
            }

            Object[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None)
                .Where(s => s.isActiveAndEnabled).Cast<Object>().ToArray();
            if (surfaces.Length > 0)
                NavMeshAssetManager.instance.StartBakingSurfaces(surfaces);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[JungleWater] 프랍 정리: 바위 콜라이더 +{rockColliders}, 나무·잎 콜라이더 제거 {removedColliders}, " +
                      $"줄기 머티리얼 교체 {barkSwaps}, 카메라 반투명 처리 {(fader ? "추가" : "이미 있음")}, NavMesh 다시 굽기 {surfaces.Length}");
        }
    }
}
