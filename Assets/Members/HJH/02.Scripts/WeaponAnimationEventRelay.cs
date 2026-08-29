using Members.KYR._01_Scripts.Modules;
using UnityEngine;
public class WeaponAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerWeapon playerWeapon;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip[] reloadingSound;
    public void Anim_MuzzleFlash()
    {
        playerWeapon?.Anim_MuzzleFlash();
        SoundManager.Instance.PlaySFX(fireSound);
    }
    public void Anim_SwordOverlapHit()
    {
        playerWeapon?.TriggerSkillOverlapHit();
    }
    public void Reloading(int i)
    {
        SoundManager.Instance.PlaySFX(reloadingSound[i]);
    }
}