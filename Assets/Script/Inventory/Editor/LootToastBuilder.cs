#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepVain.Items.EditorTools
{
    /// <summary>
    /// Menu: DeepVain > Loot toasts > ...
    ///  1  Prepare sprites   import settings for the pictures in Assets/Inventory/ChestLoot/Sprites (Sprite, Point, PPU 1, 9-slice on the count chip)
    ///  2  Build in scene    makes "LootToastCanvas" (bottom right corner) with everything wired up
    /// </summary>
    public static class LootToastBuilder
    {
        const string Folder = "Assets/Inventory/ChestLoot/Sprites";

        static readonly Color Cream = new Color32(0xF0, 0xDC, 0xB0, 255);
        static readonly Color Shadow = new Color32(0x1A, 0x0E, 0x08, 255);

        [MenuItem("DeepVain/Loot toasts/1 - Prepare sprites")]
        public static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { EditorUtility.DisplayDialog("Loot toasts", "Fant ikke mappen " + Folder + ".\nKopier ChestLoot-mappen til Assets/Inventory først.", "OK"); return; }
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
                    imp.spritePixelsPerUnit = 1f;
                    imp.filterMode = FilterMode.Point;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    float b = Path.GetFileNameWithoutExtension(path) == "Toast_Chip" ? 3f : 0f;
                    imp.spriteBorder = new Vector4(b, b, b, b);
                    imp.SaveAndReimport();
                    n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Debug.Log("[Loot toasts] Sprite-innstillinger satt på " + n + " bilder.");
        }

        static Sprite Sp(string file)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + file);
            if (s == null) Debug.LogWarning("[Loot toasts] Mangler bilde: " + Folder + "/" + file);
            return s;
        }

        static RectTransform NewRT(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        static TMP_Text NewText(string name, Transform parent, float x, float y, float w, float h, TextAlignmentOptions align, Color col, bool bold)
        {
            var rt = NewRT(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = ""; t.fontSize = 8f; t.color = col; t.alignment = align; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
            if (bold) t.fontStyle = FontStyles.Bold;
            Place(rt, x, y, w, h);
            return t;
        }

        [MenuItem("DeepVain/Loot toasts/2 - Build in scene")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { EditorUtility.DisplayDialog("Loot toasts", "Fant ikke mappen " + Folder + ".\nKopier ChestLoot-mappen til Assets/Inventory først.", "OK"); return; }
            Prepare();

            var old = GameObject.Find("LootToastCanvas");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var canvasGO = new GameObject("LootToastCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Build loot toasts");
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 15; canvas.pixelPerfect = true;   // under the book (20)
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 1f;

            // container: bottom right corner, 10 px from the edges
            var container = NewRT("Container", canvasGO.transform);
            container.anchorMin = container.anchorMax = new Vector2(1f, 0f); container.pivot = new Vector2(1f, 0f);
            container.anchoredPosition = new Vector2(-10f, 10f); container.sizeDelta = new Vector2(LootToastUI.Width, 200f);

            // the strip that is copied (inactive)
            var tpl = NewRT("Toast_Template", container);
            tpl.anchorMin = tpl.anchorMax = new Vector2(1f, 0f); tpl.pivot = new Vector2(1f, 0f);
            tpl.sizeDelta = new Vector2(LootToastUI.Width, LootToastUI.Height); tpl.anchoredPosition = Vector2.zero;
            var bg = tpl.gameObject.AddComponent<Image>(); bg.raycastTarget = false; bg.sprite = Sp("Toast_Common.png");
            tpl.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            var icon = NewRT("Icon", tpl); Place(icon, 6f, 6f, 18f, 18f);
            var iconImg = icon.gameObject.AddComponent<Image>(); iconImg.raycastTarget = false; iconImg.preserveAspect = true;

            var chip = NewRT("Chip", tpl); Place(chip, 118f, 8f, 24f, 13f);
            var chipImg = chip.gameObject.AddComponent<Image>(); chipImg.raycastTarget = false; chipImg.type = Image.Type.Sliced; chipImg.sprite = Sp("Toast_Chip.png");

            // shadows first so they are drawn under the real text
            NewText("NameShadow", tpl, 32f, 6f, 80f, 11f, TextAlignmentOptions.MidlineLeft, Shadow, true);
            NewText("Name", tpl, 32f, 5f, 80f, 11f, TextAlignmentOptions.MidlineLeft, Cream, true);
            NewText("SubShadow", tpl, 32f, 17f, 112f, 11f, TextAlignmentOptions.MidlineLeft, Shadow, false);
            NewText("Sub", tpl, 32f, 16f, 112f, 11f, TextAlignmentOptions.MidlineLeft, Cream, false);
            NewText("CountShadow", tpl, 118f, 10f, 24f, 11f, TextAlignmentOptions.Center, Shadow, true);
            NewText("Count", tpl, 118f, 9f, 24f, 11f, TextAlignmentOptions.Center, Cream, true);
            tpl.gameObject.SetActive(false);

            var ui = canvasGO.AddComponent<LootToastUI>();
            ui.container = container; ui.template = tpl; ui.chipSprite = chipImg.sprite;
            string[] r = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
            ui.rarityBackgrounds = new Sprite[5];
            for (int i = 0; i < 5; i++) ui.rarityBackgrounds[i] = Sp("Toast_" + r[i] + ".png");

            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
            Selection.activeGameObject = canvasGO;
            Debug.Log("[Loot toasts] Ferdig. Kista viser nå en stripe per ting når den åpnes.");
        }
    }
}
#endif
