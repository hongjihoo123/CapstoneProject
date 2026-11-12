using Members.JJH._02_Scripts.Agents.Enemies;
using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01.Scripts.Modules;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Health
{
    // 플레이어(PlayerHealth)·적(AgentHealth) 체력을 같은 모양(현재/최대)으로 읽어 주는 어댑터.
    // 다른 팀원 코드(KYR/JJH)는 이벤트도 최대 체력 프로퍼티도 통일이 안 돼 있어서, 읽는 법을 여기 한 곳에만 둠.
    public readonly struct HealthReader
    {
        private readonly PlayerHealth _player;
        private readonly AgentHealth _agent;
        private readonly AbstractEnemy _enemy;

        private HealthReader(PlayerHealth player, AgentHealth agent, AbstractEnemy enemy)
        {
            _player = player;
            _agent = agent;
            _enemy = enemy;
        }

        public bool IsValid => _player != null || _agent != null;
        public bool IsPlayer => _player != null;

        public float Current => _player != null ? Mathf.Max(0f, _player.CurrentHealth)
            : _agent != null ? Mathf.Max(0f, _agent.CurrentHealth) : 0f;

        public float Max => _player != null ? _player.MaxHealth
            : _enemy != null && _enemy.EnemyData != null ? _enemy.EnemyData.Health : 0f;

        public float Shield => _player != null ? Mathf.Max(0f, _player.CurrentShield) : 0f;

        // 아무 컴포넌트(맞은 콜라이더, 체력바 등)에서 시작해서 그 캐릭터의 체력을 찾음
        public static HealthReader For(Component from)
        {
            if (from == null)
                return default;

            ModuleOwner owner = from.GetComponentInParent<ModuleOwner>();
            Transform root = owner != null ? owner.transform : from.transform;

            AgentHealth agent = root.GetComponentInChildren<AgentHealth>(true);
            if (agent != null)
                return new HealthReader(null, agent, agent.GetComponentInParent<AbstractEnemy>());

            PlayerHealth player = root.GetComponentInChildren<PlayerHealth>(true);
            return player != null ? new HealthReader(player, null, null) : default;
        }

        public static bool TryGetMax(object target, out float max)
        {
            max = 0f;
            if (target is not Component component)
                return false;

            HealthReader reader = For(component);
            max = reader.Max;
            return reader.IsValid && max > 0f;
        }
    }
}
