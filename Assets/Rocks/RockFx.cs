using System.Collections;
using UnityEngine;

/// <summary>
/// Det som skjer med en stein når den hakkes:
///   - hvert slag: steinen skjelver (TreeSway.Shake) og to småbiter spretter
///   - siste slag: kort hvit blink og sammenpress, så forsvinner steinen i en røyksky (10 bilder) og småbiter flyr
/// Legges på spilleren (samme objekt som RockMiner). Settes opp av menyen DeepVain > Rocks > 2 - Set up mining on the player.
/// </summary>
[DisallowMultipleComponent]
public class RockFx : MonoBehaviour
{
    [Header("Slag")]
    public float shakeStrength = 1.1f;
    public float shakeDuration = 0.35f;
    [Tooltip("Småbiter per slag / når den knuses.")]
    public int chipsPerHit = 2;
    public int chipsOnBreak = 7;

    [Header("Knusing")]
    [Tooltip("Hvor lenge den blinker hvit og presses sammen før den blir til røyk.")]
    public float flashTime = 0.12f;
    [Tooltip("Hvor mye den presses sammen på høyden (1 = ikke).")]
    [Range(0.5f, 1f)] public float squash = 0.85f;

    [Header("Bilder")]
    public Sprite[] smokeFrames;
    public float smokeFrameTime = 0.07f;
    public Sprite[] chipSprites;
    [Tooltip("Tomt = finner Sprite-Unlit-Default selv, ellers vanlig sprite-materiale.")]
    public Material fxMaterial;

    [Header("Småbiter")]
    public float chipSpeed = 3.2f;
    public float chipGravity = 13f;
    public float chipScale = 2.5f;

    RockMiner miner;

    void Awake() { miner = GetComponent<RockMiner>(); }

    void OnEnable()
    {
        if (miner == null) miner = GetComponent<RockMiner>();
        if (miner != null) { miner.RockHit += OnHit; miner.RockBroken += OnBroken; }
    }

    void OnDisable()
    {
        if (miner != null) { miner.RockHit -= OnHit; miner.RockBroken -= OnBroken; }
    }

    Material Mat()
    {
        if (fxMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null) fxMaterial = new Material(shader) { name = "RockFx (auto)" };
        }
        return fxMaterial;
    }

    void OnHit(GameObject rock, float hpLeft)
    {
        if (rock == null) return;
        var sway = rock.GetComponent<TreeSway>();
        if (sway != null) sway.Shake(shakeStrength, shakeDuration);
        else StartCoroutine(Jitter(rock.transform));
        var m = rock.GetComponent<Mineable>();
        SpawnChips(rock.transform.position + Vector3.up * 0.25f * rock.transform.localScale.y, m != null ? m.chipColor : Color.gray, chipsPerHit, 0.6f, rock);
    }

    IEnumerator Jitter(Transform t)
    {
        Vector3 p0 = t.position; float el = 0f;
        while (el < shakeDuration && t != null)
        {
            el += Time.deltaTime;
            t.position = p0 + new Vector3(Mathf.Round(Mathf.Sin(el * 70f) * (1f - el / shakeDuration) * 2f) * 0.04f, 0f, 0f);
            yield return null;
        }
        if (t != null) t.position = p0;
    }

    void OnBroken(GameObject rock)
    {
        if (rock != null) StartCoroutine(Break(rock));
    }

    IEnumerator Break(GameObject rock)
    {
        TreeHover.Exclude(rock);
        var sr = rock.GetComponentInChildren<SpriteRenderer>();
        var sway = rock.GetComponent<TreeSway>();
        var m = rock.GetComponent<Mineable>();
        Color chip = m != null ? m.chipColor : Color.gray;
        foreach (var c in rock.GetComponentsInChildren<Collider2D>()) c.enabled = false;

        // white flash + squash
        if (sway != null) { sway.highlightBrighten = 0.9f; sway.SetHighlighted(true); }
        Vector3 s0 = rock.transform.localScale;
        float el = 0f;
        while (el < flashTime && rock != null)
        {
            el += Time.deltaTime;
            float k = Mathf.Clamp01(el / flashTime);
            rock.transform.localScale = new Vector3(s0.x * (1f + (1f - squash) * 0.5f * k), s0.y * Mathf.Lerp(1f, squash, k), s0.z);
            yield return null;
        }
        if (rock == null) yield break;

        Vector3 pos = rock.transform.position;
        int layerId = sr != null ? sr.sortingLayerID : 0;
        int order = sr != null ? sr.sortingOrder : 0;
        float scale = s0.x;

        // smoke
        if (smokeFrames != null && smokeFrames.Length > 0)
        {
            var go = new GameObject("RockSmoke");
            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var ssr = go.AddComponent<SpriteRenderer>();
            ssr.sprite = smokeFrames[0];
            ssr.sortingLayerID = layerId; ssr.sortingOrder = order + 6;
            var mat = Mat(); if (mat != null) ssr.sharedMaterial = mat;
            var fb = go.AddComponent<SpriteFlipbook>();
            fb.frames = smokeFrames; fb.frameTime = smokeFrameTime;
        }
        SpawnChips(pos + Vector3.up * 0.4f * scale, chip, chipsOnBreak, 1f, rock);
        Destroy(rock);
    }

    void SpawnChips(Vector3 at, Color color, int n, float power, GameObject near)
    {
        if (chipSprites == null || chipSprites.Length == 0) return;
        var sr = near != null ? near.GetComponentInChildren<SpriteRenderer>() : null;
        for (int i = 0; i < n; i++)
            StartCoroutine(Chip(at, color, power, sr != null ? sr.sortingLayerID : 0, sr != null ? sr.sortingOrder + 7 : 5));
    }

    IEnumerator Chip(Vector3 at, Color color, float power, int layerId, int order)
    {
        var go = new GameObject("RockChip");
        go.transform.position = at + new Vector3(Random.Range(-0.25f, 0.25f), 0f, 0f);
        go.transform.localScale = Vector3.one * chipScale * Random.Range(0.8f, 1.2f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = chipSprites[Random.Range(0, chipSprites.Length)];
        sr.color = color;
        sr.sortingLayerID = layerId; sr.sortingOrder = order;
        var mat = Mat(); if (mat != null) sr.sharedMaterial = mat;
        sr.flipX = Random.value < 0.5f;

        Vector2 v = new Vector2(Random.Range(-1f, 1f) * chipSpeed * power, Random.Range(0.6f, 1f) * chipSpeed * 1.4f * power);
        float groundY = at.y - 0.35f, life = 0f;
        while (go != null && life < 0.9f)
        {
            float dt = Time.deltaTime; life += dt;
            v.y -= chipGravity * dt;
            go.transform.position += new Vector3(v.x * dt, v.y * dt, 0f);
            if (go.transform.position.y < groundY && v.y < 0f) break;
            yield return null;
        }
        // lie for a moment, then blink out
        float t = 0f;
        while (go != null && t < 0.35f)
        {
            t += Time.deltaTime;
            if (t > 0.2f) sr.enabled = ((int)(t * 40f) % 2) == 0;
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
