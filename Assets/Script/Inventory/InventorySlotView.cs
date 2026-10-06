using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepVain.Items
{
    /// <summary>One slot in the book: backpack, equipment or hotkey. The builder creates and wires all of them.</summary>
    public class InventorySlotView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Deler av sloten")]
        public Image background;
        public Image icon;
        public Image rarity;
        public Image glyph;                 // dimmed picture shown when an equipment slot is empty
        public TMP_Text count;
        public TMP_Text countShadow;

        [Header("Bakgrunner")]
        public Sprite emptySprite;
        public Sprite selectedSprite;
        public Sprite lockedSprite;

        [Header("Rarity (Uncommon, Rare, Epic, Legendary)")]
        public Sprite[] raritySprites = new Sprite[4];
        public Sprite[] raritySelSprites = new Sprite[4];   // same, but one pixel further in so the gold frame stays visible

        public int Index { get; private set; }
        public RectTransform Rect { get { return (RectTransform)transform; } }
        public event Action<InventorySlotView, PointerEventData.InputButton> Clicked;

        public void Init(int index) { Index = index; }

        /// <summary>Backpack slot.</summary>
        public void Show(InventorySlot slot, bool locked, bool selected)
        {
            bool has = !locked && slot != null && !slot.IsEmpty;
            Apply(has ? slot.item : null, has ? slot.count : 0, locked, selected, false);
        }

        /// <summary>Equipment / hotkey slot.</summary>
        public void ShowItem(ItemData item, int amount, bool selected, bool dim = false)
        {
            Apply(item, amount, false, selected, dim);
        }

        void Apply(ItemData item, int amount, bool locked, bool selected, bool dim)
        {
            background.sprite = locked ? lockedSprite : selected ? selectedSprite : emptySprite;
            bool has = item != null;

            icon.enabled = has;
            if (has) { icon.sprite = item.icon; icon.color = dim ? new Color(1f, 1f, 1f, 0.35f) : Color.white; }

            if (glyph != null) glyph.enabled = !has && !locked && glyph.sprite != null;

            // Common = ingen ramme
            int r = has ? (int)item.rarity - 1 : -1;
            var set = selected ? raritySelSprites : raritySprites;
            bool showRar = r >= 0 && r < set.Length && set[r] != null;
            rarity.enabled = showRar;
            if (showRar) rarity.sprite = set[r];

            bool showCount = has && amount > 1;
            SetCount(count, showCount, amount);
            SetCount(countShadow, showCount, amount);
        }

        static void SetCount(TMP_Text t, bool show, int amount)
        {
            if (t == null) return;
            t.enabled = show;
            if (show) t.text = amount.ToString();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (Clicked != null) Clicked(this, e.button);
        }
    }
}
