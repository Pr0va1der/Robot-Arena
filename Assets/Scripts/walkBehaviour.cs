using UnityEngine;
using UnityEngine.AI;

public class walkBehaviour : StateMachineBehaviour
{
    NavMeshAgent agent;
    Transform[] points;
    int currentIndex = 0;
    float timer;

    public float waitTime = 2f;
    bool isWaiting = false;
    float waitTimer = 0f;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        timer = 0;
        isWaiting = false;
        waitTimer = 0f;

        if (agent == null)
            agent = animator.GetComponent<NavMeshAgent>();

        if (points == null)
        {
            BOBotPatrol patrol = animator.GetComponent<BOBotPatrol>();
            if (patrol != null)
                points = patrol.GetPoints();

            if (points == null || points.Length == 0)
                Debug.LogWarning(animator.name + " не имеет назначенных точек маршрута!");
        }

        if (points != null && points.Length > 0)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(points[currentIndex].position);
            }
        }
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (agent == null || !agent.isOnNavMesh || points == null || points.Length == 0) return;

        if (isWaiting)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitTime)
            {
                isWaiting = false;
                waitTimer = 0f;

                currentIndex = (currentIndex + 1) % points.Length;
                agent.isStopped = false;
                agent.SetDestination(points[currentIndex].position);
            }
            return;
        }

        timer += Time.deltaTime;


        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            agent.isStopped = true;
            isWaiting = true;
            waitTimer = 0f;

            animator.SetBool("isPatrolling", false);
        }
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = true;
    }
}
