using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.SkillSwap
{
    // A skill lying in the stage (Shape of Dreams "memory"): the skill icon in a soft element-colored glow,
    // one flat billboard that always faces the camera, plus a small point light on the floor. SkillSwapInteractor trades it with one of the player's Q/E skills,
    // so after a swap it holds the player's old skill (and that skill's cooldown keeps running here).
    public class SkillPickup : MonoBehaviour
    {
        private static readonly List<SkillPickup> Active = new();
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [SerializeField] private SkillData skill;
        [SerializeField] private ElementPalette palette;

        [Header("Visual")]
        [SerializeField, Tooltip("Faces the camera every frame (glow, icon).")]
        private Transform billboard;
        [SerializeField] private Renderer icon;
        [SerializeField] private Renderer glow;
        [SerializeField] private TMP_Text label;
        [SerializeField, Tooltip("Optional point light tinted with the element.")] private Light glowLight;
        [SerializeField] private float glowIntensity = 1f;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float highlightScale = 1.18f;

        private MaterialPropertyBlock _block;
        private Vector3 _billboardRest;
        private Vector3 _billboardScale;
        private float _scale = 1f;
        private bool _highlighted;
        private string _prompt;

        public static IReadOnlyList<SkillPickup> All => Active;
        public SkillData Skill => skill;
        public SkillCooldownState Cooldown { get; private set; }

        private void Awake()
        {
            if (billboard != null)
            {
                _billboardRest = billboard.localPosition;
                _billboardScale = billboard.localScale;
            }

            if (label != null)
                label.alpha = 0f;
        }

        private void OnEnable()
        {
            Active.Add(this);
            Refresh();
        }

        private void OnDisable() => Active.Remove(this);

        // Puts the incoming skill (with its cooldown) on the ground and hands back what was here.
        // An empty incoming skill (the slot had nothing) uses the pickup up.
        public SkillData Swap(SkillData incoming, SkillCooldownState incomingCooldown, out SkillCooldownState outgoingCooldown)
        {
            SkillData outgoing = skill;
            outgoingCooldown = Cooldown;
            skill = incoming;
            Cooldown = incomingCooldown;
            _prompt = null;
            Refresh();

            if (skill == null)
                gameObject.SetActive(false);

            return outgoing;
        }

        // The label only shows while highlighted with a prompt (Shape of Dreams keeps the floor quiet);
        // a null prompt hides it, e.g. while the swap box is open over the orb.
        public void SetHighlighted(bool highlighted, string prompt)
        {
            _highlighted = highlighted;
            if (_prompt == prompt)
                return;

            _prompt = prompt;
            RefreshLabel();
        }

        public static string ElementLabel(SkillData data) =>
            data != null && data.TryGetElement(out ElementType element)
                ? $"{ElementComboBook.KoreanName(element)} 속성"
                : "무속성";

        public Color ElementColor()
        {
            ElementPalette colors = palette != null ? palette : ElementPalette.Default;
            if (skill != null && skill.TryGetElement(out ElementType element)
                && colors != null && colors.TryGet(element, out ElementPalette.Entry entry))
                return entry.color;

            return colors != null ? colors.NeutralColor : Color.gray;
        }

        private void Refresh()
        {
            RefreshLabel();

            Color color = ElementColor();
            Tint(glow, color * glowIntensity);
            if (glowLight != null)
                glowLight.color = color;

            if (icon != null)
            {
                _block ??= new MaterialPropertyBlock();
                icon.GetPropertyBlock(_block);
                _block.SetColor(TintId, Color.white);
                Texture texture = skill != null && skill.Icon != null ? skill.Icon.texture : null;
                if (texture != null)
                    _block.SetTexture(MainTexId, texture);
                icon.SetPropertyBlock(_block);
                icon.enabled = texture != null;
                if (texture != null)
                    FitIconToSprite(skill.Icon);
            }
        }

        // 아이콘을 사각형 판 대신 "원판"에 그려서 구슬처럼 동그랗게 보이게 함. 텍스처는 안 건드리고 그리는 모양만 바꾸는 거임.
        //  - 원판 = 가운데 점 1개 + 둘레 점 여러 개로 만든 부채꼴 조각들 (피자 조각 모양 삼각형들)
        //  - 둘레 점마다 "그림의 어느 부분을 찍어 올지(UV)"를 원 모양으로 정해줘서, 그림의 원 바깥(사각형 모서리)은 아예 안 그려짐
        //  - 스프라이트가 큰 이미지 일부만 쓰는 경우(JJH 아이콘: 1920x1080 중 가운데)도 그 영역 안에서만 찍어옴
        //  - 원판 메쉬는 구슬마다 따로 만들어서 다른 구슬엔 영향 없음
        [SerializeField, Range(0.3f, 0.5f), Tooltip("그림에서 잘라 쓸 원의 반지름 (0.5 = 사각형에 딱 맞는 원)")]
        private float iconCircle = 0.47f;

        private const int CircleSegments = 48;
        private Mesh _disc;

        private void FitIconToSprite(Sprite sprite)
        {
            if (!icon.TryGetComponent(out MeshFilter filter))
                return;

            Rect r = sprite.textureRect;
            float w = sprite.texture.width;
            float h = sprite.texture.height;
            // 스프라이트 영역의 가운데와 크기 (UV 0~1 기준)
            var uvCenter = new Vector2(r.center.x / w, r.center.y / h);
            var uvSize = new Vector2(r.width / w, r.height / h);

            var vertices = new Vector3[CircleSegments + 1];
            var uvs = new Vector2[CircleSegments + 1];
            var colors = new Color[CircleSegments + 1];
            var triangles = new int[CircleSegments * 3];

            vertices[0] = Vector3.zero;          // 가운데
            uvs[0] = uvCenter;
            colors[0] = Color.white;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices[i + 1] = new Vector3(dir.x, dir.y, 0f) * 0.5f;                       // 판 크기는 기존 사각형(1x1)과 같게
                uvs[i + 1] = uvCenter + Vector2.Scale(dir * iconCircle, uvSize);           // 그림에서 원 모양으로 찍어옴
                colors[i + 1] = Color.white;                                                // 이펙트 셰이더가 정점 색을 곱해서 흰색 필요

                // 카메라 쪽(-Z)에서 앞면이 보이게 시계방향으로 이음: 가운데 -> 다음 점 -> 이번 점
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % CircleSegments + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            if (_disc == null)
                _disc = new Mesh { name = "PickupIconDisc" };
            _disc.Clear();
            _disc.vertices = vertices;
            _disc.uv = uvs;
            _disc.colors = colors;
            _disc.triangles = triangles;
            _disc.RecalculateBounds();
            filter.sharedMesh = _disc;
        }

        private void OnDestroy()
        {
            if (_disc != null)
                Destroy(_disc);
        }

        private void Tint(Renderer target, Color color)
        {
            if (target == null)
                return;

            _block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(_block);
            _block.SetColor(TintId, color);
            target.SetPropertyBlock(_block);
        }

        private void RefreshLabel()
        {
            if (label == null)
                return;

            if (skill == null)
            {
                label.text = string.Empty;
                return;
            }

            // Name only; the element is already the orb's color and the details live in the swap box.
            string text = skill.DisplayName;
            if (!string.IsNullOrEmpty(_prompt))
                text += $"\n<size=68%><color=#C9CCD6>{_prompt}</color></size>";
            label.text = text;
        }

        private void LateUpdate()
        {
            float target = _highlighted ? highlightScale : 1f;
            _scale = Mathf.MoveTowards(_scale, target, Time.deltaTime * 2f);

            if (label != null)
            {
                bool show = _highlighted && !string.IsNullOrEmpty(_prompt);
                label.alpha = Mathf.MoveTowards(label.alpha, show ? 1f : 0f, Time.deltaTime * 6f);
            }

            Camera cam = Camera.main;

            if (billboard != null)
            {
                billboard.localPosition = _billboardRest + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.03f;
                billboard.localScale = _billboardScale * (_scale * pulse);
                if (cam != null)
                    billboard.rotation = cam.transform.rotation;
            }

            // Billboard: the label faces the top-down camera.
            if (label != null && cam != null)
                label.transform.rotation = cam.transform.rotation;
        }
    }
}
