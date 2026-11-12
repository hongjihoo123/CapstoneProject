using System.IO;
using Assets.Members.HJH._02.Scripts.Char.Health;
using Assets.Members.HJH._02.Scripts.Char.Movement;
using Assets.Members.HJH._02.Scripts.Char.Visual.Highlight;
using Assets.Members.HJH._02.Scripts.UI.HealthBar;
using Members.KYR._01.Scripts.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HJH.EditorTools
{
    // 전투 가독성 세팅 (한 번씩 누르면 되는 메뉴). 다시 눌러도 이미 된 건 건너뜀 → 바뀐 게 있을 때만 저장/더티.
    //  1) HPBar 프리팹(KYR) → 지연 체력바 (WorldBarFollow + HealthSignal + DelayedHealthBarView + HealthBarAutoHide, HealGhost 추가)
    //  2) PlayerRig 프리팹 → DashPassThrough
    //  3) 지금 열린 씬 → CharacterHighlighter (발밑 링 켬, 림·바닥 빛은 꺼 둠)
    public static class CombatReadabilitySetup
    {
        private const string HpBarPath = "Assets/Members/KYR/02.Prefabs/HPBar.prefab";
        private const string PlayerRigPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";
        private const string RingMaterialPath = "Assets/Members/HJH/05.Data/Fx/M_FootRing.mat";
        private const string RingTexturePath = "Assets/Members/HJH/08.Effects/Learn/Textures/T_Ring.png";
        private const string HighlighterName = "CharacterHighlighter";

        [MenuItem("HJH/세팅/1 체력바 지연 연출 (HPBar 프리팹)")]
        private static void SetupHealthBar()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            GameObject root = PrefabUtility.LoadPrefabContents(HpBarPath);
            bool changed = false;
            try
            {
                if (root.GetComponent<WorldBarFollow>() == null)
                {
                    root.AddComponent<WorldBarFollow>();
                    changed = true;
                }

                Image back = Find(root, "Back"), trail = Find(root, "Trail"), shield = Find(root, "Shield"), fill = Find(root, "Fill");
                Image heal = Find(root, "HealGhost");
                if (heal == null && trail != null)
                {
                    heal = Object.Instantiate(trail.gameObject, trail.transform.parent).GetComponent<Image>();
                    heal.name = "HealGhost";
                    heal.transform.SetSiblingIndex(trail.transform.GetSiblingIndex() + 1);
                    changed = true;
                }

                if (root.GetComponent<HealthSignal>() == null) { root.AddComponent<HealthSignal>(); changed = true; }
                if (root.GetComponent<HealthBarAutoHide>() == null) { root.AddComponent<HealthBarAutoHide>(); changed = true; }

                var view = root.GetComponent<DelayedHealthBarView>();
                if (view == null) { view = root.AddComponent<DelayedHealthBarView>(); changed = true; }
                var so = new SerializedObject(view);
                changed |= SetRef(so, "fill", fill);
                changed |= SetRef(so, "damageGhost", trail);
                changed |= SetRef(so, "healGhost", heal);
                changed |= SetRef(so, "shield", shield);
                so.ApplyModifiedPropertiesWithoutUndo();

                if (back == null) Debug.LogWarning("[Setup] HPBar 에 Back 이 없음 (그대로 진행)");
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, HpBarPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            Debug.Log(changed ? "[Setup] HPBar 지연 체력바 세팅 완료" : "[Setup] HPBar 는 이미 세팅돼 있음 (변경 없음)");
        }

        [MenuItem("HJH/세팅/2 대시 적 통과 (PlayerRig 프리팹)")]
        private static void SetupDash()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            bool changed = false;
            try
            {
                PlayerAgent agent = root.GetComponentInChildren<PlayerAgent>(true);
                if (agent == null) { Debug.LogError("[Setup] PlayerRig 안에 PlayerAgent 가 없음"); return; }
                if (agent.GetComponent<DashPassThrough>() == null)
                {
                    agent.gameObject.AddComponent<DashPassThrough>();
                    changed = true;
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerRigPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            Debug.Log(changed ? "[Setup] PlayerRig 에 DashPassThrough 추가" : "[Setup] DashPassThrough 이미 있음 (변경 없음)");
        }

        // HJH 맵 씬 4개 (Map_*, _HJH_Map). 열려 있는 씬에 저장 안 된 변경이 있으면 아무것도 안 함
        [MenuItem("HJH/세팅/3 적 가시성 (HJH 맵 씬 4개에 발밑 링 + 림라이트)")]
        private static void SetupHighlighterAllMaps()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                { Debug.LogError("[Setup] 저장 안 된 씬이 있어 중단 (저장 후 다시)"); return; }

            string original = EditorSceneManager.GetActiveScene().path;
            foreach (string path in Directory.GetFiles("Assets/Members/HJH/01.Scene", "*.unity"))
            {
                string p = path.Replace('\\', '/');
                string file = Path.GetFileName(p);
                if (!file.StartsWith("Map_") && file != "_HJH_Map.unity") continue;

                var scene = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                if (GameObject.Find(HighlighterName) == null)
                    AssetDatabase.CopyAsset(p, "Assets/Members/HJH/Backups/" + Path.GetFileNameWithoutExtension(p) + "_before_highlighter.unity");
                SetupHighlighter();
            }
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        }

        private static void SetupHighlighter()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Material ring = RingMaterial();
            var go = GameObject.Find(HighlighterName);
            bool changed = false;
            if (go == null)
            {
                go = new GameObject(HighlighterName);
                Undo.RegisterCreatedObjectUndo(go, "Highlighter");
                go.AddComponent<CharacterHighlighter>();
                changed = true;
            }

            var foot = go.GetComponent<FootRingLayer>();
            if (foot == null)
            {
                foot = go.AddComponent<FootRingLayer>();
                var so = new SerializedObject(foot);
                so.FindProperty("ringMaterial").objectReferenceValue = ring;
                so.FindProperty("forPlayer").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            // 2026-11-12 비교 캡처 후 림라이트(b) 채택 → 켬. 바닥 빛(c)은 이 맵에서 효과가 거의 없어 꺼 둠
            var rim = go.GetComponent<RimHighlightLayer>();
            if (rim == null) { rim = go.AddComponent<RimHighlightLayer>(); changed = true; }
            if (!rim.enabled) { rim.enabled = true; changed = true; }
            if (go.GetComponent<GroundLightLayer>() == null) { go.AddComponent<GroundLightLayer>().enabled = false; changed = true; }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(go.scene);
                EditorSceneManager.SaveScene(go.scene);
            }
            Debug.Log(changed ? "[Setup] " + go.scene.name + " 에 CharacterHighlighter 추가·저장" : "[Setup] 이미 있음 (변경 없음)");
        }

        private static Material RingMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(RingMaterialPath);
            if (mat != null) return mat;

            // URP Particles/Unlit, 투명 알파 블렌드. 링 텍스처의 밝기를 알파로 씀
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_FootRing" };
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RingTexturePath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;   // 이펙트보다 먼저 (바닥에 깔림)
            Directory.CreateDirectory(Path.GetDirectoryName(RingMaterialPath));
            AssetDatabase.CreateAsset(mat, RingMaterialPath);
            return mat;
        }

        private static Image Find(GameObject root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.GetComponent<Image>();
            return null;
        }

        private static bool SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null || p.objectReferenceValue == value) return false;
            p.objectReferenceValue = value;
            return true;
        }
    }
}
