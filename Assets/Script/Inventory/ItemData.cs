using UnityEngine;

namespace DeepVain.Items
{
    public enum ItemType { Weapon, Shield, Consumable, Material, Quest }
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

    [CreateAssetMenu(menuName = "DeepVain/Item", fileName = "NewItem")]
    public class ItemData : ScriptableObject
    {
        [Header("Generelt")]
        public string id;                       // unikt navn, brukes til lagring, f.eks. "potion_heal"
        public string displayName;
        public Sprite icon;                     // 30x30 ikonet
        public ItemType type;
        public ItemRarity rarity;
        [TextArea(2, 4)] public string description;

        [Header("Stack")]
        [Tooltip("1 = kan ikke stakkes. Bruk 10, 50 eller 100.")]
        public int maxStack = 1;
        public int sellValue;

        [Header("Våpen / skjold")]
        public int damage;
        public float attackSpeed = 1f;
        public float blockAmount;

        [Header("Forbruk")]
        public int healAmount;
        public float useTime = 0.8f;
    }
}