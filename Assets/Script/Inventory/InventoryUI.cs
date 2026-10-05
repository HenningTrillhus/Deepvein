using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepVain.Items
{
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] Inventory inventory;
        [SerializeField] GameObject window;
        [SerializeField] Transform grid;
        [SerializeField] InventorySlotView slotPrefab;
        [SerializeField] ItemDetailPanel detail;

        InventorySlotView[] views;
        int selected = -1;

        void Start()
        {
            int n = inventory.Slots.Length;
            views = new InventorySlotView[n];
            for (int i = 0; i < n; i++)
            {
                var v = Instantiate(slotPrefab, grid);
                v.name = "Slot_" + i;
                v.Init(i);
                v.Clicked += OnSlotClicked;
                views[i] = v;
            }
            inventory.OnChanged += Refresh;
            Refresh();
            window.SetActive(false);
        }

        void OnDestroy() { if (inventory != null) inventory.OnChanged -= Refresh; }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                window.SetActive(!window.activeSelf);
                if (window.activeSelf) Refresh();
            }
        }

        void OnSlotClicked(int index)
        {
            selected = (selected == index) ? -1 : index;
            Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < views.Length; i++)
                views[i].Show(inventory.Slots[i], i >= inventory.UnlockedSlots, i == selected);
            if (detail != null) detail.Show(selected >= 0 && selected < inventory.UnlockedSlots ? inventory.Slots[selected] : null);
        }
    }
}