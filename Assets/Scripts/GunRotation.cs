using Cinemachine;
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

    private void OnEnable()
    {
        CinemachineCore.CameraUpdatedEvent.AddListener(SynchronizeAfterCameraUpdate);
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(SynchronizeAfterCameraUpdate);
    }

    private void Update()
    {
        SynchronizeTargetPosition();
    }

    private void LateUpdate()
    {
        SynchronizeAim();
    }

    private void SynchronizeAfterCameraUpdate(CinemachineBrain brain)
    {
        if (brain == null || cameraTransform == null)
        {
            return;
        }

        Camera outputCamera = brain.OutputCamera;
        if (outputCamera == null || outputCamera.transform != cameraTransform)
        {
            return;
        }

        SynchronizeAim();
    }

    private void SynchronizeAim()
    {
        SynchronizeTargetPosition();

        if (cameraTransform == null)
        {
            return;
        }

        // The final camera forward direction is the aiming authority. The event
        // path runs after Cinemachine collision correction; LateUpdate remains a
        // fallback for ordinary cameras and keeps the component self-contained.
        transform.rotation = PlayerWeaponAim.TurretRotation(
            cameraTransform.forward,
            minElevation,
            maxElevation);
    }

    private void SynchronizeTargetPosition()
    {
        if (target != null)
        {
            transform.position = target.position;
        }
    }
}
