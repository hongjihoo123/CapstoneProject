using Assets.Members.HJH._02.Scripts.Char.Character;
using DG.Tweening;
using Members.KYR._01.Scripts.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 플레이어가 죽으면 (쓰러지는 연출이 끝날 즈음) 화면이 어두워지며 "쓰러졌습니다" + 버튼이 뜸.
    // 버튼(임시)을 누르면 고른 캐릭터를 지우고 캐릭터 픽 화면으로 돌아감. Enter 키도 같은 동작.
    // 모양은 코드로 만들고, HUD_Canvas 아래 빈 오브젝트 하나로 붙임.
    public class DeathOverlay : MonoBehaviour
    {
        [SerializeField, Tooltip("캐릭터 픽 화면 씬 이름 (빌드 설정에 있어야 함)")]
        private string pickScene = "CharacterPick";
        [SerializeField, Min(0f), Tooltip("죽은 뒤 (실제 초) 이만큼 지나면 나타남. 쓰러지는 슬로모션이 끝난 뒤")]
        private float showDelay = 1.6f;
        [SerializeField, Min(0.05f)] private float fadeTime = 0.6f;

        private PlayerAgent _agent;
        private CanvasGroup _group;
        private float _deadSince = -1f;
        private bool _shown;
        private bool _leaving;

        private void Awake()
        {
            var rect = (RectTransform)transform;
            HudUiKit.Stretch(rect);
            rect.SetAsLastSibling();
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Image dim = HudUiKit.NewImage("Dim", transform, null, new Color(0.02f, 0.01f, 0.03f, 0.72f));
            HudUiKit.Stretch(dim.rectTransform);

            Image glow = HudUiKit.NewImage("RedGlow", transform, HudUiKit.Load("soft_glow"), new Color(0.8f, 0.1f, 0.1f, 0.35f));
            HudUiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1500f, 700f));

            TMP_Text title = HudUiKit.NewText("Title", transform, "쓰러졌습니다", 72f, new Color(0.95f, 0.85f, 0.85f), TextAlignmentOptions.Center, 10f);
            HudUiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1200f, 110f));

            Image line = HudUiKit.NewImage("Line", transform, HudUiKit.Load("divider"), new Color(1f, 0.8f, 0.8f, 0.7f));
            HudUiKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(560f, 18f));

            Image buttonBack = HudUiKit.NewImage("Button", transform, HudUiKit.Load("button_hover"), Color.white);
            HudUiKit.Place(buttonBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(520f, 76f));
            buttonBack.raycastTarget = true;
            TMP_Text label = HudUiKit.NewText("Label", buttonBack.transform, "캐릭터 다시 고르기", 30f, HudUiKit.Title, TextAlignmentOptions.Center, 2f);
            HudUiKit.Stretch(label.rectTransform);
            var button = buttonBack.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonBack;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(GoToPick);

            TMP_Text hint = HudUiKit.NewText("Hint", transform, "(임시) 버튼 또는 Enter 키", 16f, HudUiKit.Label, TextAlignmentOptions.Center);
            HudUiKit.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(500f, 28f));
        }

        private void Update()
        {
            if (_leaving)
                return;

            if (!_shown)
            {
                if (_agent == null)
                    _agent = FindFirstObjectByType<PlayerAgent>();
                if (_agent == null || _agent.IsAlive)
                {
                    _deadSince = -1f;
                    return;
                }

                if (_deadSince < 0f)
                    _deadSince = Time.unscaledTime;
                if (Time.unscaledTime - _deadSince >= showDelay)
                    Show();
                return;
            }

            // 조준 커서/잠금이 마우스를 가두지 않게 계속 풀어 둠
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                GoToPick();
        }

        private void Show()
        {
            _shown = true;
            transform.SetAsLastSibling();

            // 버튼이 눌리려면 이벤트 시스템이 있어야 함
            HudUiKit.EnsureEventSystem();

            foreach (AimCursorView aim in FindObjectsByType<AimCursorView>(FindObjectsSortMode.None))
                aim.gameObject.SetActive(false);

            _group.blocksRaycasts = true;
            _group.interactable = true;
            _group.DOFade(1f, fadeTime).SetUpdate(true);
        }

        public bool IsShown => _shown;

        // 테스트 도구용 (버튼을 누른 것과 같음)
        public void DebugGoToPick() => GoToPick();

        private void GoToPick()
        {
            if (_leaving)
                return;

            _leaving = true;
            Time.timeScale = 1f;
            CharacterSelection.Clear();
            _group.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() => SceneManager.LoadScene(pickScene));
        }
    }
}
