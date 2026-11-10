using System.Collections;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.SkillSwap;
using Members.KYR._01.Scripts.Currency;
using Members.KYR._01.Scripts.Player;
using Members.KYR._01.Scripts.temporary;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.KYR._01.Scripts.Store
{
    public class Shop : MonoBehaviour
    {
        public static readonly Color AffordableColor = new Color(1f, 0.86f, 0.35f);
        public static readonly Color ShortColor = new Color(1f, 0.35f, 0.3f);

        [Header("진열")]
        [SerializeField] private ShopSlot slotPrefab;
        [SerializeField] private SkillPickup pickupPrefab;
        [SerializeField, Min(1)] private int slotCount = 3;
        [SerializeField] private SkillData[] skillPool;
        [SerializeField] private Transform slotAnchor;
        [SerializeField] private Transform[] slotPoints;
        [SerializeField, Min(0f)] private float slotSpacing = 2.2f;
        [SerializeField, Min(0.1f)] private float slotDetectRadius = 1.05f;

        [Header("가격")]
        [SerializeField, Min(0)] private int basePrice = 30;
        [SerializeField, Min(0f)] private float priceVariance = 0.1f;

        [Header("리롤")]
        [SerializeField, Min(0)] private int baseRerollCost = 10;
        [SerializeField, Min(1f)] private float rerollMultiplier = 2f;
        [SerializeField, Min(0.1f)] private float rerollRadius = 1.6f;
        [SerializeField] private TMP_Text rerollLabel;

        [Header("제단")]
        [SerializeField] private Altar altarPrefab;
        [SerializeField] private Transform altarPoint;

        private readonly List<ShopSlot> _slots = new();
        private readonly List<SkillData> _lastOffer = new();
        private PlayerAgent _player;
        private Altar _altar;
        private int _rerollCost;
        private bool _warnedEmpty;
        private int _shownCost = int.MinValue;
        private bool _shownHint;
        private bool _shownAfford = true;

        private void Awake() => EnsureRerollLabel();

        private void OnEnable()
        {
            if (GameBootstrap.Currency != null)
                GameBootstrap.Currency.OnChanged += HandleGoldChanged;
        }

        private void Start()
        {
            _rerollCost = Mathf.Max(0, baseRerollCost);
            Roll();
            SpawnAltar();
        }

        private void OnDisable()
        {
            if (GameBootstrap.Currency != null)
                GameBootstrap.Currency.OnChanged -= HandleGoldChanged;
        }

        private void Update()
        {
            CachePlayer();
            ShopSlot nearest = NearestSlot();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame && !InAltarRange())
            {
                if (nearest != null)
                    Buy(nearest);
                else if (InRerollRange())
                    TryReroll();
            }

            RefreshPrompts(nearest);
            RefreshRerollLabel(InRerollRange(), nearest != null);
        }

        private void LateUpdate()
        {
            if (rerollLabel == null || Camera.main == null)
                return;

            rerollLabel.transform.rotation = Camera.main.transform.rotation;
        }

        private void HandleGoldChanged(CurrencyType type, int _)
        {
            if (type != CurrencyType.RunGold)
                return;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null)
                    _slots[i].RefreshLabel();
            }

            _shownCost = int.MinValue;
        }

        private void Buy(ShopSlot slot)
        {
            if (slot == null || slot.Skill == null || pickupPrefab == null || !CanAfford(slot.Price))
                return;
            if (!TrySpend(slot.Price))
                return;

            SkillData skill = slot.Skill;
            Vector3 position = slot.transform.position;
            Quaternion rotation = slot.transform.rotation;
            RemoveSlot(slot);
            
            SkillPickup pickup = Instantiate(pickupPrefab, position, rotation);
            pickup.gameObject.SetActive(false);
            pickup.Swap(skill, default, out _);
            StartCoroutine(EnableNextFrame(pickup.gameObject));
        }

        private static IEnumerator EnableNextFrame(GameObject pickup)
        {
            yield return null;
            if (pickup != null)
                pickup.SetActive(true);
        }

        private void SpawnAltar()
        {
            if (altarPrefab == null || _altar != null)
                return;

            Transform point = altarPoint != null ? altarPoint : transform;
            _altar = Instantiate(altarPrefab, point.position, point.rotation, transform);
        }

        private bool InAltarRange()
        {
            if (_altar == null || _player == null)
                return false;

            SphereCollider range = _altar.GetComponent<SphereCollider>();
            float radius = range != null ? range.radius : 2.5f;
            Vector3 scale = _altar.transform.lossyScale;
            radius *= Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            return (Flat(_player.transform.position) - Flat(_altar.transform.position)).sqrMagnitude <= radius * radius;
        }

        private void TryReroll()
        {
            if (slotPrefab == null || !TrySpend(_rerollCost))
                return;

            _rerollCost = NextRerollCost(_rerollCost);
            _shownCost = int.MinValue;
            Roll();
        }

        private void Roll()
        {
            if (slotPrefab == null)
            {
                Debug.LogWarning($"{name}: 슬롯 프리팹이 없습니다.", this);
                return;
            }

            ClearSlots();

            int count = Mathf.Max(1, slotCount);
            List<SkillData> drawn = Draw(count);
            _lastOffer.Clear();
            _lastOffer.AddRange(drawn);

            if (drawn.Count == 0)
            {
                if (!_warnedEmpty)
                {
                    _warnedEmpty = true;
                    Debug.LogWarning($"{name}: 스킬 풀이 비어 있습니다.", this);
                }

                return;
            }

            for (int i = 0; i < drawn.Count; i++)
            {
                ShopSlot slot = Instantiate(slotPrefab, SlotPosition(i, drawn.Count), Quaternion.identity, transform);
                slot.Show(drawn[i], RollPrice());
                _slots.Add(slot);
            }
        }

        private List<SkillData> Draw(int count)
        {
            List<SkillData> pool = UniquePool();
            var fresh = new List<SkillData>(pool.Count);
            foreach (SkillData skill in pool)
            {
                if (!_lastOffer.Contains(skill))
                    fresh.Add(skill);
            }

            List<SkillData> bag = fresh.Count >= count ? fresh : pool;
            int take = Mathf.Min(count, bag.Count);
            for (int i = 0; i < take; i++)
            {
                int swap = Random.Range(i, bag.Count);
                (bag[i], bag[swap]) = (bag[swap], bag[i]);
            }

            if (take < bag.Count)
                bag.RemoveRange(take, bag.Count - take);
            return bag;
        }

        private List<SkillData> UniquePool()
        {
            var pool = new List<SkillData>();
            if (skillPool == null)
                return pool;

            foreach (SkillData skill in skillPool)
            {
                if (skill != null && !pool.Contains(skill))
                    pool.Add(skill);
            }

            return pool;
        }

        private int RollPrice()
        {
            float spread = Mathf.Max(0f, priceVariance);
            float min = Mathf.Max(0f, 1f - spread);
            float max = 1f + spread;
            float scale = Mathf.Approximately(min, max) ? min : Random.Range(min, max);
            int price = Mathf.RoundToInt(basePrice * scale);
            return basePrice > 0 ? Mathf.Max(1, price) : Mathf.Max(0, price);
        }

        private int NextRerollCost(int paid)
        {
            if (paid <= 0)
                return 0;

            float next = paid * Mathf.Max(1f, rerollMultiplier);
            if (float.IsInfinity(next) || next >= int.MaxValue)
                return int.MaxValue;

            return Mathf.Max(paid, Mathf.RoundToInt(next));
        }

        private void ClearSlots()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null)
                    Destroy(_slots[i].gameObject);
            }

            _slots.Clear();
        }

        private void RemoveSlot(ShopSlot slot)
        {
            _slots.Remove(slot);
            if (slot != null)
                Destroy(slot.gameObject);
        }

        private Vector3 SlotPosition(int index, int count)
        {
            if (slotPoints != null && index < slotPoints.Length && slotPoints[index] != null)
                return slotPoints[index].position;

            Transform origin = slotAnchor != null ? slotAnchor : transform;
            float span = Mathf.Max(0, count - 1) * slotSpacing;
            return origin.position + origin.right * (index * slotSpacing - span * 0.5f);
        }

        private void RefreshPrompts(ShopSlot nearest)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                ShopSlot slot = _slots[i];
                if (slot == null)
                    continue;

                bool here = slot == nearest;
                string prompt = here && CanAfford(slot.Price) ? "<color=#FFD36B>F</color>  구매" : null;// 나중에 UI 똑바로 바꿀것.
                slot.SetHighlighted(here, prompt);
            }
        }

        private void RefreshRerollLabel(bool near, bool slotClaimedF)
        {
            if (rerollLabel == null)
                return;

            bool showHint = near && !slotClaimedF;
            bool afford = CanAfford(_rerollCost);
            if (_shownCost == _rerollCost && _shownHint == showHint && _shownAfford == afford)
                return;

            _shownCost = _rerollCost;
            _shownHint = showHint;
            _shownAfford = afford;

            string hex = ColorUtility.ToHtmlStringRGB(afford ? AffordableColor : ShortColor);
            string hint = showHint ? "<color=#FFD36B>F</color>  리롤\n" : string.Empty; // 나중에 UI 똑바로 바꿀것.
            rerollLabel.text = $"{hint}<size=80%><color=#{hex}>{_rerollCost}</color></size>";
        }

        private void CachePlayer()
        {
            if (_player != null)
                return;

            _player = FindFirstObjectByType<PlayerAgent>();
        }

        private ShopSlot NearestSlot()
        {
            if (_player == null)
                return null;

            ShopSlot best = null;
            float bestSqr = slotDetectRadius * slotDetectRadius;
            Vector3 origin = Flat(_player.transform.position);
            for (int i = 0; i < _slots.Count; i++)
            {
                ShopSlot slot = _slots[i];
                if (slot == null || slot.Skill == null)
                    continue;

                float sqr = (Flat(slot.transform.position) - origin).sqrMagnitude;
                if (sqr > bestSqr)
                    continue;

                bestSqr = sqr;
                best = slot;
            }

            return best;
        }

        private bool InRerollRange()
        {
            if (_player == null)
                return false;

            return (Flat(_player.transform.position) - Flat(transform.position)).sqrMagnitude
                <= rerollRadius * rerollRadius;
        }

        private static Vector3 Flat(Vector3 position)
        {
            position.y = 0f;
            return position;
        }

        private static bool CanAfford(int price)
        {
            if (price <= 0)
                return true;

            CurrencyService currency = GameBootstrap.Currency;
            return currency != null && currency.CanAfford(CurrencyType.RunGold, price);
        }

        private bool TrySpend(int price)
        {
            if (price <= 0)
                return true;

            CurrencyService currency = GameBootstrap.Currency;
            return currency != null && currency.TrySpend(CurrencyType.RunGold, price);
        }

        private void EnsureRerollLabel()
        {
            if (rerollLabel != null)
                return;

            TMP_Text sample = slotPrefab != null ? slotPrefab.GetComponentInChildren<TMP_Text>(true) : null;
            var labelObject = new GameObject("RerollLabel");
            labelObject.transform.SetParent(transform, false);

            var text = labelObject.AddComponent<TextMeshPro>();
            labelObject.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            if (sample != null && sample.font != null)
            {
                text.font = sample.font;
                text.fontSharedMaterial = sample.fontSharedMaterial;
            }
            else if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 3.2f;
            text.richText = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(8f, 4f);
            rerollLabel = text;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.84f, 0.35f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, rerollRadius);

            Gizmos.color = new Color(0.45f, 0.85f, 1f, 0.9f);
            int count = Mathf.Max(1, slotCount);
            for (int i = 0; i < count; i++)
                Gizmos.DrawWireSphere(SlotPosition(i, count) + Vector3.up * 0.9f, slotDetectRadius);

            if (altarPoint == null && altarPrefab == null)
                return;

            Vector3 altarPosition = altarPoint != null ? altarPoint.position : transform.position;
            Gizmos.color = new Color(0.75f, 0.45f, 1f, 0.9f);
            Gizmos.DrawWireSphere(altarPosition + Vector3.up * 0.9f, 2.5f);
        }
    }
}
