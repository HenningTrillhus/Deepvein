#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepVain.GoblinGateTools
{
    /// <summary>
    /// DeepVain > Gate > Build goblin gate (door)
    ///   1. Importerer portbildene (Gate_Goblin_Closed og Gate_Goblin_Open_01..08 i en mappe som heter GoblinSmall) som veggene: 100 px per unit, Point, pivot i midten.
    ///   2. Lager prefaben Assets/Gate/Gate_Goblin_Small.prefab: ett bilde, scale 2 (som veggene), GoblinGate, ingen collider. Pair Id = "goblin_camp".
    ///   3. Legger DoorEnterDriver på spilleren hvis den mangler.
    ///   4. Setter en port i scenen. Sett en til et annet sted: to porter med samme Pair Id går til hverandre.
    /// </summary>
    public static class GoblinGateMenu
    {
        static string Find(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name && p.IndexOf("GoblinSmall", System.StringComparison.OrdinalIgnoreCase) >= 0) return p;
            }
            return null;
        }

        static Sprite Prepare(string path)
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
                if (st.spriteMeshType != SpriteMeshType.FullRect || st.spriteAlignment != (int)SpriteAlignment.Center)
                {
                    st.spriteMeshType = SpriteMeshType.FullRect; st.spriteAlignment = (int)SpriteAlignment.Center; imp.SetTextureSettings(st); changed = true;
                }
                if (changed) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("DeepVain/Gate/Build goblin gate (door)")]
        public static void Build()
        {
            string closedPath = Find("Gate_Goblin_Closed");
            if (closedPath == null) { EditorUtility.DisplayDialog("Gate", "Fant ikke Gate_Goblin_Closed i en mappe som heter GoblinSmall. Kopier mappen Gate_v2/Goblin inn i Assets/Gate/GoblinSmall først.", "OK"); return; }
            var closed = Prepare(closedPath);
            var open = new Sprite[8];
            for (int i = 0; i < 8; i++)
            {
                string p = Find("Gate_Goblin_Open_" + (i + 1).ToString("00"));
                if (p == null) { EditorUtility.DisplayDialog("Gate", "Mangler Gate_Goblin_Open_" + (i + 1).ToString("00") + " i GoblinSmall.", "OK"); return; }
                open[i] = Prepare(p);
            }

            string dir = Path.GetDirectoryName(Path.GetDirectoryName(closedPath)).Replace('\\', '/');          // Assets/Gate
            if (Path.GetFileName(Path.GetDirectoryName(closedPath)) != "GoblinSmall") dir = Path.GetDirectoryName(closedPath).Replace('\\', '/');
            string prefabPath = dir + "/Gate_Goblin_Small.prefab";

            var go = new GameObject("Gate_Goblin_Small", typeof(SpriteRenderer));
            go.transform.localScale = new Vector3(2f, 2f, 1f);
            var sr = go.GetComponent<SpriteRenderer>(); sr.sprite = closed;
            var gate = go.AddComponent<GoblinGate>();
            gate.spriteRenderer = sr; gate.closedSprite = closed; gate.openSprites = open; gate.pairId = "goblin_camp";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            // the player
            string msg = "";
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null)
            {
                if (pm.GetComponent<DoorEnterDriver>() == null) { Undo.AddComponent<DoorEnterDriver>(pm.gameObject); msg += " DoorEnterDriver lagt på spilleren."; }
            }
            else msg += " (Fant ingen spiller i scenen, så DoorEnterDriver ble ikke lagt på. Legg den på spilleren selv.)";

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(inst, "Goblin gate");
            if (pm != null) inst.transform.position = pm.transform.position + new Vector3(3f, 1.7f, 0f);
            Selection.activeGameObject = inst;
            Debug.Log("[GoblinGate] Prefab " + prefabPath + " laget, en port satt i scenen." + msg + " Pair Id = goblin_camp: sett en port til et annet sted med samme Pair Id, så går de til hverandre.", prefab);
        }
    }
}
#endif
