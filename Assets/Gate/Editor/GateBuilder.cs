#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepVain.Items.EditorTools
{
    /// <summary>
    /// Menu: DeepVain > Gate > ...
    ///  1  Prepare sprites       import settings for the pictures in Assets/Gate (Point, PPU 100, pivot at the feet of the gate)
    ///  2  Create gate prefabs   one prefab per type folder: Assets/Gate/Gate_Goblin.prefab  (Assets/Gate/<Type>/Gate_<Type>_Closed.png + Open/...)
    /// The picture is 300 x 300 px = 3 x 3 tiles.
    /// </summary>
    public static class GateBuilder
    {
        const string Folder = "Assets/Gate";

        [MenuItem("DeepVain/Gate/1 - Prepare sprites")]
        public static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { EditorUtility.DisplayDialog("Gate", "Fant ikke mappen " + Folder + ".\nKopier Gate-mappen dit først.", "OK"); return; }
            AssetDatabase.StartAssetEditing();
            int n = 0;
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) continue;
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.spritePixelsPerUnit = 100f;                       // 100 px = one tile = one unit
                    imp.filterMode = FilterMode.Point;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    var s = new TextureImporterSettings();
                    imp.ReadTextureSettings(s);
                    s.spriteAlignment = (int)SpriteAlignment.Custom;
                    s.spritePivot = new Vector2(0.5f, 4f / 300f);         // the ground line of the picture
                    imp.SetTextureSettings(s);
                    imp.SaveAndReimport();
                    n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Debug.Log("[Gate] Sprite-innstillinger satt på " + n + " bilder.");
        }

        [MenuItem("DeepVain/Gate/2 - Create gate prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { EditorUtility.DisplayDialog("Gate", "Fant ikke mappen " + Folder + ".\nKopier Gate-mappen dit først.", "OK"); return; }
            Prepare();
            GameObject first = null;
            foreach (string typeFolder in AssetDatabase.GetSubFolders(Folder))
            {
                string type = Path.GetFileName(typeFolder);
                var closed = AssetDatabase.LoadAssetAtPath<Sprite>(typeFolder + "/Gate_" + type + "_Closed.png");
                if (closed == null || !AssetDatabase.IsValidFolder(typeFolder + "/Open")) continue;
                var p = BuildOne(type, typeFolder, closed);
                if (first == null) first = p;
            }
            if (first == null) { EditorUtility.DisplayDialog("Gate", "Fant ingen porttyper (Assets/Gate/<Type>/Gate_<Type>_Closed.png og Open/).", "OK"); return; }

            var inv = UnityEngine.Object.FindFirstObjectByType<Inventory>();
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(first);
            if (inv != null) inst.transform.position = inv.transform.position + new Vector3(5f, -0.5f, 0f);
            Undo.RegisterCreatedObjectUndo(inst, "Place gate");
            EditorSceneManager.MarkSceneDirty(inst.scene);
            Selection.activeGameObject = inst;
            Debug.Log("[Gate] Portene er laget i " + Folder + ". En port ligger i scenen. Åpne med G når du står ved den. Sett bunnen på bakken.");
        }

        static GameObject BuildOne(string type, string typeFolder, Sprite closed)
        {
            var root = new GameObject("Gate_" + type);
            var gate = root.AddComponent<GateDoor>();

            var sprite = new GameObject("Sprite");
            sprite.transform.SetParent(root.transform, false);
            var sr = sprite.AddComponent<SpriteRenderer>();
            sr.sprite = closed; sr.sortingOrder = 0;
            gate.gateRenderer = sr; gate.closedSprite = closed;
            gate.openFrames = AssetDatabase.FindAssets("t:Sprite", new[] { typeFolder + "/Open" })
                .Select(g => AssetDatabase.GUIDToAssetPath(g)).OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).Where(s => s != null).ToArray();

            // the door leaves (switched off when the gate is open)
            var door = new GameObject("Blocker");
            door.transform.SetParent(root.transform, false);
            var bc = door.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(2.4f, 2.34f); bc.offset = new Vector2(0f, 1.17f);
            gate.blocker = bc;

            // the two posts (always solid)
            foreach (float x in new[] { -1.35f, 1.35f })
            {
                var post = new GameObject(x < 0 ? "Post_L" : "Post_R");
                post.transform.SetParent(root.transform, false);
                var pc = post.AddComponent<BoxCollider2D>();
                pc.size = new Vector2(0.3f, 2.8f); pc.offset = new Vector2(x, 1.4f);
            }

            // trigger in front of the gate
            var trig = root.AddComponent<BoxCollider2D>();
            trig.isTrigger = true; trig.size = new Vector2(3.4f, 2.4f); trig.offset = new Vector2(0f, 1.2f);

            // "[G] Open" hint
            var pgo = new GameObject("Prompt");
            pgo.transform.SetParent(root.transform, false);
            pgo.transform.localPosition = new Vector3(0f, 3.1f, 0f);
            var tmp = pgo.AddComponent<TextMeshPro>();
            tmp.text = "[G] Open"; tmp.fontSize = 3f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; tmp.sortingOrder = 10;
            gate.prompt = tmp;

            string prefabPath = Folder + "/Gate_" + type + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
#endif
