using System;
using Members.KYR._01_Scripts;
using RobotWeapons;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Follows whatever weapon the player holds (it changes with the character) and re-raises its
    // presentation events, so listeners subscribe once instead of re-binding on every weapon swap.
    // Call Track() every frame (cheap reference compare) and Clear() when the listener is disabled.
    public sealed class WeaponVisualTracker
    {
        private IShotVisualSource _shots;
        private ISwingVisualSource _swings;

        public event Action<ShotVisual> ShotFired;
        public event Action<ShotProjectile> ProjectileLaunched;
        public event Action<SwingVisual> Swung;

        public void Track(PlayerAgent player)
        {
            IWeapon weapon = player != null && player.Weapon != null ? player.Weapon.Weapon : null;
            if (!ReferenceEquals(weapon as IShotVisualSource, _shots))
                BindShots(weapon as IShotVisualSource);
            if (!ReferenceEquals(weapon as ISwingVisualSource, _swings))
                BindSwings(weapon as ISwingVisualSource);
        }

        public void Clear()
        {
            BindShots(null);
            BindSwings(null);
        }

        private void BindShots(IShotVisualSource source)
        {
            if (_shots != null)
            {
                _shots.ShotFired -= RaiseShot;
                _shots.ProjectileLaunched -= RaiseProjectile;
            }

            _shots = source;
            if (_shots != null)
            {
                _shots.ShotFired += RaiseShot;
                _shots.ProjectileLaunched += RaiseProjectile;
            }
        }

        private void BindSwings(ISwingVisualSource source)
        {
            if (_swings != null)
                _swings.Swung -= RaiseSwing;
            _swings = source;
            if (_swings != null)
                _swings.Swung += RaiseSwing;
        }

        private void RaiseShot(ShotVisual shot) => ShotFired?.Invoke(shot);
        private void RaiseProjectile(ShotProjectile projectile) => ProjectileLaunched?.Invoke(projectile);
        private void RaiseSwing(SwingVisual swing) => Swung?.Invoke(swing);
    }
}
