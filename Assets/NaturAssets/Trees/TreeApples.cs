using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Apples that hang in a tree. TreeApplePlacer puts this on the oaks (and sets up the apples); AppleHarvester (on the player) makes them
/// fall on the first axe hit and puts them in the bag.
///   - The apples sit under the clumps of leaves (found from the green pixels of the tree picture), and follow the wind sway of the tree.
///   - Drop(): the apples let go, fall down to the ground, bounce once and disappear. Returns how many fell.
/// </summary>
[DisallowMultipleComponent]
public class TreeApples : MonoBehaviour
{
    class Apple { public Transform t; public Vector2 local; public float h; }

    readonly List<Apple> apples = new List<Apple>();
    TreeSway sway;

    public int Count { get { return apples.Count; } }
    public bool HasApples { get { return apples.Count > 0; } }

    // ---- finding the places: the underside of the leaf clumps ------------------------------------------------------------------
    static readonly Dictionary<Sprite, List<Vector2Int>> spotCache = new Dictionary<Sprite, List<Vector2Int>>();
    static bool warnedUnreadable;

    static bool IsLeaf(Color c) { return c.a > 0.8f && c.g > c.r * 1.12f && c.g >= c.b * 1.15f; }

    /// <summary>Candidate pixels (in the sprite rectangle) where an apple can hang. Needs a readable texture, otherwise an empty list.</summary>
    static List<Vector2Int> Candidates(Sprite sprite)
    {
        List<Vector2Int> list;
        if (spotCache.TryGetValue(sprite, out list)) return list;
        list = new List<Vector2Int>();
        spotCache[sprite] = list;
        var tex = sprite.texture;
        if (tex == null || !tex.isReadable)
        {
            if (!warnedUnreadable) { warnedUnreadable = true; Debug.LogWarning("[TreeApples] The tree picture \"" + sprite.name + "\" is not readable (Read/Write), so I place the apples by guessing. Run DeepVain > Trees > 10 - Set up apples."); }
            return list;
        }
        Rect r = sprite.textureRect;
        int rx = Mathf.RoundToInt(r.x), ry = Mathf.RoundToInt(r.y), rw = Mathf.RoundToInt(r.width), rh = Mathf.RoundToInt(r.height);
        Color[] px = tex.GetPixels(rx, ry, rw, rh);
        bool[] leaf = new bool[rw * rh];
        int minY = rh, maxY = 0;
        for (int y = 0; y < rh; y++)
            for (int x = 0; x < rw; x++)
                if (IsLeaf(px[y * rw + x])) { leaf[y * rw + x] = true; if (y < minY) minY = y; if (y > maxY) maxY = y; }
        if (maxY <= minY) return list;
        int lowest = minY + Mathf.RoundToInt((maxY - minY) * 0.12f);          // skip the very lowest leaf bits (low branch leaves look odd with apples)
        int highest = maxY - Mathf.RoundToInt((maxY - minY) * 0.10f);
        int gap = Mathf.Max(2, rw / 60);
        for (int y = lowest; y <= highest; y += 1)
            for (int x = 2; x < rw - 2; x += 2)
            {
                if (!leaf[y * rw + x]) continue;
                // the underside of a clump: a leaf here, none just below, and leaf on both sides and a bit above (so the apple hangs in the leaves)
                if (y - gap < 0 || y + 3 >= rh) continue;
                if (leaf[(y - 1) * rw + x] || leaf[(y - gap) * rw + x]) continue;
                if (!leaf[y * rw + x - 2] || !leaf[y * rw + x + 2] || !leaf[(y + 3) * rw + x]) continue;
                list.Add(new Vector2Int(x, y));
            }
        return list;
    }

    /// <summary>Sets up count apples in this tree. Returns how many were made.</summary>
    public int Setup(Sprite appleSprite, int count, System.Random rng, float appleWorldScale, int orderAbove)
    {
        Clear();
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null || appleSprite == null) return 0;
        sway = GetComponent<TreeSway>();
        if (sway == null) sway = sr.GetComponent<TreeSway>();

