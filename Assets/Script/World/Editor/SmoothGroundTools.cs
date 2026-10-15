#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DeepVain.WorldTools
{
    /// <summary>
    /// Menu: DeepVain > World > ...
    ///   1 - Smooth tile collision    Gives every tile picture (Assets/Sprits/TileMaps/Tiles) a clean collision shape: the top edge of the grass
    ///                                tiles gets exactly the same height on every tile and straight lines, so it is no longer a staircase of small
    ///                                bumps (the old shape followed every blade of grass). Slopes stay slopes. Nothing is drawn differently.
    ///   2 - Merge tile colliders     Puts a Composite Collider 2D on every Tilemap Collider 2D in the chunk prefabs (Assets/Prefabs/Chuncks):
    ///                                all the tiles become ONE shape, so there are no seams between tiles to catch the feet on.
    ///                                (A Rigidbody 2D set to Static is added with it, as Unity requires.) The merged shape is SOLID
    ///                                (Polygons). Outlines is only a thin line and a fast fall goes straight through it.
    ///   3 - Restore automatic ...    Undoes 1.
    ///   4 - Remove merged colliders  Undoes 2 (back to the plain Tilemap Collider 2D).
    /// Both can be run again at any time.
    /// </summary>
    public static class SmoothGroundTools
    {
        const string TileFolder = "Assets/Sprits/TileMaps/Tiles";
        const string ChunkFolder = "Assets/Prefabs/Chuncks";
        // pixels from the middle of a 100 px tile: the grass top is 34 above it, the tile top 50
        const float GrassTop = 34f, TileTop = 50f, Snap = 8f, Simplify = 1.6f;

        [MenuItem("DeepVain/World/1 - Smooth tile collision")]
        public static void SmoothTiles()
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            int changed = 0, points = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TileFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null || imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode == SpriteImportMode.Multiple) continue;

                // the picture itself (read from the file, so it works even if the texture is not marked readable)
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path))) { Object.DestroyImmediate(tex); continue; }
                var poly = BuildOutline(tex);
                Object.DestroyImmediate(tex);
                if (poly.Count == 0) continue;

                var dp = factories.GetSpriteEditorDataProviderFromObject(imp);
                if (dp == null) continue;
                dp.InitSpriteEditorDataProvider();
                var outlines = dp.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
                foreach (var rect in dp.GetSpriteRects())
                {
                    outlines.SetOutlines(rect.spriteID, poly);
                    foreach (var p in poly) points += p.Length;
                }
                dp.Apply();
                imp.SaveAndReimport();
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Smooth] " + changed + " tile pictures got a clean collision shape (" + points + " points in all). Now run DeepVain > World > 2 - Merge tile colliders.");
        }

        /// <summary>
        /// The collision shape of a tile from its picture: for every column the highest and lowest solid pixel, the top edge cleaned up
        /// (the little grass wobble is removed, heights near the grass top / tile top snap to exactly that height, straight lines), the
        /// bottom as it is. Coordinates are in pixels from the middle of the picture.
        /// </summary>
        static List<Vector2[]> BuildOutline(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            var top = new float[w]; var bot = new float[w]; var solid = new bool[w];
            for (int x = 0; x < w; x++)
            {
                int hi = -1, lo = -1;
                for (int y = h - 1; y >= 0; y--) if (px[y * w + x].a > 100) { hi = y; break; }
                for (int y = 0; y < h; y++) if (px[y * w + x].a > 100) { lo = y; break; }
                solid[x] = hi >= 0;
                top[x] = hi + 1 - h * 0.5f; bot[x] = lo - h * 0.5f;
            }

            var result = new List<Vector2[]>();
            int i = 0;
            while (i < w)
            {
                while (i < w && !solid[i]) i++;
                if (i >= w) break;
                int x0 = i;
                while (i < w && solid[i]) i++;
                int x1 = i - 1;                                           // the run of solid columns x0..x1
                int n = x1 - x0 + 1;
                if (n < 2) continue;

                var t = new float[n]; var b = new float[n];
                for (int k = 0; k < n; k++) { t[k] = top[x0 + k]; b[k] = bot[x0 + k]; }
                b = Median(b, 5);
                for (int k = 0; k < n; k++) b[k] = SnapY(b[k]);
                float xl = x0 - w * 0.5f, xr = x1 + 1 - w * 0.5f;         // the run goes out to the column boundaries

                // the top edge: grass blades make it wobble a few pixels. A tile whose top stays within a band is simply FLAT (at the
                // upper part of the band, snapped to the common grass height). A real slope or corner is smoothed hard and straightened.
                var sorted = (float[])t.Clone(); System.Array.Sort(sorted);
                float p10 = sorted[Mathf.Clamp(Mathf.RoundToInt((n - 1) * 0.10f), 0, n - 1)];
                float p75 = sorted[Mathf.Clamp(Mathf.RoundToInt((n - 1) * 0.75f), 0, n - 1)];
                float p90 = sorted[Mathf.Clamp(Mathf.RoundToInt((n - 1) * 0.90f), 0, n - 1)];
                List<Vector2> topPts;
                if (p90 - p10 <= 9f)
                {
                    float m = SnapY(p75);
                    topPts = new List<Vector2> { new Vector2(xl, m), new Vector2(xr, m) };
                }
                else
                {
                    t = MaxFilter(t, 13);                                 // the gaps between the grass blades are filled
                    t = Median(t, 9);
                    for (int k = 0; k < n; k++) t[k] = SnapY(t[k]);
                    topPts = new List<Vector2>();
                    for (int k = 0; k < n; k++) topPts.Add(new Vector2(x0 + k + 0.5f - w * 0.5f, t[k]));
                    topPts = Douglas(topPts, 3f);
                    topPts[0] = new Vector2(xl, topPts[0].y); topPts[topPts.Count - 1] = new Vector2(xr, topPts[topPts.Count - 1].y);
                    float mn = topPts.Min(p => p.y), mx = topPts.Max(p => p.y);
                    if (mx - mn < 3.5f)
                    {
                        float m = SnapY((mn + mx) * 0.5f);
                        topPts = new List<Vector2> { new Vector2(xl, m), new Vector2(xr, m) };
                    }
                }

                var botPts = new List<Vector2>();
                for (int k = 0; k < n; k++) botPts.Add(new Vector2(x0 + k + 0.5f - w * 0.5f, b[k]));
                botPts = Douglas(botPts, Simplify);
                botPts[0] = new Vector2(xl, botPts[0].y); botPts[botPts.Count - 1] = new Vector2(xr, botPts[botPts.Count - 1].y);

                var poly = new List<Vector2>(topPts);                     // left to right along the top ...
                for (int k = botPts.Count - 1; k >= 0; k--) poly.Add(botPts[k]);       // ... then right to left along the bottom
                result.Add(poly.ToArray());
            }
            return result;
        }

        static float SnapY(float y)
        {
            if (Mathf.Abs(y - GrassTop) < Snap) return GrassTop;              // all grass tops get the same height, so the tiles join exactly
            if (Mathf.Abs(y - TileTop) < 3.2f) return TileTop;
            if (Mathf.Abs(y + TileTop) < 3.2f) return -TileTop;
            return y;
        }

        static float[] MaxFilter(float[] a, int window)
        {
            var r = new float[a.Length];
            int hw = window / 2;
            for (int i = 0; i < a.Length; i++)
            {
                float m = float.MinValue;
                for (int j = Mathf.Max(0, i - hw); j <= Mathf.Min(a.Length - 1, i + hw); j++) m = Mathf.Max(m, a[j]);
                r[i] = m;
            }
            return r;
        }

        static float[] Median(float[] a, int window)
        {
            var r = new float[a.Length];
            int hw = window / 2;
            var buf = new List<float>();
            for (int i = 0; i < a.Length; i++)
            {
                buf.Clear();
                for (int j = Mathf.Max(0, i - hw); j <= Mathf.Min(a.Length - 1, i + hw); j++) buf.Add(a[j]);
                buf.Sort();
                r[i] = buf[buf.Count / 2];
            }
            return r;
        }

        static List<Vector2> Douglas(List<Vector2> pts, float eps)
        {
            if (pts.Count < 3) return new List<Vector2>(pts);
            int idx = -1; float dmax = 0f;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                float d = DistToLine(pts[i], pts[0], pts[pts.Count - 1]);
                if (d > dmax) { dmax = d; idx = i; }
            }
            if (dmax <= eps) return new List<Vector2> { pts[0], pts[pts.Count - 1] };
            var left = Douglas(pts.GetRange(0, idx + 1), eps);
            var right = Douglas(pts.GetRange(idx, pts.Count - idx), eps);
            left.RemoveAt(left.Count - 1);
            left.AddRange(right);
            return left;
        }

        static float DistToLine(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len = ab.magnitude;
            if (len < 0.0001f) return (p - a).magnitude;
            return Mathf.Abs((p.x - a.x) * ab.y - (p.y - a.y) * ab.x) / len;
        }

        [MenuItem("DeepVain/World/4 - Remove merged colliders (undo 2)")]
        public static void UnmergeColliders()
        {
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ChunkFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var tc in root.GetComponentsInChildren<TilemapCollider2D>(true))
                    {
                        tc.compositeOperation = Collider2D.CompositeOperation.None;
                        var comp = tc.GetComponent<CompositeCollider2D>();
                        if (comp != null) Object.DestroyImmediate(comp);
                        var rb = tc.GetComponent<Rigidbody2D>();
                        if (rb != null) Object.DestroyImmediate(rb);
                        n++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Smooth] " + n + " tile colliders are back to a plain Tilemap Collider 2D (no composite).");
        }

        [MenuItem("DeepVain/World/3 - Restore automatic tile collision (undo 1)")]
        public static void RestoreTiles()
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TileFolder }))
            {
                var imp = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (imp == null || imp.textureType != TextureImporterType.Sprite) continue;
                var dp = factories.GetSpriteEditorDataProviderFromObject(imp);
                if (dp == null) continue;
                dp.InitSpriteEditorDataProvider();
                var outlines = dp.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
                foreach (var rect in dp.GetSpriteRects()) outlines.SetOutlines(rect.spriteID, new List<Vector2[]>());
                dp.Apply(); imp.SaveAndReimport(); n++;
            }
            Debug.Log("[Smooth] " + n + " tile pictures are back to the automatic collision shape.");
        }

        [MenuItem("DeepVain/World/2 - Merge tile colliders")]
        public static void MergeColliders()
        {
            int prefabs = 0, colliders = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ChunkFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;
                try
                {
                    foreach (var tc in root.GetComponentsInChildren<TilemapCollider2D>(true))
                    {
                        var go = tc.gameObject;
                        var rb = go.GetComponent<Rigidbody2D>();
                        if (rb == null) rb = go.AddComponent<Rigidbody2D>();
                        rb.bodyType = RigidbodyType2D.Static;
                        var comp = go.GetComponent<CompositeCollider2D>();
                        if (comp == null) comp = go.AddComponent<CompositeCollider2D>();
                        comp.geometryType = CompositeCollider2D.GeometryType.Polygons;       // solid: NOT Outlines (thin edges that fast falls go straight through)
                        comp.generationType = CompositeCollider2D.GenerationType.Synchronous;
                        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
                        dirty = true; colliders++;
                    }
                    if (dirty) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabs++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Smooth] " + colliders + " tile colliders in " + prefabs + " chunk prefabs are now merged (Composite Collider 2D, Static).");
        }
    }
}
#endif
