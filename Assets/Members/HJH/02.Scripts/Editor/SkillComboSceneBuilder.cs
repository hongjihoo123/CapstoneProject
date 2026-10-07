using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.UI;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01_Scripts;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Assets.Members.HJH._02.Scripts.EditorTools.SkillBarHudBuilder;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // HJH > Build Skill Combo Test Scene: copies WeaponSkillTest to SkillComboTest (first time only),
    // opens it, swaps JJH's stack manager for ElementComboChain and builds the combo HUD into HUD_Canvas:
    //  input trail (above the skill bar) / chain counter + finisher stock + move list (right) / banner (top).
    // Safe to run again: the previous "ElementComboHud" is replaced. The source scene is never touched.
    public static class SkillComboSceneBuilder
    {
        private const string SourceScenePath = "Assets/Members/HJH/Scene/WeaponSkillTest.unity";
        private const string ComboScenePath = "Assets/Members/HJH/Scene/SkillComboTest.unity";
        private const string PalettePath = "Assets/Members/HJH/Data/UI/ElementPalette.asset";
        private const string WhitePath = "Assets/Members/HJH/UI/Icons/White.png";

        private const int CellCount = 4;
        private const float CellSize = 72f;
        private const float CellGap = 26f;
        private const float NextGap = 44f;
        // Centre of the Q/E/F/R icons relative to the skill bar centre (passive sits on the left).
        private const float TrailCenterX = 66f;
        private const float TrailBottom = 222f;
        private const float RightMargin = -48f;

        private static readonly Color Gold = new Color(1f, 0.83f, 0.3f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);

        private static TMP_FontAsset _font;
        private static TMP_FontAsset _display;
        private static TMP_FontAsset _koreanDisplay;
        private static Sprite _white;
        private static Sprite _capFrame;
        private static Sprite _capOutline;
        private static Sprite _rowFrame;
        private static Sprite _smallFrame;
        private static Sprite _bannerFrame;
        private static Sprite _thinOutline;
        private static Sprite _divider;
        private static Sprite _dividerFade;
        private static Sprite _arrow;

        // Batch entry (Unity closed): Unity.exe -batchmode -quit -projectPath <proj>
        //   -executeMethod Assets.Members.HJH._02.Scripts.EditorTools.SkillComboSceneBuilder.BuildBatch
        public static void BuildBatch() => BuildScene(interactive: false);

        [MenuItem("HJH/Build Skill Combo Test Scene")]
        public static void BuildMenu() => BuildScene(interactive: true);

        private static void BuildScene(bool interactive)
        {
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ComboScenePath) == null && !AssetDatabase.CopyAsset(SourceScenePath, ComboScenePath))
            {
                Debug.LogError($"[SkillCombo] {SourceScenePath} 를 복사하지 못했습니다.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ComboScenePath, OpenSceneMode.Single);

            // Always rebuilt here so the skill bar has the combo glow and the same frames as the combo HUD.
            SkillBarHudBuilder.Build();

            if (!BuildHud())
                return;

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SkillCombo] {ComboScenePath} 에 콤보 HUD를 배치하고 저장했습니다.");
        }

        private static bool BuildHud()
        {
            Canvas canvas = GameObject.Find("HUD_Canvas").GetComponent<Canvas>();
            PlayerAgent player = Object.FindFirstObjectByType<PlayerAgent>();
            SkillStateModule skills = Object.FindFirstObjectByType<SkillStateModule>();
            ElementStackManager stackManager = Object.FindFirstObjectByType<ElementStackManager>(FindObjectsInactive.Include);
            ElementBuffController buffController = Object.FindFirstObjectByType<ElementBuffController>(FindObjectsInactive.Include);
            var palette = AssetDatabase.LoadAssetAtPath<ElementPalette>(PalettePath);

            if (stackManager == null || palette == null || skills == null)
            {
                Debug.LogError("[SkillCombo] ElementStackManager / ElementPalette / SkillStateModule 중 없는 것이 있습니다. 먼저 HJH > Build Skill Bar HUD 를 실행하세요.");
                return false;
            }

            HjhUiSkin.Load();
            _font = HjhUiSkin.BodyFont;
            _display = HjhUiSkin.DisplayFont;
            _koreanDisplay = HjhUiSkin.KoreanDisplayFont;
            _white = LoadSprite(WhitePath);
            _capFrame = HjhUiSkin.CapFrame;
            _capOutline = HjhUiSkin.CapOutline;
            _rowFrame = HjhUiSkin.RowFrame;
            _smallFrame = HjhUiSkin.SmallFrame;
            _bannerFrame = HjhUiSkin.BannerFrame;
            _thinOutline = HjhUiSkin.ThinOutline;
            _divider = HjhUiSkin.Divider;
            _dividerFade = HjhUiSkin.DividerFade;
            _arrow = HjhUiSkin.Arrow;

            ElementComboChain chain = ReplaceStackManager(stackManager, buffController, skills);

            Transform old = canvas.transform.Find("ElementComboHud");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform root = NewRect("ElementComboHud", canvas.transform);
            Stretch(root, 0f);

            ComboBanner banner = CreateBanner(root);
            CreateCounter(root, chain);
            CreateStock(root, chain, player);
            ComboListView list = CreateComboList(root);

            var hud = root.gameObject.AddComponent<ElementComboHud>();
            var so = new SerializedObject(hud);
            CreateTrail(root, so);
            so.FindProperty("chain").objectReferenceValue = chain;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("palette").objectReferenceValue = palette;
            so.FindProperty("banner").objectReferenceValue = banner;
            so.FindProperty("comboList").objectReferenceValue = list;
            so.ApplyModifiedPropertiesWithoutUndo();

            var link = root.gameObject.AddComponent<ComboSkillBarLink>();
            var linkSo = new SerializedObject(link);
            linkSo.FindProperty("chain").objectReferenceValue = chain;
            linkSo.FindProperty("skillBar").objectReferenceValue = Object.FindFirstObjectByType<SkillBarView>();
            linkSo.FindProperty("palette").objectReferenceValue = palette;
            linkSo.ApplyModifiedPropertiesWithoutUndo();

            HideJjhTimer();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeGameObject = root.gameObject;
            return true;
        }

        // JJH's manager is switched off (not removed) and the combo chain takes its place on the same object.
        // Components from earlier versions of this tool whose scripts are gone are cleaned up here too.
        private static ElementComboChain ReplaceStackManager(ElementStackManager stackManager, ElementBuffController buffController,
            SkillStateModule skills)
        {
            GameObject host = stackManager.gameObject;
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(host);

            Object channel = new SerializedObject(stackManager).FindProperty("systemChannel").objectReferenceValue;
            stackManager.enabled = false;
            EditorUtility.SetDirty(stackManager);

            if (!host.TryGetComponent(out ElementComboChain chain))
                chain = host.AddComponent<ElementComboChain>();

            var so = new SerializedObject(chain);
            so.FindProperty("skills").objectReferenceValue = skills;
            so.FindProperty("systemChannel").objectReferenceValue = channel;
            so.FindProperty("buffController").objectReferenceValue = buffController;
            so.ApplyModifiedPropertiesWithoutUndo();
            return chain;
        }

        // ---- Input trail: [cap] › [cap] › [cap] › [cap]   [NEXT], hint above, combo bracket below.
        private static void CreateTrail(RectTransform root, SerializedObject hud)
        {
            float width = CellCount * CellSize + (CellCount - 1) * CellGap;
            const float cellY = 30f + CellSize * 0.5f;

            RectTransform trail = NewRect("InputTrail", root);
            trail.anchorMin = trail.anchorMax = new Vector2(0.5f, 0f);
            trail.pivot = new Vector2(0.5f, 0f);
            trail.anchoredPosition = new Vector2(TrailCenterX, TrailBottom);
            trail.sizeDelta = new Vector2(width, 150f);

            SerializedProperty cells = hud.FindProperty("cells");
            cells.arraySize = CellCount;
            float x = -width * 0.5f + CellSize * 0.5f;
            for (int i = 0; i < CellCount; i++)
            {
                cells.GetArrayElementAtIndex(i).objectReferenceValue = CreateCell(trail, $"Input_{i + 1}", new Vector2(x, cellY));
                if (i < CellCount - 1)
                    Picture("Chevron", trail, _arrow, Dim, new Vector2(x + (CellSize + CellGap) * 0.5f, cellY), new Vector2(15f, 14f));
                x += CellSize + CellGap;
            }

            float nextX = width * 0.5f + NextGap + CellSize * 0.5f;
            hud.FindProperty("nextCell").objectReferenceValue = CreateCell(trail, "Next", new Vector2(nextX, cellY));
            TMP_Text nextLabel = Text("NextLabel", trail, "NEXT", 15f, Dim, TextAlignmentOptions.Center, _display);
            nextLabel.characterSpacing = 6f;
            Place(nextLabel.rectTransform, new Vector2(nextX, cellY + CellSize * 0.5f + 12f), new Vector2(CellSize + 20f, 20f));
            float arrowX = width * 0.5f + NextGap * 0.5f;
            Picture("NextArrow", trail, _arrow, Gold, new Vector2(arrowX - 5f, cellY), new Vector2(18f, 17f));
            Picture("NextArrow2", trail, _arrow, Gold, new Vector2(arrowX + 7f, cellY), new Vector2(18f, 17f));

            TMP_Text hint = Text("Hint", trail, "", 22f, Color.white, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0f, cellY + CellSize * 0.5f + 26f), new Vector2(720f, 30f));
            hud.FindProperty("hintText").objectReferenceValue = hint;

            // Ornamented divider, mirrored halves so both ends carry the ornament.
            RectTransform bracket = NewRect("ComboBracket", trail);
            Place(bracket, new Vector2(0f, 12f), new Vector2(width, 14f));
            DividerHalf("Left", bracket, _divider, Gold, left: true);
            DividerHalf("Right", bracket, _divider, Gold, left: false);
            TMP_Text bracketLabel = Text("Label", bracket, "", 16f, Gold, TextAlignmentOptions.Center);
            RectTransform labelRect = bracketLabel.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -2f);
            labelRect.sizeDelta = new Vector2(300f, 20f);
            hud.FindProperty("bracket").objectReferenceValue = bracket;
            hud.FindProperty("bracketLabel").objectReferenceValue = bracketLabel;
        }

        private static ComboInputCell CreateCell(RectTransform parent, string name, Vector2 center)
        {
            RectTransform cell = NewRect(name, parent);
            Place(cell, center, new Vector2(CellSize, CellSize));
            cell.gameObject.AddComponent<CanvasGroup>();

            Image ring = Framed("Ring", cell, _capOutline, -6f);
            Image background = Framed("Background", cell, _capFrame, 0f);

            RectTransform badgeRect = NewRect("Badge", cell);
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.anchoredPosition = new Vector2(14f, -14f);
            badgeRect.sizeDelta = new Vector2(22f, 22f);
            Image badge = badgeRect.gameObject.AddComponent<Image>();
            badge.preserveAspect = true;
            badge.raycastTarget = false;

            TMP_Text key = Text("Key", cell, "", 30f, Color.white, TextAlignmentOptions.Center);
            key.enableAutoSizing = true;
            key.fontSizeMin = 14f;
            key.fontSizeMax = 30f;
            RectTransform keyRect = key.rectTransform;
            keyRect.anchorMin = Vector2.zero;
            keyRect.anchorMax = Vector2.one;
            keyRect.offsetMin = new Vector2(6f, 18f);
            keyRect.offsetMax = new Vector2(-6f, -8f);

            TMP_Text element = Text("Element", cell, "", 14f, Color.white, TextAlignmentOptions.Center);
            RectTransform elementRect = element.rectTransform;
            elementRect.anchorMin = new Vector2(0f, 0f);
            elementRect.anchorMax = new Vector2(1f, 0f);
            elementRect.pivot = new Vector2(0.5f, 0f);
            elementRect.anchoredPosition = new Vector2(0f, 3f);
            elementRect.sizeDelta = new Vector2(0f, 18f);

            var view = cell.gameObject.AddComponent<ComboInputCell>();
            var so = new SerializedObject(view);
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("badge").objectReferenceValue = badge;
            so.FindProperty("keyText").objectReferenceValue = key;
            so.FindProperty("elementText").objectReferenceValue = element;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ---- Chain window gauge (right, upper): drains until the chain breaks. Gauge only.
        private static void CreateCounter(RectTransform root, ElementComboChain chain)
        {
            RectTransform counter = RightPanel("ChainGauge", root, 170f, new Vector2(260f, 12f));
            var group = counter.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            RectTransform gaugeBack = NewRect("Gauge", counter);
            Stretch(gaugeBack, 0f);
            Image back = gaugeBack.gameObject.AddComponent<Image>();
            back.sprite = _thinOutline;
            back.type = Image.Type.Sliced;
            back.color = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
            back.raycastTarget = false;

            RectTransform fillRect = NewRect("Fill", gaugeBack);
            Stretch(fillRect, 3f);
            Image fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = _white;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Right;
            fill.raycastTarget = false;

            var view = counter.gameObject.AddComponent<ComboCounterView>();
            var so = new SerializedObject(view);
            so.FindProperty("chain").objectReferenceValue = chain;
            so.FindProperty("gaugeFill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Finisher stock (right, under the counter): [R] FINISH + READY, diamond pips, stocked names.
        private static void CreateStock(RectTransform root, ElementComboChain chain, PlayerAgent player)
        {
            const int pipCount = 3;
            RectTransform stock = RightPanel("FinisherStock", root, 70f, new Vector2(320f, 130f));

            TMP_Text key = Text("Key", stock, "", 22f, Gold, TextAlignmentOptions.Right, _display);
            Pin(key.rectTransform, 48f, 28f);
            TMP_Text ready = Text("Ready", stock, "READY", 20f, new Color(1f, 0.4f, 0.35f), TextAlignmentOptions.Left, _display);
            RectTransform readyRect = ready.rectTransform;
            readyRect.anchorMin = readyRect.anchorMax = new Vector2(1f, 0.5f);
            readyRect.pivot = new Vector2(1f, 0.5f);
            readyRect.anchoredPosition = new Vector2(-170f, 48f);
            readyRect.sizeDelta = new Vector2(120f, 28f);

            var view = stock.gameObject.AddComponent<ComboStockView>();
            var so = new SerializedObject(view);
            SerializedProperty pips = so.FindProperty("pips");
            SerializedProperty names = so.FindProperty("names");
            pips.arraySize = pipCount;
            names.arraySize = pipCount;
            for (int i = 0; i < pipCount; i++)
            {
                RectTransform pip = NewRect($"Pip_{i + 1}", stock);
                pip.anchorMin = pip.anchorMax = new Vector2(1f, 0.5f);
                pip.anchoredPosition = new Vector2(-16f - (pipCount - 1 - i) * 36f, 14f);
                pip.sizeDelta = new Vector2(22f, 22f);
                pip.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Image image = pip.gameObject.AddComponent<Image>();
                image.sprite = _smallFrame;
                image.type = Image.Type.Sliced;
                image.raycastTarget = false;
                pips.GetArrayElementAtIndex(i).objectReferenceValue = image;

                TMP_Text name = Text($"Name_{i + 1}", stock, "", 17f, new Color(1f, 0.92f, 0.7f), TextAlignmentOptions.Right);
                Pin(name.rectTransform, -16f - i * 22f, 22f);
                names.GetArrayElementAtIndex(i).objectReferenceValue = name;
            }

            so.FindProperty("chain").objectReferenceValue = chain;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("keyLabel").objectReferenceValue = key;
            so.FindProperty("readyLabel").objectReferenceValue = ready;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Move list (right, lower): rows = key caps + combo name + landed count.
        private static ComboListView CreateComboList(RectTransform root)
        {
            const float width = 360f;
            const float rowHeight = 40f;

            RectTransform panel = RightPanel("ComboList", root, -190f, new Vector2(width, 300f));

            // Closed state: just a small tab hint at the top-right corner.
            TMP_Text closedTab = Text("ClosedTab", panel, "<color=#FFD34D>[Tab]</color> 콤보 리스트", 18f, Dim, TextAlignmentOptions.Right);
            RectTransform closedRect = closedTab.rectTransform;
            closedRect.anchorMin = new Vector2(0f, 1f);
            closedRect.anchorMax = Vector2.one;
            closedRect.pivot = new Vector2(0.5f, 1f);
            closedRect.offsetMin = new Vector2(0f, -26f);
            closedRect.offsetMax = new Vector2(-6f, 0f);

            RectTransform body = NewRect("Body", panel);
            Stretch(body, 0f);

            TMP_Text title = Text("Title", body, "콤보 리스트", 24f, Gold, TextAlignmentOptions.Right, _koreanDisplay);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = Vector2.one;
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(0f, -30f);
            titleRect.offsetMax = new Vector2(-6f, 0f);

            RectTransform rows = NewRect("Rows", body);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = Vector2.one;
            rows.pivot = new Vector2(0.5f, 1f);
            rows.offsetMin = new Vector2(0f, -300f);
            rows.offsetMax = new Vector2(0f, -36f);
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RectTransform row = NewRect("RowTemplate", rows);
            row.sizeDelta = new Vector2(width, rowHeight);
            row.gameObject.AddComponent<CanvasGroup>();
            Image rowBack = row.gameObject.AddComponent<Image>();
            rowBack.sprite = _rowFrame;
            rowBack.type = Image.Type.Sliced;
            rowBack.raycastTarget = false;

            RectTransform steps = NewRect("Steps", row);
            steps.anchorMin = steps.anchorMax = new Vector2(0f, 0.5f);
            steps.pivot = new Vector2(0f, 0.5f);
            steps.anchoredPosition = new Vector2(8f, 0f);
            steps.sizeDelta = new Vector2(176f, 30f);
            var stepLayout = steps.gameObject.AddComponent<HorizontalLayoutGroup>();
            stepLayout.spacing = 4f;
            stepLayout.childAlignment = TextAnchor.MiddleLeft;
            stepLayout.childControlWidth = false;
            stepLayout.childControlHeight = false;
            stepLayout.childForceExpandWidth = false;
            stepLayout.childForceExpandHeight = false;

            RectTransform step = NewRect("Step", steps);
            step.sizeDelta = new Vector2(40f, 30f);
            Image stepImage = step.gameObject.AddComponent<Image>();
            stepImage.sprite = _smallFrame;
            stepImage.type = Image.Type.Sliced;
            stepImage.raycastTarget = false;
            TMP_Text stepKey = Text("Key", step, "", 16f, Color.white, TextAlignmentOptions.Center);
            Stretch(stepKey.rectTransform, 2f);
            stepKey.enableAutoSizing = true;
            stepKey.fontSizeMin = 10f;
            stepKey.fontSizeMax = 16f;

            TMP_Text name = Text("Name", row, "", 18f, Color.white, TextAlignmentOptions.Right);
            RectTransform nameRect = name.rectTransform;
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(190f, 0f);
            nameRect.offsetMax = new Vector2(-12f, 0f);

            var view = panel.gameObject.AddComponent<ComboListView>();
            var so = new SerializedObject(view);
            so.FindProperty("rowTemplate").objectReferenceValue = row;
            so.FindProperty("rowParent").objectReferenceValue = rows;
            so.FindProperty("body").objectReferenceValue = body.gameObject;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("closedTab").objectReferenceValue = closedTab;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ---- Banner (upper middle) + full-screen flash behind everything for the finisher.
        private static ComboBanner CreateBanner(RectTransform root)
        {
            RectTransform flashRect = NewRect("ScreenFlash", root);
            Stretch(flashRect, 0f);
            Image flash = flashRect.gameObject.AddComponent<Image>();
            flash.sprite = _white;
            flash.color = new Color(1f, 0.95f, 0.85f, 0f);
            flash.raycastTarget = false;
            flash.enabled = false;

            RectTransform banner = NewRect("Banner", root);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0.5f);
            banner.anchoredPosition = new Vector2(0f, 270f);
            banner.sizeDelta = new Vector2(900f, 130f);
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            RectTransform barRect = NewRect("Bar", banner);
            barRect.sizeDelta = new Vector2(860f, 112f);
            Image bar = barRect.gameObject.AddComponent<Image>();
            bar.sprite = _bannerFrame;
            bar.type = Image.Type.Sliced;
            bar.pixelsPerUnitMultiplier = 0.6f;

            // Fading ornament lines above and below the plate, ornaments at the outer ends.
            foreach ((string name, float y) in new[] { ("TopLine", 66f), ("BottomLine", -66f) })
            {
                RectTransform line = NewRect(name, banner);
                Place(line, Vector2.zero, new Vector2(900f, 16f));
                CenterAt(line, y);
                DividerHalf("Left", line, _dividerFade, Gold, left: true);
                DividerHalf("Right", line, _dividerFade, Gold, left: false);
            }
            bar.raycastTarget = false;

            TMP_Text header = Text("Header", banner, "", 24f, Gold, TextAlignmentOptions.Center, _display);
            header.characterSpacing = 12f;
            Place(header.rectTransform, Vector2.zero, new Vector2(800f, 30f));
            CenterAt(header.rectTransform, 40f);

            TMP_Text title = Text("Title", banner, "", 54f, Color.white, TextAlignmentOptions.Center, _koreanDisplay);
            title.enableAutoSizing = true;
            title.fontSizeMin = 28f;
            title.fontSizeMax = 54f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            Place(title.rectTransform, Vector2.zero, new Vector2(820f, 60f));
            CenterAt(title.rectTransform, 2f);

            TMP_Text detail = Text("Detail", banner, "", 22f, Color.white, TextAlignmentOptions.Center);
            detail.textWrappingMode = TextWrappingModes.NoWrap;
            Place(detail.rectTransform, Vector2.zero, new Vector2(820f, 28f));
            CenterAt(detail.rectTransform, -38f);

            var view = banner.gameObject.AddComponent<ComboBanner>();
            var so = new SerializedObject(view);
            so.FindProperty("bar").objectReferenceValue = bar;
            so.FindProperty("header").objectReferenceValue = header;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("detail").objectReferenceValue = detail;
            so.FindProperty("screenFlash").objectReferenceValue = flash;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // The trail has its own feedback, so JJH's timer slider (moved under SkillBar by the skill bar builder) is hidden here.
        private static void HideJjhTimer()
        {
            ElementStackView stackView = Object.FindFirstObjectByType<ElementStackView>(FindObjectsInactive.Include);
            if (stackView != null && new SerializedObject(stackView).FindProperty("timerSlider").objectReferenceValue is Slider slider)
                slider.gameObject.SetActive(false);
        }

        // Panel hugging the right edge, centred vertically around y.
        private static RectTransform RightPanel(string name, RectTransform root, float y, Vector2 size)
        {
            RectTransform panel = NewRect(name, root);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = new Vector2(RightMargin, y);
            panel.sizeDelta = size;
            return panel;
        }

        // Full-width row inside a right panel at height y (relative to the panel centre).
        private static void Pin(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void CenterAt(RectTransform rect, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private static Image Framed(string name, RectTransform parent, Sprite sprite, float inset)
        {
            RectTransform rect = NewRect(name, parent);
            Stretch(rect, inset);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        private static Image Picture(string name, RectTransform parent, Sprite sprite, Color color, Vector2 center, Vector2 size)
        {
            RectTransform rect = NewRect(name, parent);
            Place(rect, center, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        // One half of a two-ended divider; the left half is mirrored so its ornament faces outward.
        private static void DividerHalf(string name, RectTransform parent, Sprite sprite, Color color, bool left)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = new Vector2(left ? 0f : 0.5f, 0f);
            rect.anchorMax = new Vector2(left ? 0.5f : 1f, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (left)
                rect.localScale = new Vector3(-1f, 1f, 1f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        private static TMP_Text Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment,
            TMP_FontAsset font = null)
        {
            RectTransform rect = NewRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if ((font ?? _font) != null)
                tmp.font = font ?? _font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.richText = true;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
