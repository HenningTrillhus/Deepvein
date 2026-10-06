using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeepVain.Items
{
    /// <summary>
    /// The book inventory. Put it on the canvas (NOT on the book window - that one is switched off when closed).
    /// Tab opens / closes. Left click = inspect (popup), right click = quick action (use / equip / unequip).
    /// All fields are filled in by DeepVain > Inventory > Build book UI.
    /// </summary>
    public class BookInventoryUI : MonoBehaviour
    {
        [Header("Data (on the Player)")]
        public Inventory inventory;
        public PlayerEquipment equipment;
        public QuickSlots quick;

        [Header("Book")]
        public GameObject window;
        public RectTransform bookRoot;
        public BookBackgroundClick backgroundClick;
        public InspectPopup popup;

        [Header("Slots")]
        public InventorySlotView[] gridViews;
        public InventorySlotView[] equipViews;
        public InventorySlotView[] hotViews;

        [Header("Texts")]
        public TMP_Text goldText;
        public TMP_Text slotsText;
        public TMP_Text defenceText;
        public TMP_Text attackText;

        [Header("Figure")]
        public Image figureImage;
        public Sprite[] figureFrames;
        public float figureFrameTime = 0.15f;

        enum Area { None, Grid, Equip, Hot }
        Area selArea = Area.None;
        int selIndex = -1;

        void Start()
        {
            if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
            if (equipment == null) equipment = FindFirstObjectByType<PlayerEquipment>();
            if (quick == null) quick = FindFirstObjectByType<QuickSlots>();

            Hook(gridViews, Area.Grid); Hook(equipViews, Area.Equip); Hook(hotViews, Area.Hot);
            if (backgroundClick != null) backgroundClick.clicked = ClearSelection;

            if (inventory != null) inventory.OnChanged += Refresh;
            if (equipment != null) equipment.OnChanged += Refresh;
            if (quick != null) quick.OnChanged += Refresh;

            window.SetActive(false);
            if (popup != null) popup.Hide();
            Refresh();
        }

        void OnDestroy()
        {
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (equipment != null) equipment.OnChanged -= Refresh;
            if (quick != null) quick.OnChanged -= Refresh;
        }

        void Hook(InventorySlotView[] views, Area area)
        {
            if (views == null) return;
            for (int i = 0; i < views.Length; i++)
            {
                views[i].Init(i);
                views[i].Clicked += (v, button) => OnClicked(area, v.Index, button);
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.tabKey.wasPressedThisFrame) SetOpen(!window.activeSelf);
                else if (kb.escapeKey.wasPressedThisFrame && window.activeSelf)
                {
                    if (selArea != Area.None) ClearSelection(); else SetOpen(false);
                }
            }

            if (window.activeSelf && figureImage != null && figureFrames != null && figureFrames.Length > 0)
                figureImage.sprite = figureFrames[(int)(Time.unscaledTime / Mathf.Max(0.02f, figureFrameTime)) % figureFrames.Length];
        }

        void SetOpen(bool open)
        {
            window.SetActive(open);
            if (!open) { selArea = Area.None; selIndex = -1; if (popup != null) popup.Hide(); }
            else Refresh();
        }

        // ------------------------------------------------------------ selection
        bool TryGet(Area area, int index, out ItemData item, out int amount, out InventorySlotView view)
        {
            item = null; amount = 0; view = null;
            switch (area)
            {
                case Area.Grid:
                    if (inventory == null || index < 0 || index >= gridViews.Length || index >= inventory.Slots.Length || index >= inventory.UnlockedSlots) return false;
                    var s = inventory.Slots[index];
                    if (s.IsEmpty) return false;
                    item = s.item; amount = s.count; view = gridViews[index]; return true;
                case Area.Equip:
                    if (equipment == null || index < 0 || index >= equipViews.Length) return false;
                    item = equipment.Get(index); amount = 1; view = equipViews[index]; return item != null;
                case Area.Hot:
                    if (quick == null || index < 0 || index >= hotViews.Length) return false;
                    item = quick.Get(index); amount = quick.CountOf(index); view = hotViews[index]; return item != null;
            }
            return false;
        }

        void OnClicked(Area area, int index, PointerEventData.InputButton button)
        {
            ItemData item; int amount; InventorySlotView view;
            if (!TryGet(area, index, out item, out amount, out view)) { ClearSelection(); return; }

            if (button == PointerEventData.InputButton.Right) { PrimaryAction(area, index, item); return; }
            if (selArea == area && selIndex == index) { ClearSelection(); return; }
            selArea = area; selIndex = index;
            Refresh();
        }

        public void ClearSelection()
        {
            selArea = Area.None; selIndex = -1;
            Refresh();
        }

        // ------------------------------------------------------------ actions
        void PrimaryAction(Area area, int index, ItemData item)
        {
            switch (area)
            {
                case Area.Grid:
                    if (item.type == ItemType.Consumable) quick.UseItem(item);
                    else if (item.IsEquippable) { equipment.EquipFromInventory(inventory, index); ClearSelection(); }
                    break;
                case Area.Equip: equipment.Unequip(index, inventory); ClearSelection(); break;
                case Area.Hot: quick.UseSlot(index); break;
            }
        }

        void DropFromGrid(int index)
        {
            var s = inventory.Slots[index];
            if (s.IsEmpty) return;
            bool all = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            int n = all ? s.count : 1;
            Debug.Log("[Inventory] Dropped " + n + " x " + s.item.displayName + " (spawn the item in the world here later)");
            inventory.Remove(index, n);
        }

        void ShowPopup(ItemData item, int amount, InventorySlotView view)
        {
            var labels = new List<string>(); var acts = new List<Action>();
            int idx = selIndex;
            switch (selArea)
            {
                case Area.Grid:
                    if (item.type == ItemType.Consumable)
                    {
                        labels.Add("Use"); acts.Add(() => quick.UseItem(item));
                        labels.Add("Hotkey"); acts.Add(() => { quick.Assign(item); ClearSelection(); });
                    }
                    if (item.IsEquippable) { labels.Add("Equip"); acts.Add(() => { equipment.EquipFromInventory(inventory, idx); ClearSelection(); }); }
                    labels.Add("Drop"); acts.Add(() => DropFromGrid(idx));
                    break;
                case Area.Equip:
                    labels.Add("Unequip"); acts.Add(() => { equipment.Unequip(idx, inventory); ClearSelection(); });
                    break;
                case Area.Hot:
                    labels.Add("Use"); acts.Add(() => quick.UseSlot(idx));
                    labels.Add("Clear"); acts.Add(() => { quick.Clear(idx); ClearSelection(); });
                    break;
            }
            popup.Show(item, amount, view.Rect, bookRoot, labels.ToArray(), k => { if (k < acts.Count) acts[k](); });
        }

        // ------------------------------------------------------------ drawing
        public void Refresh()
        {
            if (inventory == null || gridViews == null) return;

            int used = 0;
            for (int i = 0; i < gridViews.Length; i++)
            {
                bool inRange = i < inventory.Slots.Length;
                bool locked = !inRange || i >= inventory.UnlockedSlots;
                var slot = inRange ? inventory.Slots[i] : null;
                if (!locked && slot != null && !slot.IsEmpty) used++;
                gridViews[i].Show(slot, locked, selArea == Area.Grid && selIndex == i);
            }
            if (equipment != null)
                for (int i = 0; i < equipViews.Length; i++) equipViews[i].ShowItem(equipment.Get(i), 1, selArea == Area.Equip && selIndex == i);
            if (quick != null)
                for (int i = 0; i < hotViews.Length; i++)
                {
                    var it = quick.Get(i); int n = quick.CountOf(i);
                    hotViews[i].ShowItem(it, n, selArea == Area.Hot && selIndex == i, it != null && n == 0);
                }

            if (goldText != null) goldText.text = inventory.Gold.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');
            if (slotsText != null) slotsText.text = used + " / " + inventory.UnlockedSlots;
            if (defenceText != null && equipment != null) defenceText.text = equipment.TotalDefence.ToString();
            if (attackText != null && equipment != null) attackText.text = equipment.TotalDamage.ToString();

            // the popup follows the selection (and closes when the item is gone)
            ItemData item; int amount; InventorySlotView view;
            if (popup != null)
            {
                if (selArea != Area.None && window.activeSelf && TryGet(selArea, selIndex, out item, out amount, out view)) ShowPopup(item, amount, view);
                else { if (selArea != Area.None && !TryGetQuiet()) { selArea = Area.None; selIndex = -1; } popup.Hide(); }
            }
        }

        bool TryGetQuiet()
        {
            ItemData item; int amount; InventorySlotView view;
            return TryGet(selArea, selIndex, out item, out amount, out view);
        }
    }
}
