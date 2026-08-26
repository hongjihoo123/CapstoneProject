using RobotWeapons;

public static class CharacterSelectionContext
{
    public static WeaponData SelectedWeaponData { get; private set; }

    public static void Select(WeaponData data)
    {
        SelectedWeaponData = data;
    }

    public static void Clear()
    {
        SelectedWeaponData = null;
    }
}