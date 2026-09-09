public static class CharacterSelectionContext
{
    public static CharacterDataSO Selected { get; private set; }

    public static void Select(CharacterDataSO data)
    {
        Selected = data;
    }

    public static void Clear()
    {
        Selected = null;
    }
}
