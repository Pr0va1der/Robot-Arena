using UnityEngine;

namespace RobotArena.PlayerWeapon
{
    public static class PlayerWeaponAim
    {
        public static float ClampElevation(
            float current,
            float lookDelta,
            float lookSpeed,
            float deltaTime,
            float minElevation,
            float maxElevation)
        {
            if (maxElevation < minElevation)
            {
                float swapped = minElevation;
                minElevation = maxElevation;
                maxElevation = swapped;
            }

            return Mathf.Clamp(
                current + lookDelta * lookSpeed * deltaTime,
                minElevation,
                maxElevation);
        }

    }
}
