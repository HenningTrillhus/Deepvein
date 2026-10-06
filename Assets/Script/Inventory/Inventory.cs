using System;
using UnityEngine;

namespace DeepVain.Items
{
    [Serializable]
    public class InventorySlot
    {
        public ItemData item;
        public int count;
        public bool IsEmpty => item == null || count <= 0;
        public void Clear() { item = null; count = 0; }
    }

    public class Inventory : MonoBehaviour
    {
        [Header("Størrelse")]
        [SerializeField] int maxSlots = 35;      // største mulige antall (boka har 7 x 5)
        [SerializeField] int unlockedSlots = 24; // så mange er åpne nå

        [Header("Penger")]
        [SerializeField] int gold = 1284;

        public InventorySlot[] Slots { get; private set; }
        public int UnlockedSlots => unlockedSlots;
        public int Gold => gold;
        public event Action OnChanged;

        void Awake()
        {
            Slots = new InventorySlot[maxSlots];
            for (int i = 0; i < maxSlots; i++) Slots[i] = new InventorySlot();
        }

        // Returnerer hvor mange som IKKE fikk plass (0 = alt ble lagt til)
        public int Add(ItemData item, int amount)
        {
            if (item == null || amount <= 0) return amount;

            // 1) fyll opp eksisterende stacks først
            for (int i = 0; i < unlockedSlots && amount > 0; i++)
            {
                var s = Slots[i];
                if (s.IsEmpty || s.item != item) continue;
                int add = Mathf.Min(amount, item.maxStack - s.count);
                s.count += add; amount -= add;
            }
            // 2) deretter tomme slots
            for (int i = 0; i < unlockedSlots && amount > 0; i++)
            {
                var s = Slots[i];
                if (!s.IsEmpty) continue;
                int add = Mathf.Min(amount, item.maxStack);
                s.item = item; s.count = add; amount -= add;
            }
            OnChanged?.Invoke();
            return amount;
        }

        public bool Remove(int slotIndex, int amount = 1)
        {
            var s = Slots[slotIndex];
            if (s.IsEmpty || s.count < amount) return false;
            s.count -= amount;
            if (s.count <= 0) s.Clear();
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Hvor mange av dette itemet ligger i sekken totalt.</summary>
        public int Count(ItemData item)
        {
            if (item == null) return 0;
            int n = 0;
            for (int i = 0; i < unlockedSlots; i++)
                if (!Slots[i].IsEmpty && Slots[i].item == item) n += Slots[i].count;
            return n;
        }

        /// <summary>Tar bort et antall av et item (fra de bakerste stackene først).</summary>
        public bool RemoveItem(ItemData item, int amount = 1)
        {
            if (item == null || Count(item) < amount) return false;
            for (int i = unlockedSlots - 1; i >= 0 && amount > 0; i--)
            {
                var s = Slots[i];
                if (s.IsEmpty || s.item != item) continue;
                int take = Mathf.Min(amount, s.count);
                s.count -= take; amount -= take;
                if (s.count <= 0) s.Clear();
            }
            OnChanged?.Invoke();
            return true;
        }

        public void Swap(int a, int b)
        {
            if (a >= unlockedSlots || b >= unlockedSlots) return;
            var sa = Slots[a]; var sb = Slots[b];
            if (!sa.IsEmpty && !sb.IsEmpty && sa.item == sb.item)       // samme item: slå sammen
            {
                int move = Mathf.Min(sa.count, sa.item.maxStack - sb.count);
                sb.count += move; sa.count -= move;
                if (sa.count <= 0) sa.Clear();
            }
            else { var ti = sa.item; var tc = sa.count; sa.item = sb.item; sa.count = sb.count; sb.item = ti; sb.count = tc; }
            OnChanged?.Invoke();
        }

        public void UnlockSlots(int extra)
        {
            unlockedSlots = Mathf.Min(maxSlots, unlockedSlots + extra);
            OnChanged?.Invoke();
        }

        public void AddGold(int amount)
        {
            gold = Mathf.Max(0, gold + amount);
            OnChanged?.Invoke();
        }
    }
}
