using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(Collider))]

    public class GrassInteraction : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private Animator grassAnimator;

        private static readonly int MoveHash =
            Animator.StringToHash("Move");

        private void Awake()
        {
            if (grassAnimator == null)
                grassAnimator = GetComponent<Animator>();

            Collider grassCollider = GetComponent<Collider>();
            grassCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (grassAnimator != null)
                grassAnimator.SetTrigger(MoveHash);
        }
    }
}
