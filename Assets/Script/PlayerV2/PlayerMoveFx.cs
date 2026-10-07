using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepVain.Player;

/// <summary>
/// Dust on the ground and the wind trail of the roll. Both are their own objects (children / spawned objects), nothing is drawn into the player sprites.
///  - Roll:   a wind trail (11 pictures, one per roll frame, follows the player) + dust puffs when the roll starts, under the rolling body and when it ends.
///  - Sprint: a small dust puff behind the feet now and then while you run.
/// Put it on the same object as PlayerMovement. Made by DeepVain > Player FX > 2 - Add to player.
/// </summary>
[DisallowMultipleComponent]
public class PlayerMoveFx : MonoBehaviour
{
    [Header("Bilder (settes av byggeren)")]
    public Sprite[] windFrames;          // 11 pictures, same timing as the Roll animation
    public Sprite[] dustFrames;          // 6 pictures of one puff

    [Header("Skru av og på")]
    public bool rollWind = true;
    public bool rollDust = true;
    public bool sprintDust = true;

    [Header("Støv")]
    [Tooltip("Hvor ofte det kommer et støvpust når du løper (sekunder).")]
    public float sprintDustInterval = 0.16f;
    public float dustFrameTime = 0.07f;
    public Color dustTint = Color.white;
    [Tooltip("Hvor stort støvet blir (1 = som tegnet).")]
    public float dustSize = 1.2f;

    [Header("Størrelse (hvis spilleren ikke finnes som lag-spiller)")]
    [Tooltip("Units per tegnede piksel. Finnes PlayerLayerSync leses den automatisk.")]
    public float fallbackUnitsPerPixel = 0.025f;

    // the roll animation: duration of every picture in ms (from the sprite script)
    static readonly int[] RollMs = { 55, 50, 45, 45, 45, 45, 45, 45, 50, 60, 70 };

