using System;

namespace RobotArena.PlayerWeapon
{
    public enum PlayerWeaponCommand
    {
        None,
        FireVolley,
        StartUltimate
    }

    public sealed class PlayerWeaponController
    {
        private float recoilUntil = float.NegativeInfinity;
        private bool pendingUltimate;

        public PlayerWeaponController(float recoilDuration)
        {
            RecoilDuration = recoilDuration;
        }

        private float recoilDuration;

        public float RecoilDuration
        {
            get => recoilDuration;
            set
            {
                if (value <= 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Recoil duration must be positive.");
                }

                recoilDuration = value;
            }
        }

        public bool IsUltimateActive { get; private set; }

        public bool IsRecoilActive { get; private set; }

        public PlayerWeaponCommand Tick(
            float now,
            bool fireHeld,
            bool ultimatePressed,
            bool ultimateReady)
        {
            if (IsUltimateActive)
            {
                return PlayerWeaponCommand.None;
            }

            if (now >= recoilUntil)
            {
                IsRecoilActive = false;
            }

            if (ultimatePressed && ultimateReady)
            {
                if (now < recoilUntil)
                {
                    pendingUltimate = true;
                }
                else
                {
                    IsUltimateActive = true;
                    return PlayerWeaponCommand.StartUltimate;
                }
            }

            if (now < recoilUntil)
            {
                return PlayerWeaponCommand.None;
            }

            if (pendingUltimate)
            {
                pendingUltimate = false;
                IsUltimateActive = true;
                return PlayerWeaponCommand.StartUltimate;
            }

            if (!fireHeld)
            {
                return PlayerWeaponCommand.None;
            }

            recoilUntil = now + RecoilDuration;
            IsRecoilActive = true;
            return PlayerWeaponCommand.FireVolley;
        }

        public void CompleteUltimate()
        {
            IsUltimateActive = false;
        }
    }
}
