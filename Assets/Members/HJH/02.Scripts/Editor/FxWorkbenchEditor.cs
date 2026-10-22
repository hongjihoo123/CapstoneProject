using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // FxWorkbench 인스펙터
    //  - 스킬 쓰기 / 프리팹에 저장 / 되돌리기
    //  - 칸(castFx 등): 체크 = 보이기, 이름 클릭 = 안의 레이어 펼치기, 만 = 이 칸만, 고치기 = 사본 선택
    //  - 레이어: 체크 = 보이기 (Alt+클릭 = 이것만), 만, 고치기 / 칸마다 전부 켜기·끄기·반전
    //  - 검색: 이름으로 레이어를 모아서 한 번에 끄기·켜기·그것만 보기
    //  "보이기"는 재생 화면에서만 → 사본·프리팹은 안 바뀌고 저장도 안 됨 (비교용). 바꾸면 지금 떠 있는 이펙트에도 바로 적용.
    [CustomEditor(typeof(FxWorkbench))]
    public class FxWorkbenchEditor : Editor
    {
        private const float ButtonWidth = 28f, FixWidth = 52f, ToggleWidth = 18f;

        private readonly HashSet<GameObject> _open = new();
        private string _search = "";
        private GUIStyle _dim, _bold, _dimBold;

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var bench = (FxWorkbench)target;

            EditorGUILayout.Space(8);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Skill 칸에 원소 스킬을 넣고 Play를 누르면, 그 스킬의 파티클이 이 오브젝트 아래 [작업대 파티클]에 채워져요.\n" +
                    "그 사본을 고치면 다음 시전에 바로 보이고, '프리팹에 저장' 또는 Play를 끌 때 원래 프리팹에 저장돼요.",
                    MessageType.Info);
                return;
            }

            if (bench.LoadedSkill == null)
            {
                EditorGUILayout.HelpBox("Skill 칸이 비어 있어요.", MessageType.Warning);
                return;
            }

            _dim ??= new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
            _bold ??= new GUIStyle(EditorStyles.boldLabel);
            _dimBold ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };

            DrawTopButtons(bench);
            EditorGUILayout.Space(6);
            DrawSearch(bench);
            EditorGUILayout.Space(4);

            foreach (FxWorkbench.Part part in bench.Parts)
            {
                if (part.Copy != null)
                    DrawPart(bench, part);
            }

            EditorGUILayout.HelpBox(
                "체크 = 보이기 (재생 화면에서만, 저장 안 됨) · Alt+체크 = 그것만 보기 · 칸 이름 클릭 = 펼치기\n" +
                "● = 값을 고쳤는데 아직 저장 안 함 · 고치기 = 그 사본/레이어 선택 → Inspector 에서 값 바꾸기",
                MessageType.None);
        }

        private static void DrawTopButtons(FxWorkbench bench)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 스킬 쓰기", GUILayout.Height(28)))
                    bench.PlayNow();
                if (GUILayout.Button("💾 프리팹에 저장", GUILayout.Height(28)))
                {
                    int saved = FxWorkbenchSaver.SaveChanged(bench);
                    Debug.Log(saved > 0 ? $"[FxWorkbench] {saved}개 저장" : "[FxWorkbench] 바뀐 게 없어요");
                }
                if (GUILayout.Button("↺ 되돌리기", GUILayout.Height(28))
                    && EditorUtility.DisplayDialog("되돌리기", "저장 안 한 수정을 버리고 프리팹 상태로 다시 불러올까요?", "버리기", "취소"))
                    bench.Revert();
            }
        }

        // ---------- 검색: 이름으로 모아서 한 번에 ----------
        private void DrawSearch(FxWorkbench bench)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{bench.LoadedSkill.name} 의 파티클", EditorStyles.boldLabel, GUILayout.Width(220));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("👁 모두 보이기", GUILayout.Width(100)))
                    bench.ShowAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField("🔍 이름 검색", _search);
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    _search = "";
                    GUI.FocusControl(null);
                }
            }

            if (string.IsNullOrWhiteSpace(_search))
                return;

            var found = new List<(FxWorkbench.Part part, Transform layer)>();
            foreach (FxWorkbench.Part part in bench.Parts)
                foreach (Transform layer in FxWorkbench.Layers(part))
                    if (Matches(layer))
                        found.Add((part, layer));

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"찾은 레이어 {found.Count}개", GUILayout.Width(120));
                if (GUILayout.Button("전부 끄기"))
                    foreach ((FxWorkbench.Part part, Transform layer) in found)
                        part.HiddenLayers.Add(layer);
                if (GUILayout.Button("전부 켜기"))
                    foreach ((FxWorkbench.Part part, Transform layer) in found)
                    {
                        part.Hidden = false;
                        part.HiddenLayers.Remove(layer);
                    }
                if (GUILayout.Button("이것들만 보기"))
                    bench.SoloLayers(found);
            }
        }

        private bool Matches(Transform layer) =>
            string.IsNullOrWhiteSpace(_search) || layer.name.IndexOf(_search.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0;

        // ---------- 칸 하나 ----------
        private void DrawPart(FxWorkbench bench, FxWorkbench.Part part)
        {
            List<Transform> layers = FxWorkbench.Layers(part);
            int shown = 0;
            foreach (Transform layer in layers)
                if (!part.HiddenLayers.Contains(layer))
                    shown++;

            bool searching = !string.IsNullOrWhiteSpace(_search);
            bool open = _open.Contains(part.Copy) || searching;
            bool changed = FxWorkbenchSaver.IsChangedCached(part);

            Rect row = EditorGUILayout.GetControlRect();
            float x = row.x;
            Rect toggleRect = new(x, row.y, ToggleWidth, row.height);
            Rect fixRect = new(row.xMax - FixWidth, row.y, FixWidth, row.height);
            Rect soloRect = new(fixRect.x - ButtonWidth - 2, row.y, ButtonWidth, row.height);
            Rect labelRect = new(x + ToggleWidth + 14, row.y, soloRect.x - x - ToggleWidth - 18, row.height);

            bool visible = EditorGUI.Toggle(toggleRect, !part.Hidden);
            if (visible == part.Hidden)
            {
                if (Event.current.alt) bench.SoloPart(part);
                else part.Hidden = !visible;
            }

            // 칸 이름 전체가 펼치기 버튼
            string label = $"{(changed ? "● " : "")}{part.Slots}   {part.Prefab.name}   ({shown}/{layers.Count})";
            bool nowOpen = EditorGUI.Foldout(labelRect, open, label, true, part.Hidden ? _dimBold : _bold);
            if (!searching && nowOpen != open)
            {
                if (nowOpen) _open.Add(part.Copy);
                else _open.Remove(part.Copy);
                open = nowOpen;
            }

            if (GUI.Button(soloRect, "만"))
                bench.SoloPart(part);
            if (GUI.Button(fixRect, "고치기"))
                Select(part.Copy);

            if (!open)
                return;

            if (!searching)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(ToggleWidth + 14);
                    if (GUILayout.Button("전부 켜기", EditorStyles.miniButtonLeft))
                        FxWorkbench.SetAllLayers(part, true);
                    if (GUILayout.Button("전부 끄기", EditorStyles.miniButtonMid))
                        FxWorkbench.SetAllLayers(part, false);
                    if (GUILayout.Button("반전", EditorStyles.miniButtonRight))
                        FxWorkbench.InvertLayers(part);
                }
            }

            Transform root = part.Copy.transform;
            foreach (Transform layer in layers)
            {
                if (!Matches(layer))
                    continue;
                DrawLayer(bench, part, layer, Depth(layer, root));
            }
            EditorGUILayout.Space(4);
        }

        // ---------- 레이어 한 줄 ----------
        private void DrawLayer(FxWorkbench bench, FxWorkbench.Part part, Transform layer, int depth)
        {
            Rect row = EditorGUILayout.GetControlRect();
            float indent = ToggleWidth + 14 + depth * 12;
            Rect toggleRect = new(row.x + indent, row.y, ToggleWidth, row.height);
            Rect fixRect = new(row.xMax - FixWidth, row.y, FixWidth, row.height);
            Rect soloRect = new(fixRect.x - ButtonWidth - 2, row.y, ButtonWidth, row.height);
            Rect labelRect = new(toggleRect.xMax + 2, row.y, soloRect.x - toggleRect.xMax - 6, row.height);

            bool hidden = part.HiddenLayers.Contains(layer);
            bool visible = EditorGUI.Toggle(toggleRect, !hidden);
            if (visible == hidden)
            {
                if (Event.current.alt) bench.SoloLayer(part, layer);
                else if (visible) part.HiddenLayers.Remove(layer);
                else part.HiddenLayers.Add(layer);
            }

            bool showing = !part.Hidden && !part.HiddenLayers.Contains(layer);
            // 프리팹에서 꺼져 있는 오브젝트는 (꺼짐) 표시 = 게임에서도 안 나옴
            string text = ActiveInPrefab(layer, part.Copy.transform) ? layer.name : layer.name + "  (꺼짐)";
            // 이름 클릭 = 그 레이어 선택 (바로 값 고치기)
            if (GUI.Button(labelRect, text, showing ? EditorStyles.label : _dim))
                Select(layer.gameObject);

            if (GUI.Button(soloRect, "만"))
                bench.SoloLayer(part, layer);
            if (GUI.Button(fixRect, "고치기"))
                Select(layer.gameObject);
        }

        // 사본은 꺼둔 부모 아래 있어서 activeInHierarchy 는 항상 false → 프리팹 안에서 켜져 있는지만 봄
        private static bool ActiveInPrefab(Transform layer, Transform root)
        {
            for (Transform t = layer; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf)
                    return false;
                if (t == root)
                    return true;
            }
            return true;
        }

        private static int Depth(Transform layer, Transform root)
        {
            int depth = 0;
            for (Transform t = layer; t != root && t != null; t = t.parent)
                depth++;
            return Mathf.Max(0, depth - 1);
        }

        private static void Select(GameObject target)
        {
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
