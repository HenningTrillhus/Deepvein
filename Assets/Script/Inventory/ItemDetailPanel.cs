using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeepVain.Items
{
    public class ItemDetailPanel : MonoBehaviour
    {
        [Header("Visning")]
        [SerializeField] GameObject content;
        [SerializeField] GameObject emptyText;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text nameText, typeText, rarityText, statsText, descriptionText, infoText;

        static readonly Color[] RarityColors =
        {
            new Color32(190, 190, 200, 255),   // Common
            new Color32(95, 208, 104, 255),    // Uncommon
            new Color32(110, 180, 255, 255),   // Rare
            new Color32(176, 98, 232, 255),    // Epic
            new Color32(255, 201, 74, 255),    // Legendary
        };

        public void Show(InventorySlot slot)
        {
            bool has = slot != null && !slot.IsEmpty;
            content.SetActive(has);
            emptyText.SetActive(!has);
            if (!has) return;

            var it = slot.item;
            Color rc = RarityColors[(int)it.rarity];

            icon.sprite = it.icon;
            nameText.text = it.displayName;
            nameText.color = rc;
            typeText.text = it.type.ToString();
            rarityText.text = it.rarity.ToString();
            rarityText.color = rc;
            descriptionText.text = it.description;

            var s = new StringBuilder();
            if (it.type == ItemType.Weapon)
            {
                s.AppendLine("Damage: " + it.damage);
                s.AppendLine("Attack speed: " + it.attackSpeed.ToString("0.0") + " /s");
            }
            else if (it.type == ItemType.Shield)
            {
                s.AppendLine("Block: " + it.blockAmount);
            }
            else if (it.type == ItemType.Consumable)
            {
                if (it.healAmount > 0) s.AppendLine("Restores: +" + it.healAmount + " HP");
                s.AppendLine("Use time: " + it.useTime.ToString("0.0") + " s");
            }
            statsText.text = s.ToString();

            var info = new StringBuilder();
            info.AppendLine("Stack: " + slot.count + " / " + it.maxStack);
            info.AppendLine("Sell value: " + it.sellValue);
            infoText.text = info.ToString();
        }
    }
}