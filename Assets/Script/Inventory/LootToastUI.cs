using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeepVain.Items
{
    /// <summary>
    /// Small "you got ..." strips in the bottom right corner (leather strip, rarity colour behind the icon).
    /// New strips appear at the bottom, older ones are pushed up. Each one stays for about 3 seconds, then fades out.
    /// The same item picked up again while its strip is still there is merged into it (x3 becomes x5).
    /// Call LootToastUI.Show(item, amount) from anywhere (the chest does it). The builder creates the objects.
    /// </summary>
    public class LootToastUI : MonoBehaviour
    {
        public static LootToastUI Instance { get; private set; }

        [Header("Objekter (lages av byggeren)")]
        public RectTransform container;
        public RectTransform template;              // inactive strip that is copied
        public Sprite[] rarityBackgrounds = new Sprite[5];   // Common, Uncommon, Rare, Epic, Legendary
        public Sprite chipSprite;

        [Header("Oppførsel")]
        public float lifetime = 3f;
        public float fadeOutTime = 0.5f;
        public float gap = 3f;
        public int maxVisible = 6;

        public const float Width = 150f, Height = 30f;

        static readonly Color Cream = new Color32(0xF0, 0xDC, 0xB0, 255);
        static readonly Color Shadow = new Color32(0x1A, 0x0E, 0x08, 255);
        static readonly Color[] SubColors =
        {
            new Color32(0xB8, 0xA8, 0x88, 255), new Color32(0x5F, 0xD0, 0x68, 255), new Color32(0x4A, 0x9C, 0xF0, 255),
            new Color32(0xB0, 0x62, 0xE8, 255), new Color32(0xFF, 0xC9, 0x4A, 255)
        };

        class Entry
        {
            public ItemData item; public int amount;
            public RectTransform rt; public CanvasGroup group;
            public TMP_Text name, nameShadow, sub, subShadow, count, countShadow;
            public Image chip;
            public float age; public float x = 34f, y;
        }

        readonly List<Entry> entries = new List<Entry>();   // oldest first, newest last

        void Awake() { Instance = this; if (template != null) template.gameObject.SetActive(false); }
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Shows a strip for an item that was just picked up. Does nothing if there is no LootToastUI in the scene.</summary>
        public static void Show(ItemData item, int amount)
        {
            if (Instance != null) Instance.Add(item, amount);
        }

        public void Add(ItemData item, int amount)
        {
            if (item == null || amount <= 0 || template == null || container == null) return;

            // same item still showing: merge, restart the timer and move it to the bottom
            Entry e = entries.Find(x => x.item == item);
            if (e != null)
            {
                entries.Remove(e); entries.Add(e);
                e.amount += amount; e.age = 0f; e.x = 10f;
                Fill(e);
                return;
            }

            var go = Instantiate(template.gameObject, container);
            go.name = "Toast_" + item.displayName;
            go.SetActive(true);
            e = new Entry { item = item, amount = amount, rt = (RectTransform)go.transform };
            e.group = go.GetComponent<CanvasGroup>();
            e.name = Txt(go, "Name"); e.nameShadow = Txt(go, "NameShadow");
            e.sub = Txt(go, "Sub"); e.subShadow = Txt(go, "SubShadow");
            e.count = Txt(go, "Count"); e.countShadow = Txt(go, "CountShadow");
            var chip = go.transform.Find("Chip"); if (chip != null) e.chip = chip.GetComponent<Image>();
            e.y = StackY(0);
            entries.Add(e);
            Fill(e);
            while (entries.Count > maxVisible) Remove(entries[0]);
            e.group.alpha = 0f;
            Place(e);
        }

        static TMP_Text Txt(GameObject go, string child)
        {
            var t = go.transform.Find(child);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        static void Set(TMP_Text a, TMP_Text b, string s, Color col)
        {
            if (a != null) { a.text = s; a.color = col; }
            if (b != null) { b.text = s; b.color = Shadow; }
        }

        void Fill(Entry e)
        {
            var item = e.item;
            int r = Mathf.Clamp((int)item.rarity, 0, 4);
            var bg = e.rt.GetComponent<Image>();
            if (bg != null && rarityBackgrounds != null && r < rarityBackgrounds.Length && rarityBackgrounds[r] != null) bg.sprite = rarityBackgrounds[r];
            var icon = e.rt.Find("Icon");
            if (icon != null) { var img = icon.GetComponent<Image>(); img.sprite = item.icon; img.enabled = item.icon != null; }

            Set(e.name, e.nameShadow, item.displayName, Cream);
            Set(e.sub, e.subShadow, item.rarity + " - " + item.type, SubColors[r]);

            bool many = e.amount > 1;
            float chipW = 0f;
            if (e.count != null) e.count.enabled = many;
            if (e.countShadow != null) e.countShadow.enabled = many;
            if (e.chip != null) e.chip.enabled = many;
            if (many)
            {
                string label = "x" + e.amount;
                Set(e.count, e.countShadow, label, Cream);
                float tw = e.count != null ? e.count.GetPreferredValues(label).x : 14f;
                chipW = Mathf.Max(24f, Mathf.Ceil(tw) + 8f);
                SetBox(e.chip != null ? e.chip.rectTransform : null, Width - chipW - 8f, 8f, chipW, 13f);
                SetBox(e.count != null ? e.count.rectTransform : null, Width - chipW - 8f, 9f, chipW, 11f);
                SetBox(e.countShadow != null ? e.countShadow.rectTransform : null, Width - chipW - 8f, 10f, chipW, 11f);
            }
            float nameW = Width - 32f - (many ? chipW + 14f : 8f);
            SetBox(e.name != null ? e.name.rectTransform : null, 32f, 5f, nameW, 11f);
            SetBox(e.nameShadow != null ? e.nameShadow.rectTransform : null, 32f, 6f, nameW, 11f);
        }

        // top-left based box inside the strip (same numbers as the preview picture)
        static void SetBox(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        float StackY(int fromNewest) { return fromNewest * (Height + gap); }

        void Place(Entry e) { e.rt.anchoredPosition = new Vector2(e.x, e.y); }

        void Remove(Entry e)
        {
            entries.Remove(e);
            if (e.rt != null) Destroy(e.rt.gameObject);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float k = 1f - Mathf.Exp(-16f * dt);
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                e.age += dt;
                if (e.age >= lifetime) { Remove(e); continue; }

                int fromNewest = entries.Count - 1 - i;
                e.y = Mathf.Lerp(e.y, StackY(fromNewest), k);
                e.x = Mathf.Lerp(e.x, 0f, k);
                float a = Mathf.Clamp01(e.age / 0.15f);
                float left = lifetime - e.age;
                if (left < fadeOutTime) a = Mathf.Min(a, left / fadeOutTime);
                e.group.alpha = a;
                Place(e);
            }
        }
    }
}
