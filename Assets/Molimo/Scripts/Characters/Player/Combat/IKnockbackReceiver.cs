using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public interface IKnockbackReceiver
    {
        bool IsStunned { get; }
        void ApplyKnockback(Vector3 direction, float distance, float duration);
    }
}
