#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepVain.Player.EditorTools
{
    /// <summary>
    /// Menu: DeepVain > Stamina > ...
    ///  1  Prepare sprites   import settings for Assets/Stamina/Sprites (Point, Pixels Per Unit 1, pivot at the top centre)
    ///  2  Add to player     puts PlayerStamina + StaminaBarView on the player in the open scene and gives it the pictures
    /// </summary>
    public static class StaminaBuilder
    {
        const string Folder = "Assets/Stamina/Sprites";

        [MenuItem("DeepVain/Stamina/1 - Prepare sprites")]
        public static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { EditorUtility.DisplayDialog("Stamina", "Fant ikke mappen " + Folder + ".\nKopier Stamina/Sprites dit først (Assets/Stamina/Sprites).", "OK"); return; }
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
                    imp.spritePixelsPerUnit = 1f;                       // the bar is scaled in code to the player's pixel size
                    imp.filterMode = FilterMode.Point;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    var s = new TextureImporterSettings();
                    imp.ReadTextureSettings(s);
                    s.spriteAlignment = (int)SpriteAlignment.Custom;
                    s.spritePivot = new Vector2(0.5f, 1f);              // top centre: it hangs under the feet
                    imp.SetTextureSettings(s);
                    imp.SaveAndReimport();
                    n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Debug.Log("[Stamina] Sprite-innstillinger satt på " + n + " bilder.");
        }

        static Sprite[] Load(string prefix)
        {
            return AssetDatabase.FindAssets("t:Sprite " + prefix, new[] { Folder })
                .Select(g => AssetDatabase.GUIDToAssetPath(g)).Where(p => System.IO.Path.GetFileName(p).StartsWith(prefix)).OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).Where(s => s != null).ToArray();
        }

        [MenuItem("DeepVain/Stamina/2 - Add to player")]
        public static void AddToPlayer()
        {
            var move = Object.FindFirstObjectByType<PlayerMovement>();
            if (move == null) { EditorUtility.DisplayDialog("Stamina", "Fant ingen PlayerMovement i scenen. Åpne scenen med spilleren først.", "OK"); return; }
            Prepare();
            var go = move.gameObject;
            var st = go.GetComponent<PlayerStamina>() ?? Undo.AddComponent<PlayerStamina>(go);
            var view = go.GetComponent<StaminaBarView>() ?? Undo.AddComponent<StaminaBarView>(go);
            Undo.RecordObject(view, "Stamina bar");
            view.amber = Load("Stamina_Amber_");
            view.red = Load("Stamina_Red_");
            EditorUtility.SetDirty(view); EditorUtility.SetDirty(st);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            Debug.Log("[Stamina] Lagt på spilleren (" + view.amber.Length + " + " + view.red.Length + " bilder). Løp, rull og hopp koster stamina; baren vises under 85 %.");
        }
    }
}
#endif
