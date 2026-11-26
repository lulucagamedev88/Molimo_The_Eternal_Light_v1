using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [CreateAssetMenu(
        fileName = "NewLootItem",
        menuName = "Molimo/Loot/Loot Item"
    )]

    public class LootItem : ScriptableObject
    {
        [Header("Item Information")]
        public string itemName;

        [TextArea]
        public string description;

        [Header("visual")]
        public Sprite icon;

        [Header("World")]
        public GameObject itemPrefab;
    }
}
