using System.Collections.Generic;
using System.IO;
using Assets.Members.HJH._02.Scripts.UI;
using Members.KYR._01.Scripts.Stats;
using UnityEditor;
using UnityEngine;

namespace HJH.EditorTools
{
    public static class ComboBuffBarBakeMenu
    {
        private const string PrefabPath = "Assets/Members/HJH/05.Data/Prefab/UI/ComboBuffBar.prefab";
        private const string IconFolder = "Assets/Members/HJH/UI/Icons/Stats";

        [MenuItem("HJH/UI/버프 바만 다시 굽기 (스탯 아이콘)")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[BuffBar] 플레이 중에는 굽지 않습니다.");
                return;
            }

            EnsureStatIconSet();
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var bar = root.GetComponent<ComboBuffBarView>();
                if (bar == null)
                {
                    Debug.LogError("[BuffBar] 프리팹에 ComboBuffBarView 가 없음: " + PrefabPath);
                    return;
                }
                bar.BakeLayout();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[BuffBar] 버프 바 다시 구움: " + PrefabPath);
            WriteLog("DONE " + PrefabPath);
        }

        public static StatIconSet EnsureStatIconSet()
        {
            var set = AssetDatabase.LoadAssetAtPath<StatIconSet>(ComboBuffBarView.StatIconSetPath);
            if (set != null)
                return set;

            AssetDatabase.ImportAsset(IconFolder, ImportAssetOptions.ImportRecursive);
            var list = new List<StatIconSet.Entry>
            {
                Entry(PlayerStatId.Damage, "stat_damage", new Color(1f, 0.55f, 0.35f)),
                Entry(PlayerStatId.MagicDamage, "stat_magic", new Color(0.55f, 0.75f, 1f)),
                Entry(PlayerStatId.AttackSpeed, "stat_attackspeed", new Color(1f, 0.88f, 0.35f)),
                Entry(PlayerStatId.MoveSpeed, "stat_movespeed", new Color(0.55f, 1f, 0.7f)),
                Entry(PlayerStatId.DashSpeed, "stat_dash", new Color(0.55f, 1f, 0.7f)),
                Entry(PlayerStatId.ArmorDefense, "stat_armor", new Color(0.85f, 0.75f, 0.55f)),
                Entry(PlayerStatId.MagicDefense, "stat_magicdefense", new Color(0.75f, 0.65f, 1f)),
                Entry(PlayerStatId.MaxHp, "stat_hp", new Color(1f, 0.45f, 0.5f)),
                Entry(PlayerStatId.IncomingDamageTaken, "stat_damagetaken", new Color(1f, 0.45f, 0.4f)),
                Entry(PlayerStatId.SkillCooldownReduction, "stat_cooldown", new Color(0.6f, 0.9f, 1f)),
            };

            set = ScriptableObject.CreateInstance<StatIconSet>();
            set.Set(list, Sprite("stat_generic"), Color.white);
            Directory.CreateDirectory(Path.GetDirectoryName(ComboBuffBarView.StatIconSetPath));
            AssetDatabase.CreateAsset(set, ComboBuffBarView.StatIconSetPath);
            AssetDatabase.SaveAssets();
            WriteLog("만듦 " + ComboBuffBarView.StatIconSetPath);
            return set;
        }

        private static StatIconSet.Entry Entry(PlayerStatId stat, string file, Color color) =>
            new() { stat = stat, icon = Sprite(file), color = color };

        private static Sprite Sprite(string file)
        {
            string path = $"{IconFolder}/{file}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                WriteLog("그림 없음 (Sprite 로 import 안 됨?) " + path);
            return sprite;
        }

        private static void WriteLog(string line)
        {
            Directory.CreateDirectory("Captures");
            File.AppendAllText("Captures/_buffbar_bake_log.txt", line + "\n");
        }
    }
}
