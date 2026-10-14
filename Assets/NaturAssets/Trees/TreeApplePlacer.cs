using System.Collections;
using UnityEngine;

/// <summary>
/// Hangs apples in some of the oak trees. Lives on the same object as WorldBuilder / TreeSpawner. One frame after the world is made it goes
/// through all the trees the TreeSpawner made, and the ones whose picture name contains Name Contains (Oak) get, by chance, 2-5 apples.
/// The same world seed always gives the same apple trees. The apples are TreeApples (children of the tree); AppleHarvester on the player
/// makes them fall and gives them to the player on the first hit with the axe.
/// Right-click the script header: "Place apples now" / "Clear apples".
/// </summary>
public class TreeApplePlacer : MonoBehaviour
{
    [Header("Hvilke trær")]
    [Tooltip("Treets bilde må ha dette i navnet (Oak tar også OldOak).")]
    public string nameContains = "Oak";
    [Tooltip("Bilder med disse navnene (kommaseparert) får aldri epler. Høstløv + røde epler ser rart ut.")]
    public string excludeNames = "Autumn";
    [Tooltip("Sjansen for at et eiketre har epler (0.4 = hvert annet-tredje tre).")]
    [Range(0f, 1f)] public float treeChance = 0.4f;

    [Header("Epler")]
    public Sprite appleSprite;
    [Min(1)] public int minApples = 2;
    [Min(1)] public int maxApples = 5;
    [Tooltip("Størrelse på eplene (2 = dobbelt så stor som tegnet).")]
    [Min(0.2f)] public float appleScale = 2f;

    [Header("Kjøring")]
    public bool placeOnStart = true;
    public bool log = true;

    void Start()
    {
        if (placeOnStart && Application.isPlaying) StartCoroutine(AfterWorld());
    }

    IEnumerator AfterWorld()
    {
        yield return null;
        PlaceNow();
    }

    bool Matches(string spriteName)
    {
        if (string.IsNullOrEmpty(nameContains) || spriteName.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) < 0) return false;
        if (!string.IsNullOrEmpty(excludeNames))
            foreach (var part in excludeNames.Split(','))
            {
                string p = part.Trim();
                if (p.Length > 0 && spriteName.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            }
        return true;
    }

    [ContextMenu("Place apples now")]
    public int PlaceNow()
    {
        if (appleSprite == null) { Debug.LogWarning("[TreeApplePlacer] Ingen eple-bilde er satt (Apple Sprite). Kjør DeepVain > Trees > 10 - Set up apples.", this); return 0; }
        var wb = FindFirstObjectByType<WorldBuilder>();
        int seed = wb != null ? wb.lastSeedUsed : 12345;
        int trees = 0, apples = 0, oaks = 0;
        foreach (var marker in FindObjectsByType<SpawnedTreeMarker>(FindObjectsSortMode.None))
        {
            trees++;
            var sr = marker.GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null || !Matches(sr.sprite.name)) continue;
            oaks++;
            var old = marker.GetComponent<TreeApples>();
            if (old != null) old.Clear();
            Vector3 p = marker.transform.position;
            var rng = new System.Random(Hash(seed ^ 0x3f1a9c5, Mathf.RoundToInt(p.x * 10f), Mathf.RoundToInt(p.y * 10f)));
            if (rng.NextDouble() > treeChance) continue;
            int count = rng.Next(Mathf.Min(minApples, maxApples), Mathf.Max(minApples, maxApples) + 1);
            var ta = old != null ? old : marker.gameObject.AddComponent<TreeApples>();
            apples += ta.Setup(appleSprite, count, rng, appleScale, 1);
        }
        if (log) Debug.Log("[TreeApplePlacer] " + apples + " epler i " + oaks + " eiketrær (av " + trees + " trær).", this);
        return apples;
    }

    [ContextMenu("Clear apples")]
    public void ClearAll()
    {
        foreach (var ta in FindObjectsByType<TreeApples>(FindObjectsSortMode.None)) ta.Clear();
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
}
