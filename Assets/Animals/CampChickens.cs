using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts chickens in an area, for instance inside the goblin camp. Place the object where the middle of the camp is and set Width to how wide the
/// area is (the yellow line in the Scene view). The chickens are put on the ground inside the area and wander around within it.
/// Works in the random world too: after Start Delay seconds (the world is built by then) it finds the ground with rays and spawns, and tries again for a few
/// seconds if the ground is not there yet. Right-click the script header: Populate now / Clear.
/// </summary>
public class CampChickens : MonoBehaviour
{
    [Header("Kyllinger")]
    [Tooltip("Typene som kan komme (Hen_White, Hen_Brown, Rooster, Hen_Speckled). Settes av menyen.")]
    public GameObject[] chickenPrefabs = new GameObject[0];
    [Min(0)] public int count = 6;
    [Tooltip("Omtrent en hane for hver så mange høner (0 = ingen haner).")]
    public int hensPerRooster = 5;

    [Header("Området")]
    [Tooltip("Hvor bredt området er (units), midten er dette objektet.")]
    public float width = 12f;
    [Tooltip("Rayen mot bakken starter så høyt over objektet (units).")]
    public float rayStartHeight = 3f;
    public float rayLength = 12f;
    [Tooltip("Hva kyllingene går på. Tomt = laget Ground (eller det spilleren går på).")]
    public LayerMask groundLayers;

    [Header("Når")]
    public bool spawnOnStart = true;
    [Tooltip("Sekunder å vente etter Start før den setter ut (verdenen bygges først).")]
    public float startDelay = 1f;
    public float retryFor = 6f;

    readonly List<GameObject> spawned = new List<GameObject>();

    IEnumerator Start()
    {
        if (!spawnOnStart) yield break;
        yield return new WaitForSeconds(startDelay);
        float until = Time.time + retryFor;
        while (spawned.Count == 0)
        {
            Populate();
            if (spawned.Count > 0 || Time.time > until) break;
            yield return new WaitForSeconds(0.5f);
        }
    }

    LayerMask Mask()
    {
        if (groundLayers.value != 0) return groundLayers;
        var pm = FindFirstObjectByType<PlayerMovement>();
        return pm != null && pm.groundLayer.value != 0 ? pm.groundLayer : (LayerMask)LayerMask.GetMask("Ground");
    }

    [ContextMenu("Populate now")]
    public void Populate()
    {
        Clear();
        if (chickenPrefabs == null || chickenPrefabs.Length == 0) { Debug.LogWarning("[CampChickens] Ingen kylling-prefaber. Kjør DeepVain > Camp > Chickens > 1 - Build chicken prefabs.", this); return; }
        var mask = Mask();
        var hens = new List<GameObject>(); GameObject rooster = null;
        foreach (var p in chickenPrefabs) { if (p == null) continue; if (p.name.ToLower().Contains("rooster")) rooster = p; else hens.Add(p); }
        for (int i = 0; i < count; i++)
        {
            bool wantRooster = rooster != null && hensPerRooster > 0 && i % (hensPerRooster + 1) == hensPerRooster;
            GameObject prefab = wantRooster || hens.Count == 0 ? rooster : hens[Random.Range(0, hens.Count)];
            if (prefab == null) continue;
            float x = transform.position.x + Random.Range(-width / 2f, width / 2f);
            var hit = Physics2D.Raycast(new Vector2(x, transform.position.y + rayStartHeight), Vector2.down, rayLength, mask);
            if (hit.collider == null) continue;
            var go = Instantiate(prefab, new Vector3(x, hit.point.y, 0f), Quaternion.identity, transform);
            go.name = prefab.name;
            var c = go.GetComponent<Chicken>();
            if (c != null) { c.groundLayers = mask; c.SetHome(transform.position.x, width / 2f); }
            spawned.Add(go);
        }
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        foreach (var g in spawned) if (g != null) { if (Application.isPlaying) Destroy(g); else DestroyImmediate(g); }
        spawned.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
        Vector3 p = transform.position;
        Gizmos.DrawLine(p + Vector3.left * width / 2f, p + Vector3.right * width / 2f);
        Gizmos.DrawLine(p + new Vector3(-width / 2f, 0, 0), p + new Vector3(-width / 2f, 1f, 0));
        Gizmos.DrawLine(p + new Vector3(width / 2f, 0, 0), p + new Vector3(width / 2f, 1f, 0));
        Gizmos.DrawWireSphere(p, 0.25f);
    }
}
