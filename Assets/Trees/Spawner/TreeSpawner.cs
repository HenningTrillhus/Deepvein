using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts a random tree at every TreeSlot inside the chunks. Lives on the same object as WorldBuilder (which calls Populate after the world
/// is made), or call Populate yourself. The same world seed always gives the same trees.
/// Right-click the script header: "Populate all slots in scene" / "Clear trees" to see it without playing.
/// </summary>
public class TreeSpawner : MonoBehaviour
{
    [Header("Trær")]
    [Tooltip("Standardlisten med trær. En TreeSlot kan ha sin egen liste.")]
    public TreeSet treeSet;
    [Tooltip("Valgfritt: en prefab med SpriteRenderer (og f.eks. TreeSway). Spawneren bytter bare bildet. Tomt = lager et vanlig objekt med SpriteRenderer.")]
    public GameObject template;

    [Header("Plassering")]
    [Tooltip("Flytter treet opp/ned i forhold til TreeSlot. Gress-tilene er 0.21 'tomme' øverst, så treet står litt under flaten collideren har.")]
    public float yOffset = -0.19f;

    [Header("Størrelse")]
    [Tooltip("Hvor store trærne blir (1 = som tegnet, 2 = dobbelt så stor i både bredde og høyde).")]
    [Min(0.05f)] public float scale = 1.5f;
    [Tooltip("Tilfeldig variasjon i størrelse, i prosent opp og ned (0.1 = +/- 10 %). 0 = alle like store.")]
    [Range(0f, 0.5f)] public float scaleVariation = 0f;

    [Header("Tegnerekkefølge")]
    public string sortingLayer = "Default";
    [Tooltip("Treet ligger bak spilleren. Samme verdi som de andre trærne dine (mine har vært -4).")]
    public int sortingOrder = -4;
    [Tooltip("Hvert tre får en tilfeldig verdi fra Sorting Order til Sorting Order + dette, så trærne som overlapper blander seg pent.")]
    [Min(0)] public int orderSpread = 2;

    [Header("Variasjon")]
    [Tooltip("Unngå at samme tre kommer rett etter hverandre: hvor mange av de siste som hoppes over hvis mulig.")]
    [Min(0)] public int avoidRepeats = 2;

    [Header("Kjøring")]
    public bool log = true;

    /// <summary>Makes trees for every TreeSlot below the given roots (spawn chunk and generated chunks together, so repeats are avoided across the whole world).</summary>
    public int Populate(IEnumerable<Transform> roots, int worldSeed)
    {
        var slots = new List<TreeSlot>();
        foreach (var r in roots) if (r != null) slots.AddRange(r.GetComponentsInChildren<TreeSlot>(false));
        slots.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        var recent = new List<TreeSet.Entry>();
        int made = 0, skipped = 0, missing = 0;
        foreach (var slot in slots)
        {
            RemoveTreesOf(slot);
            var set = slot.treeSet != null ? slot.treeSet : treeSet;
            if (set == null) { missing++; continue; }

            // one random generator per slot, from the world seed and where the slot is: the same world always gets the same trees
            Vector3 p = slot.transform.position;
            var rng = new System.Random(Hash(worldSeed, Mathf.RoundToInt(p.x * 10f), Mathf.RoundToInt(p.y * 10f)));
            if (rng.NextDouble() > slot.chance) { skipped++; continue; }

            var entry = set.Pick(rng, recent);
            if (entry == null) { missing++; continue; }
            recent.Add(entry); while (recent.Count > avoidRepeats) recent.RemoveAt(0);

            Spawn(slot, entry, rng);
            made++;
        }
        if (log) Debug.Log("[TreeSpawner] " + made + " trær av " + slots.Count + " plasser (" + skipped + " ble ikke med på sjansen" + (missing > 0 ? ", " + missing + " uten tre-liste" : "") + ").");
        return made;
    }

    void Spawn(TreeSlot slot, TreeSet.Entry entry, System.Random rng)
    {
        GameObject go;
        Vector3 pos = slot.transform.position + new Vector3(0f, yOffset, 0f);
        if (slot.jitterX > 0f) pos.x += (float)(rng.NextDouble() * 2.0 - 1.0) * slot.jitterX;

        if (entry.prefab != null) go = Instantiate(entry.prefab, pos, Quaternion.identity, slot.transform);
        else if (template != null) go = Instantiate(template, pos, Quaternion.identity, slot.transform);
        else go = new GameObject("Tree", typeof(SpriteRenderer));
        if (go.transform.parent != slot.transform) { go.transform.SetParent(slot.transform, true); go.transform.position = pos; }

        go.name = "Tree_" + (string.IsNullOrEmpty(entry.label) ? (entry.sprite != null ? entry.sprite.name : "x") : entry.label);
        go.AddComponent<SpawnedTreeMarker>();

        float k = scale * (scaleVariation > 0f ? 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * scaleVariation : 1f);
        go.transform.localScale = go.transform.localScale * k;                 // the feet are the pivot, so it grows upwards from the ground

        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            if (entry.prefab == null && entry.sprite != null) sr.sprite = entry.sprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder + (orderSpread > 0 ? rng.Next(0, orderSpread + 1) : 0);
            if (slot.randomFlip) sr.flipX = rng.Next(2) == 0;
        }
#if UNITY_EDITOR
        if (!Application.isPlaying) go.hideFlags = HideFlags.DontSaveInEditor;                     // an editor preview must not be saved into the scene
#endif
    }

    static int Hash(int a, int b, int c)
    {
        unchecked
        {
            int h = 17; h = h * 31 + a; h = h * 31 + b; h = h * 31 + c;
            h ^= h << 13; h ^= h >> 17; h ^= h << 5;
            return h;
        }
    }

    /// <summary>Removes the tree this spawner made at one slot.</summary>
    public static void RemoveTreesOf(TreeSlot slot)
    {
        for (int i = slot.transform.childCount - 1; i >= 0; i--)
        {
            var c = slot.transform.GetChild(i);
            if (c.GetComponent<SpawnedTreeMarker>() == null) continue;
            if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
        }
    }

    [ContextMenu("Populate all slots in scene")]
    public void PopulateScene()
    {
        var roots = new List<Transform>();
        foreach (var s in FindObjectsByType<TreeSlot>(FindObjectsSortMode.None)) roots.Add(s.transform);
        Populate(roots, 12345);
    }

    [ContextMenu("Clear trees")]
    public void ClearScene()
    {
        foreach (var s in FindObjectsByType<TreeSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) RemoveTreesOf(s);
    }
}
