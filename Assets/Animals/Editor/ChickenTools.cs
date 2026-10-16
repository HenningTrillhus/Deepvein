#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DeepVain > Camp > Chickens > 1 - Build chicken prefabs
///   Imports the pictures in the folder Chickens (Hen_White, Hen_Brown, Rooster, Hen_Speckled, each with Idle, Peck, Walk, Run): PPU 100, Point,
///   the pivot at the feet. Makes four prefabs in Assets/Prefabs/Animals (scale 3, like the goblins): a SpriteRenderer and the Chicken script with all pictures filled in.
/// DeepVain > Camp > Chickens > 2 - Add camp chickens to the scene
///   Makes a CampChickens object (at the selected object, or the middle of the Scene view) with the four prefabs. Move it to the middle of the goblin camp
///   and set Width to the width of the camp (the yellow line in the Scene view).
/// </summary>
public static class ChickenTools
{
    const string PrefabFolder = "Assets/Prefabs/Animals";
    static readonly string[] Names = { "Hen_White", "Hen_Brown", "Rooster", "Hen_Speckled" };
    const int TileH = 24;                       // the pictures are 24 x 24, the feet touch the bottom edge of row 22 (one pixel above the bottom)

    static void Import(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 100f;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = new Vector2(0.5f, 1f / TileH);
        s.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(s);
        imp.SaveAndReimport();
    }

    static Sprite[] Frames(string name, string anim)
    {
        var list = new System.Collections.Generic.List<Sprite>();
        foreach (string guid in AssetDatabase.FindAssets(name + "_" + anim + "_ t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(p);
            if (!file.StartsWith(name + "_" + anim + "_") || file.EndsWith("Sheet") || p.IndexOf("Chickens", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            Import(p);
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (sp != null) list.Add(sp);
        }
        return list.OrderBy(s => s.name).ToArray();
    }

    [MenuItem("DeepVain/Camp/Chickens/1 - Build chicken prefabs")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Animals");
        string layer = "Default";
        foreach (var l in SortingLayer.layers) if (l.name == "InCamp") layer = l.name;
        int made = 0;
        foreach (string n in Names)
        {
            var idle = Frames(n, "Idle"); var peck = Frames(n, "Peck"); var walk = Frames(n, "Walk"); var run = Frames(n, "Run");
            if (idle.Length == 0 || walk.Length == 0)
            {
                EditorUtility.DisplayDialog("Chickens", "Fant ikke bildene til " + n + ". Kopier mappen Chickens (med undermappene Hen_White osv.) inn i Assets først.", "OK");
                return;
            }
            string path = PrefabFolder + "/Chicken_" + n + ".prefab";
            var go = new GameObject("Chicken_" + n, typeof(SpriteRenderer), typeof(Chicken));
            go.transform.localScale = new Vector3(3f, 3f, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = idle[0]; sr.sortingLayerName = layer; sr.sortingOrder = 6;
            var c = go.GetComponent<Chicken>();
            c.sr = sr; c.idle = idle; c.peck = peck; c.walk = walk; c.run = run;
            if (n == "Rooster") { c.walkSpeed = 0.9f; c.runSpeed = 3.0f; }
            AssetDatabase.DeleteAsset(path);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            made++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Chickens] " + made + " prefabs i " + PrefabFolder + " (Sorting Layer \"" + layer + "\", Order 6, skala 3). Neste: DeepVain > Camp > Chickens > 2 - Add camp chickens to the scene.");
    }

    [MenuItem("DeepVain/Camp/Chickens/2 - Add camp chickens to the scene")]
    public static void AddToScene()
    {
        var prefabs = Names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/Chicken_" + n + ".prefab")).Where(p => p != null).ToArray();
        if (prefabs.Length == 0) { EditorUtility.DisplayDialog("Chickens", "Kjør først 1 - Build chicken prefabs.", "OK"); return; }
        Vector3 pos = Vector3.zero;
        if (Selection.activeTransform != null) pos = Selection.activeTransform.position;
        else if (SceneView.lastActiveSceneView != null) { pos = SceneView.lastActiveSceneView.pivot; pos.z = 0f; }
        var go = new GameObject("CampChickens");
        go.transform.position = pos;
        var cc = go.AddComponent<CampChickens>();
        cc.chickenPrefabs = prefabs; cc.count = 6; cc.width = 12f;
        Undo.RegisterCreatedObjectUndo(go, "Add camp chickens");
        Selection.activeGameObject = go;
        Debug.Log("[Chickens] CampChickens laget. Flytt den til midten av goblin-campen og sett Width til hvor bred campen er. Høyreklikk på script-overskriften: Populate now for å se dem i editoren.");
    }
}
#endif
