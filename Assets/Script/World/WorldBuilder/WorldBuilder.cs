using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// STEG 1: setter chunker på rekke. Spawn-chunken blir stående der den er; deretter kommer "Chunk Count" tilfeldige chunker
/// rett etter hverandre mot høyre (aldri samme chunk to ganger på rad). Alle ligger på samme høyde, så bare x flyttes.
///
/// Bredden på en chunk måles fra tilene i alle Tilemaps inni den, så det spiller ingen rolle om den er 20 eller 24 tiles.
/// Høyden justeres slik at bakken i venstre kant av hver chunk møter bakken i høyre kant av forrige, så det spiller ingen rolle
/// hvor høyt de står som prefabs.
/// Legg scriptet på et tomt objekt (f.eks. "World"). Høyreklikk på scriptet (de tre prikkene) > Generate / Clear for å se verdenen
/// uten å spille.
/// </summary>
public class WorldBuilder : MonoBehaviour
{
    [Header("Chunks")]
    [Tooltip("Spawn-chunken. Blir stående der den er (scene-objekt) eller settes ut ved x = 0 (prefab).")]
    public GameObject spawnChunk;
    [Tooltip("Chunkene som kan komme etter spawn. Alle prefabs.")]
    public GameObject[] chunkPrefabs;
    [Min(0)] public int chunkCount = 10;

    [Header("Tilfeldighet")]
    public bool randomSeed = true;
    public int seed = 12345;
    [Tooltip("Seedet som faktisk ble brukt sist (kopier hit og skru av Random Seed for å få samme verden igjen).")]
    public int lastSeedUsed;

    [Header("Kjøring")]
    public bool generateOnStart = true;
    public bool log = true;

    Transform container;
    readonly List<Bounds> placed = new List<Bounds>();

    void Start()
    {
        if (generateOnStart && Application.isPlaying) Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (chunkPrefabs == null || chunkPrefabs.Length == 0) { Debug.LogWarning("[WorldBuilder] Ingen chunk-prefabs er satt."); return; }
        Clear();

        int usedSeed = randomSeed ? Random.Range(int.MinValue, int.MaxValue) : seed;
        lastSeedUsed = usedSeed;
        var rng = new System.Random(usedSeed);

        // spawn-chunken
        float cursor = 0f, cursorY = 0f;
        Transform spawnRoot = null;
        bool haveY = false;
        if (spawnChunk != null)
        {
            GameObject s = spawnChunk;
            if (!s.scene.IsValid()) s = Spawn(spawnChunk);        // prefab-asset: sett den ut
            spawnRoot = s.transform;
            if (Measure(s, out Bounds sb)) { cursor = sb.max.x; cursorY = SurfaceY(s, sb, false); haveY = true; placed.Add(sb); }
            else Debug.LogWarning("[WorldBuilder] Fant ingen tiles i spawn-chunken.");
            if (log) Debug.Log("[WorldBuilder] Spawn: x " + sb0(placed) );
        }

        EnsureContainer();
        int last = -1;
        var sb2 = new System.Text.StringBuilder();
        for (int i = 0; i < chunkCount; i++)
        {
            int idx = Pick(rng, chunkPrefabs.Length, last);
            last = idx;
            GameObject prefab = chunkPrefabs[idx];
            if (prefab == null) { Debug.LogWarning("[WorldBuilder] Chunk " + idx + " er tom i listen."); continue; }

            GameObject c = Spawn(prefab);
            if (!Measure(c, out Bounds b)) { Debug.LogWarning("[WorldBuilder] " + prefab.name + " har ingen tiles, hoppet over."); DestroyNow(c); continue; }

            // x: venstre kant av denne = høyre kant av forrige.  y: bakken i venstre kant = bakken i høyre kant av forrige
            // (så det spiller ingen rolle hvor høyt prefaben selv står i Hierarchy/Project)
            float dx = cursor - b.min.x;
            float dy = haveY ? cursorY - SurfaceY(c, b, true) : 0f;
            c.transform.position += new Vector3(dx, dy, 0f);
            b.center += new Vector3(dx, dy, 0f);
            placed.Add(b);
            cursor = b.max.x; cursorY = SurfaceY(c, b, false); haveY = true;
            sb2.Append(string.Format("\n  {0,2}: {1}   x {2:0.##} -> {3:0.##}  ({4:0.##} bred)  bakke y {5:0.##}", i + 1, prefab.name, b.min.x, b.max.x, b.size.x, cursorY));
        }
        // trær (og annet) på TreeSlot-plassene, hvis det finnes en TreeSpawner på samme objekt
        var trees = GetComponent<TreeSpawner>();
        if (trees != null)
        {
            var roots = new List<Transform>();
            if (spawnRoot != null) roots.Add(spawnRoot);
            roots.Add(container);
            trees.Populate(roots, usedSeed);
        }
        if (log) Debug.Log("[WorldBuilder] Seed " + usedSeed + ", " + placed.Count + " chunker, verden slutter ved x = " + cursor.ToString("0.##") + sb2);
    }

