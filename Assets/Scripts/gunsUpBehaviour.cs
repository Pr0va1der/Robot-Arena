using UnityEngine;
using UnityEngine.AI;

public class gunsUpBehaviour : StateMachineBehaviour
{
    NavMeshAgent agent;
    Transform player;
    public Transform targetPoint;  // 🎯 точка прицеливания (например, сфера игрока)
    float updateRate = 0.2f;
    float nextUpdateTime = 0f;

    GameObject[] barrels;
    public GameObject bulletPrefab;
    public float fireRate = 1f;
    private float nextFireTime = 0f;
    public float shootForce = 50f;
    public float aimConeAngle = 25f;

    bool canShoot = false;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        agent = animator.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = false;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;

            Transform target = playerObj.transform.Find("Sphere/HitTarget");
            targetPoint = target != null ? target : player;
        }

        Transform body = animator.transform.Find("Body");
        if (body != null)
        {
            Transform armL = body.Find("Arm_L/Forearm_L/Barrel_L");
            Transform armR = body.Find("Arm_R/Forearm_R/Barrel_R");

            barrels = new GameObject[]
            {
                armL != null ? armL.gameObject : null,
                armR != null ? armR.gameObject : null
            };
        }

        canShoot = false;
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (agent == null || !agent.isOnNavMesh || targetPoint == null)
            return;

        if (Time.time >= nextUpdateTime)
        {
            agent.SetDestination(player.position);
            nextUpdateTime = Time.time + updateRate;
        }

        Vector3 direction = (targetPoint.position - agent.transform.position).normalized;
        direction.y = 0;
        if (direction.magnitude > 0.1f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            agent.transform.rotation = Quaternion.Slerp(agent.transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

        if (!canShoot && stateInfo.normalizedTime >= 0.9f)
            canShoot = true;

        Vector3 toTarget = targetPoint.position - agent.transform.position;
        float angle = Vector3.Angle(agent.transform.forward, toTarget);

        if (canShoot && Time.time >= nextFireTime && angle <= aimConeAngle)
        {
            Fire();
            nextFireTime = Time.time + fireRate;
        }
    }

    void Fire()
    {
        if (bulletPrefab == null || barrels == null) return;

        foreach (GameObject barrel in barrels)
        {
            if (barrel == null) continue;

            Vector3 dir = (targetPoint.position - barrel.transform.position).normalized;

            Ray ray = new Ray(barrel.transform.position, dir);
            RaycastHit hit;

            Vector3 finalTarget = targetPoint.position;

            if (Physics.Raycast(ray, out hit, 100f))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    finalTarget = hit.point;
                }
            }

            GameObject bullet = GameObject.Instantiate(bulletPrefab, barrel.transform.position, Quaternion.LookRotation(finalTarget - barrel.transform.position));
            Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
            if (bulletRb != null)
            {
                bulletRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                bulletRb.useGravity = true;

                Vector3 shootDir = (finalTarget - barrel.transform.position).normalized;
                bulletRb.AddForce(shootDir * shootForce, ForceMode.Impulse);
            }

            Bullet b = bullet.GetComponent<Bullet>();
            if (b != null)
                b.owner = agent.gameObject;

            GameObject.Destroy(bullet, 5f);
        }
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = true;
        canShoot = false;
    }
}
