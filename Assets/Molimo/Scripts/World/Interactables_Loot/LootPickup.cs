using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(Collider))]

    public class LootPickup : MonoBehaviour
    {
        [Header("Item Config")]
        [SerializeField] private LootItem lootItemData;

        [Header("Pickup Settings")]
        [SerializeField] private bool destroyOnPickup = true;
        [SerializeField] private LayerMask playerLayer;

        private void OnTriggerEnter(Collider other)
        {

            if ((playerLayer.value & (1 << other.gameObject.layer)) != 0 || other.CompareTag("Player"))
            {
                Pickup(other.gameObject);
            }
        }

        private void Pickup(GameObject player)
        {
            if (lootItemData == null)
            {
                Debug.LogWarning($"{name}: LootPickup not has the LootItem assigned.");
                return;
            }

            Debug.Log($"Item coletado: {lootItemData.itemName}");

            var playerInventory = player.GetComponent<PlayerInventory>();
            if (playerInventory != null)
            {
                playerInventory.AddItem(lootItemData);
            }

            if (destroyOnPickup)
            {
                Destroy(gameObject);
            }
        }
    }
}
