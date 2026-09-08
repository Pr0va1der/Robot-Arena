using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[DefaultExecutionOrder(110)]
public class LaserPointer : MonoBehaviour
{
    public Transform barrel;
    public float maxDistance = 100f;
    public LayerMask hitLayers;
    public GameObject laserDotPrefab;
    // The imported player Armature's barrel points along its local +Y axis.
    public Vector3 localAimAxis = Vector3.up;

    public Vector3 AimTarget { get; private set; }
    public Vector3 AimDirection { get; private set; }
    public bool HasHit { get; private set; }

    private LineRenderer lineRenderer;
    private GameObject laserDot;

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
            HasHit = false;
            return false;
        }

        Vector3 axis = localAimAxis.sqrMagnitude > 0.0001f
            ? localAimAxis.normalized
            : Vector3.up;
        AimDirection = barrel.TransformDirection(axis).normalized;
        float distance = Mathf.Max(0f, maxDistance);
        Ray ray = new Ray(barrel.position, AimDirection);
        HasHit = Physics.Raycast(ray, out RaycastHit hit, distance, hitLayers);
        AimTarget = HasHit
            ? hit.point
            : barrel.position + AimDirection * distance;

        EnsureRenderer();
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, barrel.position);
            lineRenderer.SetPosition(1, AimTarget);
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
