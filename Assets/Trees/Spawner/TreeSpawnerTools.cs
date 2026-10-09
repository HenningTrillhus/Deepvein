#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepVain.TreeSpawnerTools
{
    /// <summary>
    /// GameObject > DeepVain > Tree Slot                   makes a TreeSlot (under the selected object, or at the Scene view centre)
    /// DeepVain > Trees > 3 - Make Tree Set from selected   sets the import of the selected tree pictures (50 px per unit, pivot at the feet,
    ///                                                      Point, Full Rect) and makes a TreeSet asset with all of them
    /// DeepVain > Trees > 4 - Snap selected slots to ground  moves the selected TreeSlots down onto the ground below them
    /// </summary>
    public static class TreeSpawnerMenu
    {
        public const float Ppu = 50f;                       // trees are 112 x 160 drawn pixels = 2.24 x 3.2 units
        public const float PivotY = 1.5f / 160f;            // the feet are 1.5 drawn pixels above the bottom of the picture

        [MenuItem("GameObject/DeepVain/Tree Slot", false, 20)]
        public static void CreateSlot(MenuCommand cmd)
        {
            var go = new GameObject("TreeSlot");
            go.AddComponent<TreeSlot>();
            var parent = cmd.context as GameObject != null ? ((GameObject)cmd.context).transform : (Selection.activeTransform);
            if (parent != null) go.transform.SetParent(parent, false);
            else if (SceneView.lastActiveSceneView != null) { var pv = SceneView.lastActiveSceneView.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0f); }
            Undo.RegisterCreatedObjectUndo(go, "Create TreeSlot");
            Selection.activeGameObject = go;
        }

        public static void PrepareTexture(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = Ppu;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = new Vector2(0.5f, PivotY);
            imp.SetTextureSettings(s);
            imp.SaveAndReimport();
        }

        /// <summary>Prepares the pictures and builds a TreeSet that contains them all.</summary>
        public static TreeSet MakeSet(IList<string> texturePaths, string setPath)
        {
            var entries = new List<TreeSet.Entry>();
            foreach (var path in texturePaths)
            {
                PrepareTexture(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                entries.Add(new TreeSet.Entry { label = name.StartsWith("Tree_") ? name.Substring(5) : name, sprite = sprite, weight = 1f });
            }
            var set = AssetDatabase.LoadAssetAtPath<TreeSet>(setPath);
            if (set == null) { set = ScriptableObject.CreateInstance<TreeSet>(); AssetDatabase.CreateAsset(set, setPath); }
            set.entries = entries.ToArray();
            EditorUtility.SetDirty(set); AssetDatabase.SaveAssets();
            return set;
        }

        [MenuItem("DeepVain/Trees/3 - Make Tree Set from selected pictures")]
        public static void MakeSetFromSelection()
        {
            var paths = new List<string>();
            foreach (var o in Selection.objects)
            {
                string p = AssetDatabase.GetAssetPath(o);
                if (AssetDatabase.IsValidFolder(p)) foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { p })) paths.Add(AssetDatabase.GUIDToAssetPath(g));
                else if (o is Texture2D) paths.Add(p);
            }
            paths.RemoveAll(p => !p.EndsWith(".png") || Path.GetFileName(p).Contains("Preview"));
            if (paths.Count == 0) { EditorUtility.DisplayDialog("Tree Set", "Merk tre-bildene (eller mappen de ligger i) i Project-vinduet først.", "OK"); return; }
            paths.Sort();
            string dir = Path.GetDirectoryName(paths[0]).Replace('\\', '/');
            var set = MakeSet(paths, dir + "/TreeSet.asset");
            Selection.activeObject = set; EditorGUIUtility.PingObject(set);
            Debug.Log("[TreeSpawner] TreeSet med " + set.entries.Length + " trær laget: " + dir + "/TreeSet.asset. Dra den inn i Tree Set på TreeSpawner.");
        }

        [MenuItem("DeepVain/Trees/4 - Snap selected slots to ground")]
        public static void SnapSelected()
        {
            int n = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go.GetComponent<TreeSlot>() == null) continue;
                Vector2 from = (Vector2)go.transform.position + Vector2.up * 4f;
                // the first solid thing below the slot (the player's ground); ignore triggers
                foreach (var h in Physics2D.RaycastAll(from, Vector2.down, 40f))
                {
                    if (h.collider == null || h.collider.isTrigger) continue;
                    Undo.RecordObject(go.transform, "Snap TreeSlot");
                    go.transform.position = new Vector3(go.transform.position.x, h.point.y, go.transform.position.z);
                    n++; break;
                }
            }
            Debug.Log("[TreeSpawner] " + n + " plasser flyttet ned på bakken.");
        }
    }
}
#endif
