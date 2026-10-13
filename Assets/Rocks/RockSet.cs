using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listen med steiner som RockSpawner kan velge fra. Hver oppføring er ett bilde (Rock_Stone_... eller Rock_Coal_...).
/// Oppføringer med Coal på gir kull i tillegg til stein. Lages av menyen DeepVain > Rocks > 1 - Set up rocks and items.
/// </summary>
[CreateAssetMenu(menuName = "DeepVain/Rock Set", fileName = "RockSet")]
public class RockSet : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string label;
        public Sprite sprite;
        [Tooltip("På = en stein med kull i seg (gir stein og kull). Av = vanlig stein (bare stein).")]
        public bool coal;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Hvor mye den tåler. Hvert slag tar hakkas Tool Power (Starter Pickaxe = 5): 15 = 3 slag, 20 = 4 slag.")]
        public float health = 15f;
        [Tooltip("Fargen på småbitene som spretter når den knuses.")]
        public Color chipColor = new Color(0.62f, 0.58f, 0.55f, 1f);
    }

    public Entry[] entries;

    public bool HasCoal { get { if (entries != null) foreach (var e in entries) if (e != null && e.coal && e.sprite != null) return true; return false; } }

    /// <summary>Velger en tilfeldig oppføring (vektet). wantCoal bestemmer om det skal være en kullstein. Unngår de siste hvis mulig.</summary>
    public Entry Pick(System.Random rng, bool wantCoal, List<Entry> avoid)
    {
        if (entries == null) return null;
        for (int pass = 0; pass < 2; pass++)
        {
            float sum = 0f;
            foreach (var e in entries)
                if (Ok(e, wantCoal) && (pass == 1 || avoid == null || !avoid.Contains(e))) sum += e.weight;
            if (sum <= 0f) continue;
            double r = rng.NextDouble() * sum;
            foreach (var e in entries)
            {
                if (!Ok(e, wantCoal) || (pass == 0 && avoid != null && avoid.Contains(e))) continue;
                r -= e.weight;
                if (r <= 0.0) return e;
            }
        }
        return null;
    }

    static bool Ok(Entry e, bool wantCoal) { return e != null && e.sprite != null && e.weight > 0f && e.coal == wantCoal; }
}
