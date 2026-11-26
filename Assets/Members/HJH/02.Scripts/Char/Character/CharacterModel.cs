using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Character
{
    public class CharacterModel : MonoBehaviour
    {
        private const string VisualName = "Visual";
        private static readonly string[] PlaceholderNames = { "Body", "FacingMarker" };

        private Transform _visualRoot;
        private GameObject _current;

        public Animator Animator { get; private set; }

        public GameObject Current => _current;

        public Transform Visual => VisualRoot();

        public static CharacterModel For(PlayerAgent player)
        {
            if (!player.TryGetComponent(out CharacterModel model))
                model = player.gameObject.AddComponent<CharacterModel>();
            if (!player.TryGetComponent(out CharacterAnimator _))
                player.gameObject.AddComponent<CharacterAnimator>();
            if (!player.TryGetComponent(out GunHoldIK _))
                player.gameObject.AddComponent<GunHoldIK>();
            if (!player.TryGetComponent(out PlayerHitReaction _))
                player.gameObject.AddComponent<PlayerHitReaction>();
            if (!player.TryGetComponent(out RobotWeapons.HitStagger _))
                player.gameObject.AddComponent<RobotWeapons.HitStagger>();
            return model;
        }

        public void Show(CharacterData character)
        {
            ReturnAimOrigin();
            if (_current != null)
                Destroy(_current);
            _current = null;
            Animator = null;

            bool hasModel = character != null && character.Model != null;
            SetPlaceholderVisible(!hasModel);
            if (!hasModel)
                return;

            Transform parent = VisualRoot();
            _current = Instantiate(character.Model, parent);
            _current.name = character.Model.name;
            _current.transform.SetLocalPositionAndRotation(character.ModelOffset, character.ModelRotation);
            _current.transform.localScale = Vector3.one * character.ModelScale;

            Animator = _current.GetComponentInChildren<Animator>();
            if (Animator == null)
                Animator = _current.AddComponent<Animator>();
            Animator.applyRootMotion = false;
            if (character.AnimatorController != null)
                Animator.runtimeAnimatorController = character.AnimatorController;

            if (!Animator.TryGetComponent(out PlayerAnimationEventRelay _))
                Animator.gameObject.AddComponent<PlayerAnimationEventRelay>();

            AttachAimOrigin();
        }

        private const string AimOriginMarker = "AimOrigin";

        private Transform _aimOrigin;
        private Transform _aimHome;
        private Vector3 _aimHomePosition;
        private Quaternion _aimHomeRotation;

        private void AttachAimOrigin()
        {
            Transform marker = FindDeep(_current.transform, AimOriginMarker);
            var player = GetComponent<PlayerAgent>();
            if (marker == null || player == null || player.AimOrigin == player.transform)
                return;

            _aimOrigin = player.AimOrigin;
            _aimHome = _aimOrigin.parent;
            _aimHomePosition = _aimOrigin.localPosition;
            _aimHomeRotation = _aimOrigin.localRotation;
            _aimOrigin.SetParent(marker, false);
            _aimOrigin.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private void ReturnAimOrigin()
        {
            if (_aimOrigin == null)
                return;

            _aimOrigin.SetParent(_aimHome, false);
            _aimOrigin.SetLocalPositionAndRotation(_aimHomePosition, _aimHomeRotation);
            _aimOrigin = null;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == childName)
                    return child;
            }
            return null;
        }

        private Transform VisualRoot()
        {
            if (_visualRoot == null)
            {
                _visualRoot = transform.Find(VisualName);
                if (_visualRoot == null)
                    _visualRoot = transform;
            }
            return _visualRoot;
        }

        private void SetPlaceholderVisible(bool visible)
        {
            Transform root = VisualRoot();
            foreach (string placeholder in PlaceholderNames)
            {
                Transform part = root.Find(placeholder);
                if (part == null)
                    continue;
                foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true))
                    r.enabled = visible;
            }
        }
    }
}
