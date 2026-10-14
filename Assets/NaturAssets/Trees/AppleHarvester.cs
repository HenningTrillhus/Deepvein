using UnityEngine;
using DeepVain.Items;

/// <summary>
/// Apples fall and the player gets them on the FIRST hit with the axe in a tree that has apples (TreeApples).
/// Legges på spilleren (samme objekt som TreeChopper). Settes opp av menyen DeepVain > Trees > 10 - Set up apples.
///   - Alle eplene i treet faller samtidig; hvert eple er "Per Apple" Apple i sekken (meldingen i hjørnet: "+3 Apple").
///   - Treet har ingen epler igjen etter det. Hogges treet videre, skjer det som før (logger osv.).
/// </summary>
[DisallowMultipleComponent]
public class AppleHarvester : MonoBehaviour
{
    [Tooltip("Hva eplene gir (items-filen Apple).")]
    public ItemData item;
    [Min(1)] public int perApple = 1;

    /// <summary>Antall epler som falt (til XP o.l.).</summary>
    public static event System.Action<int> Harvested;

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
        if (chopper != null) chopper.TreeHit += OnHit;
    }

    void OnDisable()
    {
        if (chopper != null) chopper.TreeHit -= OnHit;
    }

    void OnHit(GameObject tree, float hpLeft)
    {
        if (tree == null) return;
        var ta = tree.GetComponent<TreeApples>();
        if (ta == null || !ta.HasApples) return;
        int n = ta.Drop();
        if (n <= 0) return;
        if (Harvested != null) Harvested(n);
        if (item == null || inventory == null) { Debug.LogWarning("[AppleHarvester] Mangler item eller Inventory.", this); return; }
        int amount = n * Mathf.Max(1, perApple);
        int left = inventory.Add(item, amount);
        int got = amount - left;
        if (got > 0) LootToastUI.Show(item, got);
        if (left > 0) Debug.Log("[AppleHarvester] Sekken er full: " + left + " " + item.displayName + " fikk ikke plass.", this);
    }
}
