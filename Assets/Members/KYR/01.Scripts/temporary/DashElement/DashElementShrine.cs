using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Player;
using Members.KYR._01_Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.DashElement
{
    [DefaultExecutionOrder(5000)]
    public class DashElementShrine : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float detectRadius = 2.2f;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button[] choices;
        [SerializeField] private TMP_Text[] labels;
        [SerializeField] private Image[] swatches;
        [SerializeField] private GameObject prompt;

        private PlayerAgent _player;
        private PlayerInputSO _input;
        private int _inside;
        private bool _open;
        private int _freeCursor;

        private void Awake()
        {
            if (panel != null)
                panel.SetActive(false);
            if (prompt != null)
                prompt.SetActive(false);

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (choices == null)
                return;

            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                if (choices[i] != null)
                    choices[i].onClick.AddListener(() => Choose(index));
            }
        }

        private void OnDestroy()
        {
            UnbindInput();
            if (!_open)
                return;

            _open = false;
            UiFocusService.Release(this);
            Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            if (_inside > 0 && (_player == null || !_player.IsAlive))
            {
                _inside = 0;
                UnbindInput();
                _player = null;
                if (_open)
                    Close();
                else
                    SetPrompt(false);
                return;
            }

            if (_open)
                ReadChoiceKeys();
            else
                SetPrompt(_inside > 0);
        }

        private void LateUpdate()
        {
            if (prompt != null && prompt.activeSelf && Camera.main != null)
                prompt.transform.rotation = Camera.main.transform.rotation;

            if (_open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (_freeCursor <= 0)
                return;

            _freeCursor--;
            if (!UiFocusService.IsUiFocused)
                Cursor.lockState = CursorLockMode.None;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerAgent agent = other != null ? other.GetComponentInParent<PlayerAgent>() : null;
            if (agent == null)
                return;

            _inside++;
            BindInput(agent);
            if (!_open)
                SetPrompt(true);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerAgent agent = other != null ? other.GetComponentInParent<PlayerAgent>() : null;
            if (agent == null)
                return;

            _inside = Mathf.Max(0, _inside - 1);
            if (_inside == 0)
            {
                UnbindInput();
                if (!_open)
                    SetPrompt(false);
            }
        }

        private void OnValidate()
        {
            SphereCollider range = GetComponent<SphereCollider>();
            if (range == null || !range.isTrigger)
                return;

            range.radius = detectRadius;
            range.center = new Vector3(0f, 0.9f, 0f);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.55f, 0.4f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + new Vector3(0f, 0.9f, 0f), detectRadius);
        }

        private void Open()
        {
            if (panel == null || _player == null || _player.SkillFsm == null)
                return;

            RefreshLabels();
            panel.SetActive(true);
            _open = true;
            SetPrompt(false);
            UiFocusService.Acquire(this);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Close()
        {
            if (!_open)
                return;

            _open = false;
            if (panel != null)
                panel.SetActive(false);

            UiFocusService.Release(this);
            Cursor.lockState = CursorLockMode.None;
            _freeCursor = 8;
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

            if (keyboard.escapeKey.wasPressedThisFrame)
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
            int count = labels != null ? labels.Length : 0;
            for (int i = 0; i < count && i < options.Length; i++)
            {
                if (labels[i] == null)
                    continue;

                string name = ElementComboBook.KoreanName(options[i]);
                labels[i].text = current == options[i] ? $"{name}\n [] " : name;
                if (swatches != null && i < swatches.Length && swatches[i] != null && ElementPalette.Default != null)
                    swatches[i].color = ElementPalette.Default.ColorOf(options[i]);
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

        private void BindInput(PlayerAgent agent)
        {
            if (agent == null || (_player == agent && _input != null))
                return;

            UnbindInput();
            _player = agent;
            _input = agent.PlayerInput;
            if (_input != null)
                _input.OnInteractPressed += HandleInteract;
        }

        private void UnbindInput()
        {
            if (_input != null)
                _input.OnInteractPressed -= HandleInteract;
            _input = null;
        }

        private void HandleInteract()
        {
            if (_player == null || !_player.IsAlive)
                return;

            if (_open)
                Close();
            else if (_inside > 0)
                Open();
        }

        private void SetPrompt(bool visible)
        {
            if (prompt == null)
                return;

            prompt.SetActive(visible);
            if (!visible || _input == null)
                return;

            TMP_Text label = prompt.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = _input.InteractBindingLabel;
        }
    }
}
