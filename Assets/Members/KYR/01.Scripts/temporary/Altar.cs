using System.Collections;
using Members.KYR._01.Scripts.Currency;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.temporary
{
    public class Altar : MonoBehaviour
    {
        private static readonly Color AffordableColor = new Color(1f, 0.86f, 0.35f);
        private static readonly Color ShortColor = new Color(1f, 0.35f, 0.3f);

        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private bool receiveGold;
        [SerializeField, Min(0)] private int price;
        [SerializeField] private GameObject[] rewards;
        [SerializeField] private GameObject fkeyImage;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField, Min(0.1f)] private float detectRadius = 2.2f;
        [SerializeField, Min(0f)] private float sinkDistance = 3f;
        [SerializeField, Min(0.01f)] private float sinkDuration = 2.5f;

        private int _playersInside;
        private bool _used;

        private void Awake()
        {
            SphereCollider range = GetComponent<SphereCollider>();
            if (range == null)
                range = gameObject.AddComponent<SphereCollider>();

            range.isTrigger = true;
            range.radius = detectRadius;
            range.center = new Vector3(0f, 0.9f, 0f);

            SetPrompt(false);
            EnsurePriceLabel();
            FitPriceLabel();
        }

        private void OnEnable()
        {
            if (GameBootstrap.Currency != null)
                GameBootstrap.Currency.OnChanged += HandleGoldChanged;

            RefreshPrice();
        }

        private void OnDisable()
        {
            if (GameBootstrap.Currency != null)
                GameBootstrap.Currency.OnChanged -= HandleGoldChanged;
        }

        private void HandleGoldChanged(CurrencyType type, int _)
        {
            if (type == CurrencyType.RunGold)
                RefreshPrice();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_used || !IsPlayer(other))
                return;

            _playersInside++;
            SetPrompt(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_used || !IsPlayer(other))
                return;

            _playersInside = Mathf.Max(0, _playersInside - 1);
            if (_playersInside == 0)
                SetPrompt(false);
        }

        private void Update()
        {
            if (_used || _playersInside <= 0)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                Activate();
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

            if (!TryPay())
                return;

            _used = true;
            SetPrompt(false);
            RefreshPrice();
            Instantiate(prefab, transform.position, prefab.transform.rotation);

            foreach (Collider colorlider in GetComponentsInChildren<Collider>())
                GetComponent<Collider>().enabled = false;

            StartCoroutine(Sink());
        }

        private bool TryPay()
        {
            if (!receiveGold || price <= 0)
                return true;

            CurrencyService currency = GameBootstrap.Currency;
            return currency != null && currency.TrySpend(CurrencyType.RunGold, price);
        }

        private void EnsurePriceLabel()
        {
            if (!receiveGold || price <= 0 || priceLabel != null)
                return;

            Transform parent = fkeyImage != null ? fkeyImage.transform.parent : transform;
            var labelObject = new GameObject("Price", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);

            Canvas canvas = parent.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
                    | AdditionalCanvasShaderChannels.Normal
                    | AdditionalCanvasShaderChannels.Tangent;
            }

            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -0.55f);
            rect.sizeDelta = new Vector2(2f, 0.4f);

            var text = labelObject.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 0.35f;
            text.raycastTarget = false;
            priceLabel = text;
        }

        private void FitPriceLabel()
        {
            if (priceLabel == null)
                return;

            RectTransform canvasRect = priceLabel.canvas != null ? priceLabel.canvas.GetComponent<RectTransform>() : null;
            float canvasSize = canvasRect != null ? Mathf.Max(canvasRect.rect.width, canvasRect.sizeDelta.x) : 1f;
            if (canvasSize < 0.01f)
                canvasSize = 1f;

            RectTransform rect = priceLabel.rectTransform;
            if (rect.rect.width <= canvasSize * 4f && priceLabel.fontSize <= canvasSize)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -canvasSize * 0.58f);
            rect.sizeDelta = new Vector2(canvasSize * 1.6f, canvasSize * 0.4f);
            priceLabel.fontSize = canvasSize * 0.32f;
            priceLabel.alignment = TextAlignmentOptions.Center;
        }

        private void RefreshPrice()
        {
            if (priceLabel == null)
                return;

            bool show = receiveGold && price > 0 && !_used;
            priceLabel.gameObject.SetActive(show);
            if (!show)
                return;

            priceLabel.text = price.ToString();
            CurrencyService currency = GameBootstrap.Currency;
            bool canPay = currency == null || currency.CanAfford(CurrencyType.RunGold, price);
            priceLabel.color = canPay ? AffordableColor : ShortColor;
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

        private void SetPrompt(bool visible)
        {
            if (fkeyImage != null)
                fkeyImage.SetActive(visible);
        }
    }
}