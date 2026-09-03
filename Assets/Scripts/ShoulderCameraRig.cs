using Cinemachine;
using UnityEngine;

/// <summary>
/// Configures the gameplay FreeLook camera for a fixed, right-shoulder composition.
/// </summary>
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(CinemachineFreeLook))]
public sealed class ShoulderCameraRig : CinemachineExtension
{
    [Header("Shoulder composition")]
    [Min(0f)]
    public float shoulderOffset = 0.75f;

    [Range(0f, 1f)]
    public float targetScreenX = 0.375f;

    [Header("Collision")]
    [Min(0f)]
    public float minimumDistanceFromTarget = 0.6f;

    private CinemachineFreeLook freeLook;
    private CinemachineCollider cameraCollider;

    private void Start()
    {
        Configure();
    }

    private void Configure()
    {
        freeLook = GetComponent<CinemachineFreeLook>();
        if (freeLook == null)
        {
            enabled = false;
            return;
        }

        cameraCollider = GetComponent<CinemachineCollider>();
        if (cameraCollider != null)
        {
            cameraCollider.m_MinimumDistanceFromTarget = Mathf.Max(0f, minimumDistanceFromTarget);
        }

        ReorderExtensionsForCollision();
        ConfigureComposers();
    }

    private void ReorderExtensionsForCollision()
    {
        if (freeLook == null)
        {
            return;
        }

        // The shoulder offset must be part of the body state before the collider
        // solves the final camera position against nearby geometry.
        freeLook.RemoveExtension(this);
        freeLook.AddExtension(this);

        if (cameraCollider != null)
        {
            freeLook.RemoveExtension(cameraCollider);
            freeLook.AddExtension(cameraCollider);
        }
    }

    private void ConfigureComposers()
    {
        if (freeLook == null)
        {
            return;
        }

        for (int rigIndex = 0; rigIndex < 3; rigIndex++)
        {
            CinemachineVirtualCamera rig = freeLook.GetRig(rigIndex);
            if (rig == null)
            {
                continue;
            }

            CinemachineComposer composer = rig.GetCinemachineComponent<CinemachineComposer>();
            if (composer != null)
            {
                composer.m_ScreenX = Mathf.Clamp01(targetScreenX);
                composer.m_DeadZoneWidth = 0f;
                composer.m_SoftZoneWidth = 0f;
                composer.m_CenterOnActivate = false;
            }
        }
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body)
        {
            return;
        }

        // Offset only the physical camera position. Leaving ReferenceLookAt intact
        // lets the composer place the player itself at targetScreenX instead of
        // composing a shifted proxy point. Use the current orbit geometry rather
        // than the rig transform, which still contains the previous frame's aim.
        Vector3 shoulderRight = ResolveCurrentShoulderRight(state);
        state.PositionCorrection += shoulderRight * Mathf.Max(0f, shoulderOffset);
    }

    private static Vector3 ResolveCurrentShoulderRight(CameraState state)
    {
        Vector3 viewDirection = state.ReferenceLookAt - state.RawPosition;
        Vector3 planarViewDirection = Vector3.ProjectOnPlane(viewDirection, state.ReferenceUp);
        if (planarViewDirection.sqrMagnitude < 0.0001f)
        {
            planarViewDirection = Vector3.ProjectOnPlane(
                state.RawOrientation * Vector3.forward,
                state.ReferenceUp);
        }

        if (planarViewDirection.sqrMagnitude < 0.0001f)
        {
            return state.RawOrientation * Vector3.right;
        }

        return Vector3.Cross(
            state.ReferenceUp.normalized,
            planarViewDirection.normalized).normalized;
    }
}
