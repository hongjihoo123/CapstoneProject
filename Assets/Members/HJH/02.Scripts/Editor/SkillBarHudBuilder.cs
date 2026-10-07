using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.UI;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01_Scripts;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // HJH > Build Skill Bar HUD: places the skill bar into the open scene as normal, editable objects.
    // Safe to run again: the previous "SkillBar" is replaced.
    public static class SkillBarHudBuilder
    {
        private const string IconRoot = "Assets/Members/HJH/UI/Icons";
        private const string PalettePath = "Assets/Members/HJH/Data/UI/ElementPalette.asset";
        private const string SkillDataRoot = "Assets/Members/HJH/Data/Skill";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string OutlineMaterialPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";

        private const float SlotSize = 100f;
        private const float SlotGap = 26f;
        private const float FrameThickness = 12f;
        private const float PassiveSize = 76f;
        private const float PassiveGap = 56f;

        private static readonly (SkillSlotId slot, string name)[] BarSlots =
        {
            (SkillSlotId.Basic1, "Q"),
            (SkillSlotId.Basic2, "E"),
            (SkillSlotId.Weapon, "RMB"),
            (SkillSlotId.Ultimate, "R"),
            (SkillSlotId.Dash, "Space"),
        };

        private static readonly (ElementType element, string hex)[] ElementColors =
        {
            (ElementType.Fire, "#F08030"),
            (ElementType.Water, "#5090D6"),
            (ElementType.Wind, "#3FB89A"),
            (ElementType.Electric, "#E8C21C"),
            (ElementType.Earth, "#C8743C"),
        };

        // Temporary icon per skill asset (file name in Icons/Skills).
        private static readonly (string asset, string icon)[] SkillIcons =
        {
            ("ChainDash", "Dash"),
            ("ChainDashStrike", "DashStrike"),
            ("ChainPullSpin", "PullSpin"),
            ("Basic_E_Placeholder", "BasicE"),
            ("Basic_F_Placeholder", "BasicF"),
            ("Kill Count Buff Passive Data", "Passive"),
        };

        private const string HjhScenePath = "Assets/Members/HJH/Scene/WeaponSkillTest.unity";

        // Batch entry: Unity.exe -batchmode -quit -executeMethod Assets.Members.HJH._02.Scripts.EditorTools.SkillBarHudBuilder.ApplyToHjhScene
        // Opens the HJH test scene, puts the E/F placeholder skills in, builds the skill bar and saves.
        public static void ApplyToHjhScene()
        {
            var scene = EditorSceneManager.OpenScene(HjhScenePath, OpenSceneMode.Single);
            EquipPlaceholderBasics();
            Build();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SkillBarHud] HJH 씬 적용 완료");
        }

        private static void EquipPlaceholderBasics()
        {
            SkillStateModule module = Object.FindFirstObjectByType<SkillStateModule>();
            if (module == null)
                return;

            var so = new SerializedObject(module);
            SerializedProperty list = so.FindProperty("initialSkills");
            SetInitialSkill(list, SkillSlotId.Basic1, $"{SkillDataRoot}/Basic_E_Placeholder.asset");
            SetInitialSkill(list, SkillSlotId.Basic2, $"{SkillDataRoot}/Basic_F_Placeholder.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInitialSkill(SerializedProperty list, SkillSlotId slot, string assetPath)
        {
            var data = AssetDatabase.LoadAssetAtPath<SkillData>(assetPath);
            if (data == null)
            {
                Debug.LogWarning($"[SkillBarHud] {assetPath} 를 찾을 수 없습니다.");
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("slot").enumValueIndex == (int)slot)
                {
                    entry.FindPropertyRelative("data").objectReferenceValue = data;
                    return;
                }
            }

            list.arraySize++;
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("slot").enumValueIndex = (int)slot;
            added.FindPropertyRelative("data").objectReferenceValue = data;
        }

        [MenuItem("HJH/Build Skill Bar HUD")]
        public static void Build()
        {
            PlayerAgent player = Object.FindFirstObjectByType<PlayerAgent>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Skill Bar HUD", "PlayerAgent가 있는 씬을 열고 실행하세요.", "OK");
                return;
            }

            ImportIconsAsSprites();
            HjhUiSkin.Load();
            Sprite white = LoadSprite($"{IconRoot}/White.png");
            ElementPalette palette = CreateOrUpdatePalette();
            AssignSkillIcons();

            Canvas canvas = CreateCanvas();
            RectTransform bar = CreateBar(canvas.transform, player, palette, white);
            MoveElementTimerAbove(bar);
            CreateCharacterPlate(canvas.transform);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Selection.activeGameObject = bar.gameObject;
            Debug.Log("[SkillBarHud] 스킬바를 씬에 배치했습니다. 씬을 저장하세요.");
        }

        private static void ImportIconsAsSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.textureType == TextureImporterType.Sprite)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static ElementPalette CreateOrUpdatePalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<ElementPalette>(PalettePath);
            if (palette == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PalettePath));
                palette = ScriptableObject.CreateInstance<ElementPalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }

            var so = new SerializedObject(palette);
            SerializedProperty entries = so.FindProperty("entries");
            entries.arraySize = ElementColors.Length;

            for (int i = 0; i < ElementColors.Length; i++)
            {
                (ElementType element, string hex) = ElementColors[i];
                ColorUtility.TryParseHtmlString(hex, out Color color);

                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("element").enumValueIndex = (int)element;
                entry.FindPropertyRelative("color").colorValue = color;
                entry.FindPropertyRelative("badge").objectReferenceValue = LoadSprite($"{IconRoot}/Elements/{element}.png");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return palette;
        }

        private static void AssignSkillIcons()
        {
            foreach ((string asset, string icon) in SkillIcons)
            {
                Object data = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{SkillDataRoot}/{asset}.asset");
                if (data == null)
                    continue;

                var so = new SerializedObject(data);
                SerializedProperty property = so.FindProperty("icon");
                if (property == null || property.objectReferenceValue != null)
                    continue;

                property.objectReferenceValue = LoadSprite($"{IconRoot}/Skills/{icon}.png");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
        }

        private static Canvas CreateCanvas()
        {
            GameObject existing = GameObject.Find("HUD_Canvas");
            if (existing != null && existing.TryGetComponent(out Canvas found))
                return found;

            var go = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Create HUD Canvas");

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static RectTransform CreateBar(Transform canvas, PlayerAgent player, ElementPalette palette, Sprite white)
        {
            Transform old = canvas.Find("SkillBar");
            if (old != null)
            {
                // JJH's timer slider lives under the old bar (moved there below); keep it alive across rebuilds.
                ElementStackView stackView = Object.FindFirstObjectByType<ElementStackView>(FindObjectsInactive.Include);
                if (stackView != null && new SerializedObject(stackView).FindProperty("timerSlider").objectReferenceValue is Slider slider
                    && slider.transform.IsChildOf(old))
                    Undo.SetTransformParent(slider.transform, canvas, "Keep Element Timer");
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            float slotsWidth = BarSlots.Length * SlotSize + (BarSlots.Length - 1) * SlotGap;
            float totalWidth = PassiveSize + PassiveGap + slotsWidth;

            RectTransform bar = NewRect("SkillBar", canvas);
            Undo.RegisterCreatedObjectUndo(bar.gameObject, "Create Skill Bar");
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 36f);
            bar.sizeDelta = new Vector2(totalWidth, SlotSize + 60f);

            float left = -totalWidth * 0.5f;
            Image passiveIcon = CreatePassive(bar, new Vector2(left + PassiveSize * 0.5f, SlotSize * 0.5f + 14f));

            var bindings = new List<SkillBarView.SlotBinding>();
            float x = left + PassiveSize + PassiveGap + SlotSize * 0.5f;
            foreach ((SkillSlotId slot, string name) in BarSlots)
            {
                SkillSlotView view = CreateSlot(bar, $"Slot_{name}", new Vector2(x, SlotSize * 0.5f + 14f), white);
                Sprite keyIcon = slot == SkillSlotId.Dash ? HjhUiSkin.SpaceKey : null;
                bindings.Add(new SkillBarView.SlotBinding { slot = slot, view = view, keyIcon = keyIcon });
                x += SlotSize + SlotGap;
            }

            var barView = bar.gameObject.AddComponent<SkillBarView>();
            var so = new SerializedObject(barView);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("palette").objectReferenceValue = palette;
            so.FindProperty("passiveIcon").objectReferenceValue = passiveIcon;
            SerializedProperty slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = bindings.Count;
            for (int i = 0; i < bindings.Count; i++)
            {
                SerializedProperty element = slotsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("slot").enumValueIndex = (int)bindings[i].slot;
                element.FindPropertyRelative("view").objectReferenceValue = bindings[i].view;
                element.FindPropertyRelative("keyIcon").objectReferenceValue = bindings[i].keyIcon;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            return bar;
        }

        private static Image CreatePassive(RectTransform bar, Vector2 center)
        {
            ColorUtility.TryParseHtmlString("#4A44C8", out Color blue);
            RectTransform root = NewRect("Passive", bar);
            Place(root, center, new Vector2(PassiveSize, PassiveSize));
            Image frame = root.gameObject.AddComponent<Image>();
            frame.sprite = HjhUiSkin.CapFrame;
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 0.8f;
            frame.color = blue;

            RectTransform iconRect = NewRect("Icon", root);
            Stretch(iconRect, 10f);
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;

            TMP_Text label = NewText("Label", root, "PASSIVE", 18f, blue, TextAlignmentOptions.Center, false);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 4f);
            labelRect.sizeDelta = new Vector2(120f, 24f);

            return icon;
        }

        private static SkillSlotView CreateSlot(RectTransform bar, string name, Vector2 center, Sprite white)
        {
            RectTransform root = NewRect(name, bar);
            Place(root, center, new Vector2(SlotSize, SlotSize));

            // Combo glow sits behind the frame and spills past it (driven by SkillSlotView).
            RectTransform glowRect = NewRect("ComboGlow", root);
            Stretch(glowRect, -10f);
            Image glow = glowRect.gameObject.AddComponent<Image>();
            glow.sprite = HjhUiSkin.CapOutline;
            glow.type = Image.Type.Sliced;
            glow.pixelsPerUnitMultiplier = 0.7f;
            glow.raycastTarget = false;
            glow.enabled = false;

            RectTransform frameRect = NewRect("Frame", root);
            Stretch(frameRect, 0f);
            Image frame = frameRect.gameObject.AddComponent<Image>();
            frame.sprite = HjhUiSkin.CapFrame;
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 0.7f;
            frame.raycastTarget = false;

            RectTransform backingRect = NewRect("Backing", root);
            Stretch(backingRect, FrameThickness);
            Image backing = backingRect.gameObject.AddComponent<Image>();
            backing.sprite = white;
            backing.color = new Color(0.05f, 0.05f, 0.07f, 0.85f);
            backing.raycastTarget = false;

            RectTransform iconRect = NewRect("Icon", root);
            Stretch(iconRect, FrameThickness);
            Image icon = iconRect.gameObject.AddComponent<Image>();

            RectTransform cooldownRect = NewRect("Cooldown", root);
            Stretch(cooldownRect, FrameThickness);
            Image cooldown = cooldownRect.gameObject.AddComponent<Image>();
            cooldown.sprite = white;
            cooldown.color = new Color(0f, 0f, 0f, 0.65f);
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            cooldown.fillClockwise = false;
            cooldown.raycastTarget = false;
            cooldown.enabled = false;

            TMP_Text cooldownText = NewText("CooldownText", root, "", 36f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(cooldownText.rectTransform, FrameThickness);
            cooldownText.enabled = false;

            RectTransform badgeRect = NewRect("ElementBadge", root);
            badgeRect.anchorMin = badgeRect.anchorMax = Vector2.zero;
            badgeRect.anchoredPosition = new Vector2(4f, 4f);
            badgeRect.sizeDelta = new Vector2(40f, 40f);
            Image badge = badgeRect.gameObject.AddComponent<Image>();
            badge.preserveAspect = true;
            badge.raycastTarget = false;

            TMP_Text key = NewText("Key", root, "", 36f, Color.white, TextAlignmentOptions.BottomRight, true);
            key.enableAutoSizing = true;
            key.fontSizeMin = 14f;
            key.fontSizeMax = 36f;
            key.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform keyRect = key.rectTransform;
            keyRect.anchorMin = keyRect.anchorMax = new Vector2(1f, 0f);
            keyRect.pivot = new Vector2(1f, 0f);
            keyRect.anchoredPosition = new Vector2(-4f, 2f);
            keyRect.sizeDelta = new Vector2(72f, 42f);

            // Wide keys (Space) hang a key-cap icon under the slot instead of the corner text.
            RectTransform keyIconRect = NewRect("KeyIcon", root);
            keyIconRect.anchorMin = keyIconRect.anchorMax = new Vector2(0.5f, 0f);
            keyIconRect.anchoredPosition = new Vector2(0f, -6f);
            keyIconRect.sizeDelta = new Vector2(76f, 38f);
            Image keyIcon = keyIconRect.gameObject.AddComponent<Image>();
            keyIcon.preserveAspect = true;
            keyIcon.color = new Color(1f, 0.83f, 0.3f);
            keyIcon.raycastTarget = false;
            keyIcon.enabled = false;

            var view = root.gameObject.AddComponent<SkillSlotView>();
            var so = new SerializedObject(view);
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("cooldownOverlay").objectReferenceValue = cooldown;
            so.FindProperty("cooldownText").objectReferenceValue = cooldownText;
            so.FindProperty("elementBadge").objectReferenceValue = badge;
            so.FindProperty("keyText").objectReferenceValue = key;
            so.FindProperty("comboGlow").objectReferenceValue = glow;
            so.FindProperty("keyIcon").objectReferenceValue = keyIcon;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // Top-left character plate (replaces the switcher's old OnGUI label): name, ornament line, gray hint.
        private static void CreateCharacterPlate(Transform canvas)
        {
            CharacterSwitcher switcher = Object.FindFirstObjectByType<CharacterSwitcher>();
            Transform old = canvas.Find("CharacterPlate");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            if (switcher == null)
                return;

            RectTransform plate = NewRect("CharacterPlate", canvas);
            Undo.RegisterCreatedObjectUndo(plate.gameObject, "Create Character Plate");
            plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0f, 1f);
            plate.anchoredPosition = new Vector2(28f, -20f);
            plate.sizeDelta = new Vector2(420f, 90f);

            TMP_Text name = PlateText("Name", plate, 40f, Color.white, HjhUiSkin.KoreanDisplayFont, 0f, 48f);
            name.fontStyle = FontStyles.Bold;

            RectTransform accentRect = NewRect("Accent", plate);
            // Mirrored so the ornament sits under the start of the name and the line fades out to the right.
            accentRect.anchorMin = accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = new Vector2(120f, -50f);
            accentRect.sizeDelta = new Vector2(240f, 14f);
            accentRect.localScale = new Vector3(-1f, 1f, 1f);
            Image accent = accentRect.gameObject.AddComponent<Image>();
            accent.sprite = HjhUiSkin.DividerFade;
            accent.type = Image.Type.Sliced;
            accent.raycastTarget = false;

            TMP_Text hint = PlateText("Hint", plate, 15f, new Color(1f, 1f, 1f, 0.6f), HjhUiSkin.BodyFont, -66f, 20f);

            var view = plate.gameObject.AddComponent<CharacterNameView>();
            var so = new SerializedObject(view);
            so.FindProperty("switcher").objectReferenceValue = switcher;
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("accent").objectReferenceValue = accent;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_Text PlateText(string name, RectTransform parent, float size, Color color, TMP_FontAsset font, float top, float height)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, top);
            rect.sizeDelta = new Vector2(420f, height);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.BottomLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        // Layout B: keep JJH's 3-second timer bar, place it above the skill icons, hide the stack slots.
        private static void MoveElementTimerAbove(RectTransform bar)
        {
            ElementStackView stackView = Object.FindFirstObjectByType<ElementStackView>(FindObjectsInactive.Include);
            if (stackView == null)
                return;

            var so = new SerializedObject(stackView);
            if (so.FindProperty("timerSlider").objectReferenceValue is Slider slider)
            {
                var rect = (RectTransform)slider.transform;
                Undo.SetTransformParent(rect, bar, "Move Element Timer");
                float slotsLeft = -bar.sizeDelta.x * 0.5f + PassiveSize + PassiveGap;
                float slotsWidth = bar.sizeDelta.x - PassiveSize - PassiveGap;

                Undo.RecordObject(rect, "Move Element Timer");
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.localScale = Vector3.one;
                rect.anchoredPosition = new Vector2(slotsLeft, SlotSize + 30f);
                rect.sizeDelta = new Vector2(slotsWidth, 10f);
            }

            SerializedProperty slots = so.FindProperty("slotImages");
            for (int i = 0; i < slots.arraySize; i++)
            {
                if (slots.GetArrayElementAtIndex(i).objectReferenceValue is Image image)
                {
                    Undo.RecordObject(image.gameObject, "Hide Element Slots");
                    image.gameObject.SetActive(false);
                }
            }
        }

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        internal static void Place(RectTransform rect, Vector2 center, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }

        internal static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions alignment, bool outline)
        {
            RectTransform rect = NewRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (outline)
                tmp.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);

            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        internal static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
