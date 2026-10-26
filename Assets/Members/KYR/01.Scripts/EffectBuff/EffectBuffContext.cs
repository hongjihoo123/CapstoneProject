using Members.KYR._01_Scripts;

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
