using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunRotation : MonoBehaviour
{
    public Transform target;          // Шар
    public Transform cameraTransform; // Камера
    public float rotationSpeed = 10f;

    void Update()
    {
        
    }

    void LateUpdate()
    {
        if (target != null)
        {
            // Следуем точно за позицией шара (без задержки)
            transform.position = target.position;
        }

        if (cameraTransform != null)
        {
            // Поворачиваем пушку туда, куда смотрит камера
            Quaternion targetRotation = Quaternion.Euler(-90f, cameraTransform.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
}
