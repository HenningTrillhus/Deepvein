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
    }
}
#endif
