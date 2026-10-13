using UnityEngine;

/// <summary>
/// Ligger på hver stein som kan hakkes. Hvor mye den tåler, og hva den gir når den knuses.
/// Settes av RockSpawner når steinen lages. RockMiner trekker fra Health, RockLoot deler ut innholdet.
/// </summary>
public class Mineable : MonoBehaviour
{
    public float health = 15f;
    public bool coal;
    [Header("Hva den gir")]
    public int stoneMin = 2, stoneMax = 5;
    public int coalMin = 1, coalMax = 3;
    [Header("Knusing")]
    public Color chipColor = new Color(0.62f, 0.58f, 0.55f, 1f);
    [HideInInspector] public bool broken;

    public int RollStone() { return Random.Range(Mathf.Min(stoneMin, stoneMax), Mathf.Max(stoneMin, stoneMax) + 1); }
    public int RollCoal() { return Random.Range(Mathf.Min(coalMin, coalMax), Mathf.Max(coalMin, coalMax) + 1); }
}
