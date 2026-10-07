using System.IO;
using Assets.Members.HJH._02.Scripts.Char.TopDown;
using Assets.Members.HJH._02.Scripts.UI;
using Members.KYR._01_Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // Scene / prefab side of the HUD polish pass. Safe to run again: everything already present is skipped.
    //  - SkillSlotView: Content holder (what shakes), CooldownEdge, ReadyFlash, ReadyRing
    //  - AimCursor prefab (Data/UI/AimCursor.prefab, ring sprite UI/Cursor/AimRing.png) placed and wired
    //  - SkillSwapPanel: hidden FlightIcon on the HUD canvas + skill bar reference
    //  - ElementComboHud: CanvasGroup on the bracket
    // Menu: HJH > UI > Apply HUD Polish (open scene) / (all HJH test scenes).
    // Batch: Unity.exe -batchmode -quit -projectPath <project> -executeMethod
    //        Assets.Members.HJH._02.Scripts.EditorTools.HudPolishSetup.ApplyToTestScenesBatch
    public static class HudPolishSetup
    {
        private const string Root = "Assets/Members/HJH";
        private const string PrefabPath = Root + "/Data/UI/AimCursor.prefab";
        private const string RingPath = Root + "/UI/Cursor/AimRing.png";

        private static readonly string[] TestScenes =
        {
            Root + "/Scene/WeaponSkillTest.unity",
            Root + "/Scene/SkillComboTest.unity",
            Root + "/Scene/SkillSwapTest.unity",
        };

        [MenuItem("HJH/UI/Apply HUD Polish (open scene)")]
        public static void ApplyToOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("HJH/UI/Apply HUD Polish (all HJH test scenes)")]
        public static void ApplyToTestScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            foreach (string path in TestScenes)
            {
                if (!File.Exists(path))
                    continue;

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ApplyToScene(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[HudPolish] saved {path}");
            }
        }

        public static void ApplyToTestScenesBatch()
        {
            foreach (string path in TestScenes)
            {
                if (!File.Exists(path))
                    continue;

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ApplyToScene(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[HudPolish] saved {path}");
            }

            AssetDatabase.SaveAssets();
        }

        private static void ApplyToScene(Scene scene)
        {
            foreach (SkillSlotView slot in Object.FindObjectsByType<SkillSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UpgradeSlot(slot);

            PlaceAimCursor();

            foreach (SkillSwapPanel panel in Object.FindObjectsByType<SkillSwapPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                WireSwapPanel(panel);

            foreach (ElementComboHud hud in Object.FindObjectsByType<ElementComboHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                WireComboHud(hud);

            Debug.Log($"[HudPolish] applied to {scene.path}");
        }

        // ---------- Skill slots ----------

        private static void UpgradeSlot(SkillSlotView slot)
        {
            var so = new SerializedObject(slot);
            if (so.FindProperty("content").objectReferenceValue != null)
                return;

            var root = (RectTransform)slot.transform;
            // Move every existing child under Content in its original order (= draw order).
            var children = new Transform[root.childCount];
            for (int i = 0; i < children.Length; i++)
                children[i] = root.GetChild(i);

            RectTransform content = NewRect("Content", root);
            Stretch(content, 0f);
            foreach (Transform child in children)
                child.SetParent(content, true);

            var icon = so.FindProperty("icon").objectReferenceValue as Image;
            var cooldown = so.FindProperty("cooldownOverlay").objectReferenceValue as Image;
            var glow = so.FindProperty("comboGlow").objectReferenceValue as Image;

            // Bright line on the moving edge of the wedge: pivot at the slot center, pointing up at 0.
            RectTransform edgeRect = NewRect("CooldownEdge", content);
            edgeRect.anchorMin = edgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            edgeRect.pivot = new Vector2(0.5f, 0f);
            edgeRect.anchoredPosition = Vector2.zero;
            edgeRect.sizeDelta = new Vector2(3f, 40f);
            Image edge = edgeRect.gameObject.AddComponent<Image>();
            edge.color = new Color(1f, 0.93f, 0.75f, 0.95f);
            edge.raycastTarget = false;
            edge.enabled = false;
            PlaceAfter(edgeRect, cooldown != null ? cooldown.transform : null);

            // White quad over the icon area.
            RectTransform flashRect = NewRect("ReadyFlash", content);
            CopyRect(flashRect, icon != null ? icon.rectTransform : null);
            Image flash = flashRect.gameObject.AddComponent<Image>();
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.raycastTarget = false;
            flash.enabled = false;
            PlaceAfter(flashRect, edgeRect);

            // Outline ring that expands off the frame; same sprite as the combo glow.
            RectTransform ringRect = NewRect("ReadyRing", content);
            Stretch(ringRect, -4f);
            Image ring = ringRect.gameObject.AddComponent<Image>();
            if (glow != null)
            {
                ring.sprite = glow.sprite;
                ring.type = glow.type;
                ring.pixelsPerUnitMultiplier = glow.pixelsPerUnitMultiplier;
            }
            ring.raycastTarget = false;
            ring.enabled = false;
            ringRect.SetAsLastSibling();

            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("cooldownEdge").objectReferenceValue = edge;
            so.FindProperty("readyFlash").objectReferenceValue = flash;
            so.FindProperty("readyRing").objectReferenceValue = ring;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[HudPolish] upgraded slot {slot.name}");
        }

        // ---------- Aim cursor ----------

        private static void PlaceAimCursor()
        {
            var player = Object.FindFirstObjectByType<PlayerAgent>(FindObjectsInactive.Include);
            var aim = Object.FindFirstObjectByType<TopDownAimController>(FindObjectsInactive.Include);
            if (player == null || aim == null)
            {
                Debug.Log("[HudPolish] no player / TopDownAimController in scene, aim cursor skipped");
                return;
            }

            AimCursorView view = Object.FindFirstObjectByType<AimCursorView>(FindObjectsInactive.Include);
            if (view == null)
            {
                GameObject prefab = GetOrCreateCursorPrefab();
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "AimCursor";
                view = instance.GetComponent<AimCursorView>();
            }

            var so = new SerializedObject(view);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("aimController").objectReferenceValue = aim;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject GetOrCreateCursorPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
                return existing;

            Sprite ringSprite = GetOrCreateRingSprite();

            var root = new GameObject("AimCursor", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform crosshair = NewRect("Crosshair", root.transform);
            crosshair.anchorMin = crosshair.anchorMax = Vector2.zero;
            crosshair.sizeDelta = Vector2.zero;

            Image ring = NewImage("Ring", crosshair, new Vector2(30f, 30f));
            ring.sprite = ringSprite;
            Image dot = NewImage("Dot", crosshair, new Vector2(3f, 3f));

            var ticks = new Image[4];
            Vector2[] dirs = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            for (int i = 0; i < 4; i++)
            {
                Vector2 size = dirs[i].x != 0f ? new Vector2(8f, 2f) : new Vector2(2f, 8f);
                ticks[i] = NewImage($"Tick_{i}", crosshair, size);
                ticks[i].rectTransform.anchoredPosition = dirs[i] * 14f;
            }

            RectTransform hit = NewRect("HitMarker", crosshair);
            hit.sizeDelta = Vector2.zero;
            var marks = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + 90f * i;
                Image mark = NewImage($"Hit_{i}", hit, new Vector2(2.5f, 9f));
                mark.rectTransform.anchoredPosition = (Vector2)(Quaternion.Euler(0f, 0f, -angle) * Vector2.up) * 11f;
                mark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
                marks[i] = mark;
            }

            var view = root.AddComponent<AimCursorView>();
            var so = new SerializedObject(view);
            so.FindProperty("crosshair").objectReferenceValue = crosshair;
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("dot").objectReferenceValue = dot;
            SetArray(so.FindProperty("ticks"), ticks);
            so.FindProperty("hitMarker").objectReferenceValue = hit;
            SetArray(so.FindProperty("hitMarks"), marks);
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[HudPolish] created {PrefabPath}");
            return prefab;
        }

        // Anti-aliased 1.5px ring, written once as a PNG so it is a normal sprite asset.
        private static Sprite GetOrCreateRingSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
            if (sprite != null)
                return sprite;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f - 2f;
            const float halfWidth = 1.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) - radius);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(halfWidth + 0.5f - d)));
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(RingPath));
            File.WriteAllBytes(RingPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(RingPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(RingPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
        }

        // ---------- Swap panel / combo HUD ----------

        private static void WireSwapPanel(SkillSwapPanel panel)
        {
            var so = new SerializedObject(panel);

            if (so.FindProperty("skillBar").objectReferenceValue == null)
                so.FindProperty("skillBar").objectReferenceValue =
                    Object.FindFirstObjectByType<SkillBarView>(FindObjectsInactive.Include);

            if (so.FindProperty("flightIcon").objectReferenceValue == null)
            {
                Canvas canvas = panel.GetComponentInParent<Canvas>(true);
                Transform layer = canvas != null ? canvas.rootCanvas.transform : panel.transform.parent;
                Image icon = NewImage("SwapFlightIcon", layer, new Vector2(80f, 80f));
                icon.preserveAspect = true;
                icon.gameObject.SetActive(false);
                so.FindProperty("flightIcon").objectReferenceValue = icon;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireComboHud(ElementComboHud hud)
        {
            var so = new SerializedObject(hud);
            if (so.FindProperty("bracketGroup").objectReferenceValue != null)
                return;

            var bracket = so.FindProperty("bracket").objectReferenceValue as RectTransform;
            if (bracket == null)
                return;

            if (!bracket.TryGetComponent(out CanvasGroup group))
                group = bracket.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            so.FindProperty("bracketGroup").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- helpers ----------

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image NewImage(string name, Transform parent, Vector2 size)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void CopyRect(RectTransform target, RectTransform source)
        {
            if (source == null)
            {
                Stretch(target, 0f);
                return;
            }

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
        }

        private static void PlaceAfter(Transform item, Transform anchor)
        {
            if (anchor != null && anchor.parent == item.parent)
                item.SetSiblingIndex(anchor.GetSiblingIndex() + 1);
        }

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
