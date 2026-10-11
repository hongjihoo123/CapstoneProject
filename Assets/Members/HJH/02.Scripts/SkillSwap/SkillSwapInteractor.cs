using System;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.SkillSwap
{
    // Shape of Dreams style skill swap. Stand next to a SkillPickup and press F, then press Q or E:
    // that slot's skill and the pickup's skill trade places. F again or walking away cancels.
    // While choosing, Q/E are borrowed from the skill module so they do not cast.
    // Cooldowns travel with the skill, and a freshly swapped-in skill is locked for swapLockout seconds.
    public class SkillSwapInteractor : MonoBehaviour, ISkillInputInterceptor
    {
        [SerializeField] private PlayerAgent player;
        [SerializeField] private float interactRadius = 2f;
        [SerializeField] private Key interactKey = Key.F;
        [SerializeField, Tooltip("Seconds a skill cannot be cast right after it was swapped in.")]
        private float swapLockout = 1f;

        private SkillStateModule _skills;
        private SkillPickup _nearest;
        private SkillPickup _choosing;

        public PlayerAgent Player => player;
        public SkillStateModule Skills => _skills;
        public SkillPickup Choosing => _choosing;
        public string InteractKeyLabel => interactKey.ToString();

        // Fired with the pickup being chosen for, or null when choosing ends.
        public event Action<SkillPickup> ChoosingChanged;
        // slot, skill that left the slot (now on the ground), skill that entered the slot
        public event Action<SkillSlotId, SkillData, SkillData> Swapped;

        private void Awake()
        {
            if (player == null)
                player = GetComponentInParent<PlayerAgent>();
        }

        private void Start()
        {
            _skills = player != null ? player.SkillFsm : null;
            Debug.Assert(_skills != null, $"{name}: SkillSwapInteractor needs a PlayerAgent with a SkillStateModule.");
            _skills?.AddInputInterceptor(this);
        }

        private void OnDestroy() => _skills?.RemoveInputInterceptor(this);

        private void Update()
        {
            if (_skills == null)
                return;

            SkillPickup nearest = FindNearest();
            if (nearest != _nearest)
            {
                if (_nearest != null)
                    _nearest.SetHighlighted(false, null);
                _nearest = nearest;
            }

            if (_choosing != null && _choosing != _nearest)
                SetChoosing(null);

            Keyboard keyboard = Keyboard.current;
            if (_nearest != null && keyboard != null && keyboard[interactKey].wasPressedThisFrame)
                SetChoosing(_choosing == null ? _nearest : null);

            if (_nearest != null)
                _nearest.SetHighlighted(true, _choosing != null ? null : $"<color=#FFD36B>{InteractKeyLabel}</color>  교체");
        }

        public bool Intercepts(SkillSlotId slot) => _choosing != null && SkillSlots.IsSwappable(slot);

        public void OnInterceptedPress(SkillSlotId slot)
        {
            SkillPickup pickup = _choosing;
            SetChoosing(null);

            SkillData outgoing = _skills.GetSkill(slot);
            SkillCooldownState outgoingCooldown = _skills.GetCooldownState(slot);
            SkillData incoming = pickup.Swap(outgoing, outgoingCooldown, out SkillCooldownState incomingCooldown);
            _skills.Equip(slot, incoming, incomingCooldown, swapLockout);

            Debug.Log($"[스킬 교체] {KeyLabel(slot)}: {Describe(outgoing)} → {Describe(incoming)}");
            Swapped?.Invoke(slot, outgoing, incoming);
        }

        public string KeyLabel(SkillSlotId slot)
        {
            string label = player != null ? player.GetSkillKeyLabel(slot) : null;
            return string.IsNullOrEmpty(label) ? slot.ToString() : label;
        }

        private void SetChoosing(SkillPickup pickup)
        {
            if (_choosing == pickup)
                return;

            _choosing = pickup;
            ChoosingChanged?.Invoke(pickup);
        }

        private SkillPickup FindNearest()
        {
            Vector3 origin = player.transform.position;
            SkillPickup best = null;
            float bestSqr = interactRadius * interactRadius;

            foreach (SkillPickup pickup in SkillPickup.All)
            {
                if (pickup.Skill == null)
                    continue;

                Vector3 delta = pickup.transform.position - origin;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = pickup;
                }
            }

            return best;
        }

        private static string Describe(SkillData data) =>
            data != null ? $"{data.DisplayName}({SkillPickup.ElementLabel(data)})" : "빈 슬롯";
    }
}
