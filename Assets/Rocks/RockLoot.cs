using System.Collections;
using UnityEngine;
using DeepVain.Items;
using DeepVain.Levels;

/// <summary>
/// Gir spilleren stein (og kull) i sekken når en stein er knust, rett i sekken med melding i hjørnet, som med trær og kister:
///   vanlig stein: 2-5 Stone      stein med kull i seg: 1-3 Stone + 1-3 Coal
/// Tallene ligger på selve steinen (Mineable), satt av RockSpawner. Legges på spilleren. Settes opp av menyen
/// DeepVain > Rocks > 2 - Set up mining on the player.
/// </summary>
[DisallowMultipleComponent]
public class RockLoot : MonoBehaviour
{
    public ItemData stone;
    public ItemData coal;
    [Tooltip("Sekunder fra siste slag til tingene havner i sekken (røyken kommer).")]
    public float delay = 0.2f;

    RockMiner miner;
    Inventory inventory;

    void Awake()
    {
        miner = GetComponent<RockMiner>();
        inventory = GetComponentInParent<Inventory>();
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
    }

    void OnEnable()
    {
        if (miner == null) miner = GetComponent<RockMiner>();
        if (miner != null) miner.RockBroken += OnBroken;
    }

    void OnDisable() { if (miner != null) miner.RockBroken -= OnBroken; }

    void OnBroken(GameObject rock)
    {
        if (rock == null) return;
        var m = rock.GetComponent<Mineable>();
        if (m == null) return;
        int stoneN = m.RollStone() + Mathf.RoundToInt(PlayerLevels.Bonus("stone_bonus"));       // skill tree: Extra Stone ...
        int coalN = m.coal ? m.RollCoal() + Mathf.RoundToInt(PlayerLevels.Bonus("coal_bonus")) : 0;
        StartCoroutine(Give(stoneN, coalN));
    }

    IEnumerator Give(int stoneN, int coalN)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (inventory == null) { Debug.LogWarning("[RockLoot] Fant ikke Inventory.", this); yield break; }
        Add(stone, stoneN);
        if (coalN > 0) Add(coal, coalN);
    }

    void Add(ItemData item, int amount)
    {
        if (item == null || amount <= 0) { if (item == null) Debug.LogWarning("[RockLoot] Mangler item (Stone/Coal) på RockLoot.", this); return; }
        int left = inventory.Add(item, amount);
        int got = amount - left;
        if (got > 0) LootToastUI.Show(item, got);
        if (left > 0) Debug.Log("[RockLoot] Sekken er full: " + left + " " + item.displayName + " fikk ikke plass.", this);
    }
}
