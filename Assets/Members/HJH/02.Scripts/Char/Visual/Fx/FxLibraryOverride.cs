using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Put in a scene to play another FxLibrary there (e.g. a new effects pack) while every other scene keeps the default.
    // The toggle key flips between this library and the default one, to compare the two on the same moves.
    [DefaultExecutionOrder(-10000)]
    public class FxLibraryOverride : MonoBehaviour
    {
        [SerializeField] private FxLibrary library;
        [SerializeField, Tooltip("Flips between this library and the default one in Play mode.")]
        private Key toggleKey = Key.F8;
        [SerializeField] private bool showLabel = true;

        private bool _active;
        private GUIStyle _style;

        private void OnEnable() => Apply(true);

        private void OnDisable()
        {
            if (FxLibrary.Override == library)
                FxLibrary.SetOverride(null);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
                Apply(!_active);
        }

        private void Apply(bool active)
        {
            _active = active && library != null;
            FxLibrary.SetOverride(_active ? library : null);
        }

        private void OnGUI()
        {
            if (!showLabel)
                return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            string current = _active ? library.name : "default (Resources)";
            GUI.Label(new Rect(12f, Screen.height - 34f, 600f, 28f), $"FX: {current}   [{toggleKey}] 전환", _style);
        }
    }
}
