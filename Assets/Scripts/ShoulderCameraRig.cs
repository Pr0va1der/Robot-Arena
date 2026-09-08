using RobotArena.PlayerWeapon;
using Cinemachine;
using UnityEngine;

/// <summary>
/// Configures the gameplay FreeLook camera for a right-shoulder composition with
/// bounded clearance correction against the player and visible line of fire.
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

    [Header("Adaptive clearance")]
    [Min(0f)]
    public float clearanceRadius = 0.2f;

    [Min(0f)]
    public float clearanceMargin = 0.05f;

    [Min(0f)]
    public float clearanceSmoothTime = 0.08f;

    public LayerMask clearanceLayers = ~0;
    public Transform clearanceTarget;
    public Transform clearanceOwnerRoot;
    public LaserPointer laserPointer;

    public float ResolvedShoulderOffset { get; private set; }

    private CinemachineFreeLook freeLook;
    private CinemachineCollider cameraCollider;
    private float clearanceOffsetVelocity;
    private bool clearanceOffsetInitialized;
    private readonly RaycastHit[] clearanceHits = new RaycastHit[32];

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

        if (clearanceLayers.value == 0)
        {
            clearanceLayers = ~0;
        }

        if (laserPointer == null)
        {
            laserPointer = FindObjectOfType<LaserPointer>();
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
        CameraState clearanceState = state;
        float safeOffsetTarget = PlayerWeaponAim.ResolveSafeShoulderOffset(
            shoulderOffset,
            offset => IsCameraPathBlocked(clearanceState, shoulderRight, offset));
        if (!clearanceOffsetInitialized)
        {
            ResolvedShoulderOffset = safeOffsetTarget;
            clearanceOffsetInitialized = true;
        }
        else
        {
            float elapsed = deltaTime > 0f ? deltaTime : Time.deltaTime;
            if (elapsed <= 0f)
            {
                elapsed = 1f / 60f;
            }

            ResolvedShoulderOffset = Mathf.SmoothDamp(
                ResolvedShoulderOffset,
                safeOffsetTarget,
                ref clearanceOffsetVelocity,
                Mathf.Max(0f, clearanceSmoothTime),
                Mathf.Infinity,
                elapsed);
        }

        state.PositionCorrection += shoulderRight * ResolvedShoulderOffset;
    }

    private bool IsCameraPathBlocked(CameraState state, Vector3 shoulderRight, float offset)
    {
        if (clearanceRadius <= 0f || freeLook == null)
        {
            return false;
        }

        Transform target = clearanceTarget != null ? clearanceTarget : freeLook.LookAt;
        if (target == null)
        {
            return false;
        }

        Vector3 cameraPosition = state.RawPosition + shoulderRight * offset;
        if ((cameraPosition - target.position).sqrMagnitude <= 0.00000001f)
        {
            return false;
        }

        float radius = clearanceRadius + Mathf.Max(0f, clearanceMargin);
        if (Physics.CheckSphere(
                cameraPosition,
                radius,
                clearanceLayers,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        if (IsLaserSelfOccluded(cameraPosition))
        {
            return true;
        }

        return false;
    }

    private bool IsLaserSelfOccluded(Vector3 cameraPosition)
    {
        if (laserPointer == null || laserPointer.AimTarget == default)
        {
            return false;
        }

        Vector3 visibleStart = laserPointer.VisualStart;
        Vector3 visibleEnd = laserPointer.AimTarget;
        return HasOwnedColliderBetween(cameraPosition, visibleStart) ||
               HasOwnedColliderBetween(cameraPosition, Vector3.Lerp(visibleStart, visibleEnd, 0.5f)) ||
               HasOwnedColliderBetween(cameraPosition, visibleEnd);
    }

    private bool HasOwnedColliderBetween(Vector3 origin, Vector3 destination)
    {
        Vector3 toDestination = destination - origin;
        float distance = toDestination.magnitude;
        if (distance <= 0.0001f)
        {
            return false;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            toDestination / distance,
            clearanceHits,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            if (IsOwnedCollider(clearanceHits[i].collider, clearanceTarget))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOwnedCollider(Collider candidate, Transform fallbackTarget)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform root = clearanceOwnerRoot != null
            ? clearanceOwnerRoot
            : fallbackTarget;
        return root != null && (candidate.transform == root || candidate.transform.IsChildOf(root));
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
