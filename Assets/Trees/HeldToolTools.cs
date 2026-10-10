#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DeepVain.Player.EditorTools
{
    /// <summary>
    /// DeepVain > Player > Add held axe
    ///   1. Setter Axe_Held.png (og Pickaxe_Held.png) riktig i Unity: Sprite, 40 pixels per unit, Point, ingen komprimering, pivot Center.
    ///   2. Lager et objekt "HeldAxe" med SpriteRenderer under samme HandSocket (Front) som sverdet, med samme Sorting Layer / Order.
    ///   3. Legger HeldTool på spilleren (der TreeChopper ligger) og kobler alt sammen.
    /// Kan kjøres flere ganger uten å lage dobbelt.
    /// </summary>
    public static class HeldToolTools
    {
        static Sprite PrepareSprite(string assetName)
        {
            var guids = AssetDatabase.FindAssets(assetName + " t:Texture2D");
            if (guids.Length == 0) return null;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                bool changed = false;
                if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
                if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (!Mathf.Approximately(imp.spritePixelsPerUnit, 40f)) { imp.spritePixelsPerUnit = 40f; changed = true; }
                if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; changed = true; }
                if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
                if (s.spriteAlignment != (int)SpriteAlignment.Center) { s.spriteAlignment = (int)SpriteAlignment.Center; imp.SetTextureSettings(s); changed = true; }
                if (changed) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("DeepVain/Player/Add held axe")]
        public static void AddHeldAxe()
        {
            var chopper = Object.FindFirstObjectByType<TreeChopper>();
            if (chopper == null) { EditorUtility.DisplayDialog("Held axe", "Fant ingen TreeChopper i scenen. Legg TreeChopper på spilleren først.", "OK"); return; }

            var axeSprite = PrepareSprite("Axe_Held");
            var pickSprite = PrepareSprite("Pickaxe_Held");
            if (axeSprite == null) { EditorUtility.DisplayDialog("Held axe", "Fant ikke Axe_Held.png i prosjektet. Kopier den inn i Assets først.", "OK"); return; }

            // the hand the sword sits in
            HandSocket front = null;
            foreach (var h in Object.FindObjectsByType<HandSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (h.hand == HandSocket.Hand.Front) { front = h; break; }
            if (front == null) { EditorUtility.DisplayDialog("Held axe", "Fant ingen HandSocket (Front) i scenen. Den som holder sverdet.", "OK"); return; }

            // same sorting as the sword
            string layer = null; int order = 0; bool haveSort = false;
            foreach (var sr in front.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.gameObject.name != "HeldAxe" && sr.gameObject.name != "HeldPickaxe") { layer = sr.sortingLayerName; order = sr.sortingOrder; haveSort = true; break; }

            var axe = Make(front.transform, "HeldAxe", axeSprite, haveSort, layer, order);
            SpriteRenderer pick = pickSprite != null ? Make(front.transform, "HeldPickaxe", pickSprite, haveSort, layer, order) : null;

            var held = chopper.GetComponent<HeldTool>();
            if (held == null) held = Undo.AddComponent<HeldTool>(chopper.gameObject);
            Undo.RecordObject(held, "Held axe");
            held.axeRenderer = axe; held.axeSprite = axeSprite;
            held.pickaxeRenderer = pick; held.pickaxeSprite = pickSprite;
            EditorUtility.SetDirty(held);
            Debug.Log("[HeldTool] Øksa er koblet til " + front.name + " (sorting " + (haveSort ? layer + " / " + order : "standard") + "). Spill og hugg et tre.", held);
        }

        static SpriteRenderer Make(Transform parent, string name, Sprite sprite, bool haveSort, string layer, int order)
        {
            var t = parent.Find(name);
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Held tool");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (haveSort) { sr.sortingLayerName = layer; sr.sortingOrder = order; }
            sr.enabled = false;
            return sr;
        }
    }
}
#endif
