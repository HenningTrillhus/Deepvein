#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepVain.CampTools
{
    /// <summary>
    /// DeepVain > Camp > Build watchtower prefab
    ///   Importerer bildene i mappen TowerWatch (TowerWatch_Top og TowerWatch_Flag_01..08) og lager prefaben Assets/Prefabs/Camps/Tower_Watch.prefab:
    ///   toppen av vakttårnet (tak, plattform, stolper; scale 2 som veggene) med et flagg på toppen som går i løkke (SpriteLoop, 8 bilder).
    ///   Ingen collider. Sorting Layer = AssetsBackGround hvis den finnes (ellers Default), Order 4.
    /// </summary>
    public static class TowerWatchMenu
    {
        const float RoofW = 112f, RoofH = 230f;      // pixels
        const float FlagX = 57f, FlagY = 14f;        // where the flag's middle left edge sits on the roof picture, pixels from the top left

        static string Find(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name && p.IndexOf("TowerWatch", System.StringComparison.OrdinalIgnoreCase) >= 0) return p;
            }
            return null;
        }

        static Sprite Prepare(string path, Vector2 pivot)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                bool changed = false;
                if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
                if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (!Mathf.Approximately(imp.spritePixelsPerUnit, 100f)) { imp.spritePixelsPerUnit = 100f; changed = true; }
                if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; changed = true; }
                if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                if (imp.mipmapEnabled) { imp.mipmapEnabled = false; changed = true; }
                var st = new TextureImporterSettings(); imp.ReadTextureSettings(st);
                if (st.spriteMeshType != SpriteMeshType.FullRect || st.spriteAlignment != (int)SpriteAlignment.Custom || (imp.spritePivot - pivot).sqrMagnitude > 0.0001f)
                {
                    st.spriteMeshType = SpriteMeshType.FullRect; st.spriteAlignment = (int)SpriteAlignment.Custom; imp.SetTextureSettings(st);
                    imp.spritePivot = pivot; changed = true;
                }
                if (changed) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("DeepVain/Camp/Build watchtower prefab")]
        public static void Build()
        {
            string roofPath = Find("TowerWatch_Top");
            if (roofPath == null) { EditorUtility.DisplayDialog("Tower", "Fant ikke TowerWatch_Top i en mappe som heter TowerWatch. Kopier mappen inn i Assets først.", "OK"); return; }
            var roof = Prepare(roofPath, new Vector2(0.5f, 0f));
            var flags = new Sprite[8];
            for (int i = 0; i < 8; i++)
            {
                string p = Find("TowerWatch_Flag_" + (i + 1).ToString("00"));
                if (p == null) { EditorUtility.DisplayDialog("Tower", "Mangler TowerWatch_Flag_" + (i + 1).ToString("00") + ".", "OK"); return; }
                flags[i] = Prepare(p, new Vector2(0f, 0.5f));
            }

            string layer = "Default";
            foreach (var l in SortingLayer.layers) if (l.name == "AssetsBackGround") layer = l.name;

            var root = new GameObject("Tower_Watch", typeof(SpriteRenderer));
            root.transform.localScale = new Vector3(2f, 2f, 1f);
            var rsr = root.GetComponent<SpriteRenderer>();
            rsr.sprite = roof; rsr.sortingLayerName = layer; rsr.sortingOrder = 4;

            var flag = new GameObject("Flag", typeof(SpriteRenderer));
            flag.transform.SetParent(root.transform, false);
            flag.transform.localPosition = new Vector3((FlagX - RoofW / 2f) / 100f, (RoofH - FlagY) / 100f, 0f);
            var fsr = flag.GetComponent<SpriteRenderer>();
            fsr.sprite = flags[0]; fsr.sortingLayerName = layer; fsr.sortingOrder = 5;
            var loop = flag.AddComponent<SpriteLoop>();
            loop.frames = flags; loop.fps = 8f; loop.randomStart = true;

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            const string dir = "Assets/Prefabs/Camps";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Camps");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, dir + "/Tower_Watch.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[Tower] Prefab " + dir + "/Tower_Watch.prefab laget (tårnet + flagg som går i løkke, Sorting Layer \"" + layer + "\"). Dra den inn i campen bak veggen (føttene i bunnen av bildet), eller i chunk-prefaben.", prefab);
            EditorGUIUtility.PingObject(prefab);
        }
    }
}
#endif
