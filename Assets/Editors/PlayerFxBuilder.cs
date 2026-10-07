#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeepVain.Player;

namespace DeepVain.Player.EditorTools
{
    /// <summary>
    /// Menu: DeepVain > Player FX > ...
    ///  1  Prepare sprites   import settings for Assets/PlayerFx/Sprites (Point filter; the wind gets the player's Pixels Per Unit and the player's feet as pivot, the dust gets PPU 1)
    ///  2  Add to player     puts PlayerMoveFx on the player in the open scene and gives it the pictures
    /// </summary>
    public static class PlayerFxBuilder
    {
        const string Folder = "Assets/PlayerFx/Sprites";

        static float PlayerPpu()
        {
            var sync = Object.FindFirstObjectByType<PlayerLayerSync>();
            if (sync != null && sync.slots != null)
                foreach (var s in sync.slots)
                {
                    if (s == null) continue;
                    if (s.renderer != null && s.renderer.sprite != null) return s.renderer.sprite.pixelsPerUnit;
                    if (s.set != null && s.set.animations != null)                       // not drawn yet in edit mode: read it from the sprite sets
                        foreach (var a in s.set.animations)
                            if (a.frames != null && a.frames.Length > 0 && a.frames[0] != null) return a.frames[0].pixelsPerUnit;
                }
            Debug.LogWarning("[Player FX] Fant ikke spilleren i scenen. Bruker Pixels Per Unit 16. Åpne scenen med spilleren og kjør 1 på nytt hvis det er feil.");
            return 16f;
        }

        [MenuItem("DeepVain/Player FX/1 - Prepare sprites")]
        public static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { EditorUtility.DisplayDialog("Player FX", "Fant ikke mappen " + Folder + ".\nKopier RollFx_Game/Sprites dit først (Assets/PlayerFx/Sprites).", "OK"); return; }
            float ppu = PlayerPpu();
            AssetDatabase.StartAssetEditing();
            int n = 0;
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) continue;
                    bool wind = path.Contains("/Wind/");
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.spritePixelsPerUnit = wind ? ppu : 1f;
                    imp.filterMode = FilterMode.Point;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    var s = new TextureImporterSettings();
                    imp.ReadTextureSettings(s);
                    s.spriteAlignment = (int)SpriteAlignment.Custom;
                    // wind: 96 x 40, the player's feet are at x = 76, 1 px above the bottom (same as the Roll frames).  dust: 24 x 16, bottom centre
                    s.spritePivot = wind ? new Vector2(76f / 96f, 1f / 40f) : new Vector2(0.5f, 0f);
                    imp.SetTextureSettings(s);
                    imp.SaveAndReimport();
                    n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Debug.Log("[Player FX] Sprite-innstillinger satt på " + n + " bilder (vind PPU " + ppu + ").");
        }

        static Sprite[] Load(string sub)
        {
            return AssetDatabase.FindAssets("t:Sprite", new[] { Folder + "/" + sub })
                .Select(g => AssetDatabase.GUIDToAssetPath(g)).OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).Where(s => s != null).ToArray();
        }

        [MenuItem("DeepVain/Player FX/2 - Add to player")]
        public static void AddToPlayer()
        {
            var move = Object.FindFirstObjectByType<PlayerMovement>();
            if (move == null) { EditorUtility.DisplayDialog("Player FX", "Fant ingen PlayerMovement i scenen. Åpne scenen med spilleren først.", "OK"); return; }
            Prepare();
            var fx = move.GetComponent<PlayerMoveFx>();
            if (fx == null) fx = Undo.AddComponent<PlayerMoveFx>(move.gameObject);
            Undo.RecordObject(fx, "Player FX");
            fx.windFrames = Load("Wind");
            fx.dustFrames = Load("Dust");
            EditorUtility.SetDirty(fx);
            EditorSceneManager.MarkSceneDirty(move.gameObject.scene);
            Selection.activeGameObject = move.gameObject;
            Debug.Log("[Player FX] PlayerMoveFx er lagt på spilleren (" + fx.windFrames.Length + " vindbilder, " + fx.dustFrames.Length + " støvbilder). Ruller du eller løper (Shift) kommer vind og støv.");
        }
    }
}
#endif
