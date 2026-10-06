#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepVain.Items.EditorTools
{
    /// <summary>
    /// Menu: DeepVain > Chest > ...
    ///  1  Prepare sprites      import settings for the pictures in Assets/Chest (Point, PPU 100, pivot at the chest's feet)
    ///  2  Create chest prefab  makes Assets/Chest/Chest.prefab (and puts one chest in the open scene)
    /// </summary>
    public static class ChestBuilder
    {
        const string Folder = "Assets/Chest";

        [MenuItem("DeepVain/Chest/1 - Prepare sprites")]
        public static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { EditorUtility.DisplayDialog("Chest", "Fant ikke mappen " + Folder + ".\nKopier Chest-mappen dit først.", "OK"); return; }
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
                    bool big = path.Contains("/Glow/") || path.Contains("/Burst/");
                    s.spritePivot = big ? new Vector2(0.5f, 0.25f) : new Vector2(0.5f, 0.04f);   // chest feet / the light's origin
                    imp.SetTextureSettings(s);
                    imp.SaveAndReimport();
                    n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Debug.Log("[Chest] Sprite-innstillinger satt på " + n + " bilder.");
        }

        static Sprite[] Load(string sub)
        {
            return AssetDatabase.FindAssets("t:Sprite", new[] { Folder + "/" + sub })
                .Select(g => AssetDatabase.GUIDToAssetPath(g)).OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).Where(s => s != null).ToArray();
        }

        static SpriteRenderer NewRenderer(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        [MenuItem("DeepVain/Chest/2 - Create chest prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { EditorUtility.DisplayDialog("Chest", "Fant ikke mappen " + Folder + ".\nKopier Chest-mappen dit først.", "OK"); return; }
            Prepare();

            // one prefab per type folder (Assets/Chest/<Type>/Idle + Open); Glow and Burst are shared
            GameObject first = null;
            foreach (string typeFolder in AssetDatabase.GetSubFolders(Folder))
            {
                string type = System.IO.Path.GetFileName(typeFolder);
                if (type == "Glow" || type == "Burst") continue;
                if (!AssetDatabase.IsValidFolder(typeFolder + "/Idle") || !AssetDatabase.IsValidFolder(typeFolder + "/Open")) continue;
                var p = BuildOne(type);
                if (first == null) first = p;
            }
            if (first == null) { EditorUtility.DisplayDialog("Chest", "Fant ingen kistetyper (Assets/Chest/<Type>/Idle og Open).", "OK"); return; }

            // one chest in the open scene, next to the player if there is one
            var inv = UnityEngine.Object.FindFirstObjectByType<Inventory>();
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(first);
            if (inv != null) inst.transform.position = inv.transform.position + new Vector3(3f, -0.5f, 0f);
            Undo.RegisterCreatedObjectUndo(inst, "Place chest");
            EditorSceneManager.MarkSceneDirty(inst.scene);
            Selection.activeGameObject = inst;
            Debug.Log("[Chest] Kistene er laget i " + Folder + ". En kiste ligger i scenen. Sett Loot i Inspector. Åpne med G når du står ved den.");
        }

        static GameObject BuildOne(string type)
        {
            string typeFolder = Folder + "/" + type;
            var root = new GameObject("Chest");
            var chest = root.AddComponent<Chest>();
            var trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true; trigger.size = new Vector2(2.4f, 1.6f); trigger.offset = new Vector2(0f, 0.6f);

            chest.chestRenderer = NewRenderer("Sprite", root.transform, 0);
            chest.glowRenderer = NewRenderer("Glow", root.transform, 1);
            chest.burstRenderer = NewRenderer("Burst", root.transform, 2);
            chest.idleFrames = Load(type + "/Idle"); chest.openFrames = Load(type + "/Open");
            chest.glowFrames = Load("Glow"); chest.burstFrames = Load("Burst");
            if (chest.idleFrames.Length > 0) chest.chestRenderer.sprite = chest.idleFrames[0];

            // "G" hint above the chest
            var pgo = new GameObject("Prompt");
            pgo.transform.SetParent(root.transform, false);
            pgo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var tmp = pgo.AddComponent<TextMeshPro>();
            tmp.text = "[G]"; tmp.fontSize = 3f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; tmp.sortingOrder = 10;
            chest.prompt = pgo;

            // a real light in the scene (URP 2D Light2D), added only if the project has it
            Type lightType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
            if (lightType != null)
            {
                var lgo = new GameObject("Light");
                lgo.transform.SetParent(root.transform, false);
                lgo.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                var light = lgo.AddComponent(lightType) as Behaviour;
                var pType = lightType.GetProperty("lightType");
                if (pType != null) pType.SetValue(light, Enum.Parse(pType.PropertyType, "Point"));
                var pOuter = lightType.GetProperty("pointLightOuterRadius"); if (pOuter != null) pOuter.SetValue(light, 3.5f);
                var pInner = lightType.GetProperty("pointLightInnerRadius"); if (pInner != null) pInner.SetValue(light, 0.3f);
                light.enabled = false;
                chest.sceneLight = light;
            }

            // demo loot: the first items found in the project (set your own in the Inspector)
            var items = AssetDatabase.FindAssets("t:ItemData").Select(g => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g))).Where(i => i != null)
                .OrderBy(i => i.rarity).ToArray();
            if (items.Length > 0)
            {
                var picks = items.Skip(Mathf.Max(0, items.Length - 3)).ToArray();
                chest.loot = picks.Select(i => new Chest.LootEntry { item = i, amount = 1 }).ToArray();
            }

            string prefabPath = Folder + "/Chest_" + type + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
#endif
