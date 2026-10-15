using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Setter ut en tilfeldig prop (blomst, plante, sopp) på hver PropSlot i scenen. Propsene er bare pynt: de har ingen
/// collider, kan ikke pekes på, slås eller hugges, og ingenting reagerer på dem.
/// Legges på et objekt (for eksempel det samme som WorldBuilder). Med "Populate On Start" venter den én frame, så
/// WorldBuilder har laget chunkene først, og bruker samme verdens-seed: samme verden gir alltid de samme propsene.
/// Høyreklikk på script-overskriften: "Populate all slots in scene" / "Clear props" for å se det uten å spille.
/// </summary>
public class PropSpawner : MonoBehaviour
{
    [Header("Props")]
    [Tooltip("Standardlisten. En PropSlot kan ha sin egen liste.")]
    public PropSet propSet;

    [Header("Plassering")]
    [Tooltip("Flytter propen opp/ned i forhold til plassen. Gress-tilene er litt tomme øverst, så propen står litt under flaten collideren har.")]
    public float yOffset = -0.15f;
    [Tooltip("Ganger størrelsen prefaben har fra før (1 = som prefaben).")]
    [Min(0.05f)] public float scaleMultiplier = 1f;
    [Tooltip("Tilfeldig variasjon i størrelse, +/- (0.1 = 10 %). 0 = like store.")]
    [Range(0f, 0.5f)] public float scaleVariation = 0f;

    [Header("Variasjon")]
    [Tooltip("Unngå at samme prop kommer rett etter hverandre: hvor mange av de siste som hoppes over hvis mulig.")]
    [Min(0)] public int avoidRepeats = 1;

    [Header("Plukkbare (bær, urter, sopp)")]
    [Tooltip("På: plukkbare skal alltid være frie. Andre props som overlapper en plukkbar fjernes, og to plukkbare oppå hverandre blir til én. En sopp som ikke er plukkbar (mangler Forageable) settes ikke ut.")]
    public bool clearOverlaps = true;
    [Tooltip("Hvor mye bildene må overlappe før noe fjernes (1 = de berører hverandre, 0.5 = halvparten).")]
    [Range(0.2f, 1f)] public float overlapFactor = 0.8f;

    [Header("Kjøring")]
    [Tooltip("Sett ut props automatisk når spillet starter (etter at WorldBuilder har laget verden).")]
    public bool populateOnStart = true;
    public bool log = true;

    void Start()
    {
        if (populateOnStart && Application.isPlaying) StartCoroutine(PopulateAfterWorld());
    }

    IEnumerator PopulateAfterWorld()
    {
        yield return null;                                       // WorldBuilder.Start har kjørt (og laget chunkene)
        var wb = FindFirstObjectByType<WorldBuilder>();
        int seed = wb != null ? wb.lastSeedUsed : 12345;
        Populate(null, seed);
    }

    static List<PropSlot> AllSlots()
    {
        return new List<PropSlot>(FindObjectsByType<PropSlot>(FindObjectsSortMode.None));
    }

