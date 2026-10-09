using System;
using UnityEngine;

/// <summary>
/// A list of trees to pick from (with weights). Make one with: Project > Create > DeepVain > Tree Set,
/// or select the tree pictures and use DeepVain > Trees > 3 - Make Tree Set from selected pictures.
/// </summary>
[CreateAssetMenu(menuName = "DeepVain/Tree Set", fileName = "TreeSet")]
public class TreeSet : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Navn (bare for deg).")] public string label;
        [Tooltip("Bildet av treet. Brukes hvis Prefab er tomt.")] public Sprite sprite;
        [Tooltip("Valgfritt: en egen prefab for akkurat dette treet (overstyrer bildet og malen).")] public GameObject prefab;
        [Tooltip("Hvor ofte (1 = vanlig, 3 = tre ganger så ofte, 0 = aldri).")] [Min(0f)] public float weight = 1f;
        public bool IsValid { get { return sprite != null || prefab != null; } }
    }

    public Entry[] entries = new Entry[0];

    /// <summary>Picks one entry by weight. avoid = entries that should be skipped if possible (the last few picks).</summary>
    public Entry Pick(System.Random rng, System.Collections.Generic.IList<Entry> avoid = null)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            Entry e = PickOnce(rng);
            if (e == null) return null;
            if (avoid == null || !avoid.Contains(e) || attempt == 5 || entries.Length <= (avoid != null ? avoid.Count : 0)) return e;
        }
        return null;
    }

    Entry PickOnce(System.Random rng)
    {
        float total = 0f;
        foreach (var e in entries) if (e != null && e.IsValid) total += e.weight;
        if (total <= 0f) return null;
        float r = (float)(rng.NextDouble() * total);
        foreach (var e in entries)
        {
            if (e == null || !e.IsValid) continue;
            r -= e.weight;
            if (r <= 0f) return e;
        }
        return null;
    }
}
