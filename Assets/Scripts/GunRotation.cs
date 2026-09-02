using RobotArena.PlayerWeapon;
using UnityEngine;

public class GunRotation : MonoBehaviour
{
    public Transform target;
    public Transform cameraTransform;
    public float rotationSpeed = 10f;
    public float verticalAimSpeed = 180f;
    public float minElevation = -45f;
    public float maxElevation = 45f;

    public float Elevation => elevation;

    private float elevation;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        elevation = 0f;
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position;
        }

        if (cameraTransform == null)
        {
            return;
        }

        if (!PauseMenu.GameIsPaused &&
            !PauseMenu.PointerLockGestureConsumed)
        {
            float lookY = GameplayInputActions.Current.Look.y;
            elevation = PlayerWeaponAim.ClampElevation(
                elevation,
                lookY,
                verticalAimSpeed,
                Time.deltaTime,
                minElevation,
                maxElevation);
        }

        Quaternion targetRotation = Quaternion.Euler(
            -90f - elevation,
            cameraTransform.eulerAngles.y,
            0f);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed);
    }
}
