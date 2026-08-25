using UnityEngine;

public class SoundManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
public enum SoundID
{
    // Weapon
    Weapon_Gunshot, Weapon_Reload, Weapon_LaserCharge,
    // Player
    Player_Footstep, Player_JumpLand, Player_Hurt,
    // Enemy
    Enemy_Attack, Enemy_Death, Enemy_Alert,
    // UI
    UI_Click, UI_Confirm,
}