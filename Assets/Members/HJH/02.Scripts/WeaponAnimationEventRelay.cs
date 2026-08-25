using Members.KYR._01_Scripts.Modules;
using UnityEngine;

public class WeaponAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerWeapon playerWeapon;
    [SerializeField] private AudioClip fireSound;

    public void Anim_MuzzleFlash()
    {
        playerWeapon?.Anim_MuzzleFlash();
    }
}