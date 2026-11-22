namespace Members.KYR._01.Scripts.Enchant.Data
{
    public enum EnchantNodeKind
    {
        Entry = 0,      // 트리 시작점. 선행 없이 배울 수 있고, 선행을 연결할 수 없다.
        Stat = 1,
        Ability = 2,
        Key = 3,
    }
}
