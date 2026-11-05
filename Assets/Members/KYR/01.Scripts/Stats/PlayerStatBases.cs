namespace Members.KYR._01.Scripts.Stats
{
    public struct PlayerStatBases
    {
        //----------공격----------
        public float Damage;                    // 공격력
        public float AttackSpeed;               // 공격 속도
        public float ArmorPenetration;          // 물리 관통력
        public float MagicPenetration;          // 마법 관통력
        public float Omnivamp;                  // 피해 흡혈
        public float SkillCooldownReduction;    // 스킬 쿨타임 감소
        public float FirePenetration;           // 화염 관통력
        public float WaterPenetration;          // 물 관통력
        public float WindPenetration;           // 바람 관통력
        public float ElectricPenetration;       // 전기 관통력
        public float EarthPenetration;          // 대지 관통력

        //----------방어----------
        public float MaxHp;                     // 최대 체력
        public float DamageReduction;           // 피해 감소
        public float IncomingDamageTaken;       // 받는 피해 배율
        public float HealReceived;              // 받는 회복 배율
        public float Tenacity;                  // 강인함
        public float ArmorDefense;              // 물리 방어력
        public float MagicDefense;              // 마법 방어력

        //----------이동----------
        public float MoveSpeed;                 // 이동 속도
        public float DashSpeed;                 // 대시 속도
        public float DashDuration;              // 대시 지속시간
    }
}