using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Members.KYR._01.Scripts.Currency;
using Members.KYR._01.Scripts.Enchant.Data;
using Members.KYR._01.Scripts.Enchant.So;
using Members.KYR._01.Scripts.Stats;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Members.KYR._01.Scripts.Enchant.Editor
{
    public sealed class EnchantTreeWindow : EditorWindow
    {
        private const string SessionKey = "KYR.EnchantTree.Path";

        private EnchantTree _tree;
        private ObjectField _treeField;
        private ObjectField _characterField;
        private Button _addButton;
        private Button _deleteButton;
        private EnchantTreeGraphView _graph;
        private ScrollView _inspector;
        private EnchantTreeNodeView _selected;
        private List<EnchantTreeIssue> _issues;

        [MenuItem("KYR/인챈트 트리 편집기")]
        public static void OpenMenu()
        {
            Open(Selection.activeObject as EnchantTree);
        }

        [OnOpenAsset(1)]
        public static bool OpenAsset(int instanceId, int line)
        {
            EnchantTree tree = EditorUtility.InstanceIDToObject(instanceId) as EnchantTree;
            if (tree == null)
                return false;

            Open(tree);
            return true;
        }

        public static void Open(EnchantTree tree)
        {
            EnchantTreeWindow window = GetWindow<EnchantTreeWindow>("인챈트 트리");
            window.Show();
            window.SetTree(tree);
        }

        private void OnEnable()
        {
            minSize = new Vector2(880f, 480f);
            Undo.undoRedoPerformed += OnUndo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
        }

        private void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            Toolbar toolbar = new Toolbar();
            _treeField = new ObjectField("트리")
            {
                objectType = typeof(EnchantTree),
                allowSceneObjects = false
            };
            _treeField.style.width = 280;
            _treeField.RegisterValueChangedCallback(evt => SetTree(evt.newValue as EnchantTree));
            toolbar.Add(_treeField);

            _characterField = new ObjectField("캐릭터")
            {
                objectType = typeof(CharacterData),
                allowSceneObjects = false
            };
            _characterField.style.width = 280;
            _characterField.RegisterValueChangedCallback(evt =>
            {
                if (_tree == null)
                    return;

                Undo.RecordObject(_tree, "캐릭터");
                _tree.Character = evt.newValue as CharacterData;
                EditorUtility.SetDirty(_tree);
            });
            toolbar.Add(_characterField);

            toolbar.Add(new ToolbarButton(CreateTree) { text = "새 트리" });
            _addButton = new ToolbarButton(AddNode) { text = "노드 추가" };
            _deleteButton = new ToolbarButton(DeleteSelection) { text = "선택 삭제" };
            toolbar.Add(_addButton);
            toolbar.Add(_deleteButton);
            toolbar.Add(new ToolbarButton(() =>
            {
                if (_graph != null)
                    _graph.FrameAll();
            })
            { text = "전체 보기" });
            toolbar.Add(new ToolbarButton(RunValidation) { text = "검사" });
            rootVisualElement.Add(toolbar);

            VisualElement body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            rootVisualElement.Add(body);

            _graph = new EnchantTreeGraphView();
            _graph.SelectionChanged += OnSelectionChanged;
            _graph.Rejected += message => ShowNotification(new GUIContent(message));
            body.Add(_graph);

            _inspector = new ScrollView();
            _inspector.style.width = 300;
            _inspector.style.paddingLeft = 8;
            _inspector.style.paddingRight = 8;
            _inspector.style.paddingTop = 8;
            _inspector.style.borderLeftWidth = 1;
            _inspector.style.borderLeftColor = new Color(0f, 0f, 0f, 0.4f);
            body.Add(_inspector);

            if (_tree == null)
                _tree = Selection.activeObject as EnchantTree;
            if (_tree == null)
                _tree = AssetDatabase.LoadAssetAtPath<EnchantTree>(SessionState.GetString(SessionKey, string.Empty));

            SetTree(_tree);
        }

        private void OnUndo()
        {
            if (_graph == null)
                return;

            string id = _graph.SelectedId;
            if (_characterField != null && _tree != null)
                _characterField.SetValueWithoutNotify(_tree.Character);
            _graph.SetTree(_tree);
            _graph.Select(id);
        }

        private void SetTree(EnchantTree tree)
        {
            _tree = tree;
            _selected = null;
            _issues = null;
            SessionState.SetString(SessionKey, tree != null ? AssetDatabase.GetAssetPath(tree) : string.Empty);

            if (_treeField == null)
                return;

            _treeField.SetValueWithoutNotify(tree);
            _characterField.SetEnabled(tree != null);
            _characterField.SetValueWithoutNotify(tree != null ? tree.Character : null);
            _addButton.SetEnabled(tree != null);
            _deleteButton.SetEnabled(tree != null);
            _graph.SetTree(tree);
            ShowInspector();
        }

        private void OnSelectionChanged(EnchantTreeNodeView view)
        {
            _selected = view;
            ShowInspector();
        }

        private void AddNode()
        {
            if (_graph == null || _tree == null)
                return;

            _graph.AddNode(_graph.ViewCenter());
        }

        private void DeleteSelection()
        {
            if (_graph == null)
                return;

            _graph.DeleteSelection();
        }

        private void CreateTree()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "인챈트 트리",
                "EnchantTree",
                "asset",
                "저장할 위치를 고르세요.",
                "Assets/Members/KYR/03.SO/EnchantSO");
            if (string.IsNullOrEmpty(path))
                return;

            EnchantTree tree = CreateInstance<EnchantTree>();
            AssetDatabase.CreateAsset(tree, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = tree;
            SetTree(tree);
        }

        private void ShowInspector()
        {
            if (_inspector == null)
                return;

            _inspector.Clear();
            _inspector.Add(new HelpBox(
                "오른쪽 포트에서 다음 노드의 왼쪽으로 잇습니다. 들어오는 선이 여러 개면 그중 하나만 선행으로 칩니다.",
                HelpBoxMessageType.Info));

            if (_tree == null)
            {
                _inspector.Add(Hint("트리를 선택하거나 새로 만드세요."));
                return;
            }

            ShowIssues();

            if (_selected == null || _selected.Data == null)
            {
                EnumField currency = new EnumField("비용 재화", _tree.CostCurrency);
                currency.style.marginTop = 8;
                currency.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_tree, "비용 재화");
                    _tree.CostCurrency = (CurrencyType)evt.newValue;
                    EditorUtility.SetDirty(_tree);
                });
                _inspector.Add(currency);
                _inspector.Add(Hint("노드를 선택하면 이름, 종류, 랭크, 비용, 배타 그룹, 설명, 효과를 고칠 수 있습니다."));
                return;
            }

            EnchantTreeNode data = _selected.Data;
            EnchantTreeNodeView view = _selected;

            TextField id = new TextField("ID") { value = data.id, isReadOnly = true };
            _inspector.Add(id);

            TextField name = new TextField("이름") { value = data.displayName ?? string.Empty, isDelayed = true };
            name.RegisterValueChangedCallback(evt => Edit(view, "이름", () => view.Data.displayName = evt.newValue));
            _inspector.Add(name);

            List<EnchantNodeKind> kinds = new List<EnchantNodeKind>((EnchantNodeKind[])Enum.GetValues(typeof(EnchantNodeKind)));
            PopupField<EnchantNodeKind> kind = new PopupField<EnchantNodeKind>("종류", kinds, data.kind, EnchantNodeKindStyle.LabelOf, EnchantNodeKindStyle.LabelOf);
            kind.RegisterValueChangedCallback(evt => Edit(view, "종류", () => view.Data.kind = evt.newValue));
            _inspector.Add(kind);

            IntegerField max = new IntegerField("최대 랭크") { value = data.maxRank, isDelayed = true };
            max.RegisterValueChangedCallback(evt =>
            {
                int value = Mathf.Max(1, evt.newValue);
                max.SetValueWithoutNotify(value);
                Edit(view, "최대 랭크", () => view.Data.maxRank = value);
            });
            _inspector.Add(max);

            IntegerField cost = new IntegerField("비용") { value = data.cost, isDelayed = true };
            cost.RegisterValueChangedCallback(evt =>
            {
                int value = Mathf.Max(0, evt.newValue);
                cost.SetValueWithoutNotify(value);
                Edit(view, "비용", () => view.Data.cost = value);
            });
            _inspector.Add(cost);

            TextField excl = new TextField("배타 그룹") { value = data.exclusiveGroup ?? string.Empty, isDelayed = true };
            excl.RegisterValueChangedCallback(evt => Edit(view, "배타 그룹", () => view.Data.exclusiveGroup = evt.newValue));
            _inspector.Add(excl);

            TextField desc = new TextField("설명") { value = data.description ?? string.Empty, isDelayed = true, multiline = true };
            desc.style.minHeight = 96;
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.RegisterValueChangedCallback(evt => Edit(view, "설명", () => view.Data.description = evt.newValue));
            _inspector.Add(desc);

            ShowEffects(view);
        }

        // 효과 목록. SerializedObject로 고치면 노드 객체가 새로 만들어져 그래프가 들고 있는 참조가 끊기므로 직접 고친다.
        private void ShowEffects(EnchantTreeNodeView view)
        {
            Label header = new Label("효과 (랭크당 값)");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 10;
            _inspector.Add(header);

            List<EnchantStatEffect> effects = view.Data.Effects;
            List<PlayerStatId> stats = new List<PlayerStatId>((PlayerStatId[])Enum.GetValues(typeof(PlayerStatId)));
            stats.Remove(PlayerStatId.None);

            for (int i = 0; i < effects.Count; i++)
            {
                EnchantStatEffect effect = effects[i];
                if (effect == null)
                    continue;

                VisualElement box = new VisualElement();
                box.style.marginTop = 4;
                box.style.paddingLeft = 4;
                box.style.paddingRight = 4;
                box.style.paddingTop = 4;
                box.style.paddingBottom = 4;
                box.style.backgroundColor = new Color(0f, 0f, 0f, 0.15f);

                PlayerStatId current = stats.Contains(effect.stat) ? effect.stat : stats[0];
                PopupField<PlayerStatId> stat = new PopupField<PlayerStatId>("스탯", stats, current, EnchantText.StatLabel, EnchantText.StatLabel);
                stat.RegisterValueChangedCallback(evt => Edit(view, "효과 스탯", () => effect.stat = evt.newValue));
                box.Add(stat);

                EnumField type = new EnumField("방식", effect.type);
                type.tooltip = "Flat: 값 그대로 더함 / PercentAdd: 다른 % 와 합산 / PercentMore: 따로 곱함";
                box.Add(type);

                FloatField value = new FloatField("랭크당 값") { value = effect.valuePerRank, isDelayed = true };
                value.tooltip = "PercentAdd/PercentMore는 0.05 = 5%";
                box.Add(value);

                Label preview = new Label();
                preview.style.opacity = 0.7f;
                box.Add(preview);
                void UpdatePreview() => preview.text = "1랭크 " + EnchantText.Effect(effect, 1) + "   최대 " + EnchantText.Effect(effect, view.Data.maxRank);
                UpdatePreview();

                stat.RegisterValueChangedCallback(_ => UpdatePreview());
                type.RegisterValueChangedCallback(evt =>
                {
                    Edit(view, "효과 방식", () => effect.type = (StatModifierType)evt.newValue);
                    UpdatePreview();
                });
                value.RegisterValueChangedCallback(evt =>
                {
                    Edit(view, "효과 값", () => effect.valuePerRank = evt.newValue);
                    UpdatePreview();
                });

                int index = i;
                box.Add(new Button(() =>
                {
                    Edit(view, "효과 삭제", () => view.Data.Effects.RemoveAt(index));
                    ShowInspector();
                })
                { text = "효과 삭제" });

                _inspector.Add(box);
            }

            _inspector.Add(new Button(() =>
            {
                Edit(view, "효과 추가", () => view.Data.Effects.Add(new EnchantStatEffect()));
                ShowInspector();
            })
            { text = "효과 추가" });
        }

        private void RunValidation()
        {
            if (_tree == null)
                return;

            _issues = EnchantTreeValidator.Validate(_tree);
            if (_issues.Count == 0)
                ShowNotification(new GUIContent("문제 없음"));
            ShowInspector();
        }

        private void ShowIssues()
        {
            if (_issues == null || _issues.Count == 0)
                return;

            Label header = new Label("검사 결과 " + _issues.Count + "건 (누르면 노드 선택)");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            _inspector.Add(header);

            foreach (EnchantTreeIssue issue in _issues)
            {
                HelpBox box = new HelpBox(issue.Message, issue.IsError ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning);
                string id = issue.NodeId;
                if (!string.IsNullOrEmpty(id))
                    box.RegisterCallback<ClickEvent>(_ => _graph.Select(id));
                _inspector.Add(box);
            }

            _inspector.Add(new Button(() =>
            {
                _issues = null;
                ShowInspector();
            })
            { text = "검사 결과 닫기" });
        }

        private void Edit(EnchantTreeNodeView view, string undo, Action apply)
        {
            if (_tree == null || view == null || view.Data == null)
                return;

            Undo.RecordObject(_tree, undo);
            apply();
            EditorUtility.SetDirty(_tree);
            view.Refresh();
        }

        private static Label Hint(string text)
        {
            Label label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 8;
            return label;
        }
    }

    [CustomEditor(typeof(EnchantTree))]
    public sealed class EnchantTreeInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            root.Add(new Button(() => EnchantTreeWindow.Open((EnchantTree)target)) { text = "트리 편집기 열기" });
            root.Add(new PropertyField(serializedObject.FindProperty("character")));
            root.Add(new PropertyField(serializedObject.FindProperty("costCurrency"), "비용 재화"));
            root.Add(new Label("노드 " + ((EnchantTree)target).Nodes.Count + "개 · 연결 " + ((EnchantTree)target).Links.Count + "개"));
            root.Bind(serializedObject);
            return root;
        }
    }
}
