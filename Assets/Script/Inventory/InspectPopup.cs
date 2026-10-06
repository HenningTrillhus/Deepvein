using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeepVain.Items
{
    /// <summary>
    /// The inspect popup that opens under the selected slot. Everything is positioned in code, in "book pixels"
    /// (640 x 360, origin top-left) - the same numbers as in BookUI/README.txt. The builder creates the objects.
    /// Put this on the popup root object.
    /// </summary>
    public class InspectPopup : MonoBehaviour
    {
        public RectTransform arrow, divider1, divider2;
        public InventorySlotView iconSlot;
        public TMP_Text nameText, typeText, rarityText, descText;
        public Image[] statIcons = new Image[3];
        public TMP_Text[] statLabels = new TMP_Text[3];
        public TMP_Text[] statValues = new TMP_Text[3];
        public Button[] buttons = new Button[3];
        public TMP_Text[] buttonTexts = new TMP_Text[3];

        [Header("Sprites")]
        public Sprite damageIcon, blockIcon, speedIcon, healIcon, coinIcon;
        public Sprite brass, brassHover, leather, leatherHover;

        public const float Width = 196f;
        static readonly Color[] RarityColors =
        {
            new Color32(0x6A, 0x52, 0x38, 255), new Color32(0x2A, 0x8A, 0x40, 255), new Color32(0x2A, 0x5A, 0xB8, 255),
            new Color32(0x8A, 0x3A, 0xB0, 255), new Color32(0xB0, 0x70, 0x08, 255)
        };

        struct Row { public Sprite icon; public string label, value; }
        Action<int> onButton;
        bool wired;

        RectTransform Root { get { return (RectTransform)transform; } }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        void Wire()
        {
            if (wired) return;
            wired = true;
            for (int i = 0; i < buttons.Length; i++)
            {
                int k = i;
                buttons[i].onClick.AddListener(() => { if (onButton != null) onButton(k); });
            }
        }

        static Row R(Sprite icon, string label, string value) { Row r; r.icon = icon; r.label = label; r.value = value; return r; }

        List<Row> BuildRows(ItemData item)
        {
            var rows = new List<Row>();
            switch (item.type)
            {
                case ItemType.Weapon:
                    if (item.damage > 0) rows.Add(R(damageIcon, "Damage", item.damage.ToString()));
                    rows.Add(R(speedIcon, "Attack speed", item.attackSpeed.ToString("0.0", CultureInfo.InvariantCulture) + " /s"));
                    if (item.defence > 0) rows.Add(R(blockIcon, "Defence", item.defence.ToString()));
                    break;
                case ItemType.Shield:
                    if (item.blockAmount > 0f) rows.Add(R(blockIcon, "Block", item.blockAmount.ToString("0.#", CultureInfo.InvariantCulture)));
                    if (item.defence > 0) rows.Add(R(blockIcon, "Defence", item.defence.ToString()));
                    break;
                case ItemType.Armor:
                case ItemType.Talisman:
                    if (item.defence > 0) rows.Add(R(blockIcon, "Defence", item.defence.ToString()));
                    break;
                case ItemType.Consumable:
                    if (item.healAmount > 0) rows.Add(R(healIcon, "Restores", "+" + item.healAmount + " HP"));
                    rows.Add(R(speedIcon, "Use time", item.useTime.ToString("0.0", CultureInfo.InvariantCulture) + " s"));
                    break;
            }
            if (item.sellValue > 0 && rows.Count < 3) rows.Add(R(coinIcon, "Value", item.sellValue.ToString()));
            if (rows.Count > 3) rows.RemoveRange(3, rows.Count - 3);
            return rows;
        }

        /// <summary>Shows the popup for an item, under the slot it belongs to.</summary>
        public void Show(ItemData item, int amount, RectTransform slot, RectTransform bookRoot, string[] labels, Action<int> onClick)
        {
            Wire();
            onButton = onClick;
            gameObject.SetActive(true);

            var rows = BuildRows(item);
            int n = rows.Count;
            float h = 64f + 14f * n + 70f;

            // ---- header
            iconSlot.ShowItem(item, 1, false);
            Color rc = RarityColors[Mathf.Clamp((int)item.rarity, 0, RarityColors.Length - 1)];
            nameText.text = item.displayName; nameText.color = rc;
            typeText.text = item.type + (item.maxStack > 1 ? "   " + amount + " / " + item.maxStack : "");
            rarityText.text = item.rarity.ToString(); rarityText.color = rc;

            // ---- stat rows
            for (int i = 0; i < 3; i++)
            {
                bool on = i < n;
                statIcons[i].gameObject.SetActive(on); statLabels[i].gameObject.SetActive(on); statValues[i].gameObject.SetActive(on);
                if (!on) continue;
                float ry = 64f + i * 14f;
                statIcons[i].sprite = rows[i].icon; statIcons[i].enabled = rows[i].icon != null;
                statLabels[i].text = rows[i].label; statValues[i].text = rows[i].value;
                Place(statIcons[i].rectTransform, 10f, ry - 5f, 11f, 11f);
                Place(statLabels[i].rectTransform, 26f, ry - 6f, 100f, 12f);
                Place(statValues[i].rectTransform, Width - 100f, ry - 6f, 90f, 12f);
            }
            float ys = 64f + 14f * n + 2f;
            Place(divider2, 10f, ys, Width - 20f, 2f);
            descText.text = item.description;
            Place(descText.rectTransform, 14f, ys + 8f, Width - 28f, 30f);

            // ---- buttons
            int count = Mathf.Min(labels.Length, 3);
            float bw = (Width - 20f - 4f * (count - 1)) / Mathf.Max(1, count);
            for (int i = 0; i < 3; i++)
            {
                bool on = i < count;
                buttons[i].gameObject.SetActive(on);
                if (!on) continue;
                Place((RectTransform)buttons[i].transform, 10f + i * (bw + 4f), h - 28f, bw, 17f);
                buttonTexts[i].text = labels[i];
                var img = (Image)buttons[i].targetGraphic;
                bool primary = i == 0 && labels[0] != "Drop" && labels[0] != "Clear";
                img.sprite = primary ? brass : leather;
                var st = new SpriteState();
                st.highlightedSprite = primary ? brassHover : leatherHover;
                st.pressedSprite = primary ? brassHover : leatherHover;
                st.selectedSprite = primary ? brass : leather;
                buttons[i].spriteState = st;
            }

            // ---- position: under the slot, kept inside the book. If there is no room below, it goes above.
            var corners = new Vector3[4];
            slot.GetWorldCorners(corners);                                   // 0 bottom-left, 1 top-left
            Vector3 tl = bookRoot.InverseTransformPoint(corners[1]);
            float sx = tl.x - bookRoot.rect.xMin;
            float sy = bookRoot.rect.yMax - tl.y;

            float px = Mathf.Clamp(sx - 70f, 8f, 640f - 8f - Width);
            float py = sy + 46f;
            bool above = false;
            if (py + h > 352f) { py = Mathf.Max(8f, sy - h - 10f); above = true; }
            Place(Root, px, py, Width, h);

            float ax = Mathf.Clamp(sx + 12f - px, 8f, Width - 21f);
            if (!above) { Place(arrow, ax, -8f, 13f, 9f); arrow.localScale = Vector3.one; }
            else { Place(arrow, ax, h + 9f, 13f, 9f); arrow.localScale = new Vector3(1f, -1f, 1f); }
            Place(divider1, 10f, 54f, Width - 20f, 2f);
            Root.SetAsLastSibling();
        }

        public void Hide() { gameObject.SetActive(false); }
    }
}
