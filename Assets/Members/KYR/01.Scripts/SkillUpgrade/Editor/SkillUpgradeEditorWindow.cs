using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01.Scripts.Player;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Members.KYR._01.Scripts.SkillUpgrade.Editor
{
    // 스킬 강화표 편집기: 스킬이 많아서 인스펙터로는 찾기/고치기 힘들어 표 형태로 본다.
    // 플레이 중에는 지금 장착한 스킬과 현재 강화 레벨을 같이 보여 준다.
    public sealed class SkillUpgradeEditorWindow : EditorWindow
    {
        private const string AllGroups = "전체";

        private SkillUpgradeTableSO _table;
        private string _search = string.Empty;
        private string _group = AllGroups;
        private bool _equippedOnly;
        private Vector2 _scroll;

        // 일괄 설정
        private bool _showBulk;
        private int _bulkLevels = 3;
        private int _bulkBaseCost = 50;
        private int _bulkCostStep = 50;
        private float _bulkFlat;
        private float _bulkPercent = 10f;

        private readonly Dictionary<string, SkillSlotId> _equipped = new Dictionary<string, SkillSlotId>();

        [MenuItem("KYR/스킬 강화 편집기")]
        public static void OpenMenu() => Open(null);

        [OnOpenAsset(1)]
        public static bool OpenAsset(int instanceId, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceId) is SkillUpgradeTableSO table)
            {
                Open(table);
                return true;
            }

            return false;
        }

        public static void Open(SkillUpgradeTableSO table)
        {
            SkillUpgradeEditorWindow window = GetWindow<SkillUpgradeEditorWindow>("스킬 강화");
            window.minSize = new Vector2(760f, 360f);
            if (table != null)
                window._table = table;
            window.Show();
        }

        private void OnEnable()
        {
            if (_table == null)
                _table = FindTable();
            EditorApplication.playModeStateChanged += HandlePlayMode;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= HandlePlayMode;
        }

        private void HandlePlayMode(PlayModeStateChange change) => Repaint();

        // 경로 대신 타입으로 찾는다: 표를 옮기거나 이름을 바꿔도 된다. 여러 개면 첫 번째.
        private static SkillUpgradeTableSO FindTable()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(SkillUpgradeTableSO)))
            {
                SkillUpgradeTableSO table = AssetDatabase.LoadAssetAtPath<SkillUpgradeTableSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (table != null)
                    return table;
            }

            return null;
        }

        // 플레이 중 레벨/장착 표시를 갱신
        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_table == null)
            {
                EditorGUILayout.HelpBox("강화표(SkillUpgradeTable)를 선택하세요.", MessageType.Info);
                return;
            }

            CollectEquipped();
            List<SkillUpgradeTableSO.Entry> rows = Filtered();

            DrawBulk(rows);
            EditorGUILayout.LabelField($"{rows.Count}개 표시 / 전체 {CountEntries()}개", EditorStyles.miniLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (SkillUpgradeTableSO.Entry entry in rows)
                DrawEntry(entry);
            EditorGUILayout.EndScrollView();
        }

        // ---------- 상단 ----------

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _table = (SkillUpgradeTableSO)EditorGUILayout.ObjectField(_table, typeof(SkillUpgradeTableSO), false, GUILayout.Width(220f));

                List<string> groups = Groups();
                int index = Mathf.Max(0, groups.IndexOf(_group));
                index = EditorGUILayout.Popup(index, groups.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(110f));
                _group = groups[index];

                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120f));

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    _equippedOnly = GUILayout.Toggle(_equippedOnly && Application.isPlaying, "장착 스킬만", EditorStyles.toolbarButton, GUILayout.Width(80f));

                _showBulk = GUILayout.Toggle(_showBulk, "일괄 설정", EditorStyles.toolbarButton, GUILayout.Width(70f));

                using (new EditorGUI.DisabledScope(_table == null))
                {
                    if (GUILayout.Button("누락 스킬 추가", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                        SkillUpgradeTableEditor.AddMissingSkills(_table);
                }
            }
        }

        private void DrawBulk(List<SkillUpgradeTableSO.Entry> rows)
        {
            if (!_showBulk)
                return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("일괄 설정: 지금 보이는 스킬들의 레벨 목록을 이 값으로 덮어씁니다", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _bulkLevels = Mathf.Clamp(EditorGUILayout.IntField("레벨 수", _bulkLevels), 1, 20);
                    _bulkBaseCost = Mathf.Max(0, EditorGUILayout.IntField("1레벨 비용", _bulkBaseCost));
                    _bulkCostStep = EditorGUILayout.IntField("비용 증가", _bulkCostStep);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _bulkFlat = EditorGUILayout.FloatField("레벨당 고정", _bulkFlat);
                    _bulkPercent = EditorGUILayout.FloatField("레벨당 %", _bulkPercent);
                    GUILayout.FlexibleSpace();
                }

                string preview = string.Empty;
                for (int i = 0; i < Mathf.Min(_bulkLevels, 5); i++)
                    preview += $"Lv{i + 1} {Mathf.Max(0, _bulkBaseCost + _bulkCostStep * i)}G  ";
                EditorGUILayout.LabelField(preview + (_bulkLevels > 5 ? "…" : string.Empty), EditorStyles.miniLabel);

                if (GUILayout.Button($"보이는 {rows.Count}개 스킬에 적용") &&
                    EditorUtility.DisplayDialog("스킬 강화", $"{rows.Count}개 스킬의 레벨 설정을 덮어씁니다.", "적용", "취소"))
                {
                    Undo.RecordObject(_table, "강화표 일괄 설정");
                    foreach (SkillUpgradeTableSO.Entry entry in rows)
                    {
                        entry.levels = new List<SkillUpgradeTableSO.Level>();
                        for (int i = 0; i < _bulkLevels; i++)
                        {
                            entry.levels.Add(new SkillUpgradeTableSO.Level
                            {
                                cost = Mathf.Max(0, _bulkBaseCost + _bulkCostStep * i),
                                flatDamage = _bulkFlat,
                                percentDamage = _bulkPercent / 100f
                            });
                        }
                    }

                    EditorUtility.SetDirty(_table);
                }
            }
        }

        // ---------- 스킬 한 줄 ----------

        private void DrawEntry(SkillUpgradeTableSO.Entry entry)
        {
            SkillData skill = entry.skill;
            string key = SkillUpgradeService.KeyOf(skill);
            bool equipped = _equipped.TryGetValue(key, out SkillSlotId slot);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Texture icon = skill != null && skill.Icon != null ? skill.Icon.texture : null;
                    GUILayout.Label(icon, GUILayout.Width(28f), GUILayout.Height(28f));

                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(220f)))
                    {
                        EditorGUI.BeginChangeCheck();
                        SkillData next = (SkillData)EditorGUILayout.ObjectField(skill, typeof(SkillData), false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(_table, "스킬 변경");
                            entry.skill = next;
                            EditorUtility.SetDirty(_table);
                        }

                        string status = equipped ? $"장착: {SlotLabel(slot)}" : string.Empty;
                        if (Application.isPlaying)
                            status += $"   현재 Lv {SkillUpgradeService.GetLevel(skill)}";
                        GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
                        if (equipped)
                            style.normal.textColor = new Color(1f, 0.75f, 0.3f);
                        EditorGUILayout.LabelField(status, style);
                    }

                    DrawLevels(entry);
                }
            }
        }

        private void DrawLevels(SkillUpgradeTableSO.Entry entry)
        {
            if (entry.levels == null)
                entry.levels = new List<SkillUpgradeTableSO.Level>();

            int remove = -1;
            for (int i = 0; i < entry.levels.Count; i++)
            {
                SkillUpgradeTableSO.Level level = entry.levels[i];
                if (level == null)
                    continue;

                using (new EditorGUILayout.VerticalScope("box", GUILayout.Width(118f)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label($"Lv{i + 1}", EditorStyles.boldLabel, GUILayout.Width(36f));
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(20f)))
                            remove = i;
                    }

                    EditorGUI.BeginChangeCheck();
                    int cost = Row("비용", level.cost);
                    float flat = Row("고정", level.flatDamage);
                    float percent = Row("%", level.percentDamage * 100f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_table, "강화 레벨 수정");
                        level.cost = Mathf.Max(0, cost);
                        level.flatDamage = flat;
                        level.percentDamage = percent / 100f;
                        EditorUtility.SetDirty(_table);
                    }
                }
            }

            if (remove >= 0)
            {
                Undo.RecordObject(_table, "강화 레벨 삭제");
                entry.levels.RemoveAt(remove);
                EditorUtility.SetDirty(_table);
            }

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(60f)))
            {
                if (GUILayout.Button("+ 레벨"))
                {
                    Undo.RecordObject(_table, "강화 레벨 추가");
                    SkillUpgradeTableSO.Level last = entry.levels.Count > 0 ? entry.levels[entry.levels.Count - 1] : null;
                    entry.levels.Add(new SkillUpgradeTableSO.Level
                    {
                        cost = last != null ? last.cost + 50 : 50,
                        flatDamage = last != null ? last.flatDamage : 0f,
                        percentDamage = last != null ? last.percentDamage : 0.1f
                    });
                    EditorUtility.SetDirty(_table);
                }

                float flatSum = 0f;
                float percentSum = 0f;
                int costSum = 0;
                foreach (SkillUpgradeTableSO.Level level in entry.levels)
                {
                    if (level == null)
                        continue;
                    flatSum += level.flatDamage;
                    percentSum += level.percentDamage;
                    costSum += level.cost;
                }

                EditorGUILayout.LabelField(SkillUpgradeService.Describe(flatSum, percentSum), EditorStyles.miniLabel, GUILayout.Width(60f));
                EditorGUILayout.LabelField($"{costSum}G", EditorStyles.miniLabel, GUILayout.Width(60f));
            }

            GUILayout.FlexibleSpace();
        }

        private static int Row(string label, int value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(30f));
                return EditorGUILayout.IntField(value);
            }
        }

        private static float Row(string label, float value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(30f));
                return EditorGUILayout.FloatField(value);
            }
        }

        // ---------- 필터 ----------

        private List<SkillUpgradeTableSO.Entry> Filtered()
        {
            List<SkillUpgradeTableSO.Entry> result = new List<SkillUpgradeTableSO.Entry>();
            if (_table.Entries == null)
                return result;

            foreach (SkillUpgradeTableSO.Entry entry in _table.Entries)
            {
                if (entry == null)
                    continue;

                string name = entry.skill != null ? entry.skill.name : "(비어 있음)";
                if (_group != AllGroups && GroupOf(name) != _group)
                    continue;
                if (!string.IsNullOrEmpty(_search) && name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0
                    && (entry.skill == null || entry.skill.DisplayName.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                if (_equippedOnly && Application.isPlaying && !_equipped.ContainsKey(SkillUpgradeService.KeyOf(entry.skill)))
                    continue;

                result.Add(entry);
            }

            return result;
        }

        private List<string> Groups()
        {
            List<string> groups = new List<string> { AllGroups };
            if (_table == null || _table.Entries == null)
                return groups;

            foreach (SkillUpgradeTableSO.Entry entry in _table.Entries)
            {
                string group = GroupOf(entry != null && entry.skill != null ? entry.skill.name : string.Empty);
                if (!groups.Contains(group))
                    groups.Add(group);
            }

            return groups;
        }

        // 에셋 이름 앞부분(Earth_Boulder → Earth)으로 묶는다.
        private static string GroupOf(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
                return "기타";

            int underscore = assetName.IndexOf('_');
            if (underscore > 0)
                return assetName.Substring(0, underscore);

            for (int i = 1; i < assetName.Length; i++)
            {
                if (char.IsUpper(assetName[i]))
                    return assetName.Substring(0, i);
            }

            return assetName;
        }

        private int CountEntries() => _table != null && _table.Entries != null ? _table.Entries.Count : 0;

        private void CollectEquipped()
        {
            _equipped.Clear();
            if (!Application.isPlaying)
                return;

            PlayerAgent player = FindFirstObjectByType<PlayerAgent>();
            if (player == null || player.SkillFsm == null)
                return;

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                SkillData skill = player.SkillFsm.GetSkill(slot);
                if (skill != null && !(skill is EmptySkillData))
                    _equipped[SkillUpgradeService.KeyOf(skill)] = slot;
            }
        }

        private static string SlotLabel(SkillSlotId slot)
        {
            switch (slot)
            {
                case SkillSlotId.Basic1: return "Q";
                case SkillSlotId.Basic2: return "E";
                case SkillSlotId.Weapon: return "우클릭";
                case SkillSlotId.Dash: return "Space";
                case SkillSlotId.Ultimate: return "R";
                default: return slot.ToString();
            }
        }
    }
}
