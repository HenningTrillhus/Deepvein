#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using DeepVain.Items;

namespace DeepVain.ForageTools
{
    /// <summary>
    /// DeepVain > Props > Build forageables
    ///   1. Lager item-ene (Blueberry, Wild Strawberry, Healing Herb, Cotton, Brown Mushroom, Red Cap) hvis de ikke finnes,
    ///      med ikonene Icon_*.png.
    ///   2. For de fire plantene (Forage_Blueberry, Forage_Strawberry, Forage_HealingHerb, Forage_Cotton): lager animasjon for
    ///      normal og plukket tilstand (8 bilder, 120 ms, loop), en controller med tilstandene Normal og Picked, og en prefab
    ///      med SpriteRenderer + Animator + AnimatorStartOffset + Forageable.
    ///   3. Soppene (Mushroom_Brown, Mushroom_RedCluster): legger Forageable på de eksisterende prefabene (de forsvinner når de plukkes).
    ///   4. Legger ForagePicker (E-tasten) på spilleren.
    /// Kan kjøres flere ganger. Etterpå: DeepVain > Props > Make Prop Set from animated props, så kommer de tilfeldig i verden.
    /// </summary>
    public static class ForageMenu
    {
        const float FrameTime = 0.12f;

        class Plant
        {
            public string folder;      // Forage_Blueberry
            public string itemId, itemName, icon, description;
            public int min, max;
        }

        static readonly Plant[] Plants =
        {
            new Plant { folder = "Forage_Blueberry",  itemId = "blueberry",       itemName = "Blueberry",       icon = "Icon_Blueberry",       min = 2, max = 4, description = "Sweet blue berries from the forest." },
            new Plant { folder = "Forage_Strawberry", itemId = "wild_strawberry", itemName = "Wild Strawberry", icon = "Icon_WildStrawberry",  min = 2, max = 4, description = "Small, sweet wild strawberries." },
            new Plant { folder = "Forage_HealingHerb", itemId = "healing_herb",   itemName = "Healing Herb",    icon = "Icon_HealingHerb",     min = 1, max = 2, description = "A feathery herb with a pink flower. Said to heal." },
            new Plant { folder = "Forage_Cotton",     itemId = "cotton",          itemName = "Cotton",          icon = "Icon_Cotton",          min = 1, max = 3, description = "Soft white cotton from an open boll." },
        };

        class Mushroom { public string prefab, itemId, itemName, icon, description; public int min, max; }
        static readonly Mushroom[] Mushrooms =
        {
            new Mushroom { prefab = "Mushroom_Brown",      itemId = "brown_mushroom", itemName = "Brown Mushroom", icon = "Icon_BrownMushroom", min = 1, max = 2, description = "A firm brown mushroom with a pale stem." },
            new Mushroom { prefab = "Mushroom_RedCluster", itemId = "red_cap",        itemName = "Red Cap",        icon = "Icon_RedCap",        min = 1, max = 2, description = "A red mushroom with white spots. Not for eating." },
        };

