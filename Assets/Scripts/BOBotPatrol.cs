using System.Collections;
using System.Collections.Generic;
using RobotArena.Session;
using UnityEngine;

public class BOBotPatrol : MonoBehaviour
{
    [Header("Список точек патрулирования")]
    public Transform routeParent;

    private void Awake()
    {
        if (GetComponent<SessionBotRegistration>() == null)
        {
            gameObject.AddComponent<SessionBotRegistration>();
        }
    }

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
