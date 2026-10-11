using System;
using UnityEngine;

/// <summary>
/// En liste med props å velge fra (med vekter): blomster, planter, sopp ... Hver rad er en PREFAB (de animerte propsene fra
/// DeepVain > Props > Build animated props). Lag en med: Project > Create > DeepVain > Prop Set,
/// eller med menyen DeepVain > Props > Make Prop Set from animated props.
/// </summary>
[CreateAssetMenu(menuName = "DeepVain/Prop Set", fileName = "PropSet")]
public class PropSet : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Navn (bare for deg).")] public string label;
        [Tooltip("Prefaben som settes ut (f.eks. Flower_Daisy.prefab).")] public GameObject prefab;
        [Tooltip("Hvor ofte (1 = vanlig, 3 = tre ganger så ofte, 0 = aldri).")] [Min(0f)] public float weight = 1f;
        public bool IsValid { get { return prefab != null; } }
    }

    public Entry[] entries = new Entry[0];

    /// <summary>Velger en rad etter vekt. avoid = rader som hoppes over hvis mulig (de siste valgene).</summary>
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
