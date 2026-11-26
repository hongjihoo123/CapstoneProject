using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Character;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Assets.Members.HJH._02.Scripts.Element;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using Assets.Members.HJH._02.Scripts.Tutorial;
using DG.Tweening;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Enchant.So;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 캐릭터 픽 화면. 3단 배치:
    //   왼쪽  = 캐릭터 3명 고르기 + 이름·태그(원거리/근거리, 플레이 스타일)·짧은 설명 + 고정 스킬 4개(패시브, 우클릭, 대시, 궁극기)
    //   가운데 = 고른 캐릭터 모델 (모델 없는 캐릭터는 테마색 원판 + 이름 첫 글자)
    //   오른쪽 = 시작 콤보(우클릭·궁극기 원소 A→B / B→A 중 1개, 줄은 PickComboRow 프리팹) + 아웃 업그레이드 버튼 + 게임 시작
    // "게임 시작"을 누르면 고른 캐릭터를 CharacterSelection 에, 고른 시작 콤보는 ComboLoadout 에 남기고 다음 씬으로 넘어감.
    // Q/E 스킬은 비운 채로 시작함 (게임 안에서 주워서 채움).
    // 키보드: 1~3 또는 ←/→ 캐릭터, Enter 시작.
    // 화면은 씬 하이어라키(PickCanvas)에 저장된 그대로 → 폰트·크기·위치는 인스펙터에서 수정. 처음 만들 때만 ⋮ > 레이아웃 굽기.
    // 실행 중에는 고른 캐릭터에 맞춰 글자·아이콘·색만 바꿈. 버튼은 OnClick 에 연결돼 있음 (SelectCharacter, SelectPrev/Next, SelectSkill, Confirm)
    // 튜토리얼에는 ITutorialPickScreen 으로 자리(초상화/스킬/콤보/시작)와 바꾼 횟수만 알려 줌.
    public class CharacterPickScreen : MonoBehaviour, ITutorialPickScreen, IUiBake
    {
        [SerializeField] private CharacterData[] roster;
        [SerializeField, Tooltip("캐릭터를 고른 뒤 넘어갈 씬 (빌드 설정에 있어야 함)")]
        private string nextScene = "Map_Bridge_Normal";
        [SerializeField, Min(0.05f)] private float fadeTime = 0.5f;

        [Header("가운데 모델")]
        [SerializeField, Range(0.3f, 0.9f), Tooltip("모델 키가 화면 높이에서 차지하는 비율")] private float modelScreenHeight = 0.62f;
        [SerializeField, Tooltip("모델을 정면에서 이만큼 돌려 세움 (도)")] private float modelTurn = -22f;
        [SerializeField, Tooltip("끄면 픽 화면에 3D 모델을 안 세우고 플레이스홀더(원형 초상)만 보여줌 (재킷이 총에 뚫려 보여서 임시로 끔)")] private bool showModel;
        [SerializeField, Tooltip("모델이 올라가는 자리")] private Transform stage;

        [Serializable]
        public sealed class PortraitView
        {
            public RectTransform root;
            public CanvasGroup group;
        }

        [Serializable]
        public sealed class TagView
        {
            public GameObject root;
            public TMP_Text text;
            public Image line;
        }

        [Serializable]
        public sealed class SkillSlotView
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image icon;
            public Image ring;
            public Image elementDot;
            public TMP_Text key;
            public TMP_Text elementName;
        }

        [Header("구워 둔 화면 (⋮ > 레이아웃 굽기)")]
        [Header("아웃 업그레이드 (스킬 트리)")]
        [SerializeField, Tooltip("아웃 업그레이드 버튼이 여는 창 프리팹 (05.Data/Prefab/UI/SkillTreeScreen)")] private SkillTreeScreen skillTreeScreenPrefab;
        [SerializeField, Tooltip("캐릭터별 스킬 트리. 고른 캐릭터와 맞는 것을 열고, 없으면 준비 중으로 보임")] private EnchantTree[] skillTrees = Array.Empty<EnchantTree>();

        [SerializeField] private ElementPalette palette;
        [SerializeField] private PickComboRowView comboRowPrefab;
        [SerializeField] private List<PortraitView> portraits = new();
        [SerializeField] private RectTransform portraitRow;
        [SerializeField] private RectTransform info;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TagView[] tags;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private RectTransform skillsArea;
        [SerializeField] private TMP_Text skillNone;
        [SerializeField] private SkillSlotView[] skillSlots;
        [SerializeField] private GameObject skillDetail;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private Image detailBadge;
        [SerializeField] private TMP_Text detailElement;
        [SerializeField] private Image detailLine;
        [SerializeField] private TMP_Text detailDesc;
        [Header("스킬 미리보기 영상 (SkillData.PreviewClip)")]
        [SerializeField, Tooltip("영상이 그려지는 칸")] private RawImage detailPreview;
        [SerializeField, Tooltip("영상 재생기 (소리 없음, 반복)")] private VideoPlayer previewPlayer;
        [SerializeField, Tooltip("영상이 없는 스킬에 보이는 안내")] private TMP_Text previewNote;
        [SerializeField] private GameObject placeholder;
        [SerializeField] private Image placeholderGlow;
        [SerializeField] private Image placeholderDisc;
        [SerializeField] private Image placeholderFace;
        [SerializeField] private TMP_Text placeholderInitial;
        [SerializeField] private Image placeholderRing;
        [SerializeField] private TMP_Text pageText;
        [SerializeField, Tooltip("준비 안 된 캐릭터를 고르려 할 때 잠깐 뜨는 글자")] private TMP_Text notReadyText;
        [SerializeField] private RectTransform comboArea;
        [SerializeField] private TMP_Text comboHint;
        [SerializeField] private RectTransform startButton;
        [SerializeField] private Image fade;

        private const float ColumnWidth = 580f;
        private const float SideMargin = 70f;
        private static readonly Color Warn = new(1f, 0.55f, 0.45f, 1f);

        private readonly struct SkillEntry
        {
            public readonly string key, title, description;
            public readonly Sprite icon;
            public readonly bool hasElement;
            public readonly ElementType element;
            public readonly SkillData skill;

            public SkillEntry(string key, Sprite icon, string title, string description, SkillData skill)
            {
                this.skill = skill;
                this.key = key;
                this.icon = icon;
                this.title = title;
                this.description = description;
                element = default;
                hasElement = skill != null && skill.TryGetElement(out element);
            }
        }

        private readonly List<PickComboRowView> _comboRows = new();
        private readonly List<SkillEntry> _entries = new();
        private string _comboKey;
        private GameObject _model;
        private Camera _camera;
        private int _selected;
        private int _skill;
        private Color _theme = Color.white;
        private bool _busy;

        public int SelectionChanges { get; private set; }
        public int ComboChanges { get; private set; }

        public bool TryGetAnchor(string id, out RectTransform rect)
        {
            rect = id switch
            {
                TutorialAnchorIds.PickPortraits => portraitRow,
                TutorialAnchorIds.PickSkills => skillsArea,
                TutorialAnchorIds.PickCombos => comboArea,
                TutorialAnchorIds.PickStart => startButton,
                _ => null,
            };
            return rect != null;
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _camera = Camera.main;
            HudUiKit.EnsureEventSystem();

            if (fade != null)
            {
                fade.raycastTarget = true;
                fade.color = Color.black;
                fade.DOFade(0f, fadeTime).SetUpdate(true).OnComplete(() => fade.raycastTarget = false);
            }

            _selected = Mathf.Clamp(CharacterSelection.HasPick ? CharacterSelection.Index : 0, 0, Mathf.Max(0, Count - 1));
            ShowCharacter();
            RefreshCombos();
        }

        private int Count => roster != null ? roster.Length : 0;

        private ElementPalette Palette => palette != null ? palette : ElementPalette.Default;

        private void Update()
        {
            if (_busy || Keyboard.current == null || Count == 0)
                return;

            for (int i = 0; i < Count && i < 9; i++)
            {
                if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                    SelectCharacter(i);
            }

            if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
                SelectPrev();
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
                SelectNext();

            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                Confirm();
        }

        // ---------------------------------------------------------------- 모델

        // 고른 캐릭터의 모델을 무대에 세우고 카메라를 키에 맞춰 당김. 모델이 없으면 false
        private bool ShowModel(CharacterData data)
        {
            if (_model != null)
                Destroy(_model);
            _model = null;

            if (!showModel || data == null || data.Model == null || stage == null)
                return false;

            // 모델 프리팹에 붙은 게임용 스크립트는 플레이어가 없는 여기서 돌면 안 됨 -> 꺼진 부모 밑에 만들어서 깨어나기 전에 떼어냄
            var holder = new GameObject("ModelHolder");
            holder.SetActive(false);
            holder.transform.SetParent(stage, false);
            GameObject model = Instantiate(data.Model, holder.transform);
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (script != null)
                        DestroyImmediate(script);
                }
            }

            model.transform.localPosition = data.ModelOffset;
            model.transform.localRotation = Quaternion.Euler(0f, 180f + modelTurn, 0f) * data.ModelRotation;
            model.transform.localScale = Vector3.one * data.ModelScale;

            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator != null && data.AnimatorController != null)
            {
                animator.runtimeAnimatorController = data.AnimatorController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // 게임에선 GunHoldIK 가 양손을 총에 맞춰 줌 -> 여기도 붙여야 팔이 클립 원본 자세로 어색하게 안 뜸 (스크립트 제거 뒤에 붙임)
                if (!animator.TryGetComponent(out GunHoldIK ik))
                    ik = animator.gameObject.AddComponent<GunHoldIK>();
                ik.BindStandalone(animator);
            }

            holder.SetActive(true);
            _model = holder;
            FrameModel(model);
            return true;
        }

        private void FrameModel(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0 || _camera == null)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float height = Mathf.Max(0.5f, bounds.size.y);
            float distance = height / modelScreenHeight / (2f * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            Vector3 lookAt = bounds.center;
            _camera.transform.position = lookAt + new Vector3(0f, height * 0.08f, -distance);
            _camera.transform.LookAt(lookAt);
        }

        // ---------------------------------------------------------------- 캐릭터 고르기 (버튼 OnClick)

        // 테스트 도구용: 마우스 없이 고르고 시작까지
        public void DebugPick(int index)
        {
            SelectCharacter(index);
            Confirm();
        }

        public void SelectPrev() => SelectCharacter((_selected + Count - 1) % Mathf.Max(1, Count));
        public void SelectNext() => SelectCharacter((_selected + 1) % Mathf.Max(1, Count));

        // 아직 모델이 없는 캐릭터는 고를 수 없음 (체인 브루저, 룬 마검사). 모델이 들어오면 저절로 열림
        private static bool IsReady(CharacterData data) => data != null && data.Model != null;

        private void ShowNotReady()
        {
            if (notReadyText == null)
                return;

            notReadyText.DOKill();
            notReadyText.rectTransform.DOKill();
            notReadyText.gameObject.SetActive(true);
            notReadyText.alpha = 1f;
            notReadyText.rectTransform.localScale = Vector3.one * 1.12f;
            notReadyText.rectTransform.DOScale(1f, 0.18f).SetEase(Ease.OutQuad).SetUpdate(true);
            notReadyText.DOFade(0f, 0.35f).SetDelay(1.3f).SetUpdate(true)
                .OnComplete(() => notReadyText.gameObject.SetActive(false));
        }

        public void SelectCharacter(int index)
        {
            if (_busy || index < 0 || index >= Count || index == _selected)
                return;

            if (!IsReady(roster[index]))
            {
                ShowNotReady();
                return;
            }

            _selected = index;
            SelectionChanges++;
            ShowCharacter();
            RefreshCombos();
        }

        private void ShowCharacter()
        {
            if (Count == 0)
                return;

            CharacterData data = roster[_selected];
            _theme = data != null ? data.ThemeColor : Color.white;

            for (int i = 0; i < portraits.Count; i++)
            {
                bool on = i == _selected;
                PortraitView portrait = portraits[i];
                portrait.root.DOKill();
                portrait.root.DOScale(on ? 1.12f : 0.9f, 0.18f).SetEase(Ease.OutQuad).SetUpdate(true);
                portrait.group.alpha = on ? 1f : 0.55f;
            }

            if (pageText != null)
                pageText.text = $"{_selected + 1} / {Count}";

            info.gameObject.SetActive(data != null);
            if (data == null)
            {
                placeholder.SetActive(false);
                return;
            }

            FillInfo(data);
            bool hasModel = ShowModel(data);
            placeholder.SetActive(!hasModel);
            if (!hasModel)
                FillPlaceholder(data);
        }

        private void FillInfo(CharacterData data)
        {
            nameText.text = data.DisplayName;

            // 역할 글("원거리 카이팅 · 단일 특화")을 가운뎃점으로 잘라 태그로 (태그 칸 수만큼)
            var roles = new List<string>();
            foreach (string raw in (data.Role ?? "").Split('·'))
            {
                if (raw.Trim().Length > 0)
                    roles.Add(raw.Trim());
            }
            for (int i = 0; i < tags.Length; i++)
            {
                bool has = i < roles.Count;
                tags[i].root.SetActive(has);
                if (!has)
                    continue;
                tags[i].text.text = roles[i];
                tags[i].text.color = _theme;
                tags[i].line.color = new Color(_theme.r, _theme.g, _theme.b, 0.6f);
                tags[i].text.ForceMeshUpdate();
            }

            // 처음 고를 때는 레이아웃이 아직 안 돌아서 태그가 겹쳐 보임 -> 바로 다시 계산
            if (tags.Length > 0 && tags[0].root.transform.parent is RectTransform tagRow)
                LayoutRebuilder.ForceRebuildLayoutImmediate(tagRow);

            descText.text = data.Description;

            _entries.Clear();
            WeaponKitData kit = data.Kit;
            if (kit != null)
            {
                if (kit.passive != null)
                    _entries.Add(new SkillEntry("패시브", kit.passive.Icon, kit.passive.DisplayName, kit.passive.Description, null));
                AddEntry("우클릭", kit.weaponSkill);
                AddEntry("Space", kit.dash);
                AddEntry("R", kit.ultimate);
            }
            skillNone.gameObject.SetActive(kit == null);
            skillDetail.SetActive(_entries.Count > 0);

            for (int i = 0; i < skillSlots.Length; i++)
            {
                SkillSlotView slot = skillSlots[i];
                bool has = i < _entries.Count;
                slot.root.gameObject.SetActive(has);
                slot.key.gameObject.SetActive(has);
                slot.elementName.gameObject.SetActive(has);
                if (!has)
                    continue;

                SkillEntry entry = _entries[i];
                slot.icon.gameObject.SetActive(entry.icon != null);
                slot.icon.sprite = entry.icon;
                slot.elementDot.gameObject.SetActive(entry.hasElement);
                if (entry.hasElement)
                {
                    slot.elementDot.sprite = ElementSprite(entry.element);
                    slot.elementDot.color = ElementTint(entry.element);
                }
                slot.key.text = entry.key;
                slot.elementName.text = ElementLabel(entry);
                slot.elementName.color = entry.hasElement ? ElementTint(entry.element) : HudUiKit.Label * 0.8f;
            }

            _skill = 0;
            RefreshSkill();
        }

        private void AddEntry(string key, SkillData skill)
        {
            if (skill != null)
                _entries.Add(new SkillEntry(key, skill.Icon, skill.DisplayName, skill.Description, skill));
        }

        // 스킬 아이콘 클릭/마우스 올림 (인스펙터 OnClick, HoverSelect 에 연결)
        public void SelectSkill(int index)
        {
            if (index < 0 || index >= _entries.Count || index == _skill)
                return;
            _skill = index;
            RefreshSkill();
        }

        private void RefreshSkill()
        {
            for (int i = 0; i < skillSlots.Length && i < _entries.Count; i++)
            {
                bool on = i == _skill;
                SkillSlotView slot = skillSlots[i];
                slot.root.DOKill();
                slot.root.DOScale(on ? 1.1f : 0.94f, 0.15f).SetUpdate(true);
                slot.group.alpha = on ? 1f : 0.6f;
                slot.ring.color = on ? HudUiKit.Gold : new Color(_theme.r, _theme.g, _theme.b, 0.85f);
            }

            if (_entries.Count == 0)
                return;

            SkillEntry entry = _entries[_skill];
            detailName.text = entry.title;
            Color chip = entry.hasElement ? ElementTint(entry.element) : HudUiKit.Label;
            detailBadge.gameObject.SetActive(entry.hasElement);
            if (entry.hasElement)
            {
                detailBadge.sprite = ElementSprite(entry.element);
                detailBadge.color = ElementTint(entry.element);
            }
            detailElement.text = ElementLabel(entry);
            detailElement.color = chip;
            detailLine.color = new Color(chip.r, chip.g, chip.b, 0.5f);
            detailDesc.text = string.IsNullOrEmpty(entry.description) ? "설명 준비 중" : entry.description;
            RefreshPreview(entry);
        }

        // 스킬 미리보기 영상 칸을 굽는가. 영상이 아직 없어서 꺼 둠 (필드가 비면 RefreshPreview 는 아무것도 안 함)
        private const bool BakePreviewFrame = false;

        private RenderTexture _previewTexture;
        private VideoClip _previewClip;

        // 고른 스킬의 미리보기 영상을 반복 재생. 영상이 없으면 안내 글자
        private void RefreshPreview(SkillEntry entry)
        {
            if (detailPreview == null || previewPlayer == null)
                return;

            VideoClip clip = entry.skill != null ? entry.skill.PreviewClip : null;
            detailPreview.gameObject.SetActive(clip != null);
            if (previewNote != null)
                previewNote.gameObject.SetActive(clip == null);

            if (clip == null)
            {
                previewPlayer.Stop();
                _previewClip = null;
                return;
            }

            if (clip == _previewClip && previewPlayer.isPlaying)
                return;

            _previewClip = clip;
            if (_previewTexture == null || _previewTexture.width != (int)clip.width || _previewTexture.height != (int)clip.height)
            {
                ReleasePreviewTexture();
                _previewTexture = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = "SkillPreview" };
                previewPlayer.targetTexture = _previewTexture;
                detailPreview.texture = _previewTexture;
            }

            previewPlayer.Stop();
            previewPlayer.clip = clip;
            previewPlayer.isLooping = true;
            previewPlayer.audioOutputMode = VideoAudioOutputMode.None;
            previewPlayer.Play();
        }

        private void ReleasePreviewTexture()
        {
            if (_previewTexture == null)
                return;
            _previewTexture.Release();
            Destroy(_previewTexture);
            _previewTexture = null;
        }

        private void OnDestroy() => ReleasePreviewTexture();

        private static string ElementLabel(SkillEntry entry) =>
            entry.hasElement ? ElementComboBook.KoreanName(entry.element) + " 속성" : (entry.key == "패시브" ? "상시" : "무속성");

        private Color ElementTint(ElementType element) =>
            Palette != null ? Palette.ColorOf(element) : Color.white;

        // 배지 그림이 있으면 그걸, 없으면 색 원 (원 그림은 프리팹/씬의 원래 스프라이트 그대로)
        private Sprite ElementSprite(ElementType element)
        {
            Sprite badge = Palette != null ? Palette.BadgeOf(element) : null;
            return badge != null ? badge : placeholderDisc.sprite;
        }

        // 모델 없는 캐릭터: 테마색 원판 + 이름 첫 글자 (또는 초상화)
        private void FillPlaceholder(CharacterData data)
        {
            placeholderGlow.color = new Color(_theme.r, _theme.g, _theme.b, 0.35f);
            placeholderDisc.color = new Color(_theme.r * 0.35f, _theme.g * 0.35f, _theme.b * 0.35f, 1f);
            placeholderRing.color = _theme;
            placeholderFace.gameObject.SetActive(data.Portrait != null);
            placeholderFace.sprite = data.Portrait;
            placeholderInitial.gameObject.SetActive(data.Portrait == null);
            placeholderInitial.text = Initial(data);
            placeholderInitial.color = _theme;
        }

        private static string Initial(CharacterData data) =>
            string.IsNullOrEmpty(data.DisplayName) ? "?" : data.DisplayName.Substring(0, 1);

        // ---------------------------------------------------------------- 시작 콤보 고르기

        private void ChooseCombo(ElementType[] sequence)
        {
            if (_busy)
                return;
            ComboLoadout.ChooseStarter(sequence);
            ComboChanges++;
            RefreshCombos();
        }

        // 지금 캐릭터의 시작 콤보 줄(프리팹 복제)을 새로 만들고, 고른 것이 이 캐릭터 것이 아니면 첫 번째로
        private void RefreshCombos()
        {
            if (comboArea == null || Count == 0)
                return;

            WeaponKitData kit = roster[_selected] != null ? roster[_selected].Kit : null;
            List<ElementType[]> options = kit != null ? ComboLoadout.StarterOptions(kit.weaponSkill, kit.ultimate) : new List<ElementType[]>();
            if (kit != null)
                ComboLoadout.EnsureStarterFor(kit.weaponSkill, kit.ultimate);

            string key = string.Join(",", options.ConvertAll(ComboLoadout.NameOf));
            if (key != _comboKey && comboRowPrefab != null)
            {
                _comboKey = key;
                foreach (PickComboRowView old in _comboRows)
                {
                    if (old != null)
                        Destroy(old.gameObject);
                }
                _comboRows.Clear();
                foreach (ElementType[] option in options)
                {
                    PickComboRowView row = Instantiate(comboRowPrefab, comboArea);
                    row.name = "Combo_" + ComboLoadout.NameOf(option);
                    ElementType[] sequence = option;
                    row.Setup(sequence, Palette, () => ChooseCombo(sequence));
                    _comboRows.Add(row);
                }
            }

            foreach (PickComboRowView row in _comboRows)
                row.SetSelected(ComboLoadout.NameOf(row.Sequence) == ComboLoadout.NameOf(ComboLoadout.Starter));

            comboHint.text = options.Count == 0
                ? "이 캐릭터는 우클릭/궁극기에 원소가 없어 시작 콤보가 없습니다."
                : "튜토리얼과 초반에는 이 콤보 하나로 싸웁니다. 효과는 원소마다 정해진 능력치 + 순서 보너스입니다.";
        }

        // ---------------------------------------------------------------- 아웃 업그레이드 (버튼 OnClick)

        private EnchantTree TreeOf(CharacterData character)
        {
            if (skillTrees == null)
                return null;
            foreach (EnchantTree tree in skillTrees)
            {
                if (tree != null && tree.Character == character)
                    return tree;
            }
            return null;
        }

        public void OpenOutUpgrade()
        {
            if (_busy || Count == 0 || SkillTreeScreen.IsOpen)
                return;

            CharacterData data = roster[_selected];
            SkillTreeScreen.Open(skillTreeScreenPrefab, TreeOf(data), data != null ? data.DisplayName : string.Empty);
        }

        // ---------------------------------------------------------------- 시작 (버튼 OnClick)

        public void Confirm()
        {
            if (_busy || Count == 0)
                return;

            _busy = true;
            CharacterSelection.Pick(_selected);
            SkillTreeProgress.Apply(TreeOf(roster[_selected]));   // 이 캐릭터의 스킬 트리 강화를 이번 런에 올림
            RunReset.Begin();
            ComboLoadout.BeginRun();
            // 튜토리얼은 콤보 바인딩 상점이 있는 상점 맵에서 진행
            string scene = TutorialSession.Active ? TutorialDefaults.ShopScene : nextScene;

            if (fade == null)
            {
                SceneManager.LoadScene(scene);
                return;
            }
            fade.raycastTarget = true;
            fade.DOFade(1f, fadeTime).SetUpdate(true).OnComplete(() => SceneManager.LoadScene(scene));
        }

        // ---------------------------------------------------------------- 레이아웃 굽기 (에디터 전용)

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);
            palette = palette != null ? palette : ElementPalette.Default;
            comboRowPrefab = comboRowPrefab != null ? comboRowPrefab
                : UnityEditor.AssetDatabase.LoadAssetAtPath<PickComboRowView>("Assets/Members/HJH/05.Data/Prefab/UI/PickComboRow.prefab");

            // 무대: 카메라 배경, 조명, 모델 자리 + 마우스로 돌리기
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.035f, 0.05f, 0.09f);
            }
            var keyLight = new GameObject("Key Light", typeof(Light));
            keyLight.transform.SetParent(transform, false);
            keyLight.transform.rotation = Quaternion.Euler(35f, 25f, 0f);
            var light = keyLight.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.97f, 0.92f);
            stage = new GameObject("Stage").transform;
            stage.SetParent(transform, false);
            var spin = GetComponent<PickModelSpin>() ? GetComponent<PickModelSpin>() : gameObject.AddComponent<PickModelSpin>();
            spin.Stage = stage;

            // 캔버스: 카메라 바로 앞 (모델보다 앞). 화면 캡처 도구가 카메라로 찍어도 UI 가 같이 나옴
            var canvasGo = new GameObject("PickCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.5f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGo.transform;

            Image glow = HudUiKit.NewImage("BackGlow", root, HudUiKit.Load("soft_glow"), new Color(0.3f, 0.6f, 1f, 0.10f));
            HudUiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 1300f));
            foreach (float side in new[] { 0f, 1f })
            {
                Image shade = HudUiKit.NewImage(side < 0.5f ? "ShadeLeft" : "ShadeRight", root, null, new Color(0.01f, 0.015f, 0.03f, 0.55f));
                HudUiKit.Place(shade.rectTransform, new Vector2(side, 0.5f), Vector2.zero, new Vector2(ColumnWidth + SideMargin * 2f, 1400f));
            }

            BakeLeft(root);
            BakeCenter(root);
            BakeRight(root);

            fade = HudUiKit.NewImage("Fade", root, null, new Color(0f, 0f, 0f, 0f));
            HudUiKit.Stretch(fade.rectTransform);
