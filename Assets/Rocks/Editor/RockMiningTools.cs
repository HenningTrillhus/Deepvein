#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeepVain.Items;

namespace DeepVain.RockMiningTools
{
    /// <summary>
    /// DeepVain > Rocks > 1 - Set up rocks and items       gjør steinbildene til sprites, lager Coal-item, lager RockSet og legger RockSpawner i scenen
    /// DeepVain > Rocks > 2 - Set up mining on the player  legger RockMiner, RockFx og RockLoot på spilleren
    /// DeepVain > Rocks > Scatter rock slots on selected   legger ut steinplasser langs bakken i de valgte chunkene
    /// DeepVain > Rocks > Snap selected rock slots to ground
    /// GameObject > DeepVain > Rock Slot                   lager en plass for hånd
    /// </summary>
    public static class RockMiningMenu
    {
        const string CoalIconName = "Icon_Coal";

        // ---------- helpers
        static string FindAsset(string name, string filter)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " " + filter))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name) return p;
            }
            return null;
        }

        static void PrepareSprite(string path, Vector2 pivot, bool fullRect)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100f;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
            s.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            imp.SetTextureSettings(s);
            imp.spritePivot = pivot;
            imp.SaveAndReimport();
        }

        static Color AverageColor(string assetPath)
        {
            try
            {
                string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(full));
                var px = tex.GetPixels32();
                float r = 0, g = 0, b = 0; int n = 0;
                foreach (var c in px)
                {
                    if (c.a < 230) continue;
                    float l = (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;
                    if (l < 0.22f || l > 0.9f) continue;                     // skip the outline and the white glints
                    r += c.r; g += c.g; b += c.b; n++;
                }
                Object.DestroyImmediate(tex);
                if (n > 0) return new Color(r / n / 255f, g / n / 255f, b / n / 255f, 1f);
            }
            catch (System.Exception) { }
            return new Color(0.6f, 0.58f, 0.56f, 1f);
        }

        static string ItemFolder()
        {
            string folder = "Assets/Inventory/Items";
            var any = AssetDatabase.FindAssets("t:ItemData");
            if (any.Length > 0) folder = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(any[0])).Replace((char)92, '/');
            if (!AssetDatabase.IsValidFolder(folder)) Directory.CreateDirectory(folder);
            return folder;
        }

        static ItemData FindItem(string id)
        {
            foreach (var g in AssetDatabase.FindAssets("t:ItemData"))
            {
                var it = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g));
                if (it != null && it.id == id) return it;
            }
            return null;
        }

        static ItemData EnsureCoal()
        {
            var item = FindItem("coal");
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, ItemFolder() + "/coal.asset");
            }
            item.id = "coal"; item.displayName = "Coal"; item.type = ItemType.Material; item.rarity = ItemRarity.Common;
            item.description = "A lump of black coal. Burns hot.";
            item.maxStack = 99; item.sellValue = 2;
            string ip = FindAsset(CoalIconName, "t:Texture2D");
            if (ip != null)
            {
                var imp = AssetImporter.GetAtPath(ip) as TextureImporter;
                if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.filterMode != FilterMode.Point))
                {
                    imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                    imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.SaveAndReimport();
                }
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(ip);
                if (sp != null) item.icon = sp;
            }
            else Debug.LogWarning("[Rocks] Fant ikke " + CoalIconName + ".png. Kopier ikonet inn i Assets (UI/Inventory/Materials/Icon_Coal.png) og kjør menyen på nytt.");
            EditorUtility.SetDirty(item);
            return item;
        }

        // ---------- 1: rocks and items
        [MenuItem("DeepVain/Rocks/1 - Set up rocks and items")]
        public static void SetUpRocks()
        {
            var entries = new List<RockSet.Entry>();
            string folder = null;
            foreach (var g in AssetDatabase.FindAssets("Rock_ t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                string n = Path.GetFileNameWithoutExtension(p);
                if (!n.StartsWith("Rock_Stone_") && !n.StartsWith("Rock_Coal_")) continue;
                PrepareSprite(p, new Vector2(0.5f, 0.04f), true);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (sp == null) continue;
                bool coal = n.StartsWith("Rock_Coal_");
                entries.Add(new RockSet.Entry { label = n.Substring(5), sprite = sp, coal = coal, weight = 1f, health = coal ? 20f : 15f, chipColor = AverageColor(p) });
                if (folder == null) folder = Path.GetDirectoryName(p).Replace((char)92, '/');
            }
            if (entries.Count == 0)
            {
                EditorUtility.DisplayDialog("Rocks", "Fant ingen steinbilder (Rock_Stone_*.png / Rock_Coal_*.png). Kopier mappen Rocks/v6/Pictures inn i Assets først.", "OK");
                return;
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.label, b.label));

            // the smoke and the chips
            int smoke = 0, chips = 0;
            for (int i = 1; i <= 10; i++)
            {
                string p = FindAsset("Smoke_" + i.ToString("00"), "t:Texture2D");
                if (p != null) { PrepareSprite(p, new Vector2(0.5f, 7f / 64f), false); smoke++; }
            }
            foreach (var c in new[] { "A", "B", "C" })
            {
                string p = FindAsset("Chip_" + c, "t:Texture2D");
                if (p != null) { PrepareSprite(p, new Vector2(0.5f, 0.5f), false); chips++; }
            }
            if (smoke < 10 || chips < 3) Debug.LogWarning("[Rocks] Fant " + smoke + " av 10 røykbilder og " + chips + " av 3 småbiter. Kopier Rocks/Crush/Smoke og Rocks/Crush/Chips inn i Assets.");

            var stone = FindItem("stone");
            if (stone == null) Debug.LogWarning("[Rocks] Fant ikke item-et Stone (id \"stone\").");
            var coalItem = EnsureCoal();

            string setPath = folder + "/RockSet.asset";
            var set = AssetDatabase.LoadAssetAtPath<RockSet>(setPath);
            if (set == null) { set = ScriptableObject.CreateInstance<RockSet>(); AssetDatabase.CreateAsset(set, setPath); }
            set.entries = entries.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();

            // the spawner (next to the others)
            var spawner = Object.FindFirstObjectByType<RockSpawner>();
            if (spawner == null)
            {
                var host = Object.FindFirstObjectByType<PropSpawner>();
                GameObject go = host != null ? host.gameObject : null;
                if (go == null) { var wb = Object.FindFirstObjectByType<WorldBuilder>(); go = wb != null ? wb.gameObject : new GameObject("RockSpawner"); }
                spawner = Undo.AddComponent<RockSpawner>(go);
            }
            Undo.RecordObject(spawner, "Rock Set");
            spawner.rockSet = set;
            var ts = Object.FindFirstObjectByType<TreeSpawner>();
            if (ts != null)
            {
                spawner.sortingLayer = ts.sortingLayer;
                spawner.sortingOrder = ts.sortingOrder + ts.orderSpread + 1;
                if (ts.swayMaterial != null) spawner.swayMaterial = ts.swayMaterial;
            }
            EditorUtility.SetDirty(spawner);
            if (spawner.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);

            int coalN = 0; foreach (var e in entries) if (e.coal) coalN++;
            Debug.Log("[Rocks] " + setPath + ": " + entries.Count + " steiner (" + coalN + " med kull). Satt på RockSpawner på \"" + spawner.name
                + "\". Neste: meny 2, så rock slots (Scatter rock slots on selected). Lagre scenen.", set);
        }

        // ---------- 2: the player
        [MenuItem("DeepVain/Rocks/2 - Set up mining on the player")]
        public static void SetUpPlayer()
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm == null) { Debug.LogWarning("[Rocks] Fant ingen PlayerMovement i scenen (spilleren må stå i scenen)."); return; }
            var go = pm.gameObject;

            if (go.GetComponent<TreeChopper>() == null) Debug.LogWarning("[Rocks] Spilleren har ingen TreeChopper. Hakking av stein virker likevel, men sett opp trærne også.");
            var miner = go.GetComponent<RockMiner>(); if (miner == null) miner = Undo.AddComponent<RockMiner>(go);
            var fx = go.GetComponent<RockFx>(); if (fx == null) fx = Undo.AddComponent<RockFx>(go);
            var loot = go.GetComponent<RockLoot>(); if (loot == null) loot = Undo.AddComponent<RockLoot>(go);

            var smoke = new List<Sprite>();
            for (int i = 1; i <= 10; i++)
            {
                string p = FindAsset("Smoke_" + i.ToString("00"), "t:Texture2D");
                var sp = p != null ? AssetDatabase.LoadAssetAtPath<Sprite>(p) : null;
                if (sp != null) smoke.Add(sp);
            }
            var chips = new List<Sprite>();
            foreach (var c in new[] { "A", "B", "C" })
            {
                string p = FindAsset("Chip_" + c, "t:Texture2D");
                var sp = p != null ? AssetDatabase.LoadAssetAtPath<Sprite>(p) : null;
                if (sp != null) chips.Add(sp);
            }
            Undo.RecordObject(fx, "Rock FX");
            fx.smokeFrames = smoke.ToArray(); fx.chipSprites = chips.ToArray();
            Undo.RecordObject(loot, "Rock Loot");
            loot.stone = FindItem("stone"); loot.coal = FindItem("coal");
            EditorUtility.SetDirty(fx); EditorUtility.SetDirty(loot); EditorUtility.SetDirty(miner);

            if (Object.FindFirstObjectByType<TreeHover>() == null)
                Debug.LogWarning("[Rocks] Fant ingen TreeHover i scenen. Legg den på et objekt (f.eks. samme som WorldBuilder), den gir kontur og velger steiner.");
            if (go.GetComponent<HeldTool>() == null) Debug.LogWarning("[Rocks] Fant ingen HeldTool på spilleren, så hakka vises ikke i hånda. Kjør menyen for hakke/øks i hånda.");

            if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[Rocks] RockMiner, RockFx og RockLoot ligger på spilleren (" + smoke.Count + " røykbilder, " + chips.Count + " småbiter, Stone: "
                + (loot.stone != null ? "ja" : "NEI") + ", Coal: " + (loot.coal != null ? "ja" : "NEI") + "). Lagre scenen og prøv.");
        }

        // ---------- 3: slots
        [MenuItem("GameObject/DeepVain/Rock Slot", false, 22)]
        public static void CreateSlot(MenuCommand cmd)
        {
            var go = new GameObject("RockSlot");
            go.AddComponent<RockSlot>();
            var parent = cmd.context as GameObject != null ? ((GameObject)cmd.context).transform : Selection.activeTransform;
            if (parent != null) go.transform.SetParent(parent, false);
            else if (SceneView.lastActiveSceneView != null) { var pv = SceneView.lastActiveSceneView.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0f); }
            Undo.RegisterCreatedObjectUndo(go, "Create RockSlot");
            Selection.activeGameObject = go;
        }

        [MenuItem("DeepVain/Rocks/Snap selected rock slots to ground")]
        public static void SnapSelected()
        {
            Physics2D.SyncTransforms();
            int n = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go.GetComponent<RockSlot>() == null) continue;
                Vector2 from = (Vector2)go.transform.position + Vector2.up * 4f;
                foreach (var h in Physics2D.RaycastAll(from, Vector2.down, 40f))
                {
                    if (h.collider == null || h.collider.isTrigger) continue;
                    Undo.RecordObject(go.transform, "Snap RockSlot");
                    go.transform.position = new Vector3(go.transform.position.x, h.point.y, go.transform.position.z);
                    n++;
                    break;
                }
            }
            Debug.Log("[Rocks] " + n + " plasser flyttet ned på bakken.");
        }

        /// <summary>Gap between rock slots in units, random +/- 40 %. More than for props: rocks are big.</summary>
        const float Spacing = 6f;

        [MenuItem("DeepVain/Rocks/Scatter rock slots on selected")]
        public static void ScatterSelected()
        {
            Physics2D.SyncTransforms();
            var rng = new System.Random(Random.Range(0, int.MaxValue));
            int total = 0;
            foreach (var root in Selection.gameObjects)
            {
                var cols = root.GetComponentsInChildren<Collider2D>();
                if (cols.Length == 0) { Debug.LogWarning("[Rocks] " + root.name + " har ingen colliders, så jeg finner ikke bakken."); continue; }
                Bounds b = cols[0].bounds;
                foreach (var c in cols) b.Encapsulate(c.bounds);

                var oldHolder = root.transform.Find("RockSlots");
                if (oldHolder != null) Undo.DestroyObjectImmediate(oldHolder.gameObject);

                var holder = new GameObject("RockSlots");
                Undo.RegisterCreatedObjectUndo(holder, "Scatter rock slots");
                holder.transform.SetParent(root.transform, true);
                holder.transform.position = root.transform.position;

                int n = 0;
                for (float x = b.min.x + 2f; x < b.max.x - 1f; x += Spacing * (0.6f + 0.8f * (float)rng.NextDouble()))
                {
                    Vector2 from = new Vector2(x, b.max.y + 2f);
                    foreach (var h in Physics2D.RaycastAll(from, Vector2.down, b.size.y + 6f))
                    {
                        if (h.collider == null || h.collider.isTrigger) continue;
                        if (!h.collider.transform.IsChildOf(root.transform) && h.collider.transform != root.transform) continue;
                        var go = new GameObject("RockSlot");
                        Undo.RegisterCreatedObjectUndo(go, "Scatter rock slots");
                        go.transform.SetParent(holder.transform, true);
                        go.transform.position = new Vector3(x, h.point.y, 0f);
                        go.AddComponent<RockSlot>();
                        n++;
                        break;
                    }
                }
                total += n;
                Debug.Log("[Rocks] " + root.name + ": " + n + " steinplasser lagt ut langs bakken (under \"RockSlots\"). Slett de du ikke vil ha.", root);
            }
            if (total == 0) Debug.Log("[Rocks] Ingen plasser laget. Merk chunk-objektet (med colliders) i scenen først.");
        }
    }
}
#endif
