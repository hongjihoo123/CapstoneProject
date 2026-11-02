using Members.KYR._01_Scripts;
using Members.KYR._01.Scripts.Player;

namespace Members.KYR._01_Scripts.EffectBuffs
{
    public sealed class EffectBuffContext
    {
        public PlayerAgent Player { get; }

        public EffectBuffContext(PlayerAgent player)
        {
            Player = player;
        }
    }
}
