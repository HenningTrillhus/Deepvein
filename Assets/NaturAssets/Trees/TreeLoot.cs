using System.Collections;
using UnityEngine;
using DeepVain.Items;

/// <summary>
/// Gir spilleren tre i sekken når et tre er hugget ned: riktig sort etter hvilket tre det var, og 3-5 stokker (tilfeldig).
/// Meldingen i hjørnet ("+4 Birch Log") kommer på samme måte som når du åpner en kiste (LootToastUI).
/// Legges på spilleren (samme objekt som TreeChopper). Settes opp av menyen DeepVain > Trees > 7 - Set up wood from trees.
/// Hvilket tre som gir hva avgjøres av navnet på treets bilde (Rules), f.eks. "Beech" -> Birch Log.
/// </summary>
[DisallowMultipleComponent]
public class TreeLoot : MonoBehaviour
{
    [System.Serializable]
    public class LogRule
    {
        [Tooltip("Del av navnet på treets bilde, f.eks. Beech, Pine, OldOak. Første regel som passer vinner.")]
        public string nameContains;
        [Tooltip("Hva treet gir (Birch Log, Oak Log, Pine Log).")]
        public ItemData item;
        [Min(1)] public int min = 3;
        [Min(1)] public int max = 5;
    }

    public LogRule[] rules;
    [Tooltip("Sekunder fra siste treff til treet gir fra seg tre. Rundt 1 = når treet treffer bakken.")]
    public float delay = 0.9f;

    TreeChopper chopper;
    Inventory inventory;

    void Awake()
    {
        chopper = GetComponent<TreeChopper>();
        inventory = GetComponentInParent<Inventory>();
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
    }

    void OnEnable()
    {
        if (chopper == null) chopper = GetComponent<TreeChopper>();
        if (chopper != null) chopper.TreeFelled += OnFelled;
    }

    void OnDisable()
    {
        if (chopper != null) chopper.TreeFelled -= OnFelled;
    }

    LogRule Find(string spriteName)
    {
        if (rules == null) return null;
        foreach (var r in rules)
            if (r != null && r.item != null && !string.IsNullOrEmpty(r.nameContains)
                && spriteName.IndexOf(r.nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0) return r;
        return null;
    }

    void OnFelled(GameObject tree)
    {
        if (tree == null) return;
        var sr = tree.GetComponentInChildren<SpriteRenderer>();
        string n = sr != null && sr.sprite != null ? sr.sprite.name : tree.name;
        var rule = Find(n);
        if (rule == null) { Debug.LogWarning("[TreeLoot] Ingen regel for treet \"" + n + "\", så det gir ikke tre. Legg til en regel på TreeLoot.", this); return; }
        int amount = Random.Range(Mathf.Min(rule.min, rule.max), Mathf.Max(rule.min, rule.max) + 1);
        StartCoroutine(Give(rule.item, amount));
    }

    IEnumerator Give(ItemData item, int amount)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (inventory == null) { Debug.LogWarning("[TreeLoot] Fant ikke Inventory.", this); yield break; }
        int left = inventory.Add(item, amount);            // returnerer det som ikke fikk plass
        int got = amount - left;
        if (got > 0) LootToastUI.Show(item, got);          // stripen i hjørnet (hvis scenen har en)
        if (left > 0) Debug.Log("[TreeLoot] Sekken er full: " + left + " " + item.displayName + " fikk ikke plass.", this);
    }
}
