namespace Members.KYR._01.Scripts.Currency
{
    public interface ISaveRepository
    {
        CurrencySaveData Load();
        void Save(CurrencySaveData data);
    }
}
