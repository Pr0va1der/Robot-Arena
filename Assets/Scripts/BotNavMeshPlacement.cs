using UnityEngine;
using UnityEngine.AI;

public static class BotNavMeshPlacement
{
    public static bool TryPlace(
        GameObject bot,
        Vector3 desiredPosition,
        Quaternion desiredRotation,
        float searchRadius,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (bot == null)
        {
            failureReason = "Bot instance is null.";
            return false;
        }

        NavMeshAgent agent = bot.GetComponentInChildren<NavMeshAgent>(true);
        if (agent == null)
        {
            failureReason = "Bot has no NavMeshAgent.";
            return false;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };
        if (!NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, searchRadius, filter))
        {
            failureReason =
                "No compatible NavMesh point was found near " + desiredPosition + ".";
            return false;
        }

        agent.enabled = false;
        bot.transform.rotation = desiredRotation;
        Vector3 agentOffset = hit.position - agent.transform.position;
        bot.transform.position += agentOffset;
        agent.enabled = true;

        if (!agent.isOnNavMesh)
        {
            agent.enabled = false;
            failureReason = "NavMeshAgent did not register at sampled point " + hit.position + ".";
            return false;
        }

        return true;
    }
}
