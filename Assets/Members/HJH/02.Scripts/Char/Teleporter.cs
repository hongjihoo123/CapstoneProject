using Members.KYR._01_Scripts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Teleporter : MonoBehaviour
{
    public enum TeleportMode
    {
        Position,
        Scene
    }

    [SerializeField] private TeleportMode mode = TeleportMode.Position;

    [Header("Position 모드")]
    [SerializeField] private Transform destination;
    [SerializeField] private bool applyDestinationRotation = true;

    [Header("Scene 모드")]
    [SerializeField] private string sceneName;

    [Header("작동 방식")]
    [SerializeField] private bool requireKeyPress;
    [SerializeField] private Key activationKey = Key.F;

    private PlayerAgent _playerInRange;

    private void OnTriggerEnter(Collider other)
    {
        PlayerAgent player = other.GetComponentInParent<PlayerAgent>();
        if (player == null)
            return;

        _playerInRange = player;

        if (!requireKeyPress)
            Activate(player);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerAgent player = other.GetComponentInParent<PlayerAgent>();
        if (player != null && player == _playerInRange)
            _playerInRange = null;
    }

    private void Update()
    {
        if (!requireKeyPress || _playerInRange == null)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[activationKey].wasPressedThisFrame)
            Activate(_playerInRange);
    }

    private void Activate(PlayerAgent player)
    {
        if (mode == TeleportMode.Scene)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning($"{name}: Scene Name이 비어있습니다.");
                return;
            }

            SceneManager.LoadScene(sceneName);
            return;
        }

        if (destination == null)
        {
            Debug.LogWarning($"{name}: Destination이 비어있습니다.");
            return;
        }

        player.Teleport(
            destination.position,
            applyDestinationRotation ? destination.rotation : (Quaternion?)null);

        _playerInRange = null;
    }
}
