using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepVain.Items
{
    public class InventorySlotView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Deler av sloten")]
        public Image background;
        public Image icon;
        public Image rarity;
        public TMP_Text count;

        [Header("Bakgrunner")]
        public Sprite emptySprite;
        public Sprite selectedSprite;
        public Sprite lockedSprite;

        [Header("Rarity (Uncommon, Rare, Epic, Legendary)")]
        public Sprite[] raritySprites = new Sprite[4];

        public int Index { get; private set; }
        public event Action<int> Clicked;

        public void Init(int index) { Index = index; }

        public void Show(InventorySlot slot, bool locked, bool selected)
        {
            background.sprite = locked ? lockedSprite : selected ? selectedSprite : emptySprite;
            bool has = !locked && !slot.IsEmpty;

            icon.enabled = has;
            if (has) icon.sprite = slot.item.icon;

            // Common = ingen ramme
            int r = has ? (int)slot.item.rarity - 1 : -1;
            rarity.enabled = r >= 0 && r < raritySprites.Length;
            if (rarity.enabled) rarity.sprite = raritySprites[r];

            count.enabled = has && slot.count > 1;
            if (count.enabled) count.text = slot.count.ToString();
        }

        public void OnPointerClick(PointerEventData e) { Clicked?.Invoke(Index); }
    }
}