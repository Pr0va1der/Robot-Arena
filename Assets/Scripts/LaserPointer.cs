using System;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[DefaultExecutionOrder(110)]
public class LaserPointer : MonoBehaviour
{
    [Header("Aim")]
    public Transform barrel;
    public float maxDistance = 100f;
    public LayerMask hitLayers;
    public GameObject laserDotPrefab;
    // The imported player Armature's barrel points along its local +Y axis.
    public Vector3 localAimAxis = Vector3.up;

    [Header("Visual emission")]
    public Transform visualEmissionPoint;
    public Transform ownerRoot;
    [Min(0f)] public float visualClearance = 0.02f;

    public Vector3 AimTarget { get; private set; }
    public Vector3 AimDirection { get; private set; }
    public Vector3 VisualStart { get; private set; }
    public Vector3 VisualEnd { get; private set; }
    public bool HasHit { get; private set; }

    private LineRenderer lineRenderer;
    private GameObject laserDot;
    private readonly RaycastHit[] raycastHits = new RaycastHit[64];
    private Collider[] ownedColliders = Array.Empty<Collider>();
    private Transform cachedOwnerRoot;

    private void Start()
    {
        EnsureRenderer();

        if (laserDotPrefab != null)
        {
            laserDot = Instantiate(laserDotPrefab);
        }

        RefreshAim();
    }

    private void LateUpdate()
    {
        RefreshAim();
    }

    public bool TryGetAimTarget(out Vector3 target)
    {
        if (barrel == null)
        {
            target = default;
            return false;
        }

        RefreshAim();
        target = AimTarget;
        return true;
    }

    public bool TryGetAimDirection(out Vector3 direction)
    {
        if (barrel == null)
        {
            direction = default;
            return false;
        }

        RefreshAim();
        direction = AimDirection;
        return direction.sqrMagnitude > 0.0001f;
    }

    public bool RefreshAim()
    {
        if (barrel == null)
        {
            AimTarget = default;
            AimDirection = default;
            VisualStart = default;
            VisualEnd = default;
            HasHit = false;
            return false;
        }

        Vector3 axis = localAimAxis.sqrMagnitude > 0.0001f
            ? localAimAxis.normalized
            : Vector3.up;
        AimDirection = barrel.TransformDirection(axis).normalized;
        float distance = Mathf.Max(0f, maxDistance);
        Ray ray = new Ray(barrel.position, AimDirection);
        EnsureOwnedColliders();
        HasHit = TryGetExternalHit(ray, distance, out RaycastHit hit);
        Vector3 physicalTarget = HasHit
            ? hit.point
            : barrel.position + AimDirection * distance;

        Vector3 visualStart = ResolveVisualStart(ray, distance, HasHit ? hit.distance : distance);
        VisualStart = visualStart;
        AimTarget = physicalTarget;
        VisualEnd = HasHit
            ? physicalTarget
            : visualStart + AimDirection * distance;

        EnsureRenderer();
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, visualStart);
            lineRenderer.SetPosition(1, VisualEnd);
        }

        if (laserDot != null)
        {
            laserDot.SetActive(HasHit);
            if (HasHit)
            {
                laserDot.transform.position = hit.point + hit.normal * 0.001f;
                laserDot.transform.rotation = Quaternion.LookRotation(hit.normal);
            }
        }

        return HasHit;
    }

    private bool TryGetExternalHit(Ray ray, float distance, out RaycastHit closestHit)
    {
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            raycastHits,
            distance,
            hitLayers,
            QueryTriggerInteraction.Ignore);
        int closestIndex = -1;
        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            if (!IsOwnedCollider(raycastHits[i].collider) && raycastHits[i].distance < closestDistance)
            {
                closestIndex = i;
                closestDistance = raycastHits[i].distance;
            }
        }

        if (closestIndex >= 0)
        {
            closestHit = raycastHits[closestIndex];
            return true;
        }

        closestHit = default;
        return false;
    }

    private bool IsOwnedCollider(Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform root = ownerRoot != null ? ownerRoot : transform.root;
        return candidate.transform == root || candidate.transform.IsChildOf(root);
    }

    private void EnsureOwnedColliders()
    {
        Transform root = ownerRoot != null ? ownerRoot : transform.root;
        if (root == cachedOwnerRoot)
        {
            return;
        }

        cachedOwnerRoot = root;
        ownedColliders = root == null
            ? Array.Empty<Collider>()
            : root.GetComponentsInChildren<Collider>(true);
    }

    private Vector3 ResolveVisualStart(Ray aimRay, float maxAimDistance, float targetDistance)
    {
        Vector3 visualStart = visualEmissionPoint != null
            ? visualEmissionPoint.position
            : aimRay.origin;
        float configuredVisualDistance = Mathf.Clamp(
            Vector3.Dot(visualStart - aimRay.origin, aimRay.direction),
            0f,
            maxAimDistance);
        if (targetDistance < configuredVisualDistance - 0.0001f)
        {
            // An external blocker before the configured emission point must not
            // leave a beam segment drawn through the blocker.
            return aimRay.origin + aimRay.direction * targetDistance;
        }

        float visualDistance = Mathf.Min(configuredVisualDistance, targetDistance);
        float inspectionDistance = visualEmissionPoint != null
            ? visualDistance
            : Mathf.Min(maxAimDistance, targetDistance);
        float exitDistance = -1f;

        for (int i = 0; i < ownedColliders.Length; i++)
        {
            if (!TryGetBoundsIntersection(
                    ownedColliders[i].bounds,
                    aimRay,
                    out float entryDistance,
                    out float colliderExitDistance) ||
                entryDistance > inspectionDistance + 0.0001f ||
                colliderExitDistance < 0f)
            {
                continue;
            }

            exitDistance = Mathf.Max(exitDistance, colliderExitDistance);
        }

        float clampedDistance = exitDistance >= 0f
            ? exitDistance + Mathf.Max(0f, visualClearance)
            : visualDistance;
        clampedDistance = Mathf.Clamp(clampedDistance, 0f, Mathf.Min(maxAimDistance, targetDistance));
        return aimRay.origin + aimRay.direction * clampedDistance;
    }

    private static bool TryGetBoundsIntersection(
        Bounds bounds,
        Ray ray,
        out float entryDistance,
        out float exitDistance)
    {
        entryDistance = 0f;
        exitDistance = float.PositiveInfinity;
        Vector3 minimum = bounds.min;
        Vector3 maximum = bounds.max;

        for (int axis = 0; axis < 3; axis++)
        {
            float originAxis = ray.origin[axis];
            float directionAxis = ray.direction[axis];
            if (Mathf.Abs(directionAxis) < 0.0001f)
            {
                if (originAxis < minimum[axis] || originAxis > maximum[axis])
                {
                    return false;
                }

                continue;
            }

            float first = (minimum[axis] - originAxis) / directionAxis;
            float second = (maximum[axis] - originAxis) / directionAxis;
            entryDistance = Mathf.Max(entryDistance, Mathf.Min(first, second));
            exitDistance = Mathf.Min(exitDistance, Mathf.Max(first, second));
            if (entryDistance > exitDistance)
            {
                return false;
            }
        }

        return !float.IsPositiveInfinity(exitDistance);
    }

    private void EnsureRenderer()
    {
        if (lineRenderer != null)
        {
            return;
        }

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.002f;
    }
}
