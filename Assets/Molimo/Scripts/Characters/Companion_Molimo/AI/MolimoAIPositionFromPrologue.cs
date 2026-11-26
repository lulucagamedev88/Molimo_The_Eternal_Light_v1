using UnityEngine;
using MolimoByLulucaGameDev;

namespace MolimoByLulucaGameDev
{

    public class MolimoAIPositionFromPrologue : MonoBehaviour
    {

        public static bool HasPendingProloguePosition { get; private set; }

        private void OnEnable()
        {
            HasPendingProloguePosition = false;

            if (PrologueMolimoController.HasValidProloguePosition)
            {
                transform.position = PrologueMolimoController.LastProloguePosition;
                transform.rotation = PrologueMolimoController.LastPrologueRotation;
                HasPendingProloguePosition = true;

            }
        }
    }
}
