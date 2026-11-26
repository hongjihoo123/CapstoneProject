using Members.JJH._02_Scripts.Map;
using Members.KYR._01.Scripts.Currency;
using Members.KYR._01.Scripts.SkillUpgrade;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Character
{
    // 새 런을 시작할 때(캐릭터를 새로 고른 직후) 이전 런의 흔적을 정리:
    // 시간 배속 원래대로, 런 골드 0, 맵 진행도(몇 번째 방인지) 처음으로
    public static class RunReset
    {
        public static void Begin()
        {
            Time.timeScale = 1f;
            GameBootstrap.Currency?.ResetRun();
            SkillUpgradeService.ResetRun();   // 런 골드로 산 스킬 강화도 새 런에서는 처음부터

            // 씬을 넘어 살아남는 맵 UI(DontDestroyOnLoad)도 처음 상태로
            MapUI map = Object.FindFirstObjectByType<MapUI>(FindObjectsInactive.Include);
            if (map != null)
                map.ResetRun();
        }
    }
}
