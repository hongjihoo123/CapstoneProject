using Members.KYR._01_Scripts;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterSelectZone : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject prompt;
    [SerializeField] private CharacterSelectUI selectUI;
    [SerializeField] private bool closeAfterSelect = true;

    [Header("입력")]
    [SerializeField] private Key activationKey = Key.F;
    [SerializeField] private Key closeKey = Key.Escape;

    private PlayerAgent _playerInRange;
    private bool _isOpen;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
        if (prompt != null)
            prompt.SetActive(false);
    }

    private void OnEnable()
    {
        if (selectUI != null)
            selectUI.OnSelected += HandleSelected;
    }

    private void OnDisable()
    {
        if (selectUI != null)
            selectUI.OnSelected -= HandleSelected;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerAgent player = other.GetComponentInParent<PlayerAgent>();
        if (player == null)
            return;

        _playerInRange = player;
        SetPromptVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerAgent player = other.GetComponentInParent<PlayerAgent>();
        if (player == null || player != _playerInRange)
            return;

        if (_isOpen)
            Close();

        _playerInRange = null;
        SetPromptVisible(false);
    }

    private void Update()
    {
        if (_playerInRange == null)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard[activationKey].wasPressedThisFrame)
        {
            if (_isOpen)
                Close();
            else
                Open();
            return;
        }

        if (_isOpen && keyboard[closeKey].wasPressedThisFrame)
            Close();
    }

    private void Open()
    {
        if (_playerInRange == null)
            return;

        _isOpen = true;

        if (panel != null)
            panel.SetActive(true);

        SetPromptVisible(false);
        _playerInRange.SetUiMode(true);
    }

    private void Close()
    {
        _isOpen = false;

        if (panel != null)
            panel.SetActive(false);

        if (_playerInRange != null)
            _playerInRange.SetUiMode(false);

        SetPromptVisible(_playerInRange != null);
    }

    private void HandleSelected(CharacterDataSO data)
    {
        if (closeAfterSelect && _isOpen)
            Close();
    }

    private void SetPromptVisible(bool visible)
    {
        if (prompt != null)
            prompt.SetActive(visible && !_isOpen);
    }
}