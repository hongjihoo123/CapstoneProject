using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.DashElement
{
    public class DashElementShrine : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float detectRadius = 2.2f;

        private PlayerAgent _player;
        private int _inside;
        private bool _open;
        private GameObject _prompt;
        private GameObject _canvas;
        private TMP_Text[] _labels;

        private void Awake()
        {
            EnsureRange();
            EnsureMarker();
            EnsurePrompt();
            SetPrompt(false);
        }

        private void OnDestroy()
        {
            if (_open)
                UiFocusService.Release(this);

            if (_canvas != null)
                Destroy(_canvas);
        }

        private void Update()
        {
            if (_open)
            {
                ReadChoiceKeys();
                return;
            }

            SetPrompt(_inside > 0);
            Keyboard keyboard = Keyboard.current;
            if (_inside > 0 && _player != null && keyboard != null && keyboard.fKey.wasPressedThisFrame)
                Open();
        }

        private void LateUpdate()
        {
            if (_prompt == null || !_prompt.activeSelf || Camera.main == null)
                return;

            _prompt.transform.rotation = Camera.main.transform.rotation;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerAgent agent = other != null ? other.GetComponentInParent<PlayerAgent>() : null;
            if (agent == null)
                return;

            _inside++;
            _player = agent;
            if (!_open)
                SetPrompt(true);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerAgent agent = other != null ? other.GetComponentInParent<PlayerAgent>() : null;
            if (agent == null)
                return;

            _inside = Mathf.Max(0, _inside - 1);
            if (_inside == 0 && !_open)
                SetPrompt(false);
        }

        private void OnValidate()
        {
            SphereCollider range = GetComponent<SphereCollider>();
            if (range != null && range.isTrigger)
            {
                range.radius = detectRadius;
                range.center = new Vector3(0f, 0.9f, 0f);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.55f, 0.4f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + new Vector3(0f, 0.9f, 0f), detectRadius);
        }

        private void Open()
        {
            if (_player == null || _player.SkillFsm == null)
                return;

            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            if (_canvas == null)
                BuildUi();

            RefreshLabels();
            _canvas.SetActive(true);
            _open = true;
            SetPrompt(false);
            UiFocusService.Acquire(this);
        }

        private void Close()
        {
            if (!_open)
                return;

            _open = false;
            if (_canvas != null)
                _canvas.SetActive(false);

            UiFocusService.Release(this);
            SetPrompt(_inside > 0);
        }

        private void Choose(int index)
        {
            ElementType[] options = DashElementOffer.Current;
            if (index < 0 || index >= options.Length)
                return;

            if (!DashElementOffer.Apply(_player, options[index]))
                return;

            Close();
        }

        private void ReadChoiceKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                Choose(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                Choose(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                Choose(2);
        }

        private void RefreshLabels()
        {
            ElementType[] options = DashElementOffer.Current;
            ElementType? current = CurrentDashElement();
            for (int i = 0; i < _labels.Length && i < options.Length; i++)
            {
                string name = ElementComboBook.KoreanName(options[i]);
                _labels[i].text = current == options[i] ? $"{name}\n사용 중" : name;
            }
        }

        private ElementType? CurrentDashElement()
        {
            SkillData data = _player != null && _player.SkillFsm != null
                ? _player.SkillFsm.GetSkill(SkillSlotId.Dash)
                : null;

            if (data != null && data.TryGetElement(out ElementType element))
                return element;

            return null;
        }

        private void BuildUi()
        {
            _canvas = new GameObject("DashElementUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var dim = CreateImage("Dim", _canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(dim.rectTransform);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.onClick.AddListener(Close);

            var panel = CreateImage("Panel", dim.transform, new Color(0.08f, 0.07f, 0.12f, 0.94f));
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(820f, 360f);

            var title = CreateText("Title", panel.transform, "대시 속성", 42);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -28f);
            titleRect.sizeDelta = new Vector2(760f, 64f);

            var row = new GameObject("Choices", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(panel.transform, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(740f, 140f);
            rowRect.anchoredPosition = new Vector2(0f, -24f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ElementType[] options = DashElementOffer.Current;
            _labels = new TMP_Text[options.Length];
            for (int i = 0; i < options.Length; i++)
                _labels[i] = CreateChoice(row.transform, i, options[i]);
        }

        private TMP_Text CreateChoice(Transform parent, int index, ElementType element)
        {
            Color color = ElementPalette.Default != null ? ElementPalette.Default.ColorOf(element) : Color.white;
            var image = CreateImage($"Choice{index}", parent, color);
            var slot = image.gameObject.AddComponent<LayoutElement>();
            slot.preferredWidth = 220f;
            slot.preferredHeight = 120f;
            var button = image.gameObject.AddComponent<Button>();
            int captured = index;
            button.onClick.AddListener(() => Choose(captured));

            var label = CreateText("Label", image.transform, ElementComboBook.KoreanName(element), 32);
            Stretch(label.rectTransform);
            return label;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void EnsureRange()
        {
            SphereCollider range = GetComponent<SphereCollider>();
            if (range == null)
                range = gameObject.AddComponent<SphereCollider>();

            range.isTrigger = true;
            range.radius = detectRadius;
            range.center = new Vector3(0f, 0.9f, 0f);
        }

        private void EnsureMarker()
        {
            if (GetComponentInChildren<Renderer>() != null)
                return;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Marker";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, 1f, 0f);
            marker.transform.localScale = new Vector3(0.7f, 1f, 0.7f);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return;

            var material = new Material(shader);
            material.color = new Color(0.42f, 0.28f, 0.78f);
            marker.GetComponent<Renderer>().material = material;
        }

        private void EnsurePrompt()
        {
            var canvasGo = new GameObject("Prompt", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvasGo.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(0f, 2.4f, 0f);
            rect.sizeDelta = new Vector2(180f, 180f);
            rect.localScale = Vector3.one * 0.01f;

            var text = CreateText("F", canvasGo.transform, "F", 90);
            Stretch(text.rectTransform);
            text.color = new Color(1f, 0.84f, 0.42f);
            _prompt = canvasGo;
        }

        private void SetPrompt(bool visible)
        {
            if (_prompt != null)
                _prompt.SetActive(visible);
        }
    }
}
