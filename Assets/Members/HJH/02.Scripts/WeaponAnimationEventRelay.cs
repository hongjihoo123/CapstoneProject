using Members.KYR._01_Scripts.Modules;
using UnityEngine;

public class WeaponAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerWeapon playerWeapon;

    public void Anim_MuzzleFlash() => playerWeapon?.Anim_MuzzleFlash();
}