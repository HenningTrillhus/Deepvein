#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeepVain.CampTools
{
    /// <summary>
    /// DeepVain > Camp > Tower > Build goblin watchtower (from the kit)
    ///   1. imports the tower pictures (the folder Tower with Background / Floor / Ladder / Foreground): PPU 100, Point, pivot at the bottom left corner
    ///   2. makes one prefab per piece in Assets/Prefabs/Camps/TowerKit: floors get a collider (layer Ground), ladder pieces a trigger (layer Ladder), the rest only the picture.
    ///      Use these to build your own towers: drop them in and snap them together (the sizes are in Tower_Kit_Layout.txt).
    ///   3. builds the whole tower as Assets/Prefabs/Camps/Tower_Goblin.prefab: every piece on its place, scale 2 (like the walls), the origin at the middle of the
    ///      bottom of the tower (put the foot on the ground), two floors with a hatch, the ladder from the ground to the roof deck, and the flag in a loop on the roof.
    /// It can be run again: the prefabs are rebuilt.
    /// </summary>
    public static class TowerKitMenu
    {
        // ------------------------------------------------------------------ settings
        const string SortingLayerName = "InCamp";            // the pictures are drawn on this sorting layer (if it exists; otherwise Default). Everything is behind the player.
        const string PieceFolder = "Assets/Prefabs/Camps/TowerKit";
        const string TowerPath = "Assets/Prefabs/Camps/Tower_Goblin.prefab";
        const float PxToUnit = 0.01f;                        // 100 pixels per unit
        const float DeckCentre = 88f;                        // the middle of the 176 px wide deck: the tower's origin is here
        const float LadderTriggerWidth = 40f;                // art pixels (0.8 units at scale 2): easy to grab

        // piece name -> (group, sorting order)
        static readonly Dictionary<string, KeyValuePair<string, int>> Info = new Dictionary<string, KeyValuePair<string, int>>();

        static void Reg(string group, int order, params string[] names) { foreach (var n in names) Info[n] = new KeyValuePair<string, int>(group, order); }

        static TowerKitMenu()
        {
            Reg("Background", 0, "BackWall_Plank", "BackWall_Window", "BackWall_Banner");
            Reg("Background", 1, "Brace_X_72x72", "Brace_X_72x96", "Beam_112");
            Reg("Background", 2, "Post_Foot", "Post_Segment");
            Reg("Background", 8, "Roof_Left", "Roof_Right", "Roof_Ridge");
            Reg("Background", 9, "Horn_L", "Horn_R");
            Reg("Ladder", 3, "Ladder_Bottom", "Ladder_Mid", "Ladder_Top");
            Reg("Floor", 4, "Floor_End_L", "Floor_End_R", "Floor_Mid_A", "Floor_Mid_B", "Floor_HatchEdge_L", "Floor_HatchEdge_R", "Hatch_Closed");
            Reg("Floor", 5, "Hatch_Lid_Open");
            Reg("Foreground", 6, "Rail_32", "Rail_Skull_32", "Rail_End_L", "Rail_End_R");
            Reg("Foreground", 7, "Banner_Hang", "Lantern_Hang");
        }

        // the whole tower: piece, x (art px from the left of the deck), y (art px up from the ground). Same numbers as Tower_Kit_Layout.txt.
        static List<(string name, float x, float y)> Layout()
        {
            var l = new List<(string, float, float)>();
            foreach (float px in new[] { 18f, 144f })
            {
                l.Add(("Post_Foot", px - 6, 0));
                for (int k = 0; k < 6; k++) l.Add(("Post_Segment", px, 24 + 48 * k));
            }
            l.Add(("Brace_X_72x72", 32, 24)); l.Add(("Brace_X_72x96", 32, 112)); l.Add(("Beam_112", 32, 100));
            l.Add(("BackWall_Window", 32, 224));
            l.Add(("Ladder_Bottom", 108, 0));
            for (float y = 44; y < 216; y += 32) l.Add(("Ladder_Mid", 108, y));
            l.Add(("Ladder_Top", 108, 216));
            foreach (float top in new[] { 112f, 224f })
            {
                float yb = top - 16, x = 0;
                foreach (var n in new[] { "Floor_End_L", "Floor_Mid_A", "Floor_Mid_B", "Floor_HatchEdge_L" }) { l.Add((n, x, yb)); x += Width(n); }
                x += 32;                                                                       // the hatch: a 32 px gap
                foreach (var n in new[] { "Floor_HatchEdge_R", "Floor_End_R" }) { l.Add((n, x, yb)); x += Width(n); }
            }
            l.Add(("Roof_Left", -16, 312)); l.Add(("Roof_Ridge", 80, 312)); l.Add(("Roof_Right", 96, 312));
            l.Add(("Horn_L", -26, 314)); l.Add(("Horn_R", 176, 314));
            foreach (float top in new[] { 112f, 224f })
            {
                l.Add(("Rail_End_L", 0, top)); l.Add(("Rail_32", 10, top)); l.Add(("Rail_Skull_32", 42, top)); l.Add(("Rail_32", 74, top));
                l.Add(("Rail_Skull_32", 136, top)); l.Add(("Rail_End_R", 168, top));
            }
            l.Add(("Banner_Hang", 40, 154)); l.Add(("Banner_Hang", 150, 154));
            l.Add(("Lantern_Hang", 66, 266));
            return l;
        }

        static float Width(string piece)
        {
            switch (piece) { case "Floor_End_L": case "Floor_End_R": return 24; case "Floor_Mid_A": case "Floor_Mid_B": return 32; default: return 16; }
        }

        // ------------------------------------------------------------------ pictures
        static string FindPng(string name)
        {
            foreach (string g in AssetDatabase.FindAssets(name + " t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name && p.Replace('\\', '/').Contains("/Tower/")) return p;
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
                    st.spriteMeshType = SpriteMeshType.FullRect; st.spriteAlignment = (int)SpriteAlignment.Custom; st.spritePivot = pivot; imp.SetTextureSettings(st);
                    changed = true;
                }
                if (changed) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static string Layer()
        {
            foreach (var l in SortingLayer.layers) if (l.name == SortingLayerName) return l.name;
            return "Default";
        }

        static int LayerIndex(string name)
        {
            int i = LayerMask.NameToLayer(name);
            if (i < 0) Debug.LogWarning("[Tower] Fant ikke fysikk-laget \"" + name + "\" (Project Settings > Tags and Layers). Colliderne havner på Default.");
            return Mathf.Max(0, i);
        }

        // ------------------------------------------------------------------ the pieces as prefabs
        static Dictionary<string, GameObject> BuildPieces(Dictionary<string, Sprite> sprites)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Camps")) AssetDatabase.CreateFolder("Assets/Prefabs", "Camps");
            if (!AssetDatabase.IsValidFolder(PieceFolder)) AssetDatabase.CreateFolder("Assets/Prefabs/Camps", "TowerKit");
            string layer = Layer();
            int groundLayer = LayerIndex("Ground"), ladderLayer = LayerIndex("Ladder");
            var result = new Dictionary<string, GameObject>();
            foreach (var kv in sprites)
            {
                string name = kv.Key; var sp = kv.Value;
                var info = Info[name];
                var go = new GameObject(name, typeof(SpriteRenderer));
                var sr = go.GetComponent<SpriteRenderer>(); sr.sprite = sp; sr.sortingLayerName = layer; sr.sortingOrder = info.Value;
                float w = sp.rect.width * PxToUnit, h = sp.rect.height * PxToUnit;
                if (info.Key == "Floor" && name != "Hatch_Lid_Open")
                {
                    var bc = go.AddComponent<BoxCollider2D>();
                    bc.size = new Vector2(w, h); bc.offset = new Vector2(w / 2f, h / 2f);
                    go.layer = groundLayer;
                }
                else if (info.Key == "Ladder")
                {
                    var bc = go.AddComponent<BoxCollider2D>();
                    float tw = LadderTriggerWidth * PxToUnit;
                    bc.isTrigger = true; bc.size = new Vector2(tw, h); bc.offset = new Vector2(w / 2f, h / 2f);
                    go.layer = ladderLayer;
                }
                string path = PieceFolder + "/" + name + ".prefab";
                AssetDatabase.DeleteAsset(path);
                result[name] = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }
            return result;
        }

        // ------------------------------------------------------------------ the tower
        [MenuItem("DeepVain/Camp/Tower/Build goblin watchtower (from the kit)")]
        public static void Build()
        {
            var sprites = new Dictionary<string, Sprite>();
            var missing = new List<string>();
            foreach (var n in Info.Keys)
            {
                string p = FindPng(n);
                if (p == null) { missing.Add(n); continue; }
                sprites[n] = Prepare(p, Vector2.zero);
            }
            if (missing.Count > 0)
            {
                EditorUtility.DisplayDialog("Tower", "Fant ikke disse bildene i en mappe som heter Tower:\n" + string.Join(", ", missing) + "\n\nKopier mappen Pieces (Background, Floor, Ladder, Foreground) til Assets/Sprits/Camps/Goblin/Tower.", "OK");
                return;
            }
            var pieces = BuildPieces(sprites);
            string layer = Layer();

            var root = new GameObject("Tower_Goblin");
            root.transform.localScale = new Vector3(2f, 2f, 1f);
            int n2 = 0;
            foreach (var (name, x, y) in Layout())
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pieces[name], root.transform);
                inst.transform.localPosition = new Vector3((x - DeckCentre) * PxToUnit, y * PxToUnit, 0f);
                n2++;
            }

            // the flag on the top of the roof pole (the old watchtower flag pictures, if they are in the project)
            var flags = new List<Sprite>();
            for (int i = 1; i <= 8; i++)
            {
                string fp = null;
                foreach (string g in AssetDatabase.FindAssets("TowerWatch_Flag_" + i.ToString("00") + " t:Texture2D")) { fp = AssetDatabase.GUIDToAssetPath(g); break; }
                if (fp != null) { var sp = AssetDatabase.LoadAssetAtPath<Sprite>(fp); if (sp != null) flags.Add(sp); }
            }
            if (flags.Count == 8)
            {
                var flag = new GameObject("Flag", typeof(SpriteRenderer));
                flag.transform.SetParent(root.transform, false);
                // the roof ridge piece stands at x 80, base y 312, is 100 high: the pole top is 8 px under the top of the picture
                flag.transform.localPosition = new Vector3((88f - DeckCentre) * PxToUnit, (312f + 100f - 8f) * PxToUnit, 0f);
                var fsr = flag.GetComponent<SpriteRenderer>();
                fsr.sprite = flags[0]; fsr.sortingLayerName = layer; fsr.sortingOrder = 10;
                var loop = flag.AddComponent<SpriteLoop>();
                loop.frames = flags.ToArray(); loop.fps = 8f; loop.randomStart = true;
            }
            else Debug.LogWarning("[Tower] Fant ikke de 8 flaggbildene (TowerWatch_Flag_01..08), så tårnet har ikke flagg. Bygg det gamle tårnet først (DeepVain > Camp > Build watchtower prefab).");

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TowerPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[Tower] " + TowerPath + " laget av " + n2 + " deler (" + pieces.Count + " del-prefaber i " + PieceFolder + "). Sett foten på bakken: origo er midt nederst. Gulvene er på laget Ground, stigen på laget Ladder.", prefab);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
    }
}
#endif
