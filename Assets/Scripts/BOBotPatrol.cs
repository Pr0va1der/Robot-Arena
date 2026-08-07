using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BOBotPatrol : MonoBehaviour
{
    [Header("Список точек патрулирования")]
    public Transform routeParent;

    public Transform[] GetPoints()
    {
        if (routeParent == null) return new Transform[0];

        Transform[] points = new Transform[routeParent.childCount];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = routeParent.GetChild(i);
        }
        return points;
    }
}
