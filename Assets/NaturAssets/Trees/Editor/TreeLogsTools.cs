#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DeepVain.Items;

namespace DeepVain.TreeLogsTools
{
    /// <summary>
    /// DeepVain > Trees > 6 - Make forest TreeSet     lager en TreeSet med bare tre typer (Birch, Oak, Pine) av trebildene du har,
    ///                                                og setter den på TreeSpawner i scenen.
    /// DeepVain > Trees > 7 - Set up wood from trees  lager items Birch Log / Oak Log / Pine Log (hvis de ikke finnes),
    ///                                                legger TreeLoot på spilleren og fyller inn reglene (3-5 stokker).
    /// Bildene i prosjektet røres ikke. Trær som ikke er i listen vokser bare ikke lenger.
    /// </summary>
    public static class TreeLogsMenu
    {
        // species group -> sprite names
        static readonly string[] Birch = { "Tree_Ash_1", "Tree_Ash_2", "Tree_Ash_3", "Tree_Aspen_1", "Tree_Aspen_2", "Tree_Aspen_3", "Tree_Beech_1", "Tree_Beech_2", "Tree_Beech_3" };
        static readonly string[] Oak = { "Tree_Linden_1", "Tree_Linden_2", "Tree_Linden_3", "Tree_Oak_1", "Tree_Oak_2", "Tree_Oak_3", "Tree_Oak_4", "Tree_OldOak_1", "Tree_OldOak_2" };
        static readonly string[] Pine = { "Tree_Pine_1", "Tree_Pine_2", "Tree_Pine_3", "Tree_Pine_4", "Tree_Snag_1", "Tree_Snag_2" };

        static string FindAsset(string name, string typeFilter)
        {
            string best = null;
            foreach (var g in AssetDatabase.FindAssets(name + " " + typeFilter))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) != name) continue;
                if (best == null) best = p;
                if (p.Contains("50x50")) return p;             // prefer the half-size pictures (they match scale 2)
            }
            return best;
        }

        static void AddGroup(List<TreeSet.Entry> list, string[] names, string group, float groupWeight, List<string> missing)
        {
            var found = new List<Sprite>();
            foreach (var n in names)
            {
                string p = FindAsset(n, "t:Texture2D");
                var sp = p != null ? AssetDatabase.LoadAssetAtPath<Sprite>(p) : null;
                if (sp == null) { missing.Add(n); continue; }
                found.Add(sp);
            }
            foreach (var sp in found)
                list.Add(new TreeSet.Entry { label = group + " - " + sp.name.Replace("Tree_", ""), sprite = sp, weight = groupWeight / Mathf.Max(1, found.Count) });
        }

        [MenuItem("DeepVain/Trees/6 - Make forest TreeSet")]
        public static TreeSet MakeForestSet()
        {
            var entries = new List<TreeSet.Entry>();
            var missing = new List<string>();
            // every type gets the same total weight, so birch, oak and pine are about equally common
            AddGroup(entries, Birch, "Birch", 3f, missing);
            AddGroup(entries, Oak, "Oak", 3f, missing);
            AddGroup(entries, Pine, "Pine", 3f, missing);
            if (entries.Count == 0) { EditorUtility.DisplayDialog("Forest TreeSet", "Fant ingen av trebildene (Tree_Beech_1 osv.) i prosjektet.", "OK"); return null; }

            string dir = "Assets/Trees";
            if (!AssetDatabase.IsValidFolder(dir)) Directory.CreateDirectory(dir);
            string path = dir + "/ForestSet.asset";
            var set = AssetDatabase.LoadAssetAtPath<TreeSet>(path);
            if (set == null) { set = ScriptableObject.CreateInstance<TreeSet>(); AssetDatabase.CreateAsset(set, path); }
            set.entries = entries.ToArray();
            EditorUtility.SetDirty(set); AssetDatabase.SaveAssets();

            int assigned = 0;
            foreach (var sp in Object.FindObjectsByType<TreeSpawner>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(sp, "Forest TreeSet");
                sp.treeSet = set; EditorUtility.SetDirty(sp); assigned++;
            }
            Debug.Log("[TreeLogs] " + path + ": " + entries.Count + " trær (Birch/Oak/Pine). Satt på " + assigned + " TreeSpawner. "
                      + (missing.Count > 0 ? "Fant ikke: " + string.Join(", ", missing) + ". " : "") + "Lagre scenen.", set);
            return set;
        }

        // ---------- wood items ----------

        static Sprite PrepareIcon(string name)
        {
            string path = FindAsset(name, "t:Texture2D");
            if (path == null) return null;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                bool changed = false;
                if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
                if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; changed = true; }
                if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                if (changed) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static ItemData EnsureItem(string id, string displayName, string iconName, string description, string folder)
        {
            string path = folder + "/" + id + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null)
            {
                // the same item could exist somewhere else already
                foreach (var g in AssetDatabase.FindAssets("t:ItemData"))
                {
                    var other = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g));
                    if (other != null && other.id == id) { item = other; break; }
                }
            }
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, path);
            }
            item.id = id; item.displayName = displayName; item.type = ItemType.Material; item.rarity = ItemRarity.Common;
            item.description = description; item.maxStack = 99; item.sellValue = 1;
            var icon = PrepareIcon(iconName);
            if (icon != null) item.icon = icon;
            EditorUtility.SetDirty(item);
            return item;
        }

        [MenuItem("DeepVain/Trees/7 - Set up wood from trees")]
        public static void SetupLogs()
        {
            var chopper = Object.FindFirstObjectByType<TreeChopper>();
            if (chopper == null) { EditorUtility.DisplayDialog("Wood", "Fant ingen TreeChopper i scenen. Legg TreeChopper på spilleren først.", "OK"); return; }

            // where the other items live
            string folder = "Assets/Inventory/Items";
            var any = AssetDatabase.FindAssets("t:ItemData");
            if (any.Length > 0) folder = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(any[0])).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) Directory.CreateDirectory(folder);

            var birch = EnsureItem("log_birch", "Birch Log", "Log_Birch", "Light, pale wood from birch, ash and aspen.", folder);
            var oak = EnsureItem("log_oak", "Oak Log", "Log_Oak", "Hard, dark wood from oak and linden trees.", folder);
            var pine = EnsureItem("log_pine", "Pine Log", "Log_Pine", "Reddish, resinous wood from pine trees.", folder);
            AssetDatabase.SaveAssets();

            var loot = chopper.GetComponent<TreeLoot>();
            if (loot == null) loot = Undo.AddComponent<TreeLoot>(chopper.gameObject);
            Undo.RecordObject(loot, "Tree loot");
            var rules = new List<TreeLoot.LogRule>();
            foreach (var n in new[] { "Ash", "Aspen", "Beech" }) rules.Add(new TreeLoot.LogRule { nameContains = n, item = birch, min = 3, max = 5 });
            foreach (var n in new[] { "Linden", "Oak" }) rules.Add(new TreeLoot.LogRule { nameContains = n, item = oak, min = 3, max = 5 });   // "Oak" also covers OldOak
            foreach (var n in new[] { "Pine", "Snag" }) rules.Add(new TreeLoot.LogRule { nameContains = n, item = pine, min = 3, max = 5 });
            loot.rules = rules.ToArray();
            EditorUtility.SetDirty(loot);
            Debug.Log("[TreeLogs] Items i " + folder + ": Birch Log, Oak Log, Pine Log. TreeLoot er satt opp (3-5 stokker per tre).", loot);
        }
    }
}
#endif
