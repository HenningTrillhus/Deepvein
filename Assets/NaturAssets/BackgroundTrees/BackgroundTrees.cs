using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// En mellom-bakgrunn av trær langs hele verden: mindre (scale 1), mørkere, på et lag bak trærne foran, tett men tilfeldig plassert,
/// på bakken. De er bare pynt: ingen collider, kan ikke pekes på eller hugges, og de svaier svakt i vinden.
/// Legges på et tomt objekt (for eksempel det samme som WorldBuilder). Ved start venter den én frame, så WorldBuilder har laget
/// chunkene, og fyller så verden fra venstre til høyre kant. Samme verdens-seed gir alltid samme skog.
/// Høyreklikk på script-overskriften: "Populate now" / "Clear" for å se det uten å spille.
/// </summary>
public class BackgroundTrees : MonoBehaviour
{
    [Header("Trær")]
    [Tooltip("Trærne å velge fra (samme TreeSet som skogen foran går fint).")]
    public TreeSet treeSet;
    [Tooltip("Bilder som har ett av disse navnene i seg er ikke med (kommaseparert). Tørre trær ser rart ut i bakgrunnen.")]
    public string excludeNames = "Snag";

    [Header("Utseende")]
    [Tooltip("Hvert tre får en tilfeldig farge mellom disse to (ganges med bildet), så skogen får dybde. Mørkere = lenger bak.")]
    public Color darkTint = new Color(0.30f, 0.38f, 0.42f, 1f);
    public Color lightTint = new Color(0.46f, 0.54f, 0.56f, 1f);
    [Tooltip("Størrelse (1 = som tegnet, halvparten av trærne foran som har 2).")]
    [Min(0.05f)] public float scale = 1f;
    [Range(0f, 0.5f)] public float scaleVariation = 0.12f;
    public bool randomFlip = true;

    [Header("Busker (valgfritt)")]
    [Tooltip("Vanlige busker som står mellom trærne, i samme mørke tone. Tom liste = ingen busker. Fylles av DeepVain > Trees > 9 - Add background bushes.")]
    public Sprite[] bushSprites = new Sprite[0];
    [Tooltip("Størrelse på buskene (2 = dobbelt så store som tegnet).")]
    [Min(0.05f)] public float bushScale = 2f;
    [Range(0f, 0.5f)] public float bushScaleVariation = 0.15f;
    [Tooltip("Avstand mellom buskene (units).")]
    public float bushMinGap = 1.6f;
    public float bushMaxGap = 4.2f;
    [Tooltip("Legges til Sorting Order, så buskene står litt foran de bakerste trærne.")]
    public int bushOrderOffset = 1;

    [Header("Lag")]
    [Tooltip("Sorting Layer: bak trærne foran (NaturAsset), foran selve bakken (Ground).")]
    public string sortingLayer = "AssetsBackGround";
    public int sortingOrder = 0;
    [Tooltip("Hvert tre får en tilfeldig verdi fra Sorting Order til Sorting Order + dette.")]
    [Min(0)] public int orderSpread = 3;

    [Header("Plassering")]
    [Tooltip("Avstand mellom trærne (units). Lavere = tettere skog.")]
    public float minGap = 0.7f;
    public float maxGap = 1.8f;
    [Tooltip("Flytter trærne opp/ned i forhold til toppen av bakken. Gress-tilene er litt tomme øverst, så de står litt under.")]
    public float yOffset = -0.19f;
    [Tooltip("Hvor langt utenfor verdens kanter skogen fortsetter (units), så kantene ikke er nakne.")]
    public float edgeMargin = 8f;

    [Header("Følge bakken")]
    [Tooltip("1 = trærne står på bakken under seg (følger høyder og trinn). 0 = alle på samme nivå. Mellom: en blanding.")]
    [Range(0f, 1f)] public float followGround = 1f;
    [Tooltip("Hvor mye bakkehøyden glattes (units), så et trinn blir en myk skråning og ikke et hopp for hvert tre.")]
    [Min(0f)] public float smoothRadius = 0.8f;
    [Tooltip("Bakke høyere enn dette over vanlig nivå (en vegg, et tårn) følges ikke. Units.")]
    public float maxRise = 4f;
    [Tooltip("Bakke lavere enn dette under vanlig nivå (et hull) følges ikke. Units.")]
    public float maxDrop = 4f;

    [Header("Vind")]
    public bool sway = true;
    [Tooltip("Hvor langt toppen flytter seg sideveis (units). Svakere enn trærne foran.")]
    public float swayAmount = 0.05f;
    [Tooltip("Materialet 'Sprite Sway'. Tomt = lager ett felles av shaderen selv.")]
    public Material swayMaterial;

    [Header("Kjøring")]
    public bool populateOnStart = true;
    public bool log = true;

    Transform container;

    void Start()
    {
        if (populateOnStart && Application.isPlaying) StartCoroutine(AfterWorld());
    }

