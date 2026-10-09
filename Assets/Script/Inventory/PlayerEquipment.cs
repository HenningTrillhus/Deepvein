using System;
using UnityEngine;

namespace DeepVain.Items
{
    /// <summary>
    /// What the player wears and carries in hand. Put it on the Player (same object as Inventory).
    /// Slot index: 0 Helmet, 1 Chest, 2 Pants, 3 Boots, 4 Gloves, 5 Weapon (sword), 6 Shield, 7-8 Talisman, 9-10 Ring, 11 Axe, 12 Pickaxe, 13 Bow, 14 Arrows.
    /// Arrows are a stack: the whole stack goes into the arrow slot (GetCount). OnChanged is also where the visible armour layers hook in later.
    /// </summary>
    public class PlayerEquipment : MonoBehaviour
    {
        public const int Helmet = 0, Chest = 1, Pants = 2, Boots = 3, Gloves = 4, Weapon = 5, Shield = 6, TalismanA = 7, TalismanB = 8, RingA = 9, RingB = 10, Axe = 11, Pickaxe = 12, Bow = 13, Arrows = 14;
        public const int SlotCount = 15;
        public static readonly string[] SlotNames = { "Helmet", "Chest", "Pants", "Boots", "Gloves", "Weapon", "Shield", "Talisman", "Talisman", "Ring", "Ring", "Axe", "Pickaxe", "Bow", "Arrows" };

        readonly ItemData[] worn = new ItemData[SlotCount];
        readonly int[] counts = new int[SlotCount];
        public event Action OnChanged;

        public ItemData Get(int index) { return index >= 0 && index < SlotCount ? worn[index] : null; }
        public int GetCount(int index) { return index >= 0 && index < SlotCount && worn[index] != null ? Mathf.Max(1, counts[index]) : 0; }

        public int TotalDefence
        {
            get { int n = 0; for (int i = 0; i < SlotCount; i++) if (worn[i] != null) n += worn[i].defence; return n; }
        }

        public int TotalDamage { get { return worn[Weapon] != null ? worn[Weapon].damage : 0; } }
        public int BowDamage { get { return worn[Bow] != null ? worn[Bow].damage : 0; } }

        // ---- tools (used by the tree chopping and the mining later)
        public bool HasAxe { get { return worn[Axe] != null; } }
        public bool HasPickaxe { get { return worn[Pickaxe] != null; } }
        public int AxePower { get { return worn[Axe] != null ? Mathf.Max(1, worn[Axe].toolPower) : 0; } }
        public int PickaxePower { get { return worn[Pickaxe] != null ? Mathf.Max(1, worn[Pickaxe].toolPower) : 0; } }

        // ---- arrows (used by the bow later)
        public int ArrowCount { get { return counts[Arrows]; } }
        /// <summary>Takes one arrow. False when there are none.</summary>
        public bool ConsumeArrow()
        {
            if (worn[Arrows] == null || counts[Arrows] <= 0) return false;
            counts[Arrows]--;
            if (counts[Arrows] <= 0) { worn[Arrows] = null; counts[Arrows] = 0; }
            if (OnChanged != null) OnChanged();
            return true;
        }

        int FindSlot(ItemData item)
        {
            switch (item.equipSlot)
            {
                case EquipSlot.Helmet: return Helmet;
                case EquipSlot.Chest: return Chest;
                case EquipSlot.Pants: return Pants;
                case EquipSlot.Boots: return Boots;
                case EquipSlot.Gloves: return Gloves;
                case EquipSlot.Weapon: return Weapon;
                case EquipSlot.Shield: return Shield;
                case EquipSlot.Talisman: return worn[TalismanA] == null ? TalismanA : (worn[TalismanB] == null ? TalismanB : TalismanA);
                case EquipSlot.Ring: return worn[RingA] == null ? RingA : (worn[RingB] == null ? RingB : RingA);
                case EquipSlot.Axe: return Axe;
                case EquipSlot.Pickaxe: return Pickaxe;
                case EquipSlot.Bow: return Bow;
                case EquipSlot.Arrows: return Arrows;
                default: return -1;
            }
        }

        /// <summary>Takes ONE item from the inventory slot and wears it (arrows: the whole stack). What was worn there goes back into the bag.</summary>
        public bool EquipFromInventory(Inventory inv, int inventoryIndex)
        {
            if (inv == null || inventoryIndex < 0 || inventoryIndex >= inv.Slots.Length) return false;
            var s = inv.Slots[inventoryIndex];
            if (s.IsEmpty || !s.item.IsEquippable) return false;

            ItemData item = s.item;
            int target = FindSlot(item);
            if (target < 0) return false;

            // arrows: move the whole stack, and add to the stack that is already worn
            if (item.equipSlot == EquipSlot.Arrows)
            {
                int n = s.count;
                if (worn[target] == item)
                {
                    int take = Mathf.Min(n, Mathf.Max(0, item.maxStack - counts[target]));
                    if (take <= 0) return false;
                    inv.Remove(inventoryIndex, take);
                    counts[target] += take;
                    if (OnChanged != null) OnChanged();
                    return true;
                }
                ItemData oldA = worn[target]; int oldCount = counts[target];
                inv.Remove(inventoryIndex, n);
                worn[target] = item; counts[target] = n;
                if (oldA != null && inv.Add(oldA, oldCount) > 0)
                {
                    worn[target] = oldA; counts[target] = oldCount; inv.Add(item, n); return false;
                }
                if (OnChanged != null) OnChanged();
                return true;
            }

            ItemData old = worn[target];
            inv.Remove(inventoryIndex, 1);
            worn[target] = item; counts[target] = 1;
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
            int n = Mathf.Max(1, counts[slotIndex]);
            int left = inv.Add(item, n);                 // what did not fit
            if (left >= n) return false;                 // bag full
            counts[slotIndex] = left;
            if (left == 0) worn[slotIndex] = null;
            if (OnChanged != null) OnChanged();
            return left == 0;
        }
    }
}
