#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using DeepVain.Items;

namespace DeepVain.Levels.EditorTools
{
    /// <summary>
    /// DeepVain > Levels > 1 - Create skills and trees      gjør bildene til sprites og lager ni SkillDef (fem aktive + fire låste) med skill-trær
    /// DeepVain > Levels > 2 - Build level book in scene    lager boka (canvas), PlayerLevels + LevelXp på spilleren, og fanene på begge bøkene
    /// </summary>
    public static class LevelBookBuilder
    {
        const string SkillFolder = "Assets/Levels/Skills";

        // ------------------------------------------------------------------ helpers
        static string FindPath(string name, string mustContain = null)
        {
            string best = null;
            foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) != name) continue;
                if (mustContain != null && !p.Replace((char)92, '/').Contains(mustContain)) continue;
                if (best == null) best = p;
            }
            return best;
        }

        static Sprite Sp(string name, string mustContain = null)
        {
            string p = FindPath(name, mustContain);
            var s = p != null ? AssetDatabase.LoadAssetAtPath<Sprite>(p) : null;
            if (s == null) Debug.LogWarning("[Levels] Fant ikke bildet " + name + (mustContain != null ? " (i " + mustContain + ")" : "") + ".");
            return s;
        }

        static void PrepareSprite(string path, Vector4 border)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 1f;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.spriteBorder = border;
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ 1: skills
        class N
        {
            public string id, name, icon, desc, parent, key; public int req, cost, col, row; public float val;
            public N(string id, string name, string icon, string desc, string parent, int req, int cost, int col, int row, string key = "", float val = 0f)
            { this.id = id; this.name = name; this.icon = icon; this.desc = desc; this.parent = parent; this.req = req; this.cost = cost; this.col = col; this.row = row; this.key = key; this.val = val; }
        }

        static readonly HashSet<string> WiredKeys = new HashSet<string> { "mine_speed", "stone_bonus", "coal_bonus", "forage_speed", "forage_bonus" };

        static List<N> Tree(string prefix, string rootName, string rootIcon, string rootDesc, string[][] branches, string[] icons, string[] keys, int[] reqs, int[] costs)
        {
            var l = new List<N> { new N("root", rootName, rootIcon, rootDesc, "", 1, 0, 1, 0) };
            for (int b = 0; b < 3; b++)
            {
                string parent = "root";
                for (int i = 0; i < 3; i++)
                {
                    string[] nd = branches[b * 3 + i];                          // { name, description }
                    string id = prefix + (char)('a' + b) + (i + 1);
                    l.Add(new N(id, nd[0], icons[b * 3 + i], nd[1], parent, reqs[b * 3 + i], costs[b * 3 + i], b, i + 1, keys[b * 3 + i], keys[b * 3 + i] == "" ? 0f : float.Parse(nd[2], System.Globalization.CultureInfo.InvariantCulture)));
                    parent = id;
                }
            }
            return l;
        }

        static readonly int[] Reqs = { 2, 5, 10, 3, 6, 12, 8, 12, 20 };
        static readonly int[] Costs = { 1, 1, 2, 1, 1, 2, 1, 2, 3 };

        static void MakeDef(SkillType type, string display, string iconName, Color color, bool available, List<N> nodes)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Levels")) AssetDatabase.CreateFolder("Assets", "Levels");
            if (!AssetDatabase.IsValidFolder(SkillFolder)) AssetDatabase.CreateFolder("Assets/Levels", "Skills");
            string path = SkillFolder + "/Skill_" + type + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<SkillDef>(path);
            if (def == null) { def = ScriptableObject.CreateInstance<SkillDef>(); AssetDatabase.CreateAsset(def, path); }
            def.skill = type; def.displayName = display; def.color = color; def.available = available;
            def.icon = Sp(iconName);
            def.nodes = new List<SkillNode>();
            foreach (var n in nodes)
            {
                string desc = n.desc;
                if (!string.IsNullOrEmpty(n.key) && !WiredKeys.Contains(n.key)) desc += " (coming soon)";
                def.nodes.Add(new SkillNode
                {
                    id = n.id, displayName = n.name, icon = Sp(n.icon), description = desc, parentId = n.parent, requiredLevel = n.req, cost = n.cost,
                    column = n.col, row = n.row, effectKey = n.key, effectValue = n.val
                });
            }
            EditorUtility.SetDirty(def);
        }

        static string[] B(string name, string desc, string val = "0") { return new[] { name, desc, val }; }

        [MenuItem("DeepVain/Levels/1 - Create skills and trees")]
        public static void CreateSkills()
        {
            // the skill was called Forging before: remove that asset so only Foraging is left
            if (AssetDatabase.LoadAssetAtPath<Object>(SkillFolder + "/Skill_Forging.asset") != null) AssetDatabase.DeleteAsset(SkillFolder + "/Skill_Forging.asset");

            // the pictures made for the level book
            int prepared = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g).Replace((char)92, '/');
                string n = Path.GetFileNameWithoutExtension(p);
                bool mine = (p.Contains("/Levels/") && (n.StartsWith("Node_") || n.StartsWith("Tab_") || n == "Seal" || n == "Book_Base_Clean"));
                if (!mine) continue;
                PrepareSprite(p, Vector4.zero); prepared++;
            }

            MakeDef(SkillType.Mining, "Mining", "Pickaxe_Starter", new Color32(0x5A, 0x7C, 0xB0, 255), true, Tree("m", "Basic Mining", "Pickaxe_Starter", "Break rocks with a pickaxe.",
                new[] { B("Swift Swing", "Swing the pickaxe 10% faster.", "0.10"), B("Rhythm", "Another 10% faster.", "0.10"), B("Power Strike", "A further 10% faster.", "0.10"),
                        B("Extra Stone", "+1 Stone from every rock.", "1"), B("Coal Finder", "Coal rocks give +1 Coal.", "1"), B("Rich Veins", "+1 more Stone from every rock.", "1"),
                        B("Copper", "Copper ore will start to appear.", "1"), B("Iron", "Iron ore will start to appear.", "1"), B("Gems", "Rare gems hide in rocks.", "1") },
                new[] { "Node_Speed", "Node_Speed", "Node_Damage", "Stone", "Icon_Coal", "Iron_Bar", "Node_Copper", "Iron_Bar", "Node_Gem" },
                new[] { "mine_speed", "mine_speed", "mine_speed", "stone_bonus", "coal_bonus", "stone_bonus", "ore_copper", "ore_iron", "ore_gem" }, Reqs, Costs));

            MakeDef(SkillType.Woodcutting, "Woodcutting", "Axe_Starter", new Color32(0x5A, 0x9A, 0x32, 255), true, Tree("w", "Basic Chopping", "Axe_Starter", "Fell trees with an axe.",
                new[] { B("Swift Chop", "Chop 10% faster.", "0.10"), B("Rhythm Chop", "Another 10% faster.", "0.10"), B("Power Chop", "Trees fall a hit sooner.", "1"),
                        B("Extra Log", "+1 log from every tree.", "1"), B("Bark Collector", "Chance of bark from trees.", "1"), B("Lumberjack", "+1 more log from every tree.", "1"),
                        B("Pine Lore", "Pine gives better logs.", "1"), B("Oak Lore", "Oak gives better logs.", "1"), B("Old Growth", "Ancient trees can be felled.", "1") },
                new[] { "Node_Speed", "Node_Speed", "Node_Damage", "Log_Oak", "Log_Birch", "Log_Pine", "Log_Pine", "Log_Oak", "Node_Star" },
                new[] { "chop_speed", "chop_speed", "chop_power", "log_bonus", "bark_chance", "log_bonus", "lore_pine", "lore_oak", "old_growth" }, Reqs, Costs));

            MakeDef(SkillType.Foraging, "Foraging", "Icon_Blueberry", new Color32(0xC8, 0x90, 0xF0, 255), true, Tree("g", "Basic Foraging", "Icon_Blueberry", "Pick berries, mushrooms and herbs.",
                new[] { B("Quick Hands", "Pick 10% faster.", "0.10"), B("Nimble Fingers", "Pick another 10% faster.", "0.10"), B("Master Forager", "A further 10% faster.", "0.10"),
                        B("Keen Eye", "+1 item from every pick.", "1"), B("Bountiful", "+1 more item from every pick.", "1"), B("Wild Harvest", "Rare finds while picking.", "1"),
                        B("Mushroom Lore", "Know the mushrooms of the forest.", "1"), B("Herbalist", "Healing herbs work better.", "1"), B("Nature's Gift", "Plants may grow back.", "1") },
                new[] { "Node_Speed", "Node_Speed", "Node_Star", "Icon_WildStrawberry", "Icon_Blueberry", "Icon_Cotton", "Icon_RedCap", "Icon_HealingHerb", "Node_Star" },
                new[] { "forage_speed", "forage_speed", "forage_speed_2", "forage_bonus", "forage_bonus", "forage_rare", "lore_mushroom", "lore_herb", "regrow" }, Reqs, Costs));

            MakeDef(SkillType.Combat, "Combat", "Sword_Starter", new Color32(0xB0, 0x30, 0x30, 255), true, Tree("c", "Basic Combat", "Sword_Starter", "Fight with sword and shield.",
                new[] { B("Quick Strikes", "Swing the sword faster.", "0.10"), B("Heavy Blows", "Hit harder.", "1"), B("Deadly Edge", "Chance of a critical hit.", "0.05"),
                        B("Tough Skin", "Take less damage.", "1"), B("Iron Will", "More maximum health.", "1"), B("Bulwark", "The shield blocks more.", "1"),
                        B("Shield Wall", "Blocking costs less stamina.", "1"), B("Counter", "Hit back after a block.", "1"), B("Warlord", "Enemies fear you.", "1") },
                new[] { "Node_Speed", "Node_Damage", "Node_Star", "Node_Block", "Node_Heal", "Node_Block", "Node_Block", "Node_Damage", "Node_Star" },
                new[] { "attack_speed", "attack_damage", "crit_chance", "defence", "max_health", "block_power", "block_stamina", "counter", "warlord" }, Reqs, Costs));

            MakeDef(SkillType.Running, "Running", "Glyph_Boots", new Color32(0x2A, 0x9A, 0x96, 255), true, Tree("r", "Basic Running", "Glyph_Boots", "Move through the world.",
                new[] { B("Light Feet", "Run a little faster.", "0.05"), B("Sprinter", "Sprint a little faster.", "0.05"), B("Wind Walker", "Run faster still.", "0.05"),
                        B("Endurance", "Sprint for longer.", "1"), B("Deep Breath", "Stamina comes back faster.", "1"), B("Marathon", "Stamina almost never ends.", "1"),
                        B("Sure Footing", "Take less fall damage.", "1"), B("Leaper", "Jump a little higher.", "1"), B("Pathfinder", "Find hidden paths.", "1") },
                new[] { "Node_Speed", "Node_Speed", "Node_Speed", "Node_Heal", "Node_Heal", "Node_Heal", "Glyph_Boots", "Node_Speed", "Node_Star" },
                new[] { "run_speed", "sprint_speed", "run_speed_2", "stamina_max", "stamina_regen", "stamina_max_2", "fall_defence", "jump_height", "pathfinder" }, Reqs, Costs));

            // the four that come later (shown as locked)
            MakeDef(SkillType.Fishing, "Fishing", "Node_Fish", new Color32(0x4A, 0x90, 0xD0, 255), false, new List<N> { new N("root", "Basic Fishing", "Node_Fish", "Coming later.", "", 1, 0, 1, 0) });
            MakeDef(SkillType.Swordsmanship, "Sword", "Glyph_Sword", new Color32(0xA0, 0x60, 0x60, 255), false, new List<N> { new N("root", "Swordsmanship", "Glyph_Sword", "Dexterity with blades. Coming later.", "", 1, 0, 1, 0) });
            MakeDef(SkillType.Magic, "Magic", "Node_Orb", new Color32(0x8A, 0x4A, 0xC8, 255), false, new List<N> { new N("root", "Basic Magic", "Node_Orb", "Coming later.", "", 1, 0, 1, 0) });
            MakeDef(SkillType.Archery, "Archery", "Glyph_Bow", new Color32(0x80, 0x90, 0x40, 255), false, new List<N> { new N("root", "Basic Archery", "Glyph_Bow", "Bow and arrows. Coming later.", "", 1, 0, 1, 0) });

            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Levels] " + prepared + " bilder gjort om til sprites, 9 skills laget i " + SkillFolder + ". Neste: DeepVain > Levels > 2 - Build level book in scene.");
        }

        // ------------------------------------------------------------------ 2: the book
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

        static Image NewImage(string name, Transform parent, Sprite s, float x, float y, float w, float h, bool raycast)
        {
            var rt = NewRT(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s; img.raycastTarget = raycast; img.enabled = s != null;
            Place(rt, x, y, w, h);
            return img;
        }

        static void AddTabs(Transform bookRoot, bool levelsActive)
        {
            foreach (var old in new[] { "Tab_Inventory", "Tab_Levels" })
            {
                var t = bookRoot.Find(old);
                if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
            }
            var normal = Sp("Tab_Leather", "/Levels/"); var active = Sp("Tab_Leather_Active", "/Levels/");
            var bag = Sp("Tab_Glyph_Bag", "/Levels/"); var star = Sp("Tab_Glyph_Star", "/Levels/");
            for (int i = 0; i < 2; i++)
            {
                bool isLevels = i == 1;
                bool isActive = isLevels == levelsActive;
                float y = 38f + i * 32f;
                float x = isActive ? 613f : 617f, w = isActive ? 30f : 26f;
                var tab = NewImage(isLevels ? "Tab_Levels" : "Tab_Inventory", bookRoot, isActive ? active : normal, x, y, w, 27f, true);
                var t = tab.gameObject.AddComponent<BookTab>();
                t.target = isLevels ? BookTab.Target.Levels : BookTab.Target.Inventory;
                NewImage("Glyph", tab.transform, isLevels ? star : bag, (w - 14f) / 2f + 1f, 7f, 14f, 14f, false);
            }
        }

        [MenuItem("DeepVain/Levels/2 - Build level book in scene")]
        public static void Build()
        {
            var inv = Object.FindFirstObjectByType<Inventory>();
            if (inv == null) { EditorUtility.DisplayDialog("Levels", "Fant ingen Inventory i scenen (spilleren). Legg Inventory på Player først.", "OK"); return; }
            var skills = new List<SkillDef>();
            foreach (var g in AssetDatabase.FindAssets("t:SkillDef"))
            {
                var d = AssetDatabase.LoadAssetAtPath<SkillDef>(AssetDatabase.GUIDToAssetPath(g));
                if (d != null) skills.Add(d);
            }
            if (skills.Count == 0) { EditorUtility.DisplayDialog("Levels", "Fant ingen skills. Kjør DeepVain > Levels > 1 - Create skills and trees først.", "OK"); return; }
            skills.Sort((a, b) => a.skill.CompareTo(b.skill));

            // ---- data on the player
            var player = inv.gameObject;
            var pl = player.GetComponent<PlayerLevels>();
            if (pl == null) pl = Undo.AddComponent<PlayerLevels>(player);
            Undo.RecordObject(pl, "Levels");
            pl.skills = skills.ToArray();
            EditorUtility.SetDirty(pl);
            if (player.GetComponent<LevelXp>() == null) Undo.AddComponent<LevelXp>(player);

            // ---- canvas (replaces an earlier one)
            var old = GameObject.Find("LevelBookCanvas");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var canvasGO = new GameObject("LevelBookCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Build level book");
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 21; canvas.pixelPerfect = true;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 1f;
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // the toast layer (always on, also when the book is closed)
            var hud = NewRT("Hud", canvasGO.transform);
            hud.anchorMin = hud.anchorMax = new Vector2(0.5f, 0.5f); hud.pivot = new Vector2(0.5f, 0.5f);
            hud.sizeDelta = new Vector2(640f, 360f); hud.anchoredPosition = Vector2.zero;

            var root = NewRT("BookRoot", canvasGO.transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f); root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(640f, 360f); root.anchoredPosition = Vector2.zero;

            var ui = canvasGO.AddComponent<LevelBookUI>();
            ui.levels = pl;
            ui.inventoryUi = Object.FindFirstObjectByType<BookInventoryUI>();
            ui.window = root.gameObject; ui.bookRoot = root; ui.hud = hud;

            string bookUI = "/BookUI/";
            NewImage("Base", root, Sp("Book_Base_Clean", "/Levels/"), 0f, 0f, 640f, 360f, true);
            var content = NewRT("Content", root); Place(content, 0f, 0f, 640f, 360f);
            ui.content = content;

            ui.slotEmpty = Sp("Slot_Empty", bookUI); ui.slotLocked = Sp("Slot_Locked", bookUI); ui.slotSelected = Sp("Slot_Selected", bookUI);
            ui.panelInset = Sp("Panel_Inset", bookUI); ui.buttonBrass = Sp("Button_Brass", bookUI); ui.popupFrame = Sp("Popup_Frame", bookUI);
            ui.seal = Sp("Seal", "/Levels/");
            ui.ownedFrame = Sp("Rarity_Legendary_Sel", bookUI); ui.availableFrame = Sp("Rarity_Uncommon_Sel", bookUI);
            var frames = new List<Sprite>();
            for (int i = 1; i <= 8; i++) { var f = Sp("Front_Idle_Sword_0" + i, "/Portrait/"); if (f != null) frames.Add(f); }
            ui.figureFrames = frames.ToArray();

            AddTabs(root, true);

            // ---- the tabs on the inventory book too
            var invCanvas = GameObject.Find("BookInventoryCanvas");
            Transform invRoot = invCanvas != null ? invCanvas.transform.Find("BookRoot") : null;
            if (invRoot != null) { AddTabs(invRoot, false); EditorUtility.SetDirty(invRoot.gameObject); }
            else Debug.LogWarning("[Levels] Fant ikke BookInventoryCanvas/BookRoot, så sekk-boka fikk ingen fane. Kjør Inventory > 3 - Build book UI først, og så denne menyen igjen.");

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
            Selection.activeGameObject = canvasGO;
            Debug.Log("[Levels] Level-boka er bygget (" + skills.Count + " skills). Lagre scenen, trykk Play og [I]. F9 gir test-XP. Fanene på høyre kant bytter mellom sekk og levels.");
        }
    }
}
#endif
