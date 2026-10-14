#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DeepVain.BackgroundTreesTools
{
    /// <summary>
    /// DeepVain > Trees > 8 - Add background trees
    /// Legger BackgroundTrees på samme objekt som WorldBuilder (ellers på et nytt objekt) og gir den trelisten:
    /// ForestSet (fra meny 6) hvis den finnes, ellers listen til TreeSpawner.
    /// </summary>
    public static class BackgroundTreesMenu
    {
        [MenuItem("DeepVain/Trees/8 - Add background trees")]
        public static BackgroundTrees Add()
        {
            var existing = Object.FindFirstObjectByType<BackgroundTrees>();
            BackgroundTrees bg = existing;
            if (bg == null)
            {
                var wb = Object.FindFirstObjectByType<WorldBuilder>();
                GameObject host = wb != null ? wb.gameObject : new GameObject("BackgroundTrees");
                bg = Undo.AddComponent<BackgroundTrees>(host);
            }
            Undo.RecordObject(bg, "Background trees");

            TreeSet set = null;
            foreach (var g in AssetDatabase.FindAssets("ForestSet t:TreeSet"))
            {
                set = AssetDatabase.LoadAssetAtPath<TreeSet>(AssetDatabase.GUIDToAssetPath(g));
                if (set != null) break;
            }
            if (set == null)
            {
                var sp = Object.FindFirstObjectByType<TreeSpawner>();
                if (sp != null) set = sp.treeSet;
            }
            if (set != null) bg.treeSet = set;
            EditorUtility.SetDirty(bg);

            bool layerOk = false;
            foreach (var l in SortingLayer.layers) if (l.name == bg.sortingLayer) layerOk = true;
            Debug.Log("[BackgroundTrees] Satt opp på \"" + bg.name + "\". TreeSet: " + (bg.treeSet != null ? bg.treeSet.name : "MANGLER (dra en inn)")
                      + ". Sorting Layer \"" + bg.sortingLayer + "\" " + (layerOk ? "finnes." : "FINNES IKKE - velg en annen i Inspector.") + " Lagre scenen og spill.", bg);
            return bg;
        }

        /// <summary>
        /// DeepVain > Trees > 9 - Add background bushes
        /// Tar bildene Bush_*.png som ligger i en mappe som heter BackgroundBushes (for eksempel Assets/NaturAssets/BackgroundBushes),
        /// importerer dem som de andre propsene (100 px per unit, Point, pivot nederst) og legger dem på BackgroundTrees.
        /// </summary>
        [MenuItem("DeepVain/Trees/9 - Add background bushes")]
        public static void AddBushes()
        {
            var bg = Object.FindFirstObjectByType<BackgroundTrees>();
            if (bg == null) bg = Add();
            var list = new System.Collections.Generic.List<Sprite>();
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D Bush_"))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.IndexOf("BackgroundBushes", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                bool changed = false;
                if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
                if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (!Mathf.Approximately(imp.spritePixelsPerUnit, 100f)) { imp.spritePixelsPerUnit = 100f; changed = true; }
                if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; changed = true; }
                if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                if (imp.mipmapEnabled) { imp.mipmapEnabled = false; changed = true; }
                var st = new TextureImporterSettings(); imp.ReadTextureSettings(st);
                // Full Rect: the sway shader bends the top, and a tight mesh would cut it off
                if (st.spriteMeshType != SpriteMeshType.FullRect)
                {
                    st.spriteMeshType = SpriteMeshType.FullRect; imp.SetTextureSettings(st); changed = true;
                }
                if (st.spriteAlignment != (int)SpriteAlignment.Custom || !Mathf.Approximately(imp.spritePivot.y, 0.04f))
                {
                    st.spriteAlignment = (int)SpriteAlignment.Custom; imp.SetTextureSettings(st);
                    imp.spritePivot = new Vector2(0.5f, 0.04f); changed = true;
                }
                if (changed) imp.SaveAndReimport();
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) list.Add(sp);
            }
            if (list.Count == 0)
            {
                EditorUtility.DisplayDialog("Background bushes", "Fant ingen Bush_*.png i en mappe som heter BackgroundBushes. Kopier bildene dit først (Assets/NaturAssets/BackgroundBushes).", "OK");
                return;
            }
            Undo.RecordObject(bg, "Background bushes");
            bg.bushSprites = list.ToArray();
            EditorUtility.SetDirty(bg);
            Debug.Log("[BackgroundTrees] " + list.Count + " busker lagt til (scale " + bg.bushScale + "). Lagre scenen og spill, eller høyreklikk BackgroundTrees > Populate now.", bg);
        }
    }
}
#endif
