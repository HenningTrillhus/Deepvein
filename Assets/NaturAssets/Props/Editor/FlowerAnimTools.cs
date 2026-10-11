#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeepVain.FlowerAnimTools
{
    /// <summary>
    /// DeepVain > Props > Build animated props
    /// Finner alle bildeserier som heter Flower_<Navn>_01, Plant_<Navn>_01 eller Mushroom_<Navn>_01 (de 8 bildene per prop) og lager for hver:
    ///   - bildene riktig importert (100 pixels per unit som de andre propsene, Point, pivot nederst ved roten)
    ///   - en animasjon <Navn>.anim: 8 bilder, 120 ms per bilde, loop (som gresset)
    ///   - en controller <Navn>.controller
    ///   - en prefab <Navn>.prefab: SpriteRenderer + Animator + AnimatorStartOffset, scale 2 (som resten av 50x50-stilen)
    /// Alt legges i samme mappe som bildene. Kan kjøres flere ganger.
    /// </summary>
    public static class FlowerAnimMenu
    {
        const float FrameTime = 0.12f;
        static readonly Regex FramePattern = new Regex(@"^((?:Flower|Plant|Mushroom)_[A-Za-z0-9]+)_(\d\d)$");

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
            // bottom centre, 2 art pixels up (the empty rows under the roots) so the flower stands on the ground line
            if (s.spriteAlignment != (int)SpriteAlignment.Custom || !Mathf.Approximately(imp.spritePivot.y, 0.04f))
            {
                s.spriteAlignment = (int)SpriteAlignment.Custom;
                imp.SetTextureSettings(s);
                imp.spritePivot = new Vector2(0.5f, 0.04f);
                changed = true;
            }
            if (changed) imp.SaveAndReimport();
        }

        static bool HasSortingLayer(string name)
        {
            foreach (var l in SortingLayer.layers) if (l.name == name) return true;
            return false;
        }

        [MenuItem("DeepVain/Props/Build animated props")]
        public static int Build()
        {
            // group the frames by flower
            var groups = new Dictionary<string, SortedDictionary<int, string>>();
            var found = new List<string>();
            foreach (var prefix in new[] { "Flower_", "Plant_", "Mushroom_" })
                found.AddRange(AssetDatabase.FindAssets(prefix + " t:Texture2D"));
            foreach (var g in found)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var m = FramePattern.Match(Path.GetFileNameWithoutExtension(p));
                if (!m.Success) continue;
                string key = Path.GetDirectoryName(p).Replace('\\', '/') + "|" + m.Groups[1].Value;
                if (!groups.ContainsKey(key)) groups[key] = new SortedDictionary<int, string>();
                groups[key][int.Parse(m.Groups[2].Value)] = p;
            }
            if (groups.Count == 0)
            {
                EditorUtility.DisplayDialog("Flowers", "Fant ingen bilder som heter Flower_/Plant_/Mushroom_<navn>_01, _02 ... i prosjektet. Kopier bildemappene (Flower_Daisy, Plant_Fern osv.) inn i Assets først.", "OK");
                return 0;
            }

            int made = 0;
            foreach (var kv in groups)
            {
                if (kv.Value.Count < 2) continue;
                string[] parts = kv.Key.Split('|');
                string dir = parts[0], name = parts[1];

                var sprites = new List<Sprite>();
                foreach (var p in kv.Value.Values)
                {
                    PrepareFrame(p);
                    var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                    if (sp != null) sprites.Add(sp);
                }
                if (sprites.Count < 2) continue;

                // the clip: one key per frame, and the last frame repeated at the end so it gets its full 120 ms before the loop
                var keys = new ObjectReferenceKeyframe[sprites.Count + 1];
                for (int i = 0; i < sprites.Count; i++) keys[i] = new ObjectReferenceKeyframe { time = i * FrameTime, value = sprites[i] };
                keys[sprites.Count] = new ObjectReferenceKeyframe { time = sprites.Count * FrameTime - 1f / 50f, value = sprites[sprites.Count - 1] };   // Unity adds one sample (1/50 s) to the length, so the loop is exactly 8 x 120 ms

                string clipPath = dir + "/" + name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
                clip.frameRate = 50f;                                  // 0.12 s = exactly 6 sample frames, so the timing is not rounded
                var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);

                string ctrlPath = dir + "/" + name + ".controller";
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
                if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, clip);

                string prefabPath = dir + "/" + name + ".prefab";
                var go = new GameObject(name);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprites[0];
                if (HasSortingLayer("NaturAsset")) sr.sortingLayerName = "NaturAsset";
                sr.sortingOrder = -3;
                var anim = go.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.cullingMode = AnimatorCullingMode.CullCompletely;       // not animated while off screen
                go.AddComponent<AnimatorStartOffset>();
                go.transform.localScale = new Vector3(2f, 2f, 1f);
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Object.DestroyImmediate(go);
                made++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[FlowerAnim] " + made + " props bygget (animasjon, controller og prefab ligger ved siden av bildene). Dra prefabene inn i scenen.");
            return made;
        }
    }
}
#endif
