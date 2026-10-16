#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: DeepVain > Enemies > 4 - Make Goblin Chef prefab
/// Builds the Goblin Chef as a VARIANT of the normal goblin prefab (so it moves, attacks and blocks exactly like the goblin),
/// without touching the goblin:
///   1. import settings for the pictures in Assets/Sprits/Enemy/Chef (same as the goblin's: PPU 100, Point, centre pivot)
///   2. a copy of every goblin animation clip with the Chef pictures (Assets/Sprits/Enemy/Chef/Clips)
///   3. a copy of the goblin's animator controller using those clips (Assets/AnimatorControllors/Enemy/Chef)
///   4. a variant of the Enemy prefab: Enemy_ChefGoblin (Assets/Prefabs/Enemies) with the Chef pictures, a cleaver, 12 damage and
///      7 health. Nothing else is changed (see the Numbers section below)
///   5. an entry for it in the EnemySpawner in the open scene (if there is one and it does not have it already)
/// It can be run again at any time: the Chef clips, controller and prefab are rebuilt.
/// </summary>
public static class ChefTools
{
    const string SpriteFolder = "Assets/Sprits/Enemy/Chef";
    const string ClipFolder = "Assets/Sprits/Enemy/Chef/Clips";
    const string ControllerSrc = "Assets/AnimatorControllors/Enemy/Goblin/GoblinAnControl.controller";
    const string ControllerFolder = "Assets/AnimatorControllors/Enemy/Chef";
    const string ControllerOut = "Assets/AnimatorControllors/Enemy/Chef/ChefAnControl.controller";
    const string PrefabSrc = "Assets/Prefabs/Enemies/Enemy.prefab";
    const string PrefabOut = "Assets/Prefabs/Enemies/Enemy_ChefGoblin.prefab";

    // ------------------------------------------------------------------ Numbers
    // Only the looks, health and damage differ from the goblin. Everything else (speeds, ranges, timing, blocking, knockback)
    // is the goblin's, because the Chef is a PREFAB VARIANT of Enemy.prefab: change the goblin and the Chef follows.
    const int BaseDamage = 11;              // the goblin has 10
    const int BaseHealth = -1;              // -1 = the goblin's health is kept (the chef has as much health as a normal goblin); set a number to change it
    const float SizeScale = 1f;             // 1 = same size as the goblin (the body is the same; only the hat is above the old picture)
    const string DisplayName = "Goblin Chef";
    const int TopPad = 8;                   // every Chef picture has 8 empty rows on top (for the hat); the pivot is set so the feet stay where the goblin's are

