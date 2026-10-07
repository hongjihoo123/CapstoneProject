using Members.KYR._01_Scripts;
using RobotWeapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // Adds placeholder MuzzleSockets to the player in the open scene so shots leave from a gun position.
    // Once a real model exists, drag each socket under the matching hand/weapon bone and move it to the barrel tip.
    public static class MuzzleSocketSetup
    {
        private const string RuneWeaponPath = "Assets/Members/HJH/Data/Weapon/RuneMage_Bolt.asset";

        private static readonly (string id, Vector3 localPosition)[] Defaults =
        {
            ("Pistol_L", new Vector3(-0.3f, 1.1f, 0.55f)),
            ("Pistol_R", new Vector3(0.3f, 1.1f, 0.55f)),
            ("Staff", new Vector3(0.35f, 1.45f, 0.45f)),
        };

        [MenuItem("HJH/Add Muzzle Sockets To Player")]
        public static void AddToOpenScene()
        {
            PlayerAgent player = Object.FindFirstObjectByType<PlayerAgent>();
            if (player == null)
            {
                Debug.LogWarning("[MuzzleSocketSetup] No PlayerAgent in the open scene.");
                return;
            }

            Transform parent = player.transform.Find("Visual");
            if (parent == null)
                parent = player.transform;

            Transform group = parent.Find("Muzzles");
            if (group == null)
            {
                group = new GameObject("Muzzles").transform;
                group.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(group.gameObject, "Add Muzzle Sockets");
            }

            int added = 0;
            foreach ((string id, Vector3 localPosition) in Defaults)
            {
                if (HasSocket(player, id))
                    continue;

                var go = new GameObject("Muzzle_" + id);
                go.layer = player.gameObject.layer;
                go.transform.SetParent(group, false);
                // Placed in player space so the default spot is right even if Visual is offset/scaled.
                go.transform.position = player.transform.TransformPoint(localPosition);
                go.transform.rotation = player.transform.rotation;

                var socket = go.AddComponent<MuzzleSocket>();
                var so = new SerializedObject(socket);
                so.FindProperty("id").stringValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();

                Undo.RegisterCreatedObjectUndo(go, "Add Muzzle Sockets");
                added++;
            }

            FixRuneMageMuzzle();
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log($"[MuzzleSocketSetup] {added} muzzle socket(s) added under {group.name}. Save the scene.");
        }

        private static bool HasSocket(PlayerAgent player, string id)
        {
            foreach (MuzzleSocket socket in player.GetComponentsInChildren<MuzzleSocket>(true))
            {
                if (socket.Id == id)
                    return true;
            }

            return false;
        }

        // The rune mage fires from one staff, not the gunner's two pistols.
        private static void FixRuneMageMuzzle()
        {
            var rune = AssetDatabase.LoadAssetAtPath<HitscanGunData>(RuneWeaponPath);
            if (rune == null || (rune.muzzleIds.Length == 1 && rune.muzzleIds[0] == "Staff"))
                return;

            rune.muzzleIds = new[] { "Staff" };
            EditorUtility.SetDirty(rune);
            AssetDatabase.SaveAssets();
        }
    }
}
