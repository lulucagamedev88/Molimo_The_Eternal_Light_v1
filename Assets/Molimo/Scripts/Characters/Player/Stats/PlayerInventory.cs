using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class PlayerInventory : MonoBehaviour
    {
        [Header("Inventory Data")]
        [SerializeField] private List<LootItem> items = new List<LootItem>();

        public void AddItem(LootItem item)
        {
            if (item == null) return;

            items.Add(item);
            Debug.Log($"[inventory] Adicionado: {item.itemName}");
        }

        public bool RemoveItem(LootItem item)
        {
            return items.Remove(item);
        }

        public List<LootItem> GetItems()
        {
            return items;
        }
    }
}
