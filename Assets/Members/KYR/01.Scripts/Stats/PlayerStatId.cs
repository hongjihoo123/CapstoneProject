namespace Members.KYR._01.Scripts.Stats
{
    public enum PlayerStatId
    {
        None = 0,                   // 없음 (루트 노드)

        //----------공격----------
        Damage,                     // 공격력
        AttackSpeed,                // 공격 속도
        ArmorPenetration,           // 물리 관통력
        MagicPenetration,           // 마법 관통력 (모든 속성 공통)
        Omnivamp,                   // 피해 흡혈
        SkillCooldownReduction,     // 스킬 쿨타임 감소

        FirePenetration,            // 화염 관통력
        WaterPenetration,           // 물 관통력
        WindPenetration,            // 바람 관통력
        ElectricPenetration,        // 전기 관통력
        EarthPenetration,           // 대지 관통력

        //----------방어----------
        MaxHp,                      // 최대 체력

        DamageReduction,            // 피해 감소
        IncomingDamageTaken,        // 받는 피해 배율
        HealReceived,               // 받는 회복 배율
        Tenacity,                   // 강인함

        ArmorDefense,               // 물리 방어력
        MagicDefense,               // 마법 방어력

        //----------이동----------
        MoveSpeed,                  // 이동 속도
        DashSpeed,                  // 대시 속도
        DashDuration,               // 대시 지속시간
    }
}