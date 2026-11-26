using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.UI;
using Members.KYR._01.Scripts.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Element
{
    public class ComboBindStation : MonoBehaviour, ISkillInputInterceptor, IUiBake
    {
        [SerializeField] private float interactRadius = 2.5f;
        [SerializeField] private Key interactKey = Key.F;
        [SerializeField, Tooltip("머리 위 안내 글자 (프리팹 자식 Prompt). 폰트·크기는 거기서 수정")] private TextMeshPro prompt;
        [SerializeField] private string usedPromptText = "콤보 바인딩 완료";
        [SerializeField, Tooltip("바인딩 창 프리팹 (05.Data/Prefab/UI/ComboBindPanel)")] private ComboBindPanel panelPrefab;
        [SerializeField, Tooltip("화면 위 안내 프리팹 (05.Data/Prefab/UI/ScreenNotice)")] private ScreenNotice noticePrefab;

        private PlayerAgent _player;
        private SkillStateModule _skills;
        private bool _used;

        public bool PlayerNear { get; private set; }
        public bool Used => _used;
        public ComboBindPanel PanelPrefab => panelPrefab;

        public const string UsedMessage = "기회를 소진하셨습니다.";

        private void OnDestroy()
        {
            _skills?.RemoveInputInterceptor(this);
            if (ComboBindPanel.Current != null)
                ComboBindPanel.Current.Close();
        }

        private void Update()
        {
            if (!FindPlayer())
                return;

            Vector3 delta = _player.transform.position - transform.position;
            delta.y = 0f;
            PlayerNear = delta.sqrMagnitude <= interactRadius * interactRadius;

            bool open = ComboBindPanel.IsOpen;
            if (prompt != null)
            {
                prompt.gameObject.SetActive(PlayerNear && !open);
                Camera cam = Camera.main;
                if (cam != null)
                    prompt.transform.rotation = cam.transform.rotation;
            }

            Keyboard keyboard = Keyboard.current;
            if (PlayerNear && !open && keyboard != null && keyboard[interactKey].wasPressedThisFrame)
                Open();
        }

        public bool Open()
        {
            if (_used)
            {
                ScreenNotice.Show(noticePrefab, UsedMessage, new Color(1f, 0.72f, 0.6f, 1f));
                return false;
            }

            ComboBindPanel panel = null;
            panel = ComboBindPanel.Open(panelPrefab, () =>
            {
                if (panel != null && panel.WasSaved)
                    MarkUsed();
            });
            return true;
        }

        private void MarkUsed()
        {
            _used = true;
            if (prompt != null)
                prompt.text = usedPromptText;
        }

        public bool Intercepts(SkillSlotId slot) => ComboBindPanel.IsOpen;

        public void OnInterceptedPress(SkillSlotId slot) { }

        private bool FindPlayer()
        {
            if (_player != null)
                return true;

            _player = FindFirstObjectByType<PlayerAgent>();
            if (_player == null)
                return false;

            _skills = _player.SkillFsm;
            _skills?.AddInputInterceptor(this);
            return true;
        }

        [ContextMenu("레이아웃 굽기 (안내 글자 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            if (prompt != null)
                DestroyImmediate(prompt.gameObject);
            var go = new GameObject("Prompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            prompt = go.AddComponent<TextMeshPro>();
            if (HudUiKit.Font != null)
                prompt.font = HudUiKit.Font;
            prompt.text = $"<color=#FFD36B>{interactKey}</color>  콤보 바인딩";
            prompt.fontSize = 5f;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.textWrappingMode = TextWrappingModes.NoWrap;
            prompt.overflowMode = TextOverflowModes.Overflow;
            prompt.rectTransform.sizeDelta = new Vector2(12f, 1.5f);
            go.SetActive(false);
#endif
        }
    }
}
