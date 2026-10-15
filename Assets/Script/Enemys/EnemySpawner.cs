using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Setter ut fiender (goblins) i den tilfeldige verdenen. Ingen slots trengs: etter at WorldBuilder har laget verdenen går den langs hele
/// verden, finner bakken med en stråle (som BackgroundTrees), og setter ut fiender med litt tilfeldig avstand, med regler:
///   - ikke nær spilleren når verdenen starter (Min Distance From Player)
///   - ikke for tett på hverandre (Min Spacing), av og til en liten gruppe (Group Chance)
///   - bare der bakken er jevn og det er åpent over (ingen vegg, ingen hull, ingen tak)
///   - vakter ved hver GoblinGate (Guards Per Gate)
/// Samme verdens-seed gir alltid de samme fiendene. Legges på samme objekt som WorldBuilder.
/// Fiendene er prefaben(e) i Enemies-listen (lag den med DeepVain > Enemies > 1 - Make enemy prefab from selected).
/// Høyreklikk på script-overskriften: "Populate now" / "Clear".
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("Navn (bare for deg).")] public string label = "Goblin";
        public GameObject prefab;
        [Tooltip("Hvor ofte (1 = vanlig, 3 = tre ganger så ofte).")] [Min(0f)] public float weight = 1f;
        [Tooltip("Navnet som vises over fienden. Tomt = Display Name lenger ned.")] public string displayName = "";
        [Tooltip("Denne typen settes bare ut så langt unna spilleren ved start (units). 0 = overalt. Bruk f.eks. 40 for en sterkere type, så de ikke står rett ved start.")]
        [Min(0f)] public float minDistance = 0f;
    }

    [Header("Fiender")]
    public Entry[] enemies = new Entry[0];

    [Header("Spredt i verden")]
    [Tooltip("Avstand mellom hver gang spawneren prøver å sette ut en fiende (units): et tilfeldig tall mellom Min og Max.")]
    public float minGap = 25f;
    public float maxGap = 70f;
    [Tooltip("Nærmere enn dette (units) fra spilleren ved start settes ingen ut.")]
    public float minDistanceFromPlayer = 30f;
    [Tooltip("Minste avstand mellom to fiender som ikke er i samme gruppe (units).")]
    public float minSpacing = 10f;
    [Tooltip("Sjansen for at stedet får en liten gruppe i stedet for én.")]
    [Range(0f, 1f)] public float groupChance = 0.2f;
    public int groupMin = 2;
    public int groupMax = 3;
    [Tooltip("Gruppen står innen så mange units fra hverandre.")]
    public float groupRadius = 3f;
    [Tooltip("Hvor langt inn fra verdens kanter det ikke settes ut noe (units).")]
    public float edgeMargin = 8f;

    [Header("Bakken")]
    [Tooltip("Bakken tre units til hver side må ikke avvike mer enn dette i høyde (ellers er det et trinn eller en kant).")]
    public float maxHeightChange = 0.8f;
    [Tooltip("Det må være så mye åpen luft over fienden (units), ingen tak.")]
    public float clearHeight = 2.5f;
    [Tooltip("Hvor høyt over bakken fienden settes (units), så den ikke står fast i den.")]
    public float spawnLift = 0.05f;

    [Header("Vakter ved porter")]
    public bool guardGates = true;
    [Min(0)] public int guardsPerGate = 2;
    [Tooltip("Vaktene står mellom så langt fra porten (units) til hver side.")]
    public float guardMinDistance = 3f;
    public float guardMaxDistance = 7f;
    [Tooltip("Vaktene settes ut selv om spilleren står nær, men aldri nærmere enn dette (units).")]
    public float guardMinPlayerDistance = 6f;

    [Header("Levels (1-3)")]
    [Tooltip("Fiendene får level 1-3. Jo lenger fra spilleren ved start, jo høyere level.")]
    public bool useLevels = true;
    [Tooltip("Fra så langt unna (units) kan det komme level 2.")]
    public float level2FromDistance = 60f;
    [Tooltip("Fra så langt unna (units) er level 3 vanlig.")]
    public float level3FromDistance = 140f;
    [Tooltip("Vakter ved porter har minst dette levelet.")]
    [Range(1, 3)] public int guardLevel = 2;
    [Tooltip("Liv ganger så mye for level 1, 2 og 3.")]
    public float[] healthMultiplier = { 1f, 1.7f, 2.5f };
    [Tooltip("Skade ganger så mye for level 1, 2 og 3 (rundes til hele tall, minst 1: med grunnskade 1 blir det 1, 2 og 3).")]
    public float[] damageMultiplier = { 1f, 1.5f, 2.5f };
    [Tooltip("Navnet som vises over fienden (tomt = prefabens navn).")]
    public string displayName = "Goblin";

    [Header("Utelatt")]
    [Tooltip("Tilemaps som ligger under et objekt med disse ordene i navnet (kommaseparert) regnes ikke som verden: f.eks. den separate camp-chunken inne i porten.")]
    public string ignoreNames = "Interior,Inside,Inne";

    [Header("Kjøring")]
    public bool populateOnStart = true;
    public bool log = true;

    public readonly List<GameObject> Spawned = new List<GameObject>();
    Transform container;

    void Start()
    {
        if (populateOnStart && Application.isPlaying) StartCoroutine(AfterWorld());
    }

    IEnumerator AfterWorld()
    {
        yield return null;
        yield return null;                                      // one frame after the world, one more so the tiles' colliders are ready
        var wb = FindFirstObjectByType<WorldBuilder>();
        Populate(wb != null ? wb.lastSeedUsed : 12345);
    }

    // ------------------------------------------------------------------------------------------------ the ground
    bool MeasureWorld(out float minX, out float maxX, out float topY)
    {
        minX = float.PositiveInfinity; maxX = float.NegativeInfinity; topY = float.NegativeInfinity;
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (container != null && tm.transform.IsChildOf(container)) continue;
            if (Ignored(tm.transform)) continue;
            BoundsInt b = tm.cellBounds;
            if (b.size.x == 0 || b.size.y == 0) continue;
            Vector3 lo = tm.CellToWorld(new Vector3Int(b.xMin, b.yMin, 0));
            Vector3 hi = tm.CellToWorld(new Vector3Int(b.xMax, b.yMax, 0));
            minX = Mathf.Min(minX, lo.x); maxX = Mathf.Max(maxX, hi.x); topY = Mathf.Max(topY, hi.y);
        }
        return !float.IsInfinity(minX) && !float.IsInfinity(maxX);
    }

    bool Ignored(Transform t)
    {
        if (string.IsNullOrEmpty(ignoreNames)) return false;
        var parts = ignoreNames.Split(',');
        for (var p = t; p != null; p = p.parent)
            foreach (var part in parts)
            {
                string w = part.Trim();
                if (w.Length > 0 && p.name.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
        return false;
    }

    static bool GroundAt(float x, float topY, out float y)
    {
        y = 0f;
        var hits = Physics2D.RaycastAll(new Vector2(x, topY + 10f), Vector2.down, 300f);
        foreach (var h in hits)
        {
            var c = h.collider;
            if (c == null || c.isTrigger) continue;
            if (c is TilemapCollider2D || c is CompositeCollider2D) { y = h.point.y; return true; }
        }
        return false;
    }

    /// <summary>Is this a good place to stand? Even ground, nothing overhead.</summary>
    bool GoodSpot(float x, float topY, out float gy)
    {
        gy = 0f;
        if (!GroundAt(x, topY, out gy)) return false;
        float l, r;
        if (!GroundAt(x - 3f, topY, out l) || !GroundAt(x + 3f, topY, out r)) return false;
        if (Mathf.Abs(l - gy) > maxHeightChange || Mathf.Abs(r - gy) > maxHeightChange) return false;
        var up = Physics2D.Raycast(new Vector2(x, gy + 0.1f), Vector2.up, clearHeight);
        if (up.collider != null && !up.collider.isTrigger) return false;
        return true;
    }

    // ------------------------------------------------------------------------------------------------ the spawning
    bool Allowed(Entry e, float distance) { return e != null && e.prefab != null && e.minDistance <= distance; }

    Entry Pick(System.Random rng, float distance = float.MaxValue)
    {
        float total = 0f;
        foreach (var e in enemies) if (Allowed(e, distance)) total += e.weight;
        if (total <= 0f) return null;
        float r = (float)(rng.NextDouble() * total);
        Entry last = null;
        foreach (var e in enemies)
        {
            if (!Allowed(e, distance)) continue;
            last = e;
            r -= e.weight;
            if (r <= 0f) return e;
        }
        return last;
    }

    GameObject Place(Entry entry, float x, float gy, int level = 1)
    {
        var go = Instantiate(entry.prefab, new Vector3(x, gy + spawnLift, 0f), Quaternion.identity, container);
        go.name = entry.prefab.name;
        Spawned.Add(go);
        if (useLevels) ApplyLevel(go, level, entry);
        return go;
    }

    /// <summary>Picks a level from how far the spot is from the player's start: close = level 1, further out = 2 and 3.</summary>
    int RollLevel(System.Random rng, float distance)
    {
        double r = rng.NextDouble();
        float p1, p2;                                           // chance of level 1 and 2, the rest is level 3
        if (distance >= level3FromDistance) { p1 = 0.2f; p2 = 0.4f; }
        else if (distance >= level2FromDistance) { p1 = 0.5f; p2 = 0.4f; }
        else { p1 = 0.8f; p2 = 0.2f; }
        return r < p1 ? 1 : r < p1 + p2 ? 2 : 3;
    }

    void ApplyLevel(GameObject go, int level, Entry entry = null)
    {
        level = Mathf.Clamp(level, 1, 3);
        int i = level - 1;
        float hm = healthMultiplier != null && healthMultiplier.Length > i ? healthMultiplier[i] : 1f;
        float dm = damageMultiplier != null && damageMultiplier.Length > i ? damageMultiplier[i] : 1f;
        var e = go.GetComponent<Enemy>(); if (e != null) e.ScaleHealth(hm);
        var a = go.GetComponent<EnemyAttack>(); if (a != null) a.ScaleDamage(dm);
        string shown = entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : (string.IsNullOrEmpty(displayName) ? go.name : displayName);
        var bar = go.GetComponent<HealthBar>(); if (bar != null) bar.Setup(shown, level);
    }

    bool FarFromOthers(float x, float spacing)
    {
        foreach (var g in Spawned)
            if (g != null && Mathf.Abs(g.transform.position.x - x) < spacing) return false;
        return true;
    }

    public int Populate(int worldSeed)
    {
        Clear();
        if (Pick(new System.Random(1)) == null) { Debug.LogWarning("[EnemySpawner] Ingen fiende-prefab i Enemies-listen. Kjør DeepVain > Enemies > 1 - Make enemy prefab from selected.", this); return 0; }
        var go0 = new GameObject("Enemies_Generated");
        go0.transform.SetParent(transform, false);
        container = go0.transform;
        if (!MeasureWorld(out float minX, out float maxX, out float topY)) { Debug.LogWarning("[EnemySpawner] Fant ingen tiles, så jeg vet ikke hvor verden er.", this); return 0; }
        Physics2D.SyncTransforms();

        var rng = new System.Random(worldSeed ^ 0x3c6ef372);
        var player = FindFirstObjectByType<PlayerMovement>();
        float px = player != null ? player.transform.position.x : minX;
        int wild = 0, guards = 0, groups = 0;

        float x = minX + edgeMargin + (float)rng.NextDouble() * (maxGap - minGap) + minGap * 0.5f;
        while (x < maxX - edgeMargin)
        {
            float gy;
            if (Mathf.Abs(x - px) >= minDistanceFromPlayer && FarFromOthers(x, minSpacing) && GoodSpot(x, topY, out gy))
            {
                var prefab = Pick(rng, Mathf.Abs(x - px));
                if (prefab == null) { x += minGap + (float)rng.NextDouble() * Mathf.Max(0f, maxGap - minGap); continue; }
                int n = (rng.NextDouble() < groupChance) ? rng.Next(Mathf.Min(groupMin, groupMax), Mathf.Max(groupMin, groupMax) + 1) : 1;
                if (n > 1) groups++;
                for (int i = 0; i < n; i++)
                {
                    float xx = i == 0 ? x : x + ((float)rng.NextDouble() * 2f - 1f) * groupRadius;
                    float gy2;
                    if (i == 0) gy2 = gy;
                    else if (!GoodSpot(xx, topY, out gy2)) continue;
                    Place(i == 0 ? prefab : (Pick(rng, Mathf.Abs(xx - px)) ?? prefab), xx, gy2, RollLevel(rng, Mathf.Abs(xx - px)));
                    wild++;
                }
            }
            x += minGap + (float)rng.NextDouble() * Mathf.Max(0f, maxGap - minGap);
        }

        if (guardGates && guardsPerGate > 0)
        {
            var gates = FindObjectsByType<GoblinGate>(FindObjectsSortMode.None);
            System.Array.Sort(gates, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));        // the same order every time
            foreach (var gate in gates)
            {
                for (int i = 0; i < guardsPerGate; i++)
                {
                    int side = (i % 2 == 0) ? -1 : 1;
                    for (int tries = 0; tries < 6; tries++)
                    {
                        float gx = gate.transform.position.x + side * Mathf.Lerp(guardMinDistance, guardMaxDistance, (float)rng.NextDouble());
                        float gy;
                        if (Mathf.Abs(gx - px) < guardMinPlayerDistance) continue;
                        if (!FarFromOthers(gx, 2.5f) || !GoodSpot(gx, topY, out gy)) continue;
                        var prefab = Pick(rng, Mathf.Abs(gx - px));
                        if (prefab == null) break;
                        Place(prefab, gx, gy, Mathf.Max(guardLevel, RollLevel(rng, Mathf.Abs(gx - px)))); guards++;
                        break;
                    }
                }
            }
        }
        if (log) Debug.Log("[EnemySpawner] " + Spawned.Count + " fiender: " + wild + " ute i verden (" + groups + " grupper), " + guards + " vakter ved porter.", this);
        return Spawned.Count;
    }

    [ContextMenu("Populate now")]
    public void PopulateNow() { var wb = FindFirstObjectByType<WorldBuilder>(); Populate(wb != null ? wb.lastSeedUsed : 12345); }

    [ContextMenu("Clear")]
    public void Clear()
    {
        var t = transform.Find("Enemies_Generated");
        if (t != null) { if (Application.isPlaying) Destroy(t.gameObject); else DestroyImmediate(t.gameObject); }
        container = null;
        Spawned.Clear();
    }
}