    PlayerMovement movement;
    PlayerLayerSync sync;
    Collider2D col;
    Rigidbody2D rb;
    SpriteRenderer windRenderer;
    Coroutine windRoutine, rollDustRoutine;
    float sprintTimer;
    readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        sync = GetComponentInChildren<PlayerLayerSync>();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        // the wind is a child of the player's picture object, so it flips and scales exactly like the sprites
        var go = new GameObject("RollWind");
        go.transform.SetParent(sync != null ? sync.transform : transform, false);
        windRenderer = go.AddComponent<SpriteRenderer>();
        windRenderer.enabled = false;
        ApplySorting(windRenderer, -1);
    }

    void OnEnable()
    {
        if (movement == null) return;
        movement.RollStarted += OnRollStarted;
        movement.RollEnded += OnRollEnded;
    }

    void OnDisable()
    {
        if (movement != null) { movement.RollStarted -= OnRollStarted; movement.RollEnded -= OnRollEnded; }
        if (windRenderer != null) windRenderer.enabled = false;
    }

    // ------------------------------------------------------------------ helpers
    float UnitsPerPixel()
    {
        if (sync != null && sync.slots != null)
            foreach (var s in sync.slots)
                if (s != null && s.renderer != null && s.renderer.sprite != null)
                    return Mathf.Abs(s.renderer.transform.lossyScale.y) / s.renderer.sprite.pixelsPerUnit;
        return fallbackUnitsPerPixel;
    }

    void ApplySorting(SpriteRenderer sr, int offset)
    {
        string layer = sync != null ? sync.sortingLayer : "Default";
        int order = (sync != null ? sync.baseSortingOrder : 10) + offset;
        sr.sortingLayerName = layer; sr.sortingOrder = order;
    }

    Vector2 FeetPosition()
    {
        if (col != null) return new Vector2(col.bounds.center.x, col.bounds.min.y);
        return transform.position;
    }

    // ------------------------------------------------------------------ roll
    void OnRollStarted(int dir)
    {
        if (rollWind && windFrames != null && windFrames.Length > 0)
        {
            if (windRoutine != null) StopCoroutine(windRoutine);
            windRoutine = StartCoroutine(WindRoutine(dir));
        }
        if (rollDust && dustFrames != null && dustFrames.Length > 0)
        {
            if (rollDustRoutine != null) StopCoroutine(rollDustRoutine);
            rollDustRoutine = StartCoroutine(RollDustRoutine(dir));
        }
    }

    void OnRollEnded()
    {
        if (windRoutine != null) { StopCoroutine(windRoutine); windRoutine = null; }
        if (windRenderer != null) windRenderer.enabled = false;
        if (rollDust && dustFrames != null && dustFrames.Length > 0)
        {
            Vector2 f = FeetPosition(); float u = UnitsPerPixel();
            for (int i = 0; i < 5; i++)                                              // landing: a cloud that rolls on in the direction of the roll
                Puff(f + new Vector2((i - 2) * 3f * u, 0f), 0.9f + 0.5f * Random.value, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.1f, 0.5f)));
        }
    }

    IEnumerator WindRoutine(int dir)
    {
        // the pictures have their pivot where the player's feet are (set by the builder), so no offset is needed
        windRenderer.transform.localPosition = Vector3.zero;
        windRenderer.transform.localScale = Vector3.one;
        windRenderer.enabled = true;
        for (int i = 0; i < RollMs.Length && i < windFrames.Length; i++)
        {
            windRenderer.sprite = windFrames[i];
            yield return new WaitForSeconds(RollMs[i] / 1000f);
        }
        windRenderer.enabled = false;
        windRoutine = null;
    }

    IEnumerator RollDustRoutine(int dir)
    {
        float u = UnitsPerPixel();
        Vector2 f = FeetPosition();
        for (int i = 0; i < 4; i++)                                                   // take-off
            Puff(f + new Vector2((-dir) * (2 + i * 4) * u, 0f), 0.8f + 0.5f * Random.value, new Vector2(-dir * Random.Range(0.4f, 1.2f), Random.Range(0.2f, 0.6f)));
        float t = 0f;
        while (movement != null && movement.IsRolling)
        {
            yield return new WaitForSeconds(0.06f);
            t += 0.06f;
            if (t > 0.1f && t < 0.45f)
                Puff(FeetPosition() + new Vector2(-dir * 4f * u, 0f), 0.5f + 0.4f * Random.value, new Vector2(-dir * Random.Range(0.5f, 1.5f), Random.Range(0.1f, 0.4f)));
        }
        rollDustRoutine = null;
    }

    // ------------------------------------------------------------------ sprint
    void Update()
    {
        if (!sprintDust || dustFrames == null || dustFrames.Length == 0 || movement == null) return;
        if (movement.IsSprinting && Mathf.Abs(rb != null ? rb.linearVelocity.x : 1f) > 4f && GroundedGuess())
        {
            sprintTimer -= Time.deltaTime;
            if (sprintTimer <= 0f)
            {
                sprintTimer = sprintDustInterval;
                int dir = movement.Facing;
                float u = UnitsPerPixel();
                Puff(FeetPosition() + new Vector2(-dir * 7f * u, 0f), 0.45f + 0.25f * Random.value, new Vector2(-dir * Random.Range(0.3f, 0.9f), Random.Range(0.1f, 0.3f)));
            }
        }
        else sprintTimer = 0.05f;
    }

    bool GroundedGuess() { return rb == null || Mathf.Abs(rb.linearVelocity.y) < 0.2f; }

    void OnDestroy()
    {
        foreach (var p in pool) if (p != null) Destroy(p.gameObject);
    }

    // ------------------------------------------------------------------ one dust puff (pooled)
    void Puff(Vector2 pos, float size, Vector2 drift)
    {
        SpriteRenderer sr = null;
        foreach (var p in pool) if (p != null && !p.gameObject.activeSelf) { sr = p; break; }
        if (sr == null)
        {
            var go = new GameObject("DustPuff");
            sr = go.AddComponent<SpriteRenderer>();
            pool.Add(sr);
        }
        sr.gameObject.SetActive(true);
        ApplySorting(sr, -2);
        sr.color = dustTint;
        sr.flipX = Random.value < 0.5f;
        float u = UnitsPerPixel() * size * dustSize;
        sr.transform.SetParent(null);
        sr.transform.position = new Vector3(pos.x, pos.y, 0f);
        sr.transform.localScale = new Vector3(u, u, 1f);
        StartCoroutine(PuffRoutine(sr, drift));
    }

    IEnumerator PuffRoutine(SpriteRenderer sr, Vector2 drift)
    {
        for (int i = 0; i < dustFrames.Length; i++)
        {
            sr.sprite = dustFrames[i];
            float t = 0f;
            while (t < dustFrameTime)
            {
                t += Time.deltaTime;
                sr.transform.position += (Vector3)(drift * Time.deltaTime);
                yield return null;
            }
        }
        sr.gameObject.SetActive(false);
    }
}
