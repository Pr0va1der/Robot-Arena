using RobotArena.PlayerWeapon;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class GunRotation : MonoBehaviour
{
    public Transform target;
    public Transform cameraTransform;
    public float rotationSpeed = 10f;
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

        // The camera's final view direction supplies pitch only; the weapon remains the line-of-fire authority.
        float elevation = PlayerWeaponAim.ClampElevation(
            PlayerWeaponAim.VerticalViewAngle(cameraTransform.forward),
            minElevation,
            maxElevation);

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
