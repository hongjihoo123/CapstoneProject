using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerStatsModule : Module
    {
        [Header("Survival")]
        [SerializeField] private float maxHp = 100f;

        [Header("Mobility")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 7.5f;
        [SerializeField] private float crouchSpeed = 2.2f;

        [Header("Combat")]
        [SerializeField] private float damage = 1f;

        public PlayerStatTree Tree { get; private set; }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            Tree = new PlayerStatTree(maxHp, walkSpeed, runSpeed, crouchSpeed, damage);
        }

        public float Get(PlayerStatId id)
        {
            return Tree.Get(id);
        }

        public void AddModifier(PlayerStatId id, StatModifier modifier)
        {
            Tree.AddModifier(id, modifier);
        }

        public void RemoveModifiers(object source)
        {
            Tree.RemoveModifiers(source);
        }

        [ContextMenu("Log Stats")]
        private void LogStats()
        {
            if (Tree == null)
            {
                Debug.Log($"{name} Stats tree is not initialized.", this);
                return;
            }

            Debug.Log(
                $"{name} MaxHp={Get(PlayerStatId.MaxHp):0.##} " +
                $"Walk={Get(PlayerStatId.WalkSpeed):0.##} " +
                $"Run={Get(PlayerStatId.RunSpeed):0.##} " +
                $"Crouch={Get(PlayerStatId.CrouchSpeed):0.##} " +
                $"Damage={Get(PlayerStatId.Damage):0.##}",
                this);
        }
    }
}
