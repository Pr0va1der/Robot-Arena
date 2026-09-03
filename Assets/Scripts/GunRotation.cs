using RobotArena.PlayerWeapon;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class GunRotation : MonoBehaviour
{
    public Transform target;
    public Transform cameraTransform;
    public float minElevation = -45f;
    public float maxElevation = 45f;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
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

        // The final camera orientation is the aiming authority. The turret must not lag the crosshair.
        float elevation = PlayerWeaponAim.ClampElevation(
            PlayerWeaponAim.CameraOrbitElevation(cameraTransform.forward),
            minElevation,
            maxElevation);

        Quaternion targetRotation = Quaternion.Euler(
            -90f + elevation,
            cameraTransform.eulerAngles.y,
            0f);
        transform.rotation = targetRotation;
    }
}
