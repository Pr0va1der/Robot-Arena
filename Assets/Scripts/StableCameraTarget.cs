using UnityEngine;

/// <summary>
/// Keeps the gameplay camera's support point attached to the chassis position
/// without inheriting rotation from the chassis, turret, or weapon animation.
/// </summary>
[DefaultExecutionOrder(-1100)]
public sealed class StableCameraTarget : MonoBehaviour
{
    public Transform followTarget;

    private Vector3 worldOffset;

    private void Awake()
    {
        if (followTarget == null)
        {
            return;
        }

        worldOffset = transform.position - followTarget.position;
        transform.SetParent(null, true);
        transform.rotation = Quaternion.identity;
    }

    private void LateUpdate()
    {
        if (followTarget == null)
        {
            return;
        }

        transform.position = followTarget.position + worldOffset;
        transform.rotation = Quaternion.identity;
    }
}