        // ---------- helpers
        static string FindAsset(string name, string filter)
        {
            string best = null;
            foreach (var g in AssetDatabase.FindAssets(name + " " + filter))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) != name) continue;
                if (best == null) best = p;
            }
            return best;
        }

        static void PrepareFrame(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            bool changed = false;
            if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
            if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (!Mathf.Approximately(imp.spritePixelsPerUnit, 100f)) { imp.spritePixelsPerUnit = 100f; changed = true; }
            if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; changed = true; }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
            if (s.spriteAlignment != (int)SpriteAlignment.Custom || !Mathf.Approximately(imp.spritePivot.y, 0.04f))
            {
                s.spriteAlignment = (int)SpriteAlignment.Custom; imp.SetTextureSettings(s); imp.spritePivot = new Vector2(0.5f, 0.04f); changed = true;
            }
            if (changed) imp.SaveAndReimport();
        }

        static Sprite PrepareIcon(string name)
        {
            string path = FindAsset(name, "t:Texture2D");
            if (path == null) return null;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                bool changed = false;
                if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
                if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; changed = true; }
                if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                if (changed) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static string ItemFolder()
        {
            string folder = "Assets/Inventory/Items";
            var any = AssetDatabase.FindAssets("t:ItemData");
            if (any.Length > 0) folder = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(any[0])).Replace((char)92, '/');
            if (!AssetDatabase.IsValidFolder(folder)) Directory.CreateDirectory(folder);
            return folder;
        }

        static ItemData EnsureItem(string id, string displayName, string iconName, string description, int sell, string folder)
        {
            string path = folder + "/" + id + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null)
            {
                foreach (var g in AssetDatabase.FindAssets("t:ItemData"))
                {
                    var other = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g));
                    if (other != null && other.id == id) { item = other; break; }
                }
            }
            if (item == null) { item = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(item, path); }
            item.id = id; item.displayName = displayName; item.type = ItemType.Material; item.rarity = ItemRarity.Common;
            item.description = description; item.maxStack = 99; item.sellValue = sell;
            var icon = PrepareIcon(iconName);
            if (icon != null) item.icon = icon;
            EditorUtility.SetDirty(item);
            return item;
        }

        static List<Sprite> LoadFrames(string baseName)
        {
            var list = new List<Sprite>();
            for (int i = 1; i <= 8; i++)
            {
                string p = FindAsset(baseName + "_" + i.ToString("00"), "t:Texture2D");
                if (p == null) return null;
                PrepareFrame(p);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (sp == null) return null;
                list.Add(sp);
            }
            return list;
        }

        static AnimationClip MakeClip(string path, List<Sprite> sprites)
        {
            var keys = new ObjectReferenceKeyframe[sprites.Count + 1];
            for (int i = 0; i < sprites.Count; i++) keys[i] = new ObjectReferenceKeyframe { time = i * FrameTime, value = sprites[i] };
            keys[sprites.Count] = new ObjectReferenceKeyframe { time = sprites.Count * FrameTime - 1f / 50f, value = sprites[sprites.Count - 1] };
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.frameRate = 50f;
            var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// <summary>One shared material with the shine shader (a white band sweeping over the prop), next to the Forageable script.</summary>
        static Material EnsureShineMaterial()
        {
            var shader = Shader.Find("DeepVain/Sprite Shine");
            if (shader == null)
            {
                Debug.LogWarning("[Forage] Fant ikke shaderen \"DeepVain/Sprite Shine\". Kopier SpriteShine.shader inn i Assets, så får de plukkbare skinn.");
                return null;
            }
            string dir = "Assets";
            foreach (var g in AssetDatabase.FindAssets("Forageable t:MonoScript"))
            {
                string sp = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(sp) == "Forageable") { dir = Path.GetDirectoryName(sp).Replace((char)92, '/'); break; }
            }
            string path = dir + "/Forage_Shine.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static bool HasSortingLayer(string name)
        {
            foreach (var l in SortingLayer.layers) if (l.name == name) return true;
            return false;
        }

        // ---------- the menu
        [MenuItem("DeepVain/Props/Build forageables")]
        public static int Build()
        {
            string itemFolder = ItemFolder();
            int made = 0;
            var shineMat = EnsureShineMaterial();

            // the four plants
            foreach (var pl in Plants)
            {
                var normal = LoadFrames(pl.folder);
                var picked = LoadFrames(pl.folder + "_Picked");
                if (normal == null || picked == null)
                {
                    Debug.LogWarning("[Forage] Fant ikke alle 8 bildene for " + pl.folder + " og " + pl.folder + "_Picked. Kopier mappene inn i Assets først. Hoppet over.");
                    continue;
                }
                var item = EnsureItem(pl.itemId, pl.itemName, pl.icon, pl.description, 2, itemFolder);

                string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(normal[0])).Replace((char)92, '/');
                var clipN = MakeClip(dir + "/" + pl.folder + ".anim", normal);
                var clipP = MakeClip(dir + "/" + pl.folder + "_Picked.anim", picked);

                string ctrlPath = dir + "/" + pl.folder + ".controller";
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
                if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var sm = ctrl.layers[0].stateMachine;
                AnimatorState sN = null, sP = null;
                foreach (var st in sm.states) { if (st.state.name == "Normal") sN = st.state; if (st.state.name == "Picked") sP = st.state; }
                if (sN == null) sN = sm.AddState("Normal");
                if (sP == null) sP = sm.AddState("Picked");
                sN.motion = clipN; sP.motion = clipP;
                sm.defaultState = sN;
                EditorUtility.SetDirty(ctrl);

                var go = new GameObject(pl.folder);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = normal[0];
                if (HasSortingLayer("NaturAsset")) sr.sortingLayerName = "NaturAsset";
                sr.sortingOrder = -3;
                var an = go.AddComponent<Animator>();
                an.runtimeAnimatorController = ctrl;
                an.cullingMode = AnimatorCullingMode.CullCompletely;
                go.AddComponent<AnimatorStartOffset>();
                var fg = go.AddComponent<Forageable>();
                fg.item = item; fg.minAmount = pl.min; fg.maxAmount = pl.max; fg.hideWhenPicked = false; fg.pickedState = "Picked";
                fg.shine = true; fg.shineMaterial = shineMat;
                if (shineMat != null) sr.sharedMaterial = shineMat;
                go.transform.localScale = new Vector3(2f, 2f, 1f);
                PrefabUtility.SaveAsPrefabAsset(go, dir + "/" + pl.folder + ".prefab");
                Object.DestroyImmediate(go);
                made++;
            }

            // the mushrooms: the prefabs that already exist get a Forageable
            foreach (var m in Mushrooms)
            {
                string p = FindAsset(m.prefab, "t:Prefab");
                if (p == null) { Debug.LogWarning("[Forage] Fant ikke prefaben " + m.prefab + ". Kjør DeepVain > Props > Build animated props først. Hoppet over."); continue; }
                var item = EnsureItem(m.itemId, m.itemName, m.icon, m.description, 3, itemFolder);
                var root = PrefabUtility.LoadPrefabContents(p);
                var fg = root.GetComponent<Forageable>();
                if (fg == null) fg = root.AddComponent<Forageable>();
                fg.item = item; fg.minAmount = m.min; fg.maxAmount = m.max; fg.hideWhenPicked = true;
                fg.shine = true; fg.shineMaterial = shineMat;
                var rootSr = root.GetComponentInChildren<SpriteRenderer>();
                if (rootSr != null && shineMat != null) rootSr.sharedMaterial = shineMat;
                PrefabUtility.SaveAsPrefabAsset(root, p);
                PrefabUtility.UnloadPrefabContents(root);
                made++;
            }
            AssetDatabase.SaveAssets();

            // the picker on the player
            var player = Object.FindFirstObjectByType<PlayerMovement>();
            if (player != null)
            {
                var picker = player.GetComponent<ForagePicker>();
                if (picker == null) Undo.AddComponent<ForagePicker>(player.gameObject);
            }
            else Debug.LogWarning("[Forage] Fant ingen PlayerMovement i scenen, så ForagePicker ble ikke lagt på spilleren. Legg den på selv.");

            Debug.Log("[Forage] " + made + " plukkbare ferdig (prefaber ved siden av bildene). Kjør DeepVain > Props > Make Prop Set from animated props, så kommer de tilfeldig i verden. Trykk E ved dem.");
            return made;
        }
    }
}
#endif
