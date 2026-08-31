using RobotArena.Session;
using UnityEngine;

public sealed class BotCombatReporter : MonoBehaviour
{
    private BotCombatRuntime runtime;
    private BotId reportedId;
    private bool hasReportedId;

    public bool IsInCombat { get; private set; }

    public static BotCombatReporter Ensure(GameObject botObject)
    {
        if (botObject == null)
        {
            return null;
        }

        BotCombatReporter existing = botObject.GetComponentInParent<BotCombatReporter>();
        if (existing != null)
        {
            return existing;
        }

        existing = botObject.GetComponentInChildren<BotCombatReporter>(true);
        if (existing != null)
        {
            return existing;
        }

        GameObject rootObject = botObject.transform.root.gameObject;
        return rootObject.AddComponent<BotCombatReporter>();
    }

    public void EnterCombat()
    {
        if (IsInCombat)
        {
            return;
        }

        runtime = BotCombatRuntime.GetOrCreate();
        reportedId = GetBotId();
        hasReportedId = true;
        IsInCombat = runtime.Tracker.EnterCombat(reportedId);
    }

    public void ExitCombat()
    {
        if (!IsInCombat || runtime == null || !hasReportedId)
        {
            IsInCombat = false;
            return;
        }

        runtime.Tracker.ExitCombat(reportedId);
        IsInCombat = false;
    }

    private void OnDisable()
    {
        ExitCombat();
    }

    private void OnDestroy()
    {
        ExitCombat();
    }

    private BotId GetBotId()
    {
        SessionBotRegistration registration = GetComponentInParent<SessionBotRegistration>();
        if (registration == null)
        {
            registration = GetComponentInChildren<SessionBotRegistration>(true);
        }

        return registration == null ? new BotId(GetInstanceID()) : registration.Id;
    }
}
