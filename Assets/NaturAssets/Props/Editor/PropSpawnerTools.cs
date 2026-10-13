#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepVain.PropSpawnerTools
{
    /// <summary>
    /// GameObject > DeepVain > Prop Slot                           lager en PropSlot (under det valgte objektet, eller midt i Scene-vinduet)
    /// DeepVain > Props > Build still props                        lager prefaber av statiske prop-bilder (Wood_BranchPile, Wood_LogFallen, Wood_Stump,
    ///                                                              Rock_Pebbles, Ground_AcornsLeaf), uten å røre bildene
    /// DeepVain > Props > Make Prop Set from animated props         lager en PropSet av alle prop-prefaber (animerte og statiske)
    ///                                                              og setter den på en PropSpawner (lager en hvis det ikke finnes)
    /// DeepVain > Props > Snap selected prop slots to ground        flytter de valgte plassene ned på bakken under seg
    /// DeepVain > Props > Scatter prop slots on selected            fyller de valgte chunkene (i scenen) med plasser langs bakken
    /// </summary>
    public static class PropSpawnerMenu
    {
        [MenuItem("GameObject/DeepVain/Prop Slot", false, 21)]
        public static void CreateSlot(MenuCommand cmd)
        {
            var go = new GameObject("PropSlot");
            go.AddComponent<PropSlot>();
            var parent = cmd.context as GameObject != null ? ((GameObject)cmd.context).transform : Selection.activeTransform;
            if (parent != null) go.transform.SetParent(parent, false);
            else if (SceneView.lastActiveSceneView != null) { var pv = SceneView.lastActiveSceneView.pivot; go.transform.position = new Vector3(pv.x, pv.y, 0f); }
            Undo.RegisterCreatedObjectUndo(go, "Create PropSlot");
            Selection.activeGameObject = go;
        }

        static bool IsAnimatedProp(string name)
        {
            return name.StartsWith("Flower_") || name.StartsWith("Plant_") || name.StartsWith("Mushroom_");
        }

        // the still props that are made into prefabs (picture names)
        static readonly string[] StillNames = { "Wood_BranchPile", "Wood_LogFallen", "Wood_Stump", "Rock_Pebbles", "Ground_AcornsLeaf" };
        const float GroundRows = 2f;     // the 50x50 pictures have two empty rows under the thing

        /// <summary>
        /// Makes a prefab of every still prop picture (the five above, or the pictures you have selected in the Project window).
        /// The pictures and their import settings are not touched, so props you already placed by hand stay where they are:
        /// the prefab has an empty root (scale 2, at ground level) with the sprite as a child that is lifted so the thing stands on the root.
        /// </summary>
        [MenuItem("DeepVain/Props/Build still props")]
        public static int BuildStill()
        {
            var paths = new List<string>();
            foreach (var o in Selection.objects)
            {
                string p = AssetDatabase.GetAssetPath(o);
                if (p.EndsWith(".png") && o is Texture2D) paths.Add(p);
            }
            if (paths.Count == 0)
            {
                foreach (var n in StillNames)
                {
                    string best = null;
                    foreach (var g in AssetDatabase.FindAssets(n + " t:Texture2D"))
                    {
                        string p = AssetDatabase.GUIDToAssetPath(g);
                        if (Path.GetFileNameWithoutExtension(p) != n) continue;
                        if (best == null) best = p;
                        if (p.Replace((char)92, '/').Contains("/Prop/")) { best = p; break; }          // the folder with your other props
                    }
                    if (best != null) paths.Add(best);
                }
            }
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("Still props", "Fant ingen av bildene (Wood_BranchPile, Rock_Pebbles ...) i prosjektet. Merk bildene du vil bruke i Project-vinduet, og prøv igjen.", "OK");
                return 0;
            }

            bool layer = false;
            foreach (var l in SortingLayer.layers) if (l.name == "NaturAsset") layer = true;

            int made = 0;
            foreach (var path in paths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) { Debug.LogWarning("[PropSpawner] " + path + " er ikke importert som Sprite (Texture Type = Sprite). Hoppet over."); continue; }
                string name = Path.GetFileNameWithoutExtension(path);
                string dir = Path.GetDirectoryName(path).Replace((char)92, '/') + "/StillProps";
                if (!AssetDatabase.IsValidFolder(dir)) Directory.CreateDirectory(dir);

                var root = new GameObject(name);
                root.transform.localScale = new Vector3(2f, 2f, 1f);                     // like the animated props (and the rest of the 50x50 style)
                var child = new GameObject("Sprite");
                child.transform.SetParent(root.transform, false);
                // lift the picture so the thing (not the empty rows under it) rests on the root, which is the ground line.
                // Uses the pivot and the picture rectangle, so it does not matter if the sprite mesh is Tight or Full Rect.
                float lift = (sprite.pivot.y - GroundRows) / sprite.pixelsPerUnit;
                float shiftX = (sprite.rect.width * 0.5f - sprite.pivot.x) / sprite.pixelsPerUnit;
                child.transform.localPosition = new Vector3(shiftX, lift, 0f);
                var sr = child.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (layer) sr.sortingLayerName = "NaturAsset";
                sr.sortingOrder = -3;
                root.AddComponent<StillPropMarker>();

                PrefabUtility.SaveAsPrefabAsset(root, dir + "/" + name + ".prefab");
                Object.DestroyImmediate(root);
                made++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[PropSpawner] " + made + " statiske props bygget (prefaber i mappen StillProps ved siden av bildene). Kjør Make Prop Set for å ta dem med.");
            return made;
        }

        [MenuItem("DeepVain/Props/Make Prop Set from animated props")]
        public static PropSet MakeSet()
        {
            var entries = new List<PropSet.Entry>();
            foreach (var g in AssetDatabase.FindAssets("t:Prefab"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                string n = Path.GetFileNameWithoutExtension(p);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null) continue;
                bool animated = IsAnimatedProp(n) && prefab.GetComponent<AnimatorStartOffset>() != null;   // made by Build animated props
                bool still = prefab.GetComponent<StillPropMarker>() != null;                               // made by Build still props
                bool forage = prefab.GetComponent<Forageable>() != null;                                    // made by Build forageables (can be picked)
                if (!animated && !still && !forage) continue;
                float w = still ? 0.5f : (forage || n.StartsWith("Mushroom_")) ? 0.6f : 1f;               // still things, mushrooms and forageables are rarer
                entries.Add(new PropSet.Entry { label = n, prefab = prefab, weight = w });
            }
            if (entries.Count == 0)
            {
                EditorUtility.DisplayDialog("Prop Set", "Fant ingen props. Kjør DeepVain > Props > Build animated props (og/eller Build still props) først.", "OK");
                return null;
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.label, b.label));

            string dir = "Assets/Props";
            if (!AssetDatabase.IsValidFolder(dir)) Directory.CreateDirectory(dir);
            string path = dir + "/PropSet.asset";
            var set = AssetDatabase.LoadAssetAtPath<PropSet>(path);
            if (set == null) { set = ScriptableObject.CreateInstance<PropSet>(); AssetDatabase.CreateAsset(set, path); }
            set.entries = entries.ToArray();
            EditorUtility.SetDirty(set); AssetDatabase.SaveAssets();

            // put it on a PropSpawner (next to WorldBuilder if there is one)
            var spawner = Object.FindFirstObjectByType<PropSpawner>();
            if (spawner == null)
            {
                var wb = Object.FindFirstObjectByType<WorldBuilder>();
                GameObject host = wb != null ? wb.gameObject : new GameObject("PropSpawner");
                spawner = Undo.AddComponent<PropSpawner>(host);
            }
            Undo.RecordObject(spawner, "Prop Set");
            spawner.propSet = set;
            EditorUtility.SetDirty(spawner);
            Debug.Log("[PropSpawner] " + path + ": " + entries.Count + " props. Satt på PropSpawner på \"" + spawner.name + "\". Lagre scenen.", set);
            return set;
        }

        [MenuItem("DeepVain/Props/Snap selected prop slots to ground")]
        public static void SnapSelected()
        {
            Physics2D.SyncTransforms();
            int n = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go.GetComponent<PropSlot>() == null) continue;
                if (SnapToGround(go.transform)) n++;
            }
            Debug.Log("[PropSpawner] " + n + " plasser flyttet ned på bakken.");
        }

        static bool SnapToGround(Transform t)
        {
            Vector2 from = (Vector2)t.position + Vector2.up * 4f;
            foreach (var h in Physics2D.RaycastAll(from, Vector2.down, 40f))
            {
                if (h.collider == null || h.collider.isTrigger) continue;
                Undo.RecordObject(t, "Snap PropSlot");
                t.position = new Vector3(t.position.x, h.point.y, t.position.z);
                return true;
            }
            return false;
        }

        /// <summary>Spacing between slots in units (a tile is 1). Random +/- 40 % so it does not look like a fence.</summary>
        const float Spacing = 3f;

        [MenuItem("DeepVain/Props/Scatter prop slots on selected")]
        public static void ScatterSelected()
        {
            Physics2D.SyncTransforms();
            var rng = new System.Random(Random.Range(0, int.MaxValue));
            int total = 0;
            foreach (var root in Selection.gameObjects)
            {
                var cols = root.GetComponentsInChildren<Collider2D>();
                if (cols.Length == 0) { Debug.LogWarning("[PropSpawner] " + root.name + " har ingen colliders, så jeg finner ikke bakken."); continue; }
                Bounds b = cols[0].bounds;
                foreach (var c in cols) b.Encapsulate(c.bounds);

                // slots made by an earlier scatter (the PropSlots holder) are replaced; slots you placed by hand are left alone
                var oldHolder = root.transform.Find("PropSlots");
                if (oldHolder != null) Undo.DestroyObjectImmediate(oldHolder.gameObject);

                var holder = new GameObject("PropSlots");
                Undo.RegisterCreatedObjectUndo(holder, "Scatter prop slots");
                holder.transform.SetParent(root.transform, true);
                holder.transform.position = root.transform.position;

                int n = 0;
                for (float x = b.min.x + 1f; x < b.max.x - 0.5f; x += Spacing * (0.6f + 0.8f * (float)rng.NextDouble()))
                {
                    Vector2 from = new Vector2(x, b.max.y + 2f);
                    foreach (var h in Physics2D.RaycastAll(from, Vector2.down, b.size.y + 6f))
                    {
                        if (h.collider == null || h.collider.isTrigger) continue;
                        if (!h.collider.transform.IsChildOf(root.transform) && h.collider.transform != root.transform) continue;
                        var go = new GameObject("PropSlot");
                        Undo.RegisterCreatedObjectUndo(go, "Scatter prop slots");
                        go.transform.SetParent(holder.transform, true);
                        go.transform.position = new Vector3(x, h.point.y, 0f);
                        go.AddComponent<PropSlot>();
                        n++;
                        break;
                    }
                }
                total += n;
                Debug.Log("[PropSpawner] " + root.name + ": " + n + " plasser lagt ut langs bakken (under \"PropSlots\"). Slett de du ikke vil ha.", root);
            }
            if (total == 0) Debug.Log("[PropSpawner] Ingen plasser laget. Merk chunk-objektet (med colliders) i scenen først.");
        }
    }
}
#endif