    /// <summary>Lager props på de gitte plassene (alle plasser i scenen hvis null). Samme seed gir alltid de samme propsene.</summary>
    public int Populate(IList<PropSlot> slotsIn, int worldSeed)
    {
        var slots = slotsIn != null ? new List<PropSlot>(slotsIn) : AllSlots();
        slots.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        var recent = new List<PropSet.Entry>();
        int made = 0, skipped = 0, missing = 0;
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            RemovePropsOf(slot);
            var set = slot.propSet != null ? slot.propSet : propSet;
            if (set == null) { missing++; continue; }

            Vector3 p = slot.transform.position;
            var rng = new System.Random(Hash(worldSeed, Mathf.RoundToInt(p.x * 10f), Mathf.RoundToInt(p.y * 10f)));
            if (rng.NextDouble() > slot.chance) { skipped++; continue; }

            var entry = set.Pick(rng, recent);
            if (entry == null) { missing++; continue; }
            if (clearOverlaps && entry.prefab != null && entry.prefab.name.StartsWith("Mushroom_") && entry.prefab.GetComponent<Forageable>() == null)
            {
                if (!warnedPlainMushroom) { warnedPlainMushroom = true; Debug.LogWarning("[PropSpawner] " + entry.prefab.name + " er ikke plukkbar (mangler Forageable), så den settes ikke ut. Kjør DeepVain > Props > Build forageables.", this); }
                missing++; continue;
            }
            recent.Add(entry); while (recent.Count > avoidRepeats) recent.RemoveAt(0);

            Spawn(slot, entry, rng);
            made++;
        }
        int removed = clearOverlaps ? RemoveOverlaps(slots) : 0;
        made -= removed;
        if (log) Debug.Log("[PropSpawner] " + made + " props av " + slots.Count + " plasser (" + skipped + " ble ikke med på sjansen" + (missing > 0 ? ", " + missing + " uten prop-liste" : "")
                           + (removed > 0 ? ", " + removed + " fjernet fordi de overlappet en plukkbar" : "") + ").");
        return made;
    }

    void Spawn(PropSlot slot, PropSet.Entry entry, System.Random rng)
    {
        Vector3 pos = slot.transform.position + new Vector3(0f, yOffset, 0f);
        if (slot.jitterX > 0f) pos.x += (float)(rng.NextDouble() * 2.0 - 1.0) * slot.jitterX;

        var go = Instantiate(entry.prefab, pos, Quaternion.identity, slot.transform);
        go.name = "Prop_" + (string.IsNullOrEmpty(entry.label) ? entry.prefab.name : entry.label);
        go.AddComponent<SpawnedPropMarker>();

        float k = scaleMultiplier * (scaleVariation > 0f ? 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * scaleVariation : 1f);
        go.transform.localScale = go.transform.localScale * k;

        // bare pynt: ingenting å treffe, peke på eller gå inn i
        foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }

        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && slot.randomFlip) sr.flipX = rng.Next(2) == 0;
#if UNITY_EDITOR
        if (!Application.isPlaying) go.hideFlags = HideFlags.DontSaveInEditor;       // en forhåndsvisning i editor skal ikke lagres i scenen
#endif
    }

    bool warnedPlainMushroom;

    static float WidthOf(GameObject go)
    {
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        return sr != null ? Mathf.Max(0.2f, sr.bounds.size.x) : 0.8f;
    }

    bool Overlaps(GameObject a, GameObject b)
    {
        Vector3 pa = a.transform.position, pb = b.transform.position;
        if (Mathf.Abs(pa.y - pb.y) > 1f) return false;
        return Mathf.Abs(pa.x - pb.x) < 0.5f * (WidthOf(a) + WidthOf(b)) * overlapFactor;
    }

    /// <summary>Plukkbare (Forageable) skal ikke ligge under noe annet: props som overlapper dem fjernes, og dobbelt-plukkbare blir til én.</summary>
    int RemoveOverlaps(List<PropSlot> slots)
    {
        var props = new List<GameObject>();
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            for (int i = 0; i < slot.transform.childCount; i++)
            {
                var c = slot.transform.GetChild(i);
                if (c.GetComponent<SpawnedPropMarker>() != null) props.Add(c.gameObject);
            }
        }
        props.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        var forage = props.FindAll(p => p.GetComponent<Forageable>() != null);
        var kill = new HashSet<GameObject>();
        for (int i = 0; i < forage.Count; i++)
        {
            if (kill.Contains(forage[i])) continue;
            for (int j = i + 1; j < forage.Count; j++)
                if (!kill.Contains(forage[j]) && Overlaps(forage[i], forage[j])) kill.Add(forage[j]);        // the left one stays
        }
        foreach (var p in props)
        {
            if (p.GetComponent<Forageable>() != null || kill.Contains(p)) continue;
            foreach (var f in forage)
                if (!kill.Contains(f) && Overlaps(p, f)) { kill.Add(p); break; }
        }
        foreach (var go in kill)
        {
            if (go == null) continue;
            go.SetActive(false);                                  // out of Forageable.All and out of the picture right now
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
        return kill.Count;
    }

    static int Hash(int a, int b, int c)
    {
        unchecked
        {
            int h = 29; h = h * 31 + a; h = h * 31 + b; h = h * 31 + c;
            h ^= h << 13; h ^= h >> 17; h ^= h << 5;
            return h;
        }
    }

    public static void RemovePropsOf(PropSlot slot)
    {
        for (int i = slot.transform.childCount - 1; i >= 0; i--)
        {
            var c = slot.transform.GetChild(i);
            if (c.GetComponent<SpawnedPropMarker>() == null) continue;
            if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
        }
    }

    [ContextMenu("Populate all slots in scene")]
    public void PopulateScene() { Populate(null, 12345); }

    [ContextMenu("Clear props")]
    public void ClearScene()
    {
        foreach (var s in FindObjectsByType<PropSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) RemovePropsOf(s);
    }
}
