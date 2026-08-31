using UnityEngine;

public class PlayerDetector : MonoBehaviour
{
    public Animator animator;

    private BotCombatReporter combatReporter;

    private void Awake()
    {
        combatReporter = BotCombatReporter.Ensure(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (animator != null)
        {
            animator.SetBool("isPlayerVisible", true);
        }

        combatReporter?.EnterCombat();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && animator != null)
        {
            animator.SetBool("isPlayerVisible", false);
        }

        // The current controller has no transition out of Guns Up. Trigger exit
        // only changes visibility; the reporter leaves combat on a real AI exit,
        // disable, destruction, or bot death.
    }
}
