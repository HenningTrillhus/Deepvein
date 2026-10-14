#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using DeepVain.Items;

namespace DeepVain.AppleTools
{
    /// <summary>
    /// DeepVain > Trees > 10 - Set up apples
    ///   1. Importerer Apple_Hang (eplet i treet) og Icon_Apple riktig.
    ///   2. Lager items-filen Apple (id "apple") hvis den ikke finnes.
    ///   3. Skrur på Read/Write på eiketrebildene (så eplene kan plasseres under bladklumpene).
    ///   4. Legger TreeApplePlacer på WorldBuilder-objektet og AppleHarvester på spilleren (der TreeChopper er).
    /// </summary>
    public static class AppleMenu
    {
        static string FindAsset(string name, string typeFilter)
        {
            string best = null;
            foreach (var g in AssetDatabase.FindAssets(name + " " + typeFilter))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) != name) continue;
                if (best == null) best = p;
            }
            return best;
        }

        static Sprite PrepareSprite(string name, float ppu, bool icon)
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
                if (imp.mipmapEnabled) { imp.mipmapEnabled = false; changed = true; }
                if (!icon && !Mathf.Approximately(imp.spritePixelsPerUnit, ppu)) { imp.spritePixelsPerUnit = ppu; changed = true; }
                if (changed) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static ItemData EnsureApple(Sprite icon)
        {
            const string id = "apple";
            ItemData item = null;
            foreach (var g in AssetDatabase.FindAssets("t:ItemData"))
            {
                var other = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g));
                if (other != null && other.id == id) { item = other; break; }
            }
            if (item == null)
            {
                string folder = "Assets/Inventory/Items";
                var any = AssetDatabase.FindAssets("t:ItemData");
                if (any.Length > 0) folder = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(any[0])).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(folder)) Directory.CreateDirectory(folder);
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, folder + "/" + id + ".asset");
            }
            item.id = id; item.displayName = "Apple"; item.type = ItemType.Material; item.rarity = ItemRarity.Common;
            item.description = "A crisp red apple shaken from an oak tree."; item.maxStack = 99; item.sellValue = 2;
            if (icon != null) item.icon = icon;
            EditorUtility.SetDirty(item);
            return item;
        }

        [MenuItem("DeepVain/Trees/10 - Set up apples")]
        public static void Setup()
        {
            var chopper = Object.FindFirstObjectByType<TreeChopper>();
            if (chopper == null) { EditorUtility.DisplayDialog("Apples", "Fant ingen TreeChopper i scenen. Legg TreeChopper på spilleren først.", "OK"); return; }

            var hang = PrepareSprite("Apple_Hang", 100f, false);
            var icon = PrepareSprite("Icon_Apple", 100f, true);
            if (hang == null) { EditorUtility.DisplayDialog("Apples", "Fant ikke bildet Apple_Hang i prosjektet. Kopier Apple_Hang.png og Icon_Apple.png inn i Assets først.", "OK"); return; }
            var item = EnsureApple(icon);

            // Read/Write on the oak pictures: the tree pictures from every TreeSet in the project
            int readable = 0;
            foreach (var g in AssetDatabase.FindAssets("t:TreeSet"))
            {
                var set = AssetDatabase.LoadAssetAtPath<TreeSet>(AssetDatabase.GUIDToAssetPath(g));
                if (set == null || set.entries == null) continue;
                foreach (var e in set.entries)
                {
                    if (e == null || e.sprite == null || e.sprite.name.IndexOf("Oak", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(e.sprite.texture)) as TextureImporter;
                    if (imp != null && !imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); readable++; }
                }
            }

            var wb = Object.FindFirstObjectByType<WorldBuilder>();
            var host = wb != null ? wb.gameObject : (Object.FindFirstObjectByType<TreeSpawner>() != null ? Object.FindFirstObjectByType<TreeSpawner>().gameObject : null);
            if (host == null) { EditorUtility.DisplayDialog("Apples", "Fant ingen WorldBuilder/TreeSpawner i scenen.", "OK"); return; }
            var placer = host.GetComponent<TreeApplePlacer>();
            if (placer == null) placer = Undo.AddComponent<TreeApplePlacer>(host);
            Undo.RecordObject(placer, "Apples");
            placer.appleSprite = hang;
            EditorUtility.SetDirty(placer);

            var harvester = chopper.GetComponent<AppleHarvester>();
            if (harvester == null) harvester = Undo.AddComponent<AppleHarvester>(chopper.gameObject);
            Undo.RecordObject(harvester, "Apples");
            harvester.item = item;
            EditorUtility.SetDirty(harvester);

            Debug.Log("[Apples] Klart. Item Apple, eple-bilde satt, Read/Write på " + readable + " eiketrebilder. TreeApplePlacer på \"" + host.name
                      + "\", AppleHarvester på \"" + chopper.name + "\". Lagre scenen og spill (eller høyreklikk TreeApplePlacer > Place apples now).", placer);
        }
    }
}
#endif
