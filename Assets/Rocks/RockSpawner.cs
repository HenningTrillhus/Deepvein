using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Setter ut steiner på RockSlot-plassene, på samme måte som TreeSpawner gjør med trær.
/// Hver plass har en sjanse for å få en stein i det hele tatt (RockSlot.Chance), og en sjanse for at steinen har kull i seg
/// (RockSlot.Coal Chance). Samme verdens-seed gir alltid de samme steinene.
/// Legges på samme objekt som WorldBuilder/PropSpawner. Med "Populate On Start" venter den én frame, så WorldBuilder har laget chunkene.
/// Høyreklikk på script-overskriften: "Populate all slots in scene" / "Clear rocks" for å se det uten å spille.
/// </summary>
public class RockSpawner : MonoBehaviour
{
    [Header("Steiner")]
    public RockSet rockSet;

    [Header("Plassering")]
    [Tooltip("Flytter steinen opp/ned i forhold til plassen (som PropSpawner).")]
    public float yOffset = -0.15f;
    [Tooltip("50x50-bildene vises dobbelt så store (1 bilde-piksel = 2x2 skjermpiksler), som de andre propsene.")]
    [Min(0.05f)] public float scale = 2f;
    [Range(0f, 0.3f)] public float scaleVariation = 0f;
    [Min(0)] public int avoidRepeats = 2;

    [Header("Tegnerekkefølge")]
    public string sortingLayer = "Default";
    [Tooltip("Steinen ligger bak spilleren, litt foran trærne (trær har vært -4 til -2).")]
    public int sortingOrder = -1;

    [Header("Hva den gir (settes på hver stein)")]
    public int stoneMin = 2;
    public int stoneMax = 5;
    [Tooltip("Steiner med kull i seg gir mindre stein ...")]
    public int coalStoneMin = 1;
    public int coalStoneMax = 3;
    [Tooltip("... pluss kull.")]
    public int coalMin = 1;
    public int coalMax = 3;

    [Header("Vind/valg (TreeSway uten vind: gir kontur og skjelving)")]
    public Material swayMaterial;

    [Header("Kjøring")]
    public bool populateOnStart = true;
    public bool log = true;

    void Start()
    {
        if (populateOnStart && Application.isPlaying) StartCoroutine(PopulateAfterWorld());
    }

    IEnumerator PopulateAfterWorld()
    {
        yield return null;
        yield return null;                                       // one more, so the PropSpawner and TreeSpawner are done first
        var wb = FindFirstObjectByType<WorldBuilder>();
        int seed = wb != null ? wb.lastSeedUsed : 12345;
        Populate(null, seed);
    }

    public int Populate(IList<RockSlot> slotsIn, int worldSeed)
    {
        var slots = slotsIn != null ? new List<RockSlot>(slotsIn) : new List<RockSlot>(FindObjectsByType<RockSlot>(FindObjectsSortMode.None));
        slots.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        var recent = new List<RockSet.Entry>();
        int made = 0, coal = 0, skipped = 0, missing = 0;
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            RemoveRocksOf(slot);
            var set = slot.rockSet != null ? slot.rockSet : rockSet;
            if (set == null) { missing++; continue; }

            Vector3 p = slot.transform.position;
            var rng = new System.Random(Hash(worldSeed, Mathf.RoundToInt(p.x * 10f), Mathf.RoundToInt(p.y * 10f)));
            if (rng.NextDouble() > slot.chance) { skipped++; continue; }

            bool wantCoal = rng.NextDouble() < slot.coalChance && set.HasCoal;
            var entry = set.Pick(rng, wantCoal, recent);
            if (entry == null) { missing++; continue; }
            recent.Add(entry); while (recent.Count > avoidRepeats) recent.RemoveAt(0);

            Spawn(slot, entry, rng);
            made++; if (entry.coal) coal++;
        }
        if (log) Debug.Log("[RockSpawner] " + made + " steiner av " + slots.Count + " plasser (" + coal + " med kull, " + skipped + " ble ikke med på sjansen"
            + (missing > 0 ? ", " + missing + " uten stein-liste" : "") + ").");
        return made;
    }

    void Spawn(RockSlot slot, RockSet.Entry entry, System.Random rng)
    {
        Vector3 pos = slot.transform.position + new Vector3(0f, yOffset, 0f);
        if (slot.jitterX > 0f) pos.x += (float)(rng.NextDouble() * 2.0 - 1.0) * slot.jitterX;

        var go = new GameObject("Rock_" + (string.IsNullOrEmpty(entry.label) ? entry.sprite.name : entry.label), typeof(SpriteRenderer));
        go.transform.SetParent(slot.transform, false);
        go.transform.position = pos;
        float k = scale * (scaleVariation > 0f ? 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * scaleVariation : 1f);
        go.transform.localScale = new Vector3(k, k, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = entry.sprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;
        if (slot.randomFlip) sr.flipX = rng.Next(2) == 0;

        go.AddComponent<SpawnedRockMarker>();
        var m = go.AddComponent<Mineable>();
        m.health = entry.health; m.coal = entry.coal; m.chipColor = entry.chipColor;
        if (entry.coal) { m.stoneMin = coalStoneMin; m.stoneMax = coalStoneMax; }
        else { m.stoneMin = stoneMin; m.stoneMax = stoneMax; }
        m.coalMin = coalMin; m.coalMax = coalMax;

        AddSway(go);
#if UNITY_EDITOR
        if (!Application.isPlaying) go.hideFlags = HideFlags.DontSaveInEditor;
#endif
    }

    void AddSway(GameObject go)
    {
        if (swayMaterial == null)
        {
            var shader = Shader.Find("DeepVain/Sprite Sway");
            if (shader != null) swayMaterial = new Material(shader) { name = "Sprite Sway (rocks)" };
        }
        bool wasActive = go.activeSelf;
        go.SetActive(false);                                   // so the material is in place before TreeSway starts
        var sway = go.AddComponent<TreeSway>();
        sway.swayMaterial = swayMaterial;
        sway.amount = 0f;                                      // a rock does not sway: the component is only for the outline and the shake
        sway.angle = 0f;
        sway.randomPhase = false;
        go.SetActive(wasActive);
    }

    static int Hash(int a, int b, int c)
    {
        unchecked
        {
            int h = 41; h = h * 31 + a; h = h * 31 + b; h = h * 31 + c;
            h ^= h << 13; h ^= h >> 17; h ^= h << 5;
            return h;
        }
    }

    public static void RemoveRocksOf(RockSlot slot)
    {
        for (int i = slot.transform.childCount - 1; i >= 0; i--)
        {
            var c = slot.transform.GetChild(i);
            if (c.GetComponent<SpawnedRockMarker>() == null) continue;
            if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
        }
    }

    [ContextMenu("Populate all slots in scene")]
    public void PopulateScene() { Populate(null, 12345); }

    [ContextMenu("Clear rocks")]
    public void ClearScene()
    {
        foreach (var s in FindObjectsByType<RockSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) RemoveRocksOf(s);
    }
}
