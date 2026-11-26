using Members.JJH._02_Scripts.Map;
using Members.KYR._01.Scripts.Currency;
using Members.KYR._01.Scripts.SkillUpgrade;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Character
{
    public static class RunReset
    {
        public static void Begin()
        {
            Time.timeScale = 1f;
            GameBootstrap.Currency?.ResetRun();
            SkillUpgradeService.ResetRun();

            MapUI map = Object.FindFirstObjectByType<MapUI>(FindObjectsInactive.Include);
            if (map != null)
                map.ResetRun();
        }
    }
}
