using UnityEngine;

namespace DeepVain.Items
{
    // Nye verdier er lagt til på slutten, så gamle items beholder riktig type.
    public enum ItemType { Weapon, Shield, Consumable, Material, Quest, Armor, Talisman, Tool, Ring, Ammo }
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }
    public enum EquipSlot { None, Helmet, Chest, Pants, Boots, Gloves, Weapon, Shield, Talisman, Ring, Axe, Pickaxe, Bow, Arrows }

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

        [Header("Utstyr")]
        [Tooltip("Hvor den kan tas på. None = kan ikke utstyres.")]
        public EquipSlot equipSlot = EquipSlot.None;
        public int defence;

        [Header("Verktøy (øks og hakke)")]
        [Tooltip("Hvor mye skade ett hogg gjør på trær (øks) eller stein (hakke).")]
        public int toolPower;

        [Header("Forbruk")]
        public int healAmount;
        public float useTime = 0.8f;

        public bool IsEquippable { get { return equipSlot != EquipSlot.None; } }
    }
}
