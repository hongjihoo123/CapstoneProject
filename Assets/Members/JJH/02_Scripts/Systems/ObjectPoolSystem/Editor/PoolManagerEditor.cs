using Members.JJH._02_Scripts.Systems.ObjectPoolSystem.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Members.JJH._02_Scripts.Systems.ObjectPoolSystem.Editor
{
    public class PoolManagerEditor : EditorWindow
    {
        [SerializeField] private VisualTreeAsset editorView = default;
        [SerializeField] private PoolManagerSO poolManager = default;
        [SerializeField] private VisualTreeAsset itemAsset = default;

        private string _rootFolder = "Assets/ObjectPool";

        private Button _createBtn;
        private ScrollView _itemView;

        private List<PoolItemView> _itemList;
        private PoolItemView _selectedItem;

        private UnityEditor.Editor _cachedEditor;
        private VisualElement _inspector;


        [MenuItem("Tools/PoolManager")]
        public static void OpenWindow()
        {
            PoolManagerEditor wnd = GetWindow<PoolManagerEditor>();
            wnd.titleContent = new GUIContent("PoolManagerEditor");
        }

        private string GetCurrentDirectory()
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            return System.IO.Path.GetDirectoryName(scriptPath);
        }

        private void InitializeRootFolder()
        {
            string dirName = GetCurrentDirectory();
            DirectoryInfo parentDirectory = Directory.GetParent(dirName);
            Debug.Assert(parentDirectory != null, $"Parent directory is null! Check path : {dirName}");

            string dataPath = Application.dataPath;
            _rootFolder = parentDirectory.FullName.Replace('\\', '/');
            if (_rootFolder.StartsWith(dataPath))
            {
                _rootFolder = "Assets" + _rootFolder.Substring(dataPath.Length);
            }
        }

        public void CreateGUI()
        {
            InitializeRootFolder();

            VisualElement root = rootVisualElement;

            if (editorView == null)
            {
                string dirName = GetCurrentDirectory();
                editorView = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{dirName}/PoolManagerEditor.uxml");
            }
            editorView.CloneTree(root);

            InitializeItems(root);
            GeneratePoolingItemUI();
        }

        private void InitializeItems(VisualElement root)
        {
            _createBtn = root.Q<Button>("CreateBtn");
            _createBtn.clicked += HandleCreateItem;
            _itemView = root.Q<ScrollView>("ItemView");

            _itemList = new List<PoolItemView>();
            _inspector = root.Q<VisualElement>("InspectorView");
        }

        private void GeneratePoolingItemUI()
        {
            _itemView.Clear();
            _itemList.Clear();
            _inspector.Clear();

            if (poolManager == null)
            {
                string poolManagerFilePath = $"{_rootFolder}/PoolManager.asset";
                poolManager = AssetDatabase.LoadAssetAtPath<PoolManagerSO>(poolManagerFilePath);
                if (poolManager == null)
                {
                    Debug.LogWarning("풀매니저 데이터가 존재하지 않습니다. 새것을 만듭니다.");
                    poolManager = ScriptableObject.CreateInstance<PoolManagerSO>();
                    AssetDatabase.CreateAsset(poolManager, poolManagerFilePath);
                }
            }

            if (itemAsset == null)
            {
                string dirName = GetCurrentDirectory();
                itemAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{dirName}/PoolItemView.uxml");
            }

            foreach (PoolItemSO item in poolManager.itemList)
            {
                TemplateContainer itemUI = itemAsset.Instantiate();
                PoolItemView itemView = new PoolItemView(itemUI, item);

                _itemView.Add(itemUI);
                _itemList.Add(itemView);

                itemView.Name = item.name;
                itemView.IsEmpty = item.prefab == null;
                itemView.IsActive = false;

                itemView.OnSelectEvent += HandleSelectEvent;
                itemView.OnDeleteEvent += HandleDeleteEvent;
            }
        }

        private void HandleSelectEvent(PoolItemView targetView)
        {
            if (_selectedItem != null)
                _selectedItem.IsActive = false;
            _selectedItem = targetView;
            _selectedItem.IsActive = true;

            _inspector.Clear();

            UnityEditor.Editor.CreateCachedEditor(_selectedItem.TargetItem, null, ref _cachedEditor);
            VisualElement inspectorElement = _cachedEditor.CreateInspectorGUI();

            SerializedObject serializedObject = new SerializedObject(_selectedItem.TargetItem);
            inspectorElement.Bind(serializedObject);

            inspectorElement.TrackSerializedObjectValue(serializedObject, so =>
            {
                _selectedItem.Name = so.FindProperty("poolingName").stringValue;
                _selectedItem.IsEmpty = so.FindProperty("prefab").objectReferenceValue == null;
            });

            _inspector.Add(inspectorElement);
        }

        private void HandleDeleteEvent(PoolItemView targetView)
        {
            poolManager.itemList.Remove(targetView.TargetItem);
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(targetView.TargetItem));
            EditorUtility.SetDirty(poolManager);

            AssetDatabase.SaveAssets();

            if (targetView == _selectedItem)
                _selectedItem = null;

            GeneratePoolingItemUI();
        }

        private void HandleCreateItem()
        {
            Guid itemGuid = Guid.NewGuid();
            PoolItemSO newItem = ScriptableObject.CreateInstance<PoolItemSO>();
            newItem.poolingName = itemGuid.ToString();

            if (Directory.Exists($"{_rootFolder}/Items") == false)
            {
                Directory.CreateDirectory($"{_rootFolder}/Items");
            }

            AssetDatabase.CreateAsset(newItem, $"{_rootFolder}/Items/{newItem.poolingName}.asset");
            poolManager.itemList.Add(newItem);

            EditorUtility.SetDirty(poolManager);
            AssetDatabase.SaveAssets();

            GeneratePoolingItemUI();
        }
    }
}