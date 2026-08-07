using UnityEngine;

public class idleBehaviour : StateMachineBehaviour
{
    float timer;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        timer = 0;
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        timer += Time.deltaTime;
        if (timer > 3f)
        {
            animator.SetBool("isPatrolling", true);
        }
    }
}
