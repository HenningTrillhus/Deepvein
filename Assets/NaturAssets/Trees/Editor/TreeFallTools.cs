#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeepVain.TreeFallTools
{
    /// <summary>
    /// DeepVain > Trees > 5 - Set up tree falling
    ///   1. Setter stubbebildene (Stump_*.png) riktig: Sprite, 50 pixels per unit, Point, ingen komprimering, pivot nederst (samme som trærne).
    ///   2. Legger TreeFall på spilleren (der TreeChopper ligger) og fyller inn reglene: hvilken stubbe som hører til hvilket tre.
    /// Bruker stil B (hugget med øks) hvis den finnes, ellers A, ellers C. Kopier bare den stilen du vil ha, eller alle.
    /// Størrelser: S (smale trær), M, L (tykke trær). Farger: Brown og Grey (aske, bøk, osp, tørt tre).
    /// </summary>
    public static class TreeFallMenu
    {
        // (name part in the tree picture, size, colour)
        static readonly string[,] Map =
        {
            { "OldOak", "L", "Brown" }, { "Willow", "L", "Brown" }, { "Beech", "L", "Grey" },
            { "Linden", "M", "Brown" }, { "Ash", "M", "Grey" }, { "Pine", "M", "Brown" }, { "Hazel", "M", "Brown" }, { "Snag", "M", "Grey" },
            { "Juniper", "S", "Brown" }, { "Aspen", "S", "Grey" }, { "SpruceYoung", "S", "Brown" },
        };

        static Sprite PrepareStump(string assetName)
        {
            var guids = AssetDatabase.FindAssets(assetName + " t:Texture2D");
            string path = null;
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == assetName) { path = p; break; }
            }
            if (path == null) return null;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                bool changed = false;
                if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
                if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (!Mathf.Approximately(imp.spritePixelsPerUnit, 50f)) { imp.spritePixelsPerUnit = 50f; changed = true; }
                if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; changed = true; }
                if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
                // bottom centre, 1.5 art pixels up: the same as the trees, so the stump stands exactly where the tree's feet were
                if (s.spriteAlignment != (int)SpriteAlignment.Custom || !Mathf.Approximately(imp.spritePivot.y, 0.0625f) || !Mathf.Approximately(imp.spritePivot.x, 0.5f))
                {
                    s.spriteAlignment = (int)SpriteAlignment.Custom;
                    imp.SetTextureSettings(s);
                    imp.spritePivot = new Vector2(0.5f, 0.0625f);
                    changed = true;
                }
                if (changed) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static string FindStyle()
        {
            foreach (var st in new[] { "B", "A", "C" })
                if (AssetDatabase.FindAssets("Stump_" + st + "_M_Brown t:Texture2D").Length > 0) return st;
            return null;
        }

        [MenuItem("DeepVain/Trees/5 - Set up tree falling")]
        public static void Setup()
        {
            var chopper = Object.FindFirstObjectByType<TreeChopper>();
            if (chopper == null) { EditorUtility.DisplayDialog("Tree falling", "Fant ingen TreeChopper i scenen. Legg TreeChopper på spilleren først.", "OK"); return; }
            string style = FindStyle();
            if (style == null) { EditorUtility.DisplayDialog("Tree falling", "Fant ingen Stump_*.png i prosjektet. Kopier stubbebildene inn i Assets først.", "OK"); return; }

            var fall = chopper.GetComponent<TreeFall>();
            if (fall == null) fall = Undo.AddComponent<TreeFall>(chopper.gameObject);
            Undo.RecordObject(fall, "Tree falling");

            var rules = new List<TreeFall.StumpRule>();
            for (int i = 0; i < Map.GetLength(0); i++)
            {
                var sp = PrepareStump("Stump_" + style + "_" + Map[i, 1] + "_" + Map[i, 2]);
                if (sp != null) rules.Add(new TreeFall.StumpRule { nameContains = Map[i, 0], stump = sp });
            }
            fall.rules = rules.ToArray();
            fall.defaultStump = PrepareStump("Stump_" + style + "_M_Brown");
            EditorUtility.SetDirty(fall);
            Debug.Log("[TreeFall] Stil " + style + ": " + rules.Count + " regler satt opp. Spill og hugg et tre.", fall);
        }
    }
}
#endif
