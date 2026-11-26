using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class LootDropper : MonoBehaviour
    {
        [Header("Loot Configuration")]
        [SerializeField] private LootTable lootTable;

        [Header("Drop Point & Spawn Settings")]
        [SerializeField] private Transform dropPoint;
        [SerializeField] private float fallbackSpawnHeight = 0.35f;
        [SerializeField] private float fallbackSpawnForwardOffset = 0.45f;

        [Header("Drop Force")]
        [SerializeField] private bool applyPopUpForce = true;
        [SerializeField] private float dropForce = 2.5f;

        private bool hasDropped = false;

        public void DropLoot()
        {
            if (hasDropped)
                return;

            if (lootTable == null)
            {
                Debug.LogWarning($"{name}: none LootTable was assigned in the Inspector.");
                return;
            }

            LootItem lootItem = lootTable.GetRandomLoot();

            if (lootItem == null)
            {
                Debug.Log($"{name}: none loot was sorteado (or table empty).");
                return;
            }

            if (lootItem.itemPrefab == null)
            {
                Debug.LogWarning($"{name}: the LootItem '{lootItem.itemName}' does not contain the Prefab assigned.");
                return;
            }

            hasDropped = true;

            Vector3 spawnPosition = dropPoint != null
                ? dropPoint.position
                : transform.position + Vector3.up * fallbackSpawnHeight + transform.forward * fallbackSpawnForwardOffset;

            GameObject droppedLoot = Instantiate(
                lootItem.itemPrefab,
                spawnPosition,
                Quaternion.identity
            );

            PrepareDroppedLoot(droppedLoot);

            Debug.Log($"{name}: Loot created → {lootItem.itemName}");
        }

        private void PrepareDroppedLoot(GameObject droppedLoot)
        {
            if (droppedLoot == null) return;

            var throwable = droppedLoot.GetComponentInChildren<ThrowableItem>(true);
            if (throwable != null)
                throwable.DeactivateDamage();

            var rb = droppedLoot.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                if (applyPopUpForce)
                {
                    Vector3 forceDirection = Vector3.up * dropForce + transform.forward * (dropForce * 0.3f);
                    rb.AddForce(forceDirection, ForceMode.Impulse);
                }
            }
        }

        public void ResetDropper()
        {
            hasDropped = false;
        }
    }
}
