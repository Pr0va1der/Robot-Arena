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

        public static Vector3 DirectionToTarget(
            Vector3 origin,
            Vector3 target,
            Vector3 fallback)
        {
            Vector3 displacement = target - origin;
            if (displacement.sqrMagnitude > 0.0001f)
            {
                return displacement.normalized;
            }

            return fallback.sqrMagnitude > 0.0001f
                ? fallback.normalized
                : Vector3.forward;
        }
    }
}
