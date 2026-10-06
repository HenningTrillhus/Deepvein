using System;
using UnityEngine;

namespace DeepVain.Items
{
    /// <summary>
    /// What the player wears. Put it on the Player (same object as Inventory).
    /// Slot index: 0 Helmet, 1 Chest, 2 Pants, 3 Boots, 4 Gloves, 5 Weapon, 6 Shield, 7 Talisman, 8 Talisman.
    /// OnChanged is also where the visible armour layers will hook in later.
    /// </summary>
    public class PlayerEquipment : MonoBehaviour
    {
        public const int SlotCount = 9;
        public static readonly string[] SlotNames = { "Helmet", "Chest", "Pants", "Boots", "Gloves", "Weapon", "Shield", "Talisman", "Talisman" };

        readonly ItemData[] worn = new ItemData[SlotCount];
        public event Action OnChanged;

        public ItemData Get(int index) { return index >= 0 && index < SlotCount ? worn[index] : null; }

        public int TotalDefence
        {
            get { int n = 0; for (int i = 0; i < SlotCount; i++) if (worn[i] != null) n += worn[i].defence; return n; }
        }

        public int TotalDamage
        {
            get { return worn[5] != null ? worn[5].damage : 0; }
        }

        int FindSlot(ItemData item)
        {
            switch (item.equipSlot)
            {
                case EquipSlot.Helmet: return 0;
                case EquipSlot.Chest: return 1;
                case EquipSlot.Pants: return 2;
                case EquipSlot.Boots: return 3;
                case EquipSlot.Gloves: return 4;
                case EquipSlot.Weapon: return 5;
                case EquipSlot.Shield: return 6;
                case EquipSlot.Talisman: return worn[7] == null ? 7 : (worn[8] == null ? 8 : 7);
                default: return -1;
            }
        }

        /// <summary>Takes ONE item from the inventory slot and wears it. What was worn there goes back into the bag.</summary>
        public bool EquipFromInventory(Inventory inv, int inventoryIndex)
        {
            if (inv == null || inventoryIndex < 0 || inventoryIndex >= inv.Slots.Length) return false;
            var s = inv.Slots[inventoryIndex];
            if (s.IsEmpty || !s.item.IsEquippable) return false;

            ItemData item = s.item;
            int target = FindSlot(item);
            if (target < 0) return false;
            ItemData old = worn[target];

            inv.Remove(inventoryIndex, 1);
            worn[target] = item;
            if (old != null && inv.Add(old, 1) > 0)
            {
                // no room for the old piece: undo
                worn[target] = old;
                inv.Add(item, 1);
                return false;
            }
            if (OnChanged != null) OnChanged();
            return true;
        }

        public bool Unequip(int slotIndex, Inventory inv)
        {
            ItemData item = Get(slotIndex);
            if (item == null || inv == null) return false;
            if (inv.Add(item, 1) > 0) return false;      // bag full
            worn[slotIndex] = null;
            if (OnChanged != null) OnChanged();
            return true;
        }
    }
}
