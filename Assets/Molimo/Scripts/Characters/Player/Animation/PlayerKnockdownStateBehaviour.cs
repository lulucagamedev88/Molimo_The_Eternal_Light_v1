using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class PlayerKnockdownStateBehaviour : StateMachineBehaviour
    {
        override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            PlayerBrain brain = animator.GetComponent<PlayerBrain>();
            if (brain == null) brain = animator.GetComponentInParent<PlayerBrain>();

            if (brain != null)
            {
                brain.EndKnockdown();
            }
        }
    }
}
