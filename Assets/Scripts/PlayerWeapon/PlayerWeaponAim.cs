using UnityEngine;

namespace RobotArena.PlayerWeapon
{
    public static class PlayerWeaponAim
    {
        public static float CameraOrbitElevation(Vector3 cameraForward)
        {
            if (cameraForward.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            cameraForward.Normalize();
            float horizontalForwardMagnitude = new Vector2(cameraForward.x, cameraForward.z).magnitude;
            return Mathf.Atan2(
                -cameraForward.y,
                horizontalForwardMagnitude) * Mathf.Rad2Deg;
        }

        public static float CameraOrbitYaw(Vector3 cameraForward)
        {
            if (cameraForward.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            cameraForward.Normalize();
            return Mathf.Atan2(cameraForward.x, cameraForward.z) * Mathf.Rad2Deg;
        }

        public static Quaternion TurretRotation(
            Vector3 cameraForward,
            float minElevation,
            float maxElevation)
        {
            float elevation = ClampElevation(
                CameraOrbitElevation(cameraForward),
                minElevation,
                maxElevation);

            return Quaternion.Euler(
                -90f + elevation,
                CameraOrbitYaw(cameraForward),
                0f);
        }

        public static float ClampElevation(float elevation, float minElevation, float maxElevation)
        {
            if (maxElevation < minElevation)
            {
                float swapped = minElevation;
                minElevation = maxElevation;
                maxElevation = swapped;
            }

            return Mathf.Clamp(elevation, minElevation, maxElevation);
        }
    }
}
