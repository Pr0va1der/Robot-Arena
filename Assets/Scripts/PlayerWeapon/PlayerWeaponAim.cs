using System;
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

            // The imported Armature fires along local -Y. Keep the model upright
            // while aligning that axis with the final camera direction.
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

        /// <summary>
        /// Finds the largest shoulder offset whose camera path is not obstructed.
        /// The probe is supplied by the camera integration so this policy remains
        /// deterministic and independent from Unity physics in unit tests.
        /// </summary>
        public static float ResolveSafeShoulderOffset(
            float requestedOffset,
            Func<float, bool> isBlocked,
            int iterations = 8)
        {
            float requested = Mathf.Max(0f, requestedOffset);
            if (requested <= 0f || isBlocked == null || !isBlocked(requested))
            {
                return requested;
            }

            if (isBlocked(0f))
            {
                return 0f;
            }

            float clearOffset = 0f;
            float blockedOffset = requested;
            int searchIterations = Mathf.Max(1, iterations);
            for (int i = 0; i < searchIterations; i++)
            {
                float candidate = (clearOffset + blockedOffset) * 0.5f;
                if (isBlocked(candidate))
                {
                    blockedOffset = candidate;
                }
                else
                {
                    clearOffset = candidate;
                }
            }

            return clearOffset;
        }
    }
}
