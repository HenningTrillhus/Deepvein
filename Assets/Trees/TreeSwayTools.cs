#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepVain.TreeSwayTools
{
    /// <summary>
    /// DeepVain > Trees > 1 - Create sway material     makes Assets/TreeSway/TreeSway.mat (uses the shader "DeepVain/Sprite Sway")
    /// DeepVain > Trees > 2 - Add TreeSway to selected   adds the TreeSway script (with that material) to every selected prefab or scene object
    ///                                                   that has a SpriteRenderer, and sets their sprites to Mesh Type = Full Rect (so the sway is not cut off)
    /// </summary>
    public static class TreeSwayMenu
    {
        const string MatPath = "Assets/TreeSway/TreeSway.mat";

        [MenuItem("DeepVain/Trees/1 - Create sway material")]
        public static Material CreateMaterial()
        {
            var shader = Shader.Find("DeepVain/Sprite Sway");
            if (shader == null) { EditorUtility.DisplayDialog("TreeSway", "Fant ikke shaderen \"DeepVain/Sprite Sway\". Kopier SpriteSway.shader inn i Assets først.", "OK"); return null; }
            Directory.CreateDirectory("Assets/TreeSway");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MatPath); }
            else mat.shader = shader;
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
            Debug.Log("[TreeSway] Material: " + MatPath);
            return mat;
        }

        static void FullRect(Sprite sprite)
        {
            if (sprite == null) return;
            string path = AssetDatabase.GetAssetPath(sprite.texture);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
            if (s.spriteMeshType == SpriteMeshType.FullRect) return;
            s.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(s); imp.SaveAndReimport();
        }

        [MenuItem("DeepVain/Trees/2 - Add TreeSway to selected")]
        public static void AddToSelected()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath) ?? CreateMaterial();
            if (mat == null) return;
            int n = 0;
            foreach (var go in Selection.gameObjects)
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                var sway = go.GetComponent<TreeSway>() ?? go.AddComponent<TreeSway>();
                sway.swayMaterial = mat;
                FullRect(sr.sprite);
                EditorUtility.SetDirty(go);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[TreeSway] La til TreeSway på " + n + " objekt(er). Sprite-ene er satt til Full Rect.");
        }
    }
}
#endif