    IEnumerator AfterWorld()
    {
        yield return null;
        var wb = FindFirstObjectByType<WorldBuilder>();
        Populate(wb != null ? wb.lastSeedUsed : 12345);
    }

    bool Excluded(string spriteName)
    {
        if (string.IsNullOrEmpty(excludeNames)) return false;
        foreach (var part in excludeNames.Split(','))
        {
            string p = part.Trim();
            if (p.Length > 0 && spriteName.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    /// <summary>Finds the x range of the world (all tilemaps) and the y of the ground.</summary>
    bool MeasureWorld(out float minX, out float maxX, out float topY)
    {
        minX = float.PositiveInfinity; maxX = float.NegativeInfinity; topY = float.NegativeInfinity;
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (container != null && tm.transform.IsChildOf(container)) continue;
            BoundsInt b = tm.cellBounds;
            if (b.size.x == 0 || b.size.y == 0) continue;
            Vector3 lo = tm.CellToWorld(new Vector3Int(b.xMin, b.yMin, 0));
            Vector3 hi = tm.CellToWorld(new Vector3Int(b.xMax, b.yMax, 0));
            minX = Mathf.Min(minX, lo.x); maxX = Mathf.Max(maxX, hi.x); topY = Mathf.Max(topY, hi.y);
        }
        return !float.IsInfinity(minX) && !float.IsInfinity(maxX);
    }

    static bool GroundAt(float x, float topY, out float y)
    {
        y = 0f;
        var hits = Physics2D.RaycastAll(new Vector2(x, topY + 10f), Vector2.down, 200f);
        foreach (var h in hits)
        {
            var c = h.collider;
            if (c == null || c.isTrigger) continue;
            if (c is TilemapCollider2D || c is CompositeCollider2D) { y = h.point.y; return true; }       // the ground, not the player or enemies
        }
        return false;
    }

    public int Populate(int worldSeed)
    {
        Clear();
        if (treeSet == null) { Debug.LogWarning("[BackgroundTrees] Ingen TreeSet er satt.", this); return 0; }
        if (!MeasureWorld(out float minX, out float maxX, out float topY)) { Debug.LogWarning("[BackgroundTrees] Fant ingen tiles, så jeg vet ikke hvor verden er.", this); return 0; }
        Physics2D.SyncTransforms();

        var go0 = new GameObject("BackgroundTrees_Generated");
        go0.transform.SetParent(transform, false);
        container = go0.transform;

        var rng = new System.Random(worldSeed ^ 0x5bd1e995);
        Material mat = swayMaterial;
        if (sway && mat == null)
        {
            var shader = Shader.Find("DeepVain/Sprite Sway");
            if (shader != null) mat = new Material(shader) { name = "Sprite Sway (background)" };
        }

        // the ground line: the median of what the rays find (so a wall or a pit does not decide the height)
        var samples = new List<float>();
        for (float x = minX; x < maxX; x += 4f) if (GroundAt(x, topY, out float gy)) samples.Add(gy);
        if (samples.Count == 0) { Debug.LogWarning("[BackgroundTrees] Fant ingen bakke (TilemapCollider2D / CompositeCollider2D).", this); return 0; }
        samples.Sort();
        float baseY = samples[samples.Count / 2];

        // the height of the ground along the whole forest, every half unit, then smoothed a little
        const float Step = 0.5f;
        float x0 = minX - edgeMargin;
        int n = Mathf.CeilToInt((maxX - minX + 2f * edgeMargin) / Step) + 2;
        var raw = new float[n];
        for (int i = 0; i < n; i++)
        {
            float gy;
            raw[i] = (GroundAt(x0 + i * Step, topY, out gy) && gy <= baseY + maxRise && gy >= baseY - maxDrop) ? gy : baseY;
        }
        var ground = new float[n];
        int k = Mathf.RoundToInt(smoothRadius / Step);
        for (int i = 0; i < n; i++)
        {
            float sum = 0f; int cnt = 0;
            for (int j = Mathf.Max(0, i - k); j <= Mathf.Min(n - 1, i + k); j++) { sum += raw[j]; cnt++; }
            ground[i] = sum / Mathf.Max(1, cnt);
        }

        var recent = new List<TreeSet.Entry>();
        int made = 0;
        float cx = minX - edgeMargin;
        while (cx < maxX + edgeMargin)
        {
            // draw a tree for this spot; excluded ones (dry trees) are drawn again, so the spacing stays even
            TreeSet.Entry e = null;
            for (int tries = 0; tries < 8; tries++)
            {
                var cand = treeSet.Pick(rng, recent);
                if (cand == null) break;
                if (cand.sprite != null && Excluded(cand.sprite.name)) continue;
                e = cand; break;
            }
            if (e == null) break;
            recent.Add(e); while (recent.Count > 2) recent.RemoveAt(0);

            float fi = Mathf.Clamp((cx - x0) / Step, 0f, n - 1f);
            int i0 = Mathf.FloorToInt(fi), i1 = Mathf.Min(n - 1, i0 + 1);
            float gh = Mathf.Lerp(ground[i0], ground[i1], fi - i0);
            float y = baseY + (gh - baseY) * followGround;                                       // on the ground under the tree
            Spawn(e, new Vector3(cx, y + yOffset, 0f), rng, mat);
            made++;
            cx += minGap + (float)rng.NextDouble() * Mathf.Max(0f, maxGap - minGap);
        }
        int bushes = 0;
        if (bushSprites != null && bushSprites.Length > 0)
        {
            var brng = new System.Random(worldSeed ^ 0x2c1b3c6d);
            float bx = minX - edgeMargin + (float)brng.NextDouble() * bushMaxGap;
            Sprite prev = null;
            while (bx < maxX + edgeMargin)
            {
                Sprite sp = null;
                for (int tries = 0; tries < 6; tries++)
                {
                    sp = bushSprites[brng.Next(bushSprites.Length)];
                    if (sp != null && (sp != prev || bushSprites.Length == 1)) break;
                }
                if (sp != null)
                {
                    float fi = Mathf.Clamp((bx - x0) / Step, 0f, n - 1f);
                    int i0 = Mathf.FloorToInt(fi), i1 = Mathf.Min(n - 1, i0 + 1);
                    float gh = Mathf.Lerp(ground[i0], ground[i1], fi - i0);
                    float y = baseY + (gh - baseY) * followGround;
                    SpawnBush(sp, new Vector3(bx, y + yOffset, 0f), brng, mat);
                    bushes++; prev = sp;
                }
                bx += bushMinGap + (float)brng.NextDouble() * Mathf.Max(0f, bushMaxGap - bushMinGap);
            }
        }
        if (log) Debug.Log("[BackgroundTrees] " + made + " trær" + (bushes > 0 ? " og " + bushes + " busker" : "") + " fra x " + (minX - edgeMargin).ToString("0") + " til " + (maxX + edgeMargin).ToString("0") + ", bakke y " + baseY.ToString("0.00") + ".");
        return made;
    }

    void Spawn(TreeSet.Entry e, Vector3 pos, System.Random rng, Material mat)
    {
        var go = new GameObject("BG_" + (string.IsNullOrEmpty(e.label) ? "Tree" : e.label), typeof(SpriteRenderer));
        go.transform.SetParent(container, false);
        go.transform.position = pos;
        float k = scale * (scaleVariation > 0f ? 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * scaleVariation : 1f);
        go.transform.localScale = new Vector3(k, k, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = e.sprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder + (orderSpread > 0 ? rng.Next(0, orderSpread + 1) : 0);
        sr.flipX = randomFlip && rng.Next(2) == 0;
        float t = (float)rng.NextDouble();
        sr.color = Color.Lerp(darkTint, lightTint, t);

        if (sway && mat != null)
        {
            bool was = go.activeSelf;
            go.SetActive(false);                              // so the material is in place before TreeSway starts
            var sw = go.AddComponent<TreeSway>();
            sw.swayMaterial = mat;
            sw.amount = swayAmount;
            sw.speed = 0.8f;
            go.SetActive(was);
            TreeHover.Exclude(go);                            // never hovered, highlighted or chopped
        }
#if UNITY_EDITOR
        if (!Application.isPlaying) go.hideFlags = HideFlags.DontSaveInEditor;
#endif
    }

    void SpawnBush(Sprite sprite, Vector3 pos, System.Random rng, Material mat)
    {
        var go = new GameObject("BG_Bush_" + sprite.name, typeof(SpriteRenderer));
        go.transform.SetParent(container, false);
        go.transform.position = pos;
        float k = bushScale * (bushScaleVariation > 0f ? 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * bushScaleVariation : 1f);
        go.transform.localScale = new Vector3(k, k, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder + bushOrderOffset + (orderSpread > 0 ? rng.Next(0, orderSpread + 1) : 0);
        sr.flipX = randomFlip && rng.Next(2) == 0;
        sr.color = Color.Lerp(darkTint, lightTint, (float)rng.NextDouble());                // the same dark tone as the trees

        if (sway && mat != null)
        {
            bool was = go.activeSelf;
            go.SetActive(false);
            var sw = go.AddComponent<TreeSway>();
            sw.swayMaterial = mat;
            sw.amount = swayAmount;
            sw.speed = 0.9f;
            go.SetActive(was);
            TreeHover.Exclude(go);
        }
#if UNITY_EDITOR
        if (!Application.isPlaying) go.hideFlags = HideFlags.DontSaveInEditor;
#endif
    }

    [ContextMenu("Populate now")]
    public void PopulateNow() { var wb = FindFirstObjectByType<WorldBuilder>(); Populate(wb != null ? wb.lastSeedUsed : 12345); }

    [ContextMenu("Clear")]
    public void Clear()
    {
        var t = transform.Find("BackgroundTrees_Generated");
        if (t != null) { if (Application.isPlaying) Destroy(t.gameObject); else DestroyImmediate(t.gameObject); }
        container = null;
    }
}