        Sprite ts = sr.sprite;
        float ppu = ts.pixelsPerUnit;
        Rect rect = ts.textureRect;
        Vector2 pivot = ts.pivot;                                            // pixels from the bottom-left of the sprite rectangle
        Bounds b = ts.bounds;
        var chosen = new List<Vector2>();                                    // in sprite pixels
        var cands = Candidates(ts);
        float minDist = rect.width * 0.08f;
        for (int tries = 0; tries < 400 && chosen.Count < count; tries++)
        {
            Vector2 p;
            if (cands.Count > 0) { var c = cands[rng.Next(cands.Count)]; p = new Vector2(c.x, c.y); }
            else                                                              // guess: inside the top part of the picture
            {
                p = new Vector2(rect.width * (0.2f + 0.6f * (float)rng.NextDouble()), rect.height * (0.45f + 0.4f * (float)rng.NextDouble()));
            }
            bool ok = true;
            foreach (var q in chosen) if ((q - p).sqrMagnitude < minDist * minDist) { ok = false; break; }
            if (ok) chosen.Add(p);
        }

        float scale = appleWorldScale / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float appleHalf = appleSprite.bounds.size.y * 0.5f * scale;
        foreach (var p in chosen)
        {
            float lx = (p.x - pivot.x) / ppu;
            float ly = (p.y - pivot.y) / ppu - appleHalf * 0.6f;                       // hangs from the leaf: its top sits in the leaves
            if (sr.flipX) lx = -lx;
            var go = new GameObject("Apple", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var asr = go.GetComponent<SpriteRenderer>();
            asr.sprite = appleSprite;
            asr.sortingLayerName = sr.sortingLayerName;
            asr.sortingOrder = sr.sortingOrder + orderAbove;
            float h = Mathf.Clamp01((ly - b.min.y) / Mathf.Max(0.0001f, b.size.y));
            var a = new Apple { t = go.transform, local = new Vector2(lx, ly), h = h };
            apples.Add(a);
            go.transform.localPosition = new Vector3(lx, ly, 0f);
#if UNITY_EDITOR
            if (!Application.isPlaying) go.hideFlags = HideFlags.DontSaveInEditor;
#endif
        }
        return apples.Count;
    }

    void LateUpdate()
    {
        if (apples.Count == 0) return;
        for (int i = 0; i < apples.Count; i++)
        {
            var a = apples[i];
            if (a.t == null) continue;
            float dx = sway != null ? sway.SwayDx(a.h) : 0f;
            a.t.localPosition = new Vector3(a.local.x + dx, a.local.y, 0f);
        }
    }

    /// <summary>All the apples let go and fall. Returns how many fell.</summary>
    public int Drop()
    {
        int n = 0;
        float groundY = transform.position.y;
        for (int i = 0; i < apples.Count; i++)
        {
            var a = apples[i];
            if (a.t == null) continue;
            a.t.SetParent(null, true);
            var f = a.t.gameObject.AddComponent<FallingApple>();
            f.Begin(groundY + 0.05f + Random.value * 0.1f, Random.Range(-0.6f, 0.6f), i * 0.05f + Random.value * 0.08f);
            n++;
        }
        apples.Clear();
        return n;
    }

    public void Clear()
    {
        foreach (var a in apples) if (a.t != null) { if (Application.isPlaying) Destroy(a.t.gameObject); else DestroyImmediate(a.t.gameObject); }
        apples.Clear();
    }
}

/// <summary>An apple that has let go: falls, bounces once, lies a moment and fades away.</summary>
public class FallingApple : MonoBehaviour
{
    float groundY, vx, vy, delay, spin, life;
    bool landed, bounced;
    SpriteRenderer sr;

    public void Begin(float ground, float sideSpeed, float startDelay)
    {
        groundY = ground; vx = sideSpeed; delay = startDelay; vy = 0f; spin = Random.Range(-220f, 220f);
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (delay > 0f) { delay -= dt; return; }
        if (!landed)
        {
            vy -= 16f * dt;
            var p = transform.position;
            p.x += vx * dt; p.y += vy * dt;
            if (p.y <= groundY)
            {
                p.y = groundY;
                if (!bounced) { bounced = true; vy = 2.2f; vx *= 0.4f; spin *= 0.3f; }
                else { landed = true; life = 0f; }
            }
            transform.position = p;
            transform.Rotate(0f, 0f, spin * dt);
            return;
        }
        life += dt;
        if (life > 0.25f && sr != null)                                    // a short rest, then blink out
        {
            float t = (life - 0.25f) / 0.35f;
            var c = sr.color; c.a = (Mathf.FloorToInt(t * 8f) % 2 == 0) ? 1f - Mathf.Clamp01(t) : 0.3f; sr.color = c;
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
