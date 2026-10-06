using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepVain.Items
{
    /// <summary>
    /// The four hotkey slots (Q E R F). A slot remembers WHICH item it holds; using it takes one from the bag.
    /// Put it on the Player (same object as Inventory and PlayerHealth).
    /// </summary>
    public class QuickSlots : MonoBehaviour
    {
        public const int Count = 4;
        public Key[] keys = { Key.Q, Key.E, Key.R, Key.F };

        Inventory inventory;
        PlayerHealth health;
        readonly ItemData[] items = new ItemData[Count];
        float nextUse;

        public event Action OnChanged;

        void Awake()
        {
            inventory = GetComponent<Inventory>();
            health = GetComponent<PlayerHealth>();
        }

        public ItemData Get(int i) { return i >= 0 && i < Count ? items[i] : null; }
        public int CountOf(int i) { return items[i] == null || inventory == null ? 0 : inventory.Count(items[i]); }

        /// <summary>Puts the item in the slot that already holds it, else the first empty one, else slot 0.</summary>
        public void Assign(ItemData item)
        {
            if (item == null) return;
            int target = -1;
            for (int i = 0; i < Count; i++) if (items[i] == item) { target = i; break; }
            if (target < 0) for (int i = 0; i < Count; i++) if (items[i] == null) { target = i; break; }
            if (target < 0) target = 0;
            items[target] = item;
            if (OnChanged != null) OnChanged();
        }

        public void Clear(int i)
        {
            if (i < 0 || i >= Count) return;
            items[i] = null;
            if (OnChanged != null) OnChanged();
        }

        public bool UseSlot(int i) { return UseItem(Get(i)); }

        /// <summary>Drinks / uses one. Returns false if it could not be used (nothing to heal, empty, on cooldown ...).</summary>
        public bool UseItem(ItemData item)
        {
            if (item == null || inventory == null) return false;
            if (item.type != ItemType.Consumable) return false;
            if (Time.time < nextUse) return false;
            if (inventory.Count(item) <= 0) return false;

            if (item.healAmount > 0)
            {
                if (health == null || health.IsDead || health.Health >= health.MaxHealth) return false;
                health.Heal(item.healAmount);
            }

            inventory.RemoveItem(item, 1);
            nextUse = Time.time + Mathf.Max(0.1f, item.useTime);
            if (OnChanged != null) OnChanged();
            return true;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            for (int i = 0; i < Count && i < keys.Length; i++)
                if (kb[keys[i]].wasPressedThisFrame) UseSlot(i);
        }
    }
}
