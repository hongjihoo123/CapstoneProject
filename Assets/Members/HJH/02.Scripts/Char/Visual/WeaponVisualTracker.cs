using System;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    public sealed class WeaponVisualTracker
    {
        private IShotVisualSource _shots;
        private ISwingVisualSource _swings;
        private IAnimationTimedShots _timed;

        public event Action<ShotVisual> ShotFired;
        public event Action<ShotProjectile> ProjectileLaunched;
        public event Action<SwingVisual> Swung;
        public event Action<SwingHitVisual> SwingHit;
        public event Action ShotQueued;

        public bool WaitsForAnimation => _timed != null && _timed.WaitsForAnimation;

        public void Track(PlayerAgent player)
        {
            IWeapon weapon = player != null && player.Weapon != null ? player.Weapon.Weapon : null;
            if (!ReferenceEquals(weapon as IShotVisualSource, _shots))
                BindShots(weapon as IShotVisualSource);
            if (!ReferenceEquals(weapon as ISwingVisualSource, _swings))
                BindSwings(weapon as ISwingVisualSource);
            if (!ReferenceEquals(weapon as IAnimationTimedShots, _timed))
                BindTimed(weapon as IAnimationTimedShots);
        }

        public void Clear()
        {
            BindShots(null);
            BindSwings(null);
            BindTimed(null);
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
            {
                _swings.Swung -= RaiseSwing;
                _swings.SwingHit -= RaiseSwingHit;
            }

            _swings = source;
            if (_swings != null)
            {
                _swings.Swung += RaiseSwing;
                _swings.SwingHit += RaiseSwingHit;
            }
        }

        private void BindTimed(IAnimationTimedShots source)
        {
            if (_timed != null)
                _timed.ShotQueued -= RaiseQueued;

            _timed = source;
            if (_timed != null)
                _timed.ShotQueued += RaiseQueued;
        }

        private void RaiseQueued() => ShotQueued?.Invoke();
        private void RaiseShot(ShotVisual shot) => ShotFired?.Invoke(shot);
        private void RaiseProjectile(ShotProjectile projectile) => ProjectileLaunched?.Invoke(projectile);
        private void RaiseSwing(SwingVisual swing) => Swung?.Invoke(swing);
        private void RaiseSwingHit(SwingHitVisual hit) => SwingHit?.Invoke(hit);
    }
}