#endif
        }

#if UNITY_EDITOR
        private static void Wire(Button button, UnityEngine.Events.UnityAction action) =>
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);

        private static void Wire(UnityEngine.Events.UnityEvent target, UnityEngine.Events.UnityAction<int> action, int argument) =>
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(target, action, argument);

        private static Button NewButton(Image target)
        {
            var button = target.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            return button;
        }

        // 왼쪽: 초상화(캐릭터 목록 수만큼) + 정보(이름, 태그, 설명, 스킬 4칸, 고른 스킬 설명)
        private void BakeLeft(Transform root)
        {
            RectTransform column = HudUiKit.NewRect("Left", root);
            HudUiKit.Place(column, new Vector2(0f, 1f), new Vector2(SideMargin, -48f), new Vector2(ColumnWidth, 990f));

            portraits = new List<PortraitView>();
            if (Count == 0)
            {
                TMP_Text warn = HudUiKit.NewText("Warn", column, "캐릭터 목록이 비어 있습니다 (CharacterPickScreen > Roster)", 22f, Warn);
                HudUiKit.Place(warn.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(ColumnWidth, 40f));
            }

            for (int i = 0; i < Count; i++)
            {
                CharacterData data = roster[i];
                Color theme = data != null ? data.ThemeColor : Color.white;

                RectTransform portrait = HudUiKit.NewRect("Portrait_" + i, column);
                HudUiKit.Place(portrait, new Vector2(0f, 1f), new Vector2(46f + i * 112f, -46f), new Vector2(92f, 92f), new Vector2(0.5f, 0.5f));
                var group = portrait.gameObject.AddComponent<CanvasGroup>();

                Image disc = HudUiKit.NewImage("Back", portrait, HudUiKit.Load("slot_circle_bg"), new Color(theme.r * 0.4f, theme.g * 0.4f, theme.b * 0.4f, 1f));
                HudUiKit.Stretch(disc.rectTransform);
                disc.raycastTarget = true;

                if (data != null && data.Portrait != null)
                {
                    Image face = HudUiKit.NewImage("Face", portrait, data.Portrait, Color.white);
                    face.preserveAspect = true;
                    HudUiKit.Stretch(face.rectTransform, 10f);
                }
                else
                {
                    TMP_Text letter = HudUiKit.NewText("Initial", portrait, data != null ? Initial(data) : "?", 40f, theme, TextAlignmentOptions.Center);
                    HudUiKit.Stretch(letter.rectTransform);
                }

                Image ring = HudUiKit.NewImage("Ring", portrait, HudUiKit.Load("slot_circle_frame"), theme);
                HudUiKit.Stretch(ring.rectTransform);

                Wire(NewButton(disc).onClick, SelectCharacter, i);
                portraits.Add(new PortraitView { root = portrait, group = group });
            }

            // 튜토리얼 강조용 자리 (초상화 줄 전체)
            portraitRow = HudUiKit.NewRect("PortraitRow", column);
            HudUiKit.Place(portraitRow, new Vector2(0f, 1f), new Vector2(-6f, 6f), new Vector2(Mathf.Max(1, Count) * 112f, 104f));

            info = HudUiKit.NewRect("Info", column);
            HudUiKit.Place(info, new Vector2(0f, 1f), new Vector2(0f, -120f), new Vector2(ColumnWidth, 870f));

            nameText = HudUiKit.NewText("Name", info, "캐릭터 이름", 56f, HudUiKit.Title, TextAlignmentOptions.TopLeft, 4f);
            HudUiKit.Place(nameText.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(ColumnWidth, 72f));

            // 태그 줄: 글자 길이에 맞춰 칸 너비가 정해짐 (ContentSizeFitter), 칸끼리는 가로 줄
            RectTransform tagRow = HudUiKit.NewRect("Tags", info);
            HudUiKit.Place(tagRow, new Vector2(0f, 1f), new Vector2(0f, -80f), new Vector2(ColumnWidth, 32f));
            var tagLayout = tagRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tagLayout.spacing = 10f;
            tagLayout.childAlignment = TextAnchor.MiddleLeft;
            tagLayout.childControlWidth = false;
            tagLayout.childControlHeight = false;
            tagLayout.childForceExpandWidth = false;
            tagLayout.childForceExpandHeight = false;
            tags = new TagView[4];
            for (int i = 0; i < tags.Length; i++)
            {
                RectTransform tag = HudUiKit.NewRect("Tag_" + i, tagRow);
                tag.sizeDelta = new Vector2(120f, 32f);
                var pad = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
                pad.padding = new RectOffset(14, 14, 0, 0);
                pad.childControlWidth = true;
                pad.childControlHeight = true;
                tag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                TMP_Text text = HudUiKit.NewText("Text", tag, "태그", 18f, HudUiKit.Title, TextAlignmentOptions.Center);
                Image line = HudUiKit.NewImage("Line", tag, null, new Color(1f, 1f, 1f, 0.6f));
                line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                line.rectTransform.anchorMin = new Vector2(0f, 0f);
                line.rectTransform.anchorMax = new Vector2(1f, 0f);
                line.rectTransform.pivot = new Vector2(0.5f, 0f);
                line.rectTransform.offsetMin = new Vector2(6f, 0f);
                line.rectTransform.offsetMax = new Vector2(-6f, 1.5f);
                tags[i] = new TagView { root = tag.gameObject, text = text, line = line };
            }

            descText = HudUiKit.NewText("Description", info, "캐릭터 설명", 19f, HudUiKit.Label, TextAlignmentOptions.TopLeft);
            descText.textWrappingMode = TextWrappingModes.Normal;
            descText.overflowMode = TextOverflowModes.Ellipsis;
            descText.lineSpacing = 6f;
            HudUiKit.Place(descText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -126f), new Vector2(ColumnWidth, 96f));

            skillsArea = HudUiKit.NewRect("Skills", info);
            HudUiKit.Place(skillsArea, new Vector2(0f, 1f), new Vector2(0f, -246f), new Vector2(ColumnWidth, 600f));
            HudUiKit.BuildHeader(skillsArea, "스킬", ColumnWidth);

            skillNone = HudUiKit.NewText("None", skillsArea, "스킬 정보가 없습니다.", 18f, HudUiKit.Label);
            HudUiKit.Place(skillNone.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -64f), new Vector2(ColumnWidth, 30f));

            skillSlots = new SkillSlotView[4];
            for (int i = 0; i < skillSlots.Length; i++)
            {
                RectTransform slot = HudUiKit.NewRect("Skill_" + i, skillsArea);
                HudUiKit.Place(slot, new Vector2(0f, 1f), new Vector2(i * 138f + 42f, -64f), new Vector2(84f, 84f), new Vector2(0.5f, 1f));
                var group = slot.gameObject.AddComponent<CanvasGroup>();

                Image disc = HudUiKit.NewImage("IconBack", slot, HudUiKit.Load("slot_circle_bg"), new Color(0.08f, 0.11f, 0.17f, 1f));
                HudUiKit.Stretch(disc.rectTransform);
                disc.raycastTarget = true;
                Image icon = HudUiKit.NewImage("Icon", slot, null, Color.white);
                icon.preserveAspect = true;
                HudUiKit.Stretch(icon.rectTransform, 10f);
                Image ring = HudUiKit.NewImage("IconRing", slot, HudUiKit.Load("slot_circle_frame"), Color.white);
                HudUiKit.Stretch(ring.rectTransform);
                Image dot = HudUiKit.NewImage("ElementDot", slot, HudUiKit.Load("slot_circle_bg"), Color.white);
                dot.preserveAspect = true;
                HudUiKit.Place(dot.rectTransform, new Vector2(1f, 0f), new Vector2(4f, -2f), new Vector2(30f, 30f), new Vector2(1f, 0f));

                Wire(NewButton(disc).onClick, SelectSkill, i);
                Wire(slot.gameObject.AddComponent<HoverSelect>().OnEnter, SelectSkill, i);

                TMP_Text key = HudUiKit.NewText("Key_" + i, skillsArea, "키", 16f, HudUiKit.Gold, TextAlignmentOptions.Center, 2f);
                HudUiKit.Place(key.rectTransform, new Vector2(0f, 1f), new Vector2(i * 138f - 18f, -160f), new Vector2(120f, 24f));
                TMP_Text elementName = HudUiKit.NewText("ElementName_" + i, skillsArea, "속성", 15f, HudUiKit.Label, TextAlignmentOptions.Center);
                HudUiKit.Place(elementName.rectTransform, new Vector2(0f, 1f), new Vector2(i * 138f - 18f, -184f), new Vector2(120f, 22f));

                skillSlots[i] = new SkillSlotView { root = slot, group = group, icon = icon, ring = ring, elementDot = dot, key = key, elementName = elementName };
            }

            // 고른 스킬 설명
            RectTransform detail = HudUiKit.NewRect("Detail", skillsArea);
            HudUiKit.Place(detail, new Vector2(0f, 1f), new Vector2(0f, -236f), new Vector2(ColumnWidth, 240f));
            skillDetail = detail.gameObject;
            detailName = HudUiKit.NewText("Name", detail, "스킬 이름", 28f, HudUiKit.Title, TextAlignmentOptions.TopLeft, 2f);
            HudUiKit.Place(detailName.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(ColumnWidth - 190f, 40f));
            detailBadge = HudUiKit.NewImage("ElementBadge", detail, HudUiKit.Load("slot_circle_bg"), Color.white);
            detailBadge.preserveAspect = true;
            HudUiKit.Place(detailBadge.rectTransform, new Vector2(1f, 1f), new Vector2(-96f, -4f), new Vector2(30f, 30f), new Vector2(1f, 1f));
            detailElement = HudUiKit.NewText("ElementText", detail, "속성", 19f, HudUiKit.Label, TextAlignmentOptions.TopRight, 1f);
            HudUiKit.Place(detailElement.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -4f), new Vector2(92f, 30f), new Vector2(1f, 1f));
            detailLine = HudUiKit.NewImage("Line", detail, HudUiKit.Load("divider"), new Color(1f, 1f, 1f, 0.5f));
            HudUiKit.Place(detailLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -42f), new Vector2(ColumnWidth, 14f));
            detailDesc = HudUiKit.NewText("Description", detail, "스킬 설명", 18f, HudUiKit.Label, TextAlignmentOptions.TopLeft);
            detailDesc.textWrappingMode = TextWrappingModes.Normal;
            detailDesc.overflowMode = TextOverflowModes.Ellipsis;
            detailDesc.lineSpacing = 6f;
            HudUiKit.Place(detailDesc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -62f), new Vector2(ColumnWidth, 110f));

            // 미리보기 영상 칸은 영상이 준비될 때까지 굽지 않음 (켜려면 BakePreviewFrame = true 로 바꾸고 다시 굽기)
            if (!BakePreviewFrame)
                return;

            // 미리보기 영상: 설명 아래 빈 공간. 16:9 칸 + 영상이 없을 때 안내
            RectTransform frame = HudUiKit.NewRect("Preview", detail);
            HudUiKit.Place(frame, new Vector2(0f, 1f), new Vector2(0f, -184f), new Vector2(ColumnWidth, ColumnWidth * 9f / 16f));
            Image previewBack = HudUiKit.NewImage("Back", frame, null, new Color(0.02f, 0.04f, 0.07f, 0.9f));
            HudUiKit.Stretch(previewBack.rectTransform);
            detailPreview = HudUiKit.NewRect("Video", frame).gameObject.AddComponent<RawImage>();
            detailPreview.raycastTarget = false;
            HudUiKit.Stretch(detailPreview.rectTransform);
            previewPlayer = detail.gameObject.AddComponent<VideoPlayer>();
            previewPlayer.playOnAwake = false;
            previewPlayer.isLooping = true;
            previewPlayer.renderMode = VideoRenderMode.RenderTexture;
            previewPlayer.audioOutputMode = VideoAudioOutputMode.None;
            previewNote = HudUiKit.NewText("Note", frame, "미리보기 영상 준비 중", 18f, HudUiKit.Label * 0.8f, TextAlignmentOptions.Center);
            HudUiKit.Stretch(previewNote.rectTransform);
            Image previewEdge = HudUiKit.NewImage("Edge", frame, null, new Color(1f, 1f, 1f, 0.12f));
            previewEdge.raycastTarget = false;
            previewEdge.rectTransform.anchorMin = new Vector2(0f, 0f);
            previewEdge.rectTransform.anchorMax = new Vector2(1f, 0f);
            previewEdge.rectTransform.pivot = new Vector2(0.5f, 0f);
            previewEdge.rectTransform.offsetMin = Vector2.zero;
            previewEdge.rectTransform.offsetMax = new Vector2(0f, 1.5f);
            detailPreview.gameObject.SetActive(false);
        }

        // 가운데: 모델 없는 캐릭터용 원판 + 아래 쪽수 · 화살표
        private void BakeCenter(Transform root)
        {
            RectTransform center = HudUiKit.NewRect("Center", root);
            HudUiKit.Place(center, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(420f, 420f));
            placeholder = center.gameObject;
            placeholderGlow = HudUiKit.NewImage("Glow", center, HudUiKit.Load("soft_glow"), new Color(1f, 1f, 1f, 0.35f));
            HudUiKit.Place(placeholderGlow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 900f));
            placeholderDisc = HudUiKit.NewImage("Disc", center, HudUiKit.Load("slot_circle_bg"), new Color(0.3f, 0.3f, 0.3f, 1f));
            HudUiKit.Stretch(placeholderDisc.rectTransform);
            placeholderFace = HudUiKit.NewImage("Face", center, null, Color.white);
            placeholderFace.preserveAspect = true;
            HudUiKit.Stretch(placeholderFace.rectTransform, 40f);
            placeholderInitial = HudUiKit.NewText("Initial", center, "?", 190f, Color.white, TextAlignmentOptions.Center);
            HudUiKit.Stretch(placeholderInitial.rectTransform);
            placeholderRing = HudUiKit.NewImage("Ring", center, HudUiKit.Load("slot_circle_frame"), Color.white);
            HudUiKit.Stretch(placeholderRing.rectTransform);
            TMP_Text note = HudUiKit.NewText("Note", center, "모델 준비 중", 18f, HudUiKit.Label, TextAlignmentOptions.Center, 2f);
            HudUiKit.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -44f), new Vector2(300f, 28f));
            center.gameObject.SetActive(false);

            RectTransform pager = HudUiKit.NewRect("Pager", root);
            HudUiKit.Place(pager, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(360f, 56f));
            pageText = HudUiKit.NewText("Page", pager, "1 / 3", 22f, HudUiKit.Label, TextAlignmentOptions.Center, 4f);
            HudUiKit.Stretch(pageText.rectTransform);
            BakeArrow(pager, "Prev", "<", 0f, SelectPrev);
            BakeArrow(pager, "Next", ">", 1f, SelectNext);

            // 준비 안 된 캐릭터를 고르려 할 때 쪽수 위에 잠깐 뜨는 안내 (평소엔 꺼져 있음)
            notReadyText = HudUiKit.NewText("NotReady", root, "아직 준비되지 않았습니다.", 28f, Warn, TextAlignmentOptions.Center, 2f);
            HudUiKit.Place(notReadyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(560f, 44f));
            notReadyText.gameObject.SetActive(false);
        }

        private static void BakeArrow(Transform parent, string name, string glyph, float side, UnityEngine.Events.UnityAction onClick)
        {
            Image hit = HudUiKit.NewImage(name, parent, null, new Color(1f, 1f, 1f, 0f));
            HudUiKit.Place(hit.rectTransform, new Vector2(side, 0.5f), Vector2.zero, new Vector2(64f, 56f));
            hit.raycastTarget = true;
            TMP_Text text = HudUiKit.NewText("Glyph", hit.transform, glyph, 44f, HudUiKit.Title, TextAlignmentOptions.Center);
            HudUiKit.Stretch(text.rectTransform);
            Wire(NewButton(hit), onClick);
            hit.gameObject.AddComponent<HoverScale>().Scale = 1.03f;
        }

        // 오른쪽: 시작 콤보(프리팹 줄이 들어갈 세로 줄) + 아웃 업그레이드 + 게임 시작
        private void BakeRight(Transform root)
        {
            RectTransform column = HudUiKit.NewRect("Right", root);
            HudUiKit.Place(column, new Vector2(1f, 1f), new Vector2(-SideMargin, -48f), new Vector2(ColumnWidth, 990f));

            HudUiKit.BuildHeader(column, "시작 콤보", ColumnWidth);

            TMP_Text guide = HudUiKit.NewText("Guide", column,
                "우클릭 스킬과 궁극기 원소로 만든 콤보 중 하나를 들고 갑니다.\n게임 중 상점에서 새 콤보를 바인딩할 수 있습니다.", 16f, HudUiKit.Label);
            HudUiKit.Place(guide.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -52f), new Vector2(ColumnWidth, 46f));

            const float rowHeight = 116f;
            comboArea = HudUiKit.NewRect("ComboArea", column);
            HudUiKit.Place(comboArea, new Vector2(0f, 1f), new Vector2(0f, -108f), new Vector2(ColumnWidth, rowHeight * 2f + 8f));
            var rows = comboArea.gameObject.AddComponent<VerticalLayoutGroup>();
            rows.spacing = 6f;
            rows.childAlignment = TextAnchor.UpperLeft;
            rows.childControlWidth = false;
            rows.childControlHeight = false;
            rows.childForceExpandWidth = false;
            rows.childForceExpandHeight = false;

            comboHint = HudUiKit.NewText("Hint", column, "", 16f, HudUiKit.Label);
            comboHint.textWrappingMode = TextWrappingModes.Normal;
            HudUiKit.Place(comboHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -108f - rowHeight * 2f - 16f), new Vector2(ColumnWidth, 60f));

            // 아웃 업그레이드: 누르면 스킬 트리 창 (skillTreeScreenPrefab)
            Image upgrade = HudUiKit.NewImage("OutUpgrade", column, HudUiKit.Load("button_normal"), Color.white);
            HudUiKit.Place(upgrade.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(ColumnWidth, 64f));
            upgrade.raycastTarget = true;
            TMP_Text upgradeLabel = HudUiKit.NewText("Label", upgrade.transform, "아웃 업그레이드", 24f, HudUiKit.Title, TextAlignmentOptions.Center, 4f);
            HudUiKit.Stretch(upgradeLabel.rectTransform);
            Wire(NewButton(upgrade), OpenOutUpgrade);
            upgrade.gameObject.AddComponent<HoverScale>().Scale = 1.03f;

            Image start = HudUiKit.NewImage("Start", column, HudUiKit.Load("button_hover"), Color.white);
            HudUiKit.Place(start.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(ColumnWidth, 92f));
            start.raycastTarget = true;
            TMP_Text startLabel = HudUiKit.NewText("Label", start.transform, "게임 시작", 34f, HudUiKit.Title, TextAlignmentOptions.Center, 8f);
            HudUiKit.Stretch(startLabel.rectTransform);
            Wire(NewButton(start), Confirm);
            startButton = start.rectTransform;
            start.gameObject.AddComponent<HoverScale>().Scale = 1.03f;
        }
#endif
    }
}
