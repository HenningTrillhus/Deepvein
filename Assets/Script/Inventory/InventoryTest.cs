using UnityEngine;
using DeepVain.Items;

public class InventoryTest : MonoBehaviour
{
    public ItemData testItem;
    public int amount = 12;

    void Start()
    {
        var inv = GetComponent<Inventory>();
        int left = inv.Add(testItem, amount);
        Debug.Log($"La til {amount} x {testItem.displayName}, {left} fikk ikke plass");
        for (int i = 0; i < 3; i++) Debug.Log($"Slot {i}: {inv.Slots[i].item?.displayName} x{inv.Slots[i].count}");
    }
}