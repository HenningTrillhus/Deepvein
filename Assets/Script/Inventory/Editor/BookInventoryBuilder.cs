#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DeepVain.Items.EditorTools
{
    /// <summary>
    /// Menu: DeepVain > Inventory > ...   (equipment page: layout 5, 15 slots incl. rings, axe, pickaxe, bow and arrows)
    ///  1  Prepare sprites      sets the import settings (Sprite, Point, PPU 1) and the 9-slice borders of every picture in Assets/Inventory/BookUI
    ///  2  Create starter items potions, materials, starter sword and shield (+ puts them in the test script on the Player)
    ///  3  Build book UI        creates the whole book (canvas, slots, hotkeys, popup, texts) in the open scene and wires it up
    /// All positions are the numbers from BookUI/README.txt (640 x 360, origin top-left).
    /// </summary>
    public static class BookInventoryBuilder
    {
        const string Folder = "Assets/Inventory/BookUI";
        const string ItemsFolder = "Assets/Inventory/Items";

        static readonly Color Ink = new Color32(0x46, 0x28, 0x1A, 255);
        static readonly Color Dim = new Color32(0x8A, 0x64, 0x40, 255);
        static readonly Color Title = new Color32(0x4A, 0x28, 0x14, 255);
        static readonly Color Cream = new Color32(0xF0, 0xDC, 0xB0, 255);
        static readonly Color ShadowCol = new Color32(0x2A, 0x1A, 0x10, 255);

        // ------------------------------------------------------------------ 1) sprites
        [MenuItem("DeepVain/Inventory/1 - Prepare sprites")]
        public static void PrepareSprites()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { EditorUtility.DisplayDialog("Book UI", "Fant ikke mappen " + Folder + ".\nKopier BookUI-mappen dit først.", "OK"); return; }

            AssetDatabase.StartAssetEditing();
            int n = 0;
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) continue;
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.spritePixelsPerUnit = 1f;
                    imp.filterMode = FilterMode.Point;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    imp.spriteBorder = BorderFor(Path.GetFileNameWithoutExtension(path));
                    imp.SaveAndReimport();
                    n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Debug.Log("[Book UI] Sprite-innstillinger satt på " + n + " bilder.");
        }

        static Vector4 BorderFor(string name)
        {
            float b = 0f;
            switch (name)
            {
                case "Popup_Frame": case "Button_Brass": case "Button_Leather": case "Button_Brass_Hover": case "Button_Leather_Hover": b = 4f; break;
                case "Panel_Inset": b = 3f; break;
            }
            return new Vector4(b, b, b, b);
        }

        static Sprite Sp(string rel)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + rel);
            if (s == null) Debug.LogWarning("[Book UI] Mangler bilde: " + Folder + "/" + rel);
            return s;
        }

        // ------------------------------------------------------------------ 2) starter items
        static ItemData MakeItem(string file, string id, string display, ItemType type, ItemRarity rarity, string iconRel, int maxStack, int sell, string desc, Action<ItemData> extra)
        {
            if (!AssetDatabase.IsValidFolder(ItemsFolder)) AssetDatabase.CreateFolder("Assets/Inventory", "Items");
            string path = ItemsFolder + "/" + file + ".asset";
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (it == null) { it = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(it, path); }
            it.id = id; it.displayName = display; it.type = type; it.rarity = rarity; it.maxStack = maxStack; it.sellValue = sell; it.description = desc;
            it.icon = Sp("Items/" + iconRel + ".png");
            if (extra != null) extra(it);
            EditorUtility.SetDirty(it);
            return it;
        }

        [MenuItem("DeepVain/Inventory/2 - Create starter items")]
        public static void CreateStarterItems()
        {
            PrepareSprites();
            var potion = MakeItem("Potion_Heal", "potion_heal", "Healing Potion", ItemType.Consumable, ItemRarity.Common, "Potion_Heal", 10, 12, "A small red brew. Closes minor wounds.", it => { it.healAmount = 3; it.useTime = 0.8f; });
            var big = MakeItem("Potion_HealBig", "potion_heal_big", "Greater Potion", ItemType.Consumable, ItemRarity.Rare, "Potion_HealBig", 10, 60, "Thick purple brew from glow mushrooms. Closes deep wounds in seconds.", it => { it.healAmount = 6; it.useTime = 0.8f; });
            var wood = MakeItem("Wood", "wood", "Wood", ItemType.Material, ItemRarity.Common, "Wood", 100, 1, "Logs from the forest.", null);
            var stone = MakeItem("Stone", "stone", "Stone", ItemType.Material, ItemRarity.Common, "Stone", 50, 2, "A grey rock.", null);
            var iron = MakeItem("Iron_Bar", "iron_bar", "Iron Bar", ItemType.Material, ItemRarity.Uncommon, "Iron_Bar", 50, 8, "Smelted iron, ready for the forge.", null);
            var bone = MakeItem("Bone", "bone", "Bone", ItemType.Material, ItemRarity.Common, "Bone", 50, 1, "Left behind by something unlucky.", null);
            var sword = MakeItem("Sword_Starter", "sword_starter", "Starter Sword", ItemType.Weapon, ItemRarity.Common, "Sword_Starter", 1, 18, "A plain iron blade, sharp enough for goblins. Fast to swing, light in the hand.",
                it => { it.damage = 12; it.attackSpeed = 1.2f; it.equipSlot = EquipSlot.Weapon; });
            var axeItem = MakeItem("Axe_Starter", "axe_starter", "Starter Axe", ItemType.Tool, ItemRarity.Common, "Axe_Starter", 1, 10, "A plain iron axe. Fells small trees in a few swings.",
                it => { it.toolPower = 5; it.equipSlot = EquipSlot.Axe; });
            var pickItem = MakeItem("Pickaxe_Starter", "pickaxe_starter", "Starter Pickaxe", ItemType.Tool, ItemRarity.Common, "Pickaxe_Starter", 1, 10, "A plain iron pickaxe. Good for stone and ore.",
                it => { it.toolPower = 5; it.equipSlot = EquipSlot.Pickaxe; });
            var shield = MakeItem("Shield_Starter", "shield_starter", "Starter Shield", ItemType.Shield, ItemRarity.Common, "Shield_Starter", 1, 14, "Wood and iron. It has stopped a few arrows.",
                it => { it.blockAmount = 40f; it.defence = 3; it.equipSlot = EquipSlot.Shield; });
            AssetDatabase.SaveAssets();

            // give them to the player when the game starts (test script on the Player)
            var inv = UnityEngine.Object.FindFirstObjectByType<Inventory>();
            if (inv != null)
            {
                var test = inv.GetComponent<InventoryTest>();
                if (test == null) test = Undo.AddComponent<InventoryTest>(inv.gameObject);
                Undo.RecordObject(test, "Starter items");
                test.items = new[] { sword, shield, axeItem, pickItem, potion, big, wood, stone, iron, bone };
                test.amounts = new[] { 1, 1, 1, 1, 7, 3, 100, 50, 23, 12 };
                EditorUtility.SetDirty(test);
                EditorSceneManager.MarkSceneDirty(inv.gameObject.scene);
            }
            Debug.Log("[Book UI] Startitems laget i " + ItemsFolder + (inv != null ? " og lagt i InventoryTest på Player." : ". (Fant ingen Inventory i scenen.)"));
        }

        // ------------------------------------------------------------------ 3) build the book
        static RectTransform NewRT(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static Image NewImage(string name, Transform parent, Sprite s, float x, float y, float w, float h, bool raycast = false, bool sliced = false)
        {
            var rt = NewRT(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s; img.raycastTarget = raycast; img.type = sliced ? Image.Type.Sliced : Image.Type.Simple; img.enabled = s != null;
            Place(rt, x, y, w, h);
            return img;
        }

        static TMP_Text NewText(string name, Transform parent, string text, float x, float y, float w, float h, TextAlignmentOptions align, Color col, float size = 8f, bool bold = false, bool wrap = false)
        {
            var rt = NewRT(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.raycastTarget = false;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            if (bold) t.fontStyle = FontStyles.Bold;
            Place(rt, x, y, w, h);
            return t;
        }

        // centre point version (for headings)
        static TMP_Text NewTextC(string name, Transform parent, string text, float cx, float cy, float w, float h, Color col, bool bold = true)
        {
            return NewText(name, parent, text, cx - w / 2f, cy - h / 2f, w, h, TextAlignmentOptions.Center, col, 8f, bold);
        }

        static InventorySlotView NewSlot(string name, Transform parent, float x, float y, Sprite glyph)
        {
            var bg = NewImage(name, parent, Sp("Slots/Slot_Empty.png"), x, y, 36f, 36f, true);
            var view = bg.gameObject.AddComponent<InventorySlotView>();
            view.background = bg;
            view.glyph = NewImage("Glyph", bg.transform, glyph, 3f, 3f, 30f, 30f);
            view.icon = NewImage("Icon", bg.transform, null, 3f, 3f, 30f, 30f); view.icon.preserveAspect = true;
            view.rarity = NewImage("Rarity", bg.transform, null, 0f, 0f, 36f, 36f);
            view.countShadow = NewText("CountShadow", bg.transform, "", 0f, 0f, 36f, 36f, TextAlignmentOptions.BottomRight, ShadowCol, 8f, true);
            view.count = NewText("Count", bg.transform, "", 0f, 0f, 36f, 36f, TextAlignmentOptions.BottomRight, Color.white, 8f, true);
            view.countShadow.margin = new Vector4(0f, 0f, 2f, 1f);
            view.count.margin = new Vector4(0f, 0f, 3f, 2f);
            view.countShadow.enabled = false; view.count.enabled = false;
            view.emptySprite = Sp("Slots/Slot_Empty.png"); view.selectedSprite = Sp("Slots/Slot_Selected.png"); view.lockedSprite = Sp("Slots/Slot_Locked.png");
            string[] r = { "Uncommon", "Rare", "Epic", "Legendary" };
            view.raritySprites = new Sprite[4]; view.raritySelSprites = new Sprite[4];
            for (int i = 0; i < 4; i++) { view.raritySprites[i] = Sp("Slots/Rarity_" + r[i] + ".png"); view.raritySelSprites[i] = Sp("Slots/Rarity_" + r[i] + "_Sel.png"); }
            return view;
        }

        [MenuItem("DeepVain/Inventory/3 - Build book UI in scene")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { EditorUtility.DisplayDialog("Book UI", "Fant ikke mappen " + Folder + ".\nKopier BookUI-mappen dit først.", "OK"); return; }
            var inv = UnityEngine.Object.FindFirstObjectByType<Inventory>();
            if (inv == null) { EditorUtility.DisplayDialog("Book UI", "Fant ingen Inventory i scenen. Legg Inventory-komponenten på Player først.", "OK"); return; }
            PrepareSprites();

            // ---- data components on the Player
            var player = inv.gameObject;
            var equipment = player.GetComponent<PlayerEquipment>() ?? Undo.AddComponent<PlayerEquipment>(player);
            var quick = player.GetComponent<QuickSlots>() ?? Undo.AddComponent<QuickSlots>(player);
            var so = new SerializedObject(inv);
            so.FindProperty("maxSlots").intValue = 35;
            if (so.FindProperty("unlockedSlots").intValue < 24) so.FindProperty("unlockedSlots").intValue = 24;
            so.ApplyModifiedProperties();

            // ---- canvas (replaces an earlier one)
            var old = GameObject.Find("BookInventoryCanvas");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var canvasGO = new GameObject("BookInventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Build book UI");
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20; canvas.pixelPerfect = true;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 1f;                      // our pictures have 1 pixel per unit

            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var root = NewRT("BookRoot", canvasGO.transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f); root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(640f, 360f); root.anchoredPosition = Vector2.zero;

            var ui = canvasGO.AddComponent<BookInventoryUI>();
            ui.inventory = inv; ui.equipment = equipment; ui.quick = quick; ui.window = root.gameObject; ui.bookRoot = root;

            // ---- the static book picture
            var baseImg = NewImage("Base", root, Sp("Book/Book_Base.png"), 0f, 0f, 640f, 360f, true);
            ui.backgroundClick = baseImg.gameObject.AddComponent<BookBackgroundClick>();

            // ---- figure (front view, 8 frames)
            var fig = NewImage("Figure", root, Sp("Portrait/Front_Idle_Sword_01.png"), 112f, 96f, 108f, 156f);
            ui.figureImage = fig;
            ui.figureFrames = new Sprite[8];
            for (int i = 0; i < 8; i++) ui.figureFrames[i] = Sp("Portrait/Front_Idle_Sword_0" + (i + 1) + ".png");

            // ---- equipment slots (index = PlayerEquipment index). Layout: figure in the middle, two lanes of slots on each side, tools on a shelf below
            NewImage("ToolsPanel", root, Sp("Panels/Panel_Inset.png"), 112f, 262f, 108f, 46f, false, true);
            NewTextC("Label_Tools", root, "TOOLS", 166f, 259f, 60f, 10f, Dim, false);
            string[] eqNames = { "Helmet", "Chest", "Pants", "Boots", "Gloves", "Weapon", "Shield", "TalismanA", "TalismanB", "RingA", "RingB", "Axe", "Pickaxe", "Bow", "Arrows" };
            string[] eqGlyph = { "Helmet", "Chest", "Pants", "Boots", "Gloves", "Sword", "Shield", "Talisman", "Talisman", "Ring", "Ring", "Axe", "Pickaxe", "Bow", "Arrows" };
            Vector2[] eqPos =
            {
                new Vector2(148, 50),                                                                          // helmet
                new Vector2(72, 142), new Vector2(72, 186), new Vector2(28, 208), new Vector2(28, 164),        // chest, pants, boots, gloves
                new Vector2(224, 142), new Vector2(224, 186),                                                  // weapon, shield
                new Vector2(72, 98), new Vector2(224, 98),                                                     // talismans
                new Vector2(28, 120), new Vector2(268, 120),                                                   // rings
                new Vector2(120, 267), new Vector2(164, 267),                                                  // axe, pickaxe
                new Vector2(268, 164), new Vector2(268, 208)                                                   // bow, arrows
            };
            ui.equipViews = new InventorySlotView[eqNames.Length];
            for (int i = 0; i < eqNames.Length; i++) ui.equipViews[i] = NewSlot("Equip_" + eqNames[i], root, eqPos[i].x, eqPos[i].y, Sp("Glyphs/Glyph_" + eqGlyph[i] + ".png"));

            // ---- backpack grid 7 x 5
            ui.gridViews = new InventorySlotView[35];
            for (int i = 0; i < 35; i++) ui.gridViews[i] = NewSlot("Slot_" + i.ToString("00"), root, 342f + (i % 7) * 38f, 54f + (i / 7) * 38f, null);

            // ---- bookmark above the grid
            NewImage("Ribbon", root, Sp("Book/Ribbon.png"), 574f, 16f, 19f, 48f);

            // ---- hotkeys
            ui.hotViews = new InventorySlotView[4];
            string[] keys = { "Q", "E", "R", "F" };
            for (int i = 0; i < 4; i++)
            {
                float x = 402f + 50f * i;
                ui.hotViews[i] = NewSlot("Hot_" + keys[i], root, x, 270f, null);
                NewImage("KeyChip_" + keys[i], root, Sp("Slots/KeyChip.png"), x - 3f, 266f, 12f, 12f);
                NewTextC("Key_" + keys[i], root, keys[i], x + 3f, 272f, 12f, 12f, Cream);
            }

            // ---- texts
            NewTextC("Title_Equipment", root, "EQUIPMENT", 166f, 32f, 200f, 12f, Title);
            NewTextC("Title_Backpack", root, "BACKPACK", 474f, 32f, 200f, 12f, Title);
            NewText("Label_Hotkeys", root, "HOTKEYS", 348f, 266f, 50f, 12f, TextAlignmentOptions.MidlineLeft, Cream, 8f, false);
            ui.goldText = NewText("Gold", root, "1 284", 358f, 321f, 80f, 12f, TextAlignmentOptions.MidlineRight, Ink, 8f, true);
            ui.slotsText = NewText("SlotsCount", root, "0 / 24", 526f, 321f, 80f, 12f, TextAlignmentOptions.MidlineRight, Dim);
            NewImage("Icon_Defence", root, Sp("StatIcons/Stat_Block.png"), 32f, 316f, 11f, 11f);
            NewText("Label_Defence", root, "Defence", 48f, 315f, 70f, 12f, TextAlignmentOptions.MidlineLeft, Ink);
            ui.defenceText = NewText("Value_Defence", root, "0", 80f, 315f, 50f, 12f, TextAlignmentOptions.MidlineRight, Ink, 8f, true);
            NewImage("Icon_Attack", root, Sp("StatIcons/Stat_Damage.png"), 170f, 316f, 11f, 11f);
            NewText("Label_Attack", root, "Attack", 186f, 315f, 60f, 12f, TextAlignmentOptions.MidlineLeft, Ink);
            ui.attackText = NewText("Value_Attack", root, "0", 254f, 315f, 50f, 12f, TextAlignmentOptions.MidlineRight, Ink, 8f, true);

            // ---- popup (last, so it is drawn on top)
            var pop = NewImage("InspectPopup", root, Sp("Panels/Popup_Frame.png"), 0f, 0f, InspectPopup.Width, 160f, true, true);
            var popup = pop.gameObject.AddComponent<InspectPopup>();
            popup.arrow = NewImage("Arrow", pop.transform, Sp("Panels/Popup_Arrow.png"), 0f, -8f, 13f, 9f).rectTransform;
            popup.iconSlot = NewSlot("IconSlot", pop.transform, 10f, 12f, null);
            popup.iconSlot.GetComponent<Image>().raycastTarget = false;
            popup.nameText = NewText("Name", pop.transform, "", 52f, 15f, 136f, 12f, TextAlignmentOptions.MidlineLeft, Ink, 8f, true);
            popup.typeText = NewText("Type", pop.transform, "", 52f, 27f, 136f, 12f, TextAlignmentOptions.MidlineLeft, Dim);
            popup.rarityText = NewText("Rarity", pop.transform, "", 52f, 38f, 136f, 12f, TextAlignmentOptions.MidlineLeft, Ink);
            popup.divider1 = NewImage("Divider1", pop.transform, Sp("Panels/Divider.png"), 10f, 54f, InspectPopup.Width - 20f, 2f).rectTransform;
            popup.divider2 = NewImage("Divider2", pop.transform, Sp("Panels/Divider.png"), 10f, 100f, InspectPopup.Width - 20f, 2f).rectTransform;
            for (int i = 0; i < 3; i++)
            {
                popup.statIcons[i] = NewImage("StatIcon" + i, pop.transform, null, 10f, 59f + i * 14f, 11f, 11f);
                popup.statLabels[i] = NewText("StatLabel" + i, pop.transform, "", 26f, 58f + i * 14f, 100f, 12f, TextAlignmentOptions.MidlineLeft, Ink);
                popup.statValues[i] = NewText("StatValue" + i, pop.transform, "", 96f, 58f + i * 14f, 90f, 12f, TextAlignmentOptions.MidlineRight, Ink, 8f, true);
            }
            popup.descText = NewText("Description", pop.transform, "", 14f, 108f, InspectPopup.Width - 28f, 30f, TextAlignmentOptions.TopLeft, Ink, 8f, false, true);
            for (int i = 0; i < 3; i++)
            {
                var b = NewImage("Button" + i, pop.transform, Sp(i == 0 ? "Panels/Button_Brass.png" : "Panels/Button_Leather.png"), 10f + i * 60f, 132f, 56f, 17f, true, true);
                var btn = b.gameObject.AddComponent<Button>();
                btn.targetGraphic = b; btn.transition = Selectable.Transition.SpriteSwap;
                popup.buttons[i] = btn;
                popup.buttonTexts[i] = NewText("Label", b.transform, "", 0f, 0f, 56f, 17f, TextAlignmentOptions.Center, i == 0 ? Title : Cream, 8f, i == 0);
                popup.buttonTexts[i].rectTransform.anchorMin = Vector2.zero; popup.buttonTexts[i].rectTransform.anchorMax = Vector2.one;
                popup.buttonTexts[i].rectTransform.offsetMin = Vector2.zero; popup.buttonTexts[i].rectTransform.offsetMax = Vector2.zero;
            }
            popup.damageIcon = Sp("StatIcons/Stat_Damage.png"); popup.blockIcon = Sp("StatIcons/Stat_Block.png"); popup.speedIcon = Sp("StatIcons/Stat_Speed.png");
            popup.healIcon = Sp("StatIcons/Stat_Heal.png"); popup.coinIcon = Sp("StatIcons/Stat_Coin.png");
            popup.brass = Sp("Panels/Button_Brass.png"); popup.brassHover = Sp("Panels/Button_Brass_Hover.png");
            popup.leather = Sp("Panels/Button_Leather.png"); popup.leatherHover = Sp("Panels/Button_Leather_Hover.png");
            ui.popup = popup;
            pop.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
            Selection.activeGameObject = canvasGO;
            Debug.Log("[Book UI] Boka er bygget. Trykk Play og Tab. (Slå av den gamle InventoryCanvas hvis du fortsatt har den.)");
        }
    }
}
#endif