    [MenuItem("DeepVain/Enemies/3 - Make Goblin Chef prefab")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(SpriteFolder))
        {
            EditorUtility.DisplayDialog("Chef", "Fant ikke mappen " + SpriteFolder + ".\nKopier Chef-mappen dit først.", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSrc) == null || AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerSrc) == null)
        {
            EditorUtility.DisplayDialog("Chef", "Fant ikke goblin-prefaben (" + PrefabSrc + ") eller kontrolleren (" + ControllerSrc + ").", "OK");
            return;
        }

        ImportSprites();
        var sprites = LoadSprites();
        var clipMap = BuildClips(sprites);
        var controller = BuildController(clipMap);
        var prefab = BuildPrefab(controller, sprites);
        AddToSpawner(prefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        Debug.Log("[Chef] Ferdig: " + PrefabOut + " (" + clipMap.Count + " animasjoner). Skade " + BaseDamage + ", liv " + BaseHealth +
                  ". Levels settes ut av EnemySpawner (ganger liv/skade per level, som goblin).");
    }

    // ------------------------------------------------------------------ 1: pictures
    static void ImportSprites()
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = 100f;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                var s = new TextureImporterSettings();
                imp.ReadTextureSettings(s);
                if (path.Contains("/Weapon/"))
                {
                    s.spriteAlignment = (int)SpriteAlignment.Center;
                }
                else
                {
                    // the picture is the goblin's picture with TopPad empty rows added on top: keep the OLD centre at the pivot
                    int h = PngHeight(path);
                    s.spriteAlignment = (int)SpriteAlignment.Custom;
                    s.spritePivot = new Vector2(0.5f, h > TopPad ? ((h - TopPad) * 0.5f) / h : 0.5f);
                }
                s.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
    }

    /// <summary>The height of a png file, read from its header (bytes 20-23).</summary>
    static int PngHeight(string assetPath)
    {
        try
        {
            string full = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, assetPath);
            using (var fs = System.IO.File.OpenRead(full))
            {
                var b = new byte[24]; fs.Read(b, 0, 24);
                return (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
            }
        }
        catch { return 0; }
    }

    static Dictionary<string, Sprite> LoadSprites()
    {
        var d = new Dictionary<string, Sprite>();
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpriteFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null && !d.ContainsKey(sp.name)) d[sp.name] = sp;
        }
        return d;
    }

    // ------------------------------------------------------------------ 2: clips
    static Dictionary<AnimationClip, AnimationClip> BuildClips(Dictionary<string, Sprite> sprites)
    {
        if (!AssetDatabase.IsValidFolder(ClipFolder)) AssetDatabase.CreateFolder(SpriteFolder, "Clips");
        var src = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerSrc);
        var map = new Dictionary<AnimationClip, AnimationClip>();
        var walk = sprites.Where(kv => kv.Key.StartsWith("Walk_") && kv.Key != "Walk_Sheet").OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();

        foreach (var clip in src.animationClips.Distinct())
        {
            if (clip == null) continue;
            string newName = "Chef_" + clip.name.Replace("Goblin", "");
            string path = ClipFolder + "/" + newName + ".anim";
            AssetDatabase.DeleteAsset(path);
            var dst = new AnimationClip();
            EditorUtility.CopySerialized(clip, dst);
            dst.name = newName;
            AssetDatabase.CreateAsset(dst, path);

            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(dst))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(dst, b);
                if (keys == null || keys.Length == 0) continue;
                var nk = new ObjectReferenceKeyframe[keys.Length];
                bool isWalk = clip.name.IndexOf("Walk", System.StringComparison.OrdinalIgnoreCase) >= 0 && walk.Length > 0;
                if (isWalk)
                {
                    // the goblin's walk is a sliced sheet; the brute has its own frames, spread over the same length
                    nk = new ObjectReferenceKeyframe[walk.Length];
                    float len = Mathf.Max(0.05f, keys[keys.Length - 1].time + (keys.Length > 1 ? keys[1].time - keys[0].time : 0.1f));
                    for (int i = 0; i < walk.Length; i++) nk[i] = new ObjectReferenceKeyframe { time = len * i / walk.Length, value = walk[i] };
                }
                else
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        var old = keys[i].value as Sprite;
                        Sprite repl = null;
                        if (old != null && !sprites.TryGetValue(old.name, out repl))
                        {
                            // sliced goblin sheets name their pieces 'Fade_01_0': the brute has plain 'Fade_01'
                            string trimmed = System.Text.RegularExpressions.Regex.Replace(old.name, @"_\d+$", "");
                            sprites.TryGetValue(trimmed, out repl);
                        }
                        if (repl == null && old != null) Debug.LogWarning("[Chef] Fant ikke bildet '" + old.name + "' i " + SpriteFolder + " (klipp " + clip.name + "), beholder goblin-bildet.");
                        nk[i] = new ObjectReferenceKeyframe { time = keys[i].time, value = repl != null ? repl : keys[i].value };
                    }
                }
                AnimationUtility.SetObjectReferenceCurve(dst, b, nk);
            }
            EditorUtility.SetDirty(dst);
            map[clip] = dst;
        }
        return map;
    }

    // ------------------------------------------------------------------ 3: controller
    static AnimatorController BuildController(Dictionary<AnimationClip, AnimationClip> map)
    {
        if (!AssetDatabase.IsValidFolder("Assets/AnimatorControllors/Enemy/Chef")) AssetDatabase.CreateFolder("Assets/AnimatorControllors/Enemy", "Chef");
        AssetDatabase.DeleteAsset(ControllerOut);
        AssetDatabase.CopyAsset(ControllerSrc, ControllerOut);
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerOut);
        foreach (var layer in c.layers) Swap(layer.stateMachine, map);
        EditorUtility.SetDirty(c);
        return c;
    }

    static void Swap(AnimatorStateMachine sm, Dictionary<AnimationClip, AnimationClip> map)
    {
        foreach (var cs in sm.states)
        {
            var clip = cs.state.motion as AnimationClip;
            if (clip != null && map.TryGetValue(clip, out var repl)) cs.state.motion = repl;
            else if (cs.state.motion is BlendTree bt)
            {
                var kids = bt.children;
                for (int i = 0; i < kids.Length; i++)
                {
                    var kc = kids[i].motion as AnimationClip;
                    if (kc != null && map.TryGetValue(kc, out var r2)) kids[i].motion = r2;
                }
                bt.children = kids;
            }
        }
        foreach (var sub in sm.stateMachines) Swap(sub.stateMachine, map);
    }

    // ------------------------------------------------------------------ 4: prefab
    static GameObject BuildPrefab(AnimatorController controller, Dictionary<string, Sprite> sprites)
    {
        // A variant of the goblin prefab. If the Chef prefab already exists it is overwritten in place, so it keeps its GUID
        // (the spawner and the scene keep pointing at it).
        var goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSrc);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(goblinPrefab);
        inst.name = "Enemy_ChefGoblin";
        PrefabUtility.SaveAsPrefabAsset(inst, PrefabOut);
        Object.DestroyImmediate(inst);
        var root = PrefabUtility.LoadPrefabContents(PrefabOut);
        try
        {
            root.name = "Enemy_ChefGoblin";
            var anim = root.GetComponentInChildren<Animator>(true);
            if (anim != null) anim.runtimeAnimatorController = controller;

            var rootSr = root.GetComponent<SpriteRenderer>();
            if (rootSr != null && sprites.TryGetValue("Idle_01", out var idle)) rootSr.sprite = idle;

            // the weapon in the hit box: the cleaver
            Sprite cleaver;
            if (sprites.TryGetValue("Chef_Knife", out cleaver))
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.gameObject.name.Trim() == "HitboxVisual")
                    {
                        sr.sprite = cleaver;
                        sr.transform.localScale = new Vector3(1f, 1f, 1f);          // a bit bigger than the bat (0.8) so it reads
                    }

            if (BaseHealth > 0) SetField<Enemy>(root, "maxHealth", BaseHealth);
            SetField<EnemyAttack>(root, "damage", BaseDamage);

            if (Mathf.Abs(SizeScale - 1f) > 0.001f)
            {
                var ls = root.transform.localScale;
                root.transform.localScale = new Vector3(ls.x * SizeScale, ls.y * SizeScale, ls.z);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabOut);
            return prefab;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void SetField<T>(GameObject root, string field, object value) where T : Component
    {
        var comp = root.GetComponent<T>();
        if (comp == null) { Debug.LogWarning("[Chef] Prefaben mangler " + typeof(T).Name + ", hoppet over " + field); return; }
        var so = new SerializedObject(comp);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("[Chef] Fant ikke feltet " + typeof(T).Name + "." + field); return; }
        if (value is int i) p.intValue = i;
        else if (value is float f) p.floatValue = f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ 5: spawner
    static void AddToSpawner(GameObject prefab)
    {
        var sp = Object.FindFirstObjectByType<EnemySpawner>();
        if (sp == null) { Debug.Log("[Chef] Ingen EnemySpawner i scenen: legg prefaben til Enemies-listen selv (Display Name: " + DisplayName + ")."); return; }
        if (sp.enemies != null && sp.enemies.Any(e => e != null && e.prefab == prefab)) return;
        var list = new List<EnemySpawner.Entry>(sp.enemies ?? new EnemySpawner.Entry[0]);
        list.Add(new EnemySpawner.Entry { label = DisplayName, prefab = prefab, weight = 0.4f, displayName = DisplayName, minDistance = 30f });
        Undo.RecordObject(sp, "Add Goblin Chef");
        sp.enemies = list.ToArray();
        EditorUtility.SetDirty(sp);
        EditorSceneManager.MarkSceneDirty(sp.gameObject.scene);
        Debug.Log("[Chef] Lagt til i EnemySpawner: vekt 0.4, bare minst 30 units fra start.");
    }
}
#endif
