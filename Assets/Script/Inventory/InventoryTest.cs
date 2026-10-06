using UnityEngine;
using DeepVain.Items;

/// <summary>Only for testing: gives the player some items when the game starts. Delete it when you have real pickups.</summary>
public class InventoryTest : MonoBehaviour
{
    public ItemData[] items;
    public int[] amounts;

    void Start()
    {
        var inv = GetComponent<Inventory>();
        if (inv == null || items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            inv.Add(items[i], amounts != null && i < amounts.Length ? amounts[i] : 1);
        }
    }
}
