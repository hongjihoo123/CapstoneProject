using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.KYR._01.Scripts.temporary
{
    // Player 레이어가 범위에 들어오면 F 텍스처를 보여주고,
    // F를 누르면 등록된 프리팹 중 하나를 제단 위치에 소환한 뒤 제단을 내린다.
    public class Altar : MonoBehaviour
    {
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private GameObject[] rewards;
        [SerializeField] private Sprite promptSprite;
        [SerializeField, Min(0.1f)] private float detectRadius = 2.2f;
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.9f, 0f);
        [SerializeField, Min(0f)] private float sinkDistance = 3f;
        [SerializeField, Min(0.01f)] private float sinkDuration = 2.5f;

        private static readonly string[] Glyph =
        {
            "#####",
            "#    ",
            "#    ",
            "#### ",
            "#    ",
            "#    ",
            "#    ",
        };

        private SpriteRenderer _prompt;
        private int _playersInside;
        private bool _used;

        private void Awake()
        {
            if (playerLayer.value == 0)
                playerLayer = LayerMask.GetMask("Player");

            SphereCollider range = GetComponent<SphereCollider>();
            if (range == null)
                range = gameObject.AddComponent<SphereCollider>();

            range.isTrigger = true;
            range.radius = detectRadius;
            range.center = new Vector3(0f, 0.9f, 0f);

            _prompt = CreatePrompt();
            _prompt.enabled = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_used || !IsPlayer(other))
                return;

            _playersInside++;
            _prompt.enabled = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (_used || !IsPlayer(other))
                return;

            _playersInside = Mathf.Max(0, _playersInside - 1);
            if (_playersInside == 0)
                _prompt.enabled = false;
        }

        private void Update()
        {
            if (_used || _playersInside <= 0)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                Activate();
        }

        private void LateUpdate()
        {
            if (_prompt == null || !_prompt.enabled)
                return;

            Camera cam = Camera.main;
            if (cam != null)
                _prompt.transform.rotation = cam.transform.rotation;
        }

        private void OnValidate()
        {
            SphereCollider range = GetComponent<SphereCollider>();
            if (range == null)
                return;

            range.isTrigger = true;
            range.radius = detectRadius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.84f, 0.35f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + new Vector3(0f, 0.9f, 0f), detectRadius);
        }

        private void Activate()
        {
            GameObject prefab = PickReward();
            if (prefab == null)
            {
                Debug.LogWarning($"{name}: 소환할 프리팹이 없습니다.", this);
                return;
            }

            _used = true;
            _prompt.enabled = false;
            Instantiate(prefab, transform.position, prefab.transform.rotation);

            foreach (Collider collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;

            StartCoroutine(Sink());
        }

        private GameObject PickReward()
        {
            if (rewards == null || rewards.Length == 0)
                return null;

            int start = Random.Range(0, rewards.Length);
            for (int i = 0; i < rewards.Length; i++)
            {
                GameObject candidate = rewards[(start + i) % rewards.Length];
                if (candidate != null)
                    return candidate;
            }

            return null;
        }

        private IEnumerator Sink()
        {
            Vector3 start = transform.position;
            Vector3 end = start + Vector3.down * sinkDistance;
            float elapsed = 0f;

            while (elapsed < sinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / sinkDuration));
                transform.position = Vector3.LerpUnclamped(start, end, t);
                yield return null;
            }

            transform.position = end;
        }

        private bool IsPlayer(Collider other) =>
            other != null && (playerLayer.value & (1 << other.gameObject.layer)) != 0;

        private SpriteRenderer CreatePrompt()
        {
            var promptObject = new GameObject("F Prompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = promptOffset;
            promptObject.transform.localScale = Vector3.one * 0.7f;

            var renderer = promptObject.AddComponent<SpriteRenderer>();
            renderer.sprite = promptSprite != null ? promptSprite : BuildFSprite();
            renderer.sortingOrder = 20;
            return renderer;
        }

        private static Sprite BuildFSprite()
        {
            const int size = 128;
            var pixels = new Color32[size * size];

            var shadow = new Color32(0, 0, 0, 90);
            var fill = new Color32(245, 243, 236, 255);
            var border = new Color32(28, 28, 32, 255);
            var ink = new Color32(22, 22, 26, 255);

            DrawRoundRect(pixels, size, 16, 8, 112, 112, 22, shadow);
            DrawRoundRect(pixels, size, 14, 16, 114, 120, 22, border);
            DrawRoundRect(pixels, size, 20, 22, 108, 114, 16, fill);
            StampGlyph(pixels, size, ink);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "F",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "F";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void DrawRoundRect(Color32[] pixels, int size, int x0, int y0, int x1, int y1, int radius, Color32 color)
        {
            int r2 = radius * radius;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (x < 0 || y < 0 || x >= size || y >= size)
                        continue;

                    int cx = x < x0 + radius ? x0 + radius : (x > x1 - radius ? x1 - radius : x);
                    int cy = y < y0 + radius ? y0 + radius : (y > y1 - radius ? y1 - radius : y);
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy > r2)
                        continue;

                    int index = y * size + x;
                    pixels[index] = color.a == 255 ? color : Blend(pixels[index], color);
                }
            }
        }

        private static void StampGlyph(Color32[] pixels, int size, Color32 color)
        {
            const int scale = 10;
            int width = 5 * scale;
            int height = 7 * scale;
            int originX = (size - width) / 2;
            int originY = (size - height) / 2 + 4;

            for (int row = 0; row < Glyph.Length; row++)
            {
                string line = Glyph[row];
                int y = originY + (Glyph.Length - 1 - row) * scale;
                for (int col = 0; col < line.Length; col++)
                {
                    if (line[col] == ' ')
                        continue;

                    int x = originX + col * scale;
                    for (int py = 0; py < scale - 1; py++)
                    {
                        for (int px = 0; px < scale - 1; px++)
                        {
                            int sx = x + px;
                            int sy = y + py;
                            if (sx < 0 || sy < 0 || sx >= size || sy >= size)
                                continue;

                            pixels[sy * size + sx] = color;
                        }
                    }
                }
            }
        }

        private static Color32 Blend(Color32 dst, Color32 src)
        {
            float a = src.a / 255f;
            float inv = 1f - a;
            return new Color32(
                (byte)(src.r * a + dst.r * inv),
                (byte)(src.g * a + dst.g * inv),
                (byte)(src.b * a + dst.b * inv),
                (byte)Mathf.Clamp(src.a + dst.a * inv, 0f, 255f));
        }
    }
}
