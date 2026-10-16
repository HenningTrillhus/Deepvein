#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepVain.CampTools
{
    /// <summary>
    /// DeepVain > Camp > Build tent prefab (Goblin, whole tent)
    ///   Importerer bildene i mappen TentGoblin (Tent_Goblin_Roof, _WallLeft, _Door, _WallRight og Flag/TentGoblin_Flag_01..08) og lager
    ///   Assets/Prefabs/Camps/Tent_Goblin.prefab: taket med flagget som går i løkke, venstre vegg, døra og høyre vegg, i riktig rekkefølge og skala 2 (som veggene).
    ///   Origo (pivot) på hele teltet er midt nederst. Ingen collider. Døra heter "Door", så du kan legge en Teleporter på den senere.
    ///   Sorting Layer = AssetsBackGround hvis den finnes (ellers Default). Veggene og taket Order 4, flagget Order 5.
    /// </summary>
    public static class TentGoblinMenu
    {
        // the pieces on the whole 288 x 240 picture: pixels from the top left
        const float FullW = 288f, FullH = 240f, RoofH = 144f, WallH = 96f;
        const float LeftX = 31f, DoorX = 96f, RightX = 192f;
        const float FlagX = 145f, FlagY = 16f;                 // left-middle edge of the flag on the roof

        static string Find(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name && p.IndexOf("TentGoblin", System.StringComparison.OrdinalIgnoreCase) >= 0) return p;
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

        static GameObject Piece(Transform parent, string name, Sprite sp, float xPx, float bottomPx, string layer, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3((xPx - FullW / 2f) / 100f, bottomPx / 100f, 0f);      // bottom-left pivot
            var sr = go.GetComponent<SpriteRenderer>(); sr.sprite = sp; sr.sortingLayerName = layer; sr.sortingOrder = order;
            return go;
        }

        [MenuItem("DeepVain/Camp/Build tent prefab (Goblin, whole tent)")]
        public static void Build()
        {
            string[] names = { "Tent_Goblin_Roof", "Tent_Goblin_WallLeft", "Tent_Goblin_Door", "Tent_Goblin_WallRight" };
            var sp = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                string p = Find(names[i]);
                if (p == null) { EditorUtility.DisplayDialog("Tent", "Fant ikke " + names[i] + " i en mappe som heter TentGoblin. Kopier mappen TentGoblin inn i Assets først.", "OK"); return; }
                sp[i] = Prepare(p, Vector2.zero);
            }
            var flags = new Sprite[8];
            for (int i = 0; i < 8; i++)
            {
                string p = Find("TentGoblin_Flag_" + (i + 1).ToString("00"));
                if (p == null) { EditorUtility.DisplayDialog("Tent", "Mangler TentGoblin_Flag_" + (i + 1).ToString("00") + ".", "OK"); return; }
                flags[i] = Prepare(p, new Vector2(0f, 0.5f));
            }
            string layer = "Default";
            foreach (var l in SortingLayer.layers) if (l.name == "AssetsBackGround") layer = l.name;

            var root = new GameObject("Tent_Goblin");
            root.transform.localScale = new Vector3(2f, 2f, 1f);
            var roof = Piece(root.transform, "Roof", sp[0], 0f, WallH, layer, 4);
            Piece(root.transform, "WallLeft", sp[1], LeftX, 0f, layer, 4);
            Piece(root.transform, "Door", sp[2], DoorX, 0f, layer, 4);
            Piece(root.transform, "WallRight", sp[3], RightX, 0f, layer, 4);

            var flag = new GameObject("Flag", typeof(SpriteRenderer));
            flag.transform.SetParent(roof.transform, false);
            flag.transform.localPosition = new Vector3(FlagX / 100f, (RoofH - FlagY) / 100f, 0f);       // the roof has a bottom-left pivot
            var fsr = flag.GetComponent<SpriteRenderer>();
            fsr.sprite = flags[0]; fsr.sortingLayerName = layer; fsr.sortingOrder = 5;
            var loop = flag.AddComponent<SpriteLoop>();
            loop.frames = flags; loop.fps = 8f; loop.randomStart = true;

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            const string dir = "Assets/Prefabs/Camps";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Camps");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, dir + "/Tent_Goblin.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[Tent] Prefab " + dir + "/Tent_Goblin.prefab laget: tak + flagg, venstre vegg, dør, høyre vegg (Sorting Layer \"" + layer + "\"). Origo er midt nederst.", prefab);
            EditorGUIUtility.PingObject(prefab);
        }
    }
}
#endif
