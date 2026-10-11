using System.Collections.Generic;
using UnityEngine;

namespace RobotWeapons
{
    // Marks where a weapon's shots leave the model (gun barrel tip, staff head...).
    // Put it on an empty child of the hand/weapon bone so it follows the animation;
    // weapons look it up by id under their owner, e.g. HitscanGunData.muzzleIds.
    public class MuzzleSocket : MonoBehaviour
    {
        private static readonly List<MuzzleSocket> Active = new();

        [SerializeField, Tooltip("Weapon data refers to this id (e.g. Pistol_L, Pistol_R, Staff).")]
        private string id = "Muzzle";

        public string Id => id;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        // Active socket with this id somewhere under the owner (inactive character models are skipped).
        public static bool TryFind(IWeaponOwner owner, string id, out Transform socket)
        {
            socket = null;
            if (string.IsNullOrEmpty(id) || owner is not Component component)
                return false;

            Transform root = component.transform;
            foreach (MuzzleSocket candidate in Active)
            {
                if (candidate.id == id && candidate.transform.IsChildOf(root))
                {
                    socket = candidate.transform;
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, 0.06f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.3f);
        }
#endif
    }
}
