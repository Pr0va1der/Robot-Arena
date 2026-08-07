using UnityEngine;

public class PlayerDetector : MonoBehaviour
{
    public Animator animator;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            animator.SetBool("isPlayerVisible", true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            animator.SetBool("isPlayerVisible", false);
    }
}
