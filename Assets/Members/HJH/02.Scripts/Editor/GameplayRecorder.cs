using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // 실제 플레이 화면 녹화 도우미 (에디터 전용, 보여주기용). 녹화 자체는 GameplayCapture 컴포넌트가 함
    //  - 브리지 명령: rec start <경로> / rec stop / tp (정글로 이동) / equip <번호> (Q·E 에 스킬 두 개씩)
    public static class GameplayRecorder
    {
        private static GameplayCapture _active;

        public static string Begin(string path)
        {
            if (!EditorApplication.isPlaying)
                return "rec: 플레이 모드가 아님";
            if (_active != null)
                return "rec: 이미 녹화 중";
            _active = GameplayCapture.Begin(path);
            return $"rec start -> {_active.FilePath}";
        }

        public static string End()
        {
            if (_active == null)
                return "rec: 녹화 중 아님";
            string result = $"rec stop: {_active.Frames} 프레임 ({_active.Frames / GameplayCapture.FramesPerSecond:0.0}초) -> {_active.FilePath}";
            Object.Destroy(_active.gameObject);
            _active = null;
            return result;
        }

        // 플레이어를 정글 맵(물가)으로 옮김. 시작 자리가 회색 테스트 바닥이라 맵이 보이게
        public static string TeleportToJungle()
        {
            if (!EditorApplication.isPlaying)
                return "tp: 플레이 모드가 아님";
            var player = Object.FindAnyObjectByType<global::Members.KYR._01.Scripts.Player.PlayerAgent>();
            Renderer water = null;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (r.sharedMaterial != null && r.sharedMaterial.shader.name.Contains("ShallowWater"))
                    water = r;
            if (player == null || water == null)
                return "tp: 플레이어나 물을 못 찾음";

            Vector3 target = water.bounds.center + new Vector3(-3.5f, 0f, -1f);
            if (Physics.Raycast(target + Vector3.up * 40f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                target = hit.point;
            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            player.transform.position = target + Vector3.up * 0.1f;
            if (controller != null)
                controller.enabled = true;
            return $"tp -> {target}";
        }

        // ---------------------------------------------------------------- 스킬 장착 (Q·E)

        private static readonly string[] Showcase =
        {
            "Fire/Fire_Fireball", "Fire/Fire_FlameThrower",
            "Fire/Fire_FlameDash", "Fire/Fire_Inferno",
            "Fire/Fire_Meteor", "Fire/Fire_FireVeil",
            "Fire/Fire_Frenzy", "Wind/Wind_Gust",
            "Wind/Wind_WindCutter", "Wind/Wind_Tornado",
            "Wind/Wind_Leap", "Wind/Wind_Headwind",
            "Wind/Wind_Tailwind", "Wind/Wind_WindVeil",
        };

        // pair 번째 두 스킬을 Q(Basic1)·E(Basic2) 에 장착. 스킬 UI 도 같이 바뀜
        public static string EquipPair(int pair)
        {
            if (!EditorApplication.isPlaying)
                return "equip: 플레이 모드가 아님";
            SkillStateModule module = Object.FindAnyObjectByType<SkillStateModule>();
            if (module == null)
                return "equip: 플레이어 스킬 모듈 없음";
            int a = pair * 2, b = a + 1;
            if (a >= Showcase.Length)
                return "equip: 번호 범위 밖";
            var q = AssetDatabase.LoadAssetAtPath<SkillData>($"Assets/Members/HJH/05.Data/Skill/Element/{Showcase[a]}.asset");
            var e = b < Showcase.Length ? AssetDatabase.LoadAssetAtPath<SkillData>($"Assets/Members/HJH/05.Data/Skill/Element/{Showcase[b]}.asset") : null;
            module.Equip(SkillSlotId.Basic1, q);
            module.Equip(SkillSlotId.Basic2, e);
            return $"equip Q={q?.name} E={e?.name}";
        }
    }
}
