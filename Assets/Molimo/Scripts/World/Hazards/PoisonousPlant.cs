using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]

    public class PoisonousPlant : MonoBehaviour
    {
        [Header("Damage")]
        [SerializeField] private float damagePerTick = 5f;
        [SerializeField] private float tickInterval = 1f;
        [SerializeField] private bool continuousDamage = true;

        private float nextTickTime;
        private PlayerHealth playerHealthInside;

        private static PlayerHealth lastDamagedPlayer;
        private static int lastDamageFrame = -1;

        private void OnTriggerEnter(Collider other)
        {
            PlayerHealth h = ResolveHealth(other);
            if (h == null) return;

            playerHealthInside = h;
            TryDamage(h);
            nextTickTime = Time.time + tickInterval;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!continuousDamage || playerHealthInside == null) return;
            if (Time.time < nextTickTime) return;

            TryDamage(playerHealthInside);
            nextTickTime = Time.time + tickInterval;
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerHealth h = ResolveHealth(other);
            if (h != null && h == playerHealthInside)
                playerHealthInside = null;
        }

        private void TryDamage(PlayerHealth target)
        {
            if (target == null || !target.enabled) return;

            if (target == lastDamagedPlayer && lastDamageFrame == Time.frameCount)
                return;

            lastDamagedPlayer = target;
            lastDamageFrame = Time.frameCount;

            target.TakeDamage(damagePerTick);
        }

        private static PlayerHealth ResolveHealth(Collider other)
        {
            if (other == null) return null;

            PlayerHealth h = other.GetComponent<PlayerHealth>();
            if (h == null) h = other.GetComponentInParent<PlayerHealth>();
            return h;
        }
    }
}
