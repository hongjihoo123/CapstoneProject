using System.Collections.Generic;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.Hud;
using DG.Tweening;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 콤보 완성 / 피니시 때 가운데 위쪽에 뜨는 배너 (얇은 유리 스타일).
    //  - 번지는 어둠 위에 위아래 얇은 선(끝에 작은 금색 다이아) + 작은 머리말 / 큰 이름 / 효과 설명 / 키 순서
    //  - 값은 전부 PlayerHudSource 이벤트에서 받음. HUD 캔버스(PlayerRig) 하이어라키에 저장된 오브젝트, 모양은 인스펙터에서 수정
    //  - JJH ElementStackView 의 예전 금색 배너는 SuppressBanner 로 꺼서 두 번 안 뜨게 함
    public class ComboBannerView : MonoBehaviour, IUiBake
    {
        private static readonly Color FinisherAccent = new(1f, 0.5f, 0.42f);

        [SerializeField, Min(0.2f)] private float holdTime = 1.0f;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text headerText, titleText, detailText, keysText;
        [SerializeField, Tooltip("위아래 선 (뜰 때 옆으로 펼쳐짐)")] private Image[] lines;

        private PlayerHudSource _hud;
        private RectTransform _root;
        private bool _bound;

        private void Start()
        {
            // JJH ElementStackView 의 예전 금색 배너는 꺼서 두 번 안 뜨게
            foreach (ElementStackView view in FindObjectsByType<ElementStackView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                view.SuppressBanner = true;

            _root = (RectTransform)transform;
            if (group != null)
                group.alpha = 0f;
            _hud = PlayerHudSource.Instance;
            if (_hud == null || group == null)
                return;

            _hud.ComboLanded += OnComboLanded;
            _hud.FinisherArmed += OnFinisherArmed;
            _hud.FinisherExpired += OnFinisherExpired;
            _hud.FinisherReleased += OnFinisherReleased;
            _bound = true;
        }

        private void OnDestroy()
        {
            if (!_bound || _hud == null)
                return;

            _hud.ComboLanded -= OnComboLanded;
            _hud.FinisherArmed -= OnFinisherArmed;
            _hud.FinisherExpired -= OnFinisherExpired;
            _hud.FinisherReleased -= OnFinisherReleased;
        }

        private void OnComboLanded(ElementComboBook.Recipe combo) =>
            Show("COMBO", combo.Name, ComboLoadout.EffectOf(combo.Name), KeySequence(combo.Sequence), HudUiKit.Cyan);

        private void OnFinisherArmed(int stacks, float scale) =>
            Show("READY", "피니시 준비", $"다음 스킬 ×{scale:0.0#}", string.Empty, FinisherAccent);

        private void OnFinisherExpired() =>
            Show("MISS", "피니시 시간 끝", string.Empty, string.Empty, HudUiKit.Label);

        private void OnFinisherReleased(SkillSlotId slot, float scale)
        {
            Show($"FINISH  ×{scale:0.0#}", "피니시", "피해 강화", string.Empty, FinisherAccent);
        }

        // SetUpdate(true): 콤보 순간 게임이 잠깐 멈춰도(히트스톱) 배너는 계속 움직임
        private void Show(string header, string title, string detail, string keys, Color accent)
        {
            if (_root == null || group == null)
                return;

            headerText.text = header;
            headerText.color = accent;
            titleText.text = title;
            detailText.text = detail;
            keysText.text = keys;

            _root.DOKill();
            group.DOKill();
            group.alpha = 0f;
            _root.localScale = Vector3.one * 1.1f;
            foreach (Image line in lines)
            {
                line.color = new Color(accent.r, accent.g, accent.b, 0.75f);
                line.rectTransform.DOKill();
                line.rectTransform.localScale = new Vector3(0f, 1f, 1f);
                line.rectTransform.DOScaleX(1f, 0.28f).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            group.DOFade(1f, 0.1f).SetUpdate(true);
            _root.DOScale(1f, 0.16f).SetEase(Ease.OutQuad).SetUpdate(true);
            group.DOFade(0f, 0.25f).SetDelay(holdTime).SetUpdate(true);
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);
            var root = (RectTransform)transform;
            HudUiKit.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0f, 270f), new Vector2(900f, 150f));
            group = GetComponent<CanvasGroup>() ? GetComponent<CanvasGroup>() : gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image glow = HudUiKit.NewImage("Glow", root, HudUiKit.Load("soft_glow"), new Color(0f, 0.02f, 0.05f, 0.85f));
            HudUiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 260f));

            var lineList = new List<Image>();
            for (int i = 0; i < 2; i++)
            {
                float y = i == 0 ? 72f : -74f;
                Image line = HudUiKit.NewImage(i == 0 ? "LineTop" : "LineBottom", root, HudUiKit.Load("divider"), HudUiKit.Cyan);
                HudUiKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(780f, 14f));
                lineList.Add(line);

                foreach (float x in new[] { -388f, 388f })
                {
                    Image gem = HudUiKit.NewImage("Gem", root, null, HudUiKit.Gold);
                    gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    HudUiKit.Place(gem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(8f, 8f));
                }
            }
            lines = lineList.ToArray();

            headerText = HudUiKit.NewText("Header", root, "COMBO", 20f, HudUiKit.Cyan, TextAlignmentOptions.Center, 10f);
            HudUiKit.Place(headerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(800f, 28f));
            titleText = HudUiKit.NewText("Title", root, "콤보 이름", 46f, HudUiKit.Title, TextAlignmentOptions.Center, 4f);
            HudUiKit.Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(800f, 56f));
            detailText = HudUiKit.NewText("Detail", root, "효과 설명", 24f, HudUiKit.Value, TextAlignmentOptions.Center);
            HudUiKit.Place(detailText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(800f, 32f));
            keysText = HudUiKit.NewText("Keys", root, "Q 불 › E 번개", 18f, new Color(0.69f, 0.75f, 0.82f, 0.75f), TextAlignmentOptions.Center, 2f);
            HudUiKit.Place(keysText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -52f), new Vector2(800f, 26f));
#endif
        }

        // "Q 불 › E 번개" 를 속성 색으로
        private string KeySequence(IReadOnlyList<ElementType> sequence)
        {
            var text = new StringBuilder();
            for (int i = 0; i < sequence.Count; i++)
            {
                if (i > 0)
                    text.Append("  <color=#8A8A96>›</color>  ");
                text.Append($"<color=#{ColorUtility.ToHtmlStringRGB(_hud.ColorOf(sequence[i]))}>{_hud.KeyFor(sequence[i])} {_hud.ElementName(sequence[i])}</color>");
            }
            return text.ToString();
        }
    }
}
