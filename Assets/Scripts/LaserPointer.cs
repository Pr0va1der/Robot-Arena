using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserPointer : MonoBehaviour
{
    public Transform barrel;          // Откуда выходит луч
    public float maxDistance = 100f;  // Максимальная длина луча
    public LayerMask hitLayers;       // Слои, с которыми взаимодействует лазер
    public GameObject laserDotPrefab; // Префаб точки на поверхности (по желанию)

    private LineRenderer lr;
    private GameObject laserDot;

    void Start()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = 0.01f;
        lr.endWidth = 0.002f;

        if (laserDotPrefab != null)
            laserDot = Instantiate(laserDotPrefab);

        // Задаём начальные позиции луча, чтобы не мигал на старте
        if (barrel != null)
        {
            Vector3 start = barrel.position;
            Vector3 end = barrel.position + barrel.up * maxDistance;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }
    }

    void LateUpdate()
    {
        if (barrel == null) return;

        lr.SetPosition(0, barrel.position);

        Ray ray = new Ray(barrel.position, barrel.up);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitLayers))
        {
            lr.SetPosition(1, hit.point);

            if (laserDot != null)
            {
                laserDot.transform.position = hit.point + hit.normal * 0.001f;
                laserDot.transform.rotation = Quaternion.LookRotation(hit.normal);
                laserDot.SetActive(true);
            }
        }
        else
        {
            lr.SetPosition(1, barrel.position + barrel.up * maxDistance);

            if (laserDot != null)
                laserDot.SetActive(false);
        }
    }
}
