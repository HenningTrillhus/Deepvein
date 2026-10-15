#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeepVain.Items.EditorTools
{
    /// <summary>
    /// Menu: DeepVain > Chest > 3 - Goblin chests (prefabs + loot tables)
    /// Makes Chest_GoblinCrate.prefab and Chest_GoblinBone.prefab from Chest_Common.prefab (same light, prompt, glow),
    /// with the goblin pictures (Assets/Chest/GoblinCrate and GoblinBone) and a loot table each in Assets/Chest/LootTables.
    /// Existing loot tables are never overwritten, so your own numbers are safe. Run it again any time.
    /// </summary>
    public static class GoblinChestTools
    {
        const string Folder = "Assets/Chest";

        struct Row { public string find; public float chance; public int min, max; public Row(string f, float c, int a, int b) { find = f; chance = c; min = a; max = b; } }

        static readonly Row[] CrateRows =
        {
            new Row("wood", 60, 2, 5), new Row("stone", 45, 2, 4), new Row("bone", 35, 1, 3), new Row("apple", 30, 1, 3),
            new Row("coal", 25, 1, 3), new Row("Potion_Heal", 15, 1, 1), new Row("Iron_Bar", 8, 1, 2),
        };
        static readonly Row[] BoneRows =
        {
            new Row("bone", 85, 3, 6), new Row("coal", 40, 2, 5), new Row("Iron_Bar", 45, 1, 3), new Row("healing_herb", 30, 1, 3),
            new Row("Potion_Heal", 35, 1, 2), new Row("Potion_HealBig", 12, 1, 1),
        };

        [MenuItem("DeepVain/Chest/3 - Goblin chests (prefabs + loot tables)")]
        public static void Build()
        {
            string common = Folder + "/Chest_Common.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(common) == null)
            {
                EditorUtility.DisplayDialog("Goblin chests", "Fant ikke " + common + ".\nKjør først DeepVain > Chest > 2 - Create chest prefabs.", "OK");
                return;
            }
            ChestBuilder.Prepare();                                                  // import settings for the new pictures
            if (!AssetDatabase.IsValidFolder(Folder + "/LootTables")) AssetDatabase.CreateFolder(Folder, "LootTables");

            int made = 0;
            made += BuildType("GoblinCrate", common, CrateRows) ? 1 : 0;
            made += BuildType("GoblinBone", common, BoneRows) ? 1 : 0;
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Chest] Goblin-kister: " + made + " prefab(s) laget i " + Folder + ". Loot-tabellene ligger i " + Folder + "/LootTables, endre prosent der.");
        }

        static bool BuildType(string type, string commonPrefab, Row[] rows)
        {
            if (!AssetDatabase.IsValidFolder(Folder + "/" + type + "/Idle") || !AssetDatabase.IsValidFolder(Folder + "/" + type + "/Open"))
            {
                Debug.LogWarning("[Chest] Mangler " + Folder + "/" + type + "/Idle og /Open. Kopier mappen dit først.");
                return false;
            }

            // the loot table (kept if it already exists)
            string tablePath = Folder + "/LootTables/" + type + "_Loot.asset";
            var table = AssetDatabase.LoadAssetAtPath<ChestLootTable>(tablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<ChestLootTable>();
                var list = new System.Collections.Generic.List<ChestLootTable.Entry>();
                foreach (var r in rows)
                {
                    var item = FindItem(r.find);
                    if (item == null) { Debug.Log("[Chest] (" + type + ") fant ikke item '" + r.find + "', hoppet over. Legg den til selv i tabellen."); continue; }
                    list.Add(new ChestLootTable.Entry { item = item, chance = r.chance, minAmount = r.min, maxAmount = r.max });
                }
                table.entries = list.ToArray();
                AssetDatabase.CreateAsset(table, tablePath);
            }

            // the prefab: a copy of the common chest with new frames
            string path = Folder + "/Chest_" + type + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
            if (!AssetDatabase.CopyAsset(commonPrefab, path)) { Debug.LogWarning("[Chest] Klarte ikke kopiere " + commonPrefab); return false; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var chest = root.GetComponent<Chest>();
                chest.idleFrames = Load(type + "/Idle");
                chest.openFrames = Load(type + "/Open");
                if (chest.chestRenderer != null && chest.idleFrames.Length > 0) chest.chestRenderer.sprite = chest.idleFrames[0];
                chest.lootTable = table;
                chest.loot = new Chest.LootEntry[0];
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return true;
        }

        static Sprite[] Load(string sub)
        {
            return AssetDatabase.FindAssets("t:Sprite", new[] { Folder + "/" + sub })
                .Select(g => AssetDatabase.GUIDToAssetPath(g)).OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).Where(s => s != null).ToArray();
        }

        /// <summary>The ItemData whose asset name, id or display name matches (case-insensitive, exact first).</summary>
        static ItemData FindItem(string name)
        {
            var all = AssetDatabase.FindAssets("t:ItemData").Select(g => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g))).Where(i => i != null).ToArray();
            string n = name.ToLowerInvariant().Replace("_", "").Replace(" ", "");
            string K(string s) { return (s ?? "").ToLowerInvariant().Replace("_", "").Replace(" ", ""); }
            return all.FirstOrDefault(i => K(i.name) == n || K(i.id) == n || K(i.displayName) == n);
        }
    }
}
#endif
