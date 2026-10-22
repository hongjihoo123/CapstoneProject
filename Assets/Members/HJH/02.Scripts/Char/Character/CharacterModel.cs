using Assets.Members.HJH._02.Scripts.Char.Visual;
using Members.KYR._01_Scripts;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Character
{
    // 캐릭터 바뀔 때 겉모습(모델)만 바꿔주는 거임.
    //  - CharacterData 에 모델이 있으면: 플레이어 Visual 밑에 모델 생성 + 캡슐(Body, FacingMarker) 숨김
    //  - 모델 없으면: 캡슐 그대로 보여줌 (아직 모델 없는 캐릭터용)
    // 모델 안에 "AimOrigin" 빈 오브젝트(총 뼈 밑)가 있으면 플레이어 AimOrigin 을 거기로 옮겨서 총구가 모션 따라 움직이게 함.
    // 애니메이션 재생은 같이 붙는 CharacterAnimator 가 함. 씬에 따로 안 붙여도 CharacterSwitcher 가 알아서 붙임
    public class CharacterModel : MonoBehaviour
    {
        private const string VisualName = "Visual";
        private static readonly string[] PlaceholderNames = { "Body", "FacingMarker" };

        private Transform _visualRoot;
        private GameObject _current;

        public Animator Animator { get; private set; }

        public static CharacterModel For(PlayerAgent player)
        {
            if (!player.TryGetComponent(out CharacterModel model))
                model = player.gameObject.AddComponent<CharacterModel>();
            if (!player.TryGetComponent(out CharacterAnimator _))
                player.gameObject.AddComponent<CharacterAnimator>();
            return model;
        }

        public void Show(CharacterData character)
        {
            ReturnAimOrigin();   // 모델 지우기 전에 AimOrigin 부터 원래 자리로 (안 그러면 같이 지워짐)
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

            // 모델 프리팹에 Animator 없으면 붙여줌. 이동은 코드가 하니까 루트모션은 끔
            Animator = _current.GetComponentInChildren<Animator>();
            if (Animator == null)
                Animator = _current.AddComponent<Animator>();
            Animator.applyRootMotion = false;
            if (character.AnimatorController != null)
                Animator.runtimeAnimatorController = character.AnimatorController;

            AttachAimOrigin();
        }

        // 모델 안에 "AimOrigin" 이라는 빈 오브젝트가 있으면(총/손 뼈 밑에 넣어두면 됨),
        // 플레이어 AimOrigin 을 거기로 옮김 -> 총 쏘는 위치가 애니메이션 따라 총이랑 같이 움직임.
        // 캐릭터 바꾸면 원래 자리(플레이어 밑)로 돌려놓음
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