    static string sb0(List<Bounds> l) { return l.Count == 0 ? "?" : l[0].min.x.ToString("0.##") + " -> " + l[0].max.x.ToString("0.##"); }

    /// <summary>Velger en chunk, men aldri den samme som sist (hvis det finnes mer enn én).</summary>
    static int Pick(System.Random rng, int count, int last)
    {
        if (count == 1) return 0;
        if (last < 0) return rng.Next(count);
        int i = rng.Next(count - 1);
        return i >= last ? i + 1 : i;
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        placed.Clear();
        var t = transform.Find("Generated");
        if (t != null) DestroyNow(t.gameObject);
        container = null;
    }

    void EnsureContainer()
    {
        if (container != null) return;
        var t = transform.Find("Generated");
        if (t == null) { var go = new GameObject("Generated"); go.transform.SetParent(transform, false); t = go.transform; }
        container = t;
    }

    GameObject Spawn(GameObject prefab)
    {
        EnsureContainer();
#if UNITY_EDITOR
        if (!Application.isPlaying) return (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
#endif
        return Instantiate(prefab, container);
    }

    static void DestroyNow(GameObject go)
    {
        if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
    }

    /// <summary>Verdens-grensene til alle tiles i chunken (union av alle Tilemaps inni den).</summary>
    public static bool Measure(GameObject chunk, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (var tm in chunk.GetComponentsInChildren<Tilemap>())
        {
            tm.CompressBounds();
            BoundsInt b = tm.cellBounds;
            if (b.size.x == 0 || b.size.y == 0) continue;
            Vector3 lo = tm.CellToWorld(new Vector3Int(b.xMin, b.yMin, 0));
            Vector3 hi = tm.CellToWorld(new Vector3Int(b.xMax, b.yMax, 0));
            if (!any) { bounds = new Bounds(lo, Vector3.zero); any = true; }
            bounds.Encapsulate(lo); bounds.Encapsulate(hi);
        }
        return any;
    }

    /// <summary>
    /// Verdens-y til toppen av bakken helt ytterst i chunken (venstre eller høyre kant). Bruker tilemaps med collider
    /// (bakke/vegger); finnes ingen, brukes alle. Finner øverste tile i ytterste kolonne.
    /// </summary>
    public static float SurfaceY(GameObject chunk, Bounds bounds, bool leftEdge)
    {
        float x = leftEdge ? bounds.min.x + 0.01f : bounds.max.x - 0.01f;
        var all = chunk.GetComponentsInChildren<Tilemap>();
        bool anyCollider = false;
        foreach (var tm in all) if (tm.GetComponent<TilemapCollider2D>() != null) anyCollider = true;
        float best = float.NegativeInfinity;
        foreach (var tm in all)
        {
            if (anyCollider && tm.GetComponent<TilemapCollider2D>() == null) continue;
            BoundsInt b = tm.cellBounds;
            if (b.size.x == 0 || b.size.y == 0) continue;
            int cx = tm.WorldToCell(new Vector3(x, tm.transform.position.y, 0f)).x;
            for (int y = b.yMax - 1; y >= b.yMin; y--)
            {
                if (!tm.HasTile(new Vector3Int(cx, y, 0))) continue;
                best = Mathf.Max(best, tm.CellToWorld(new Vector3Int(cx, y + 1, 0)).y);
                break;
            }
        }
        return float.IsNegativeInfinity(best) ? bounds.max.y : best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        for (int i = 0; i < placed.Count; i++) Gizmos.DrawWireCube(placed[i].center, placed[i].size);
    }
}
