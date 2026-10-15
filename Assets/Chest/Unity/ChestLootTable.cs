using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepVain.Items
{
    /// <summary>
    /// What a chest can contain. One row per item: the chance in percent (0-100) that it is in the chest, and how many (min-max).
    /// Every row is rolled on its own, so 60 % Wood and 30 % Coal can give both, one or none.
    /// Change a number, or add / remove rows in the Inspector; nothing in code needs to change.
    /// Lag en ny: Project-vinduet > høyreklikk > Create > DeepVain > Chest Loot Table.
    /// </summary>
    [CreateAssetMenu(menuName = "DeepVain/Chest Loot Table", fileName = "NewChestLoot")]
    public class ChestLootTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public ItemData item;
            [Tooltip("Sjanse i prosent for at tingen ligger i kista. 100 = alltid, 0 = aldri.")]
            [Range(0f, 100f)] public float chance = 50f;
            [Tooltip("Minst så mange.")] public int minAmount = 1;
            [Tooltip("Mest så mange (like som min = alltid akkurat det antallet).")] public int maxAmount = 1;
        }

        public Entry[] entries;

        [Header("Regler")]
        [Tooltip("Mest så mange forskjellige ting i én kiste. 0 = ingen grense.")]
        public int maxDifferentItems = 0;
        [Tooltip("På: hvis ingenting ble trukket får kista likevel den tingen som har høyest sjanse (aldri tom kiste).")]
        public bool neverEmpty = true;

        /// <summary>Rolls the table once and returns what is in the chest.</summary>
        public List<Chest.LootEntry> Roll()
        {
            var result = new List<Chest.LootEntry>();
            if (entries == null) return result;

            foreach (var e in entries)
            {
                if (e == null || e.item == null) continue;
                if (UnityEngine.Random.value * 100f >= e.chance) continue;
                result.Add(Make(e));
            }

            if (result.Count == 0 && neverEmpty)
            {
                Entry best = null;
                foreach (var e in entries)
                    if (e != null && e.item != null && (best == null || e.chance > best.chance)) best = e;
                if (best != null) result.Add(Make(best));
            }

            if (maxDifferentItems > 0 && result.Count > maxDifferentItems)
            {
                for (int i = result.Count - 1; i > 0; i--)                       // shuffle, then keep the first ones
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    var t = result[i]; result[i] = result[j]; result[j] = t;
                }
                result.RemoveRange(maxDifferentItems, result.Count - maxDifferentItems);
            }
            return result;
        }

        static Chest.LootEntry Make(Entry e)
        {
            int lo = Mathf.Max(1, Mathf.Min(e.minAmount, e.maxAmount));
            int hi = Mathf.Max(lo, e.maxAmount);
            return new Chest.LootEntry { item = e.item, amount = UnityEngine.Random.Range(lo, hi + 1) };
        }
    }
}
