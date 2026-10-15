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
    [Tooltip("Et lite støvpust når du hopper av gårde og lander.")]
    public bool jumpDust = true;
    [Tooltip("Små støvpust for hvert skritt når du går og løper.")]
    public bool footstepDust = true;
    [Tooltip("Et lite støvpust når du snur fort.")]
    public bool turnDust = true;
    [Tooltip("En liten strekk når du hopper og klem når du lander (bare litt).")]
    public bool squashAndStretch = true;
    [Tooltip("Et lite ryst i kameraet ved harde landinger (0 = av).")]
    public float landShake = 0.03f;

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
    int lastFrame = -1; string lastAnim = "";
    float lastDirSign; float turnCooldown;
    Vector2 squash = Vector2.one; float squashTimer, squashTime = 0.15f; Vector2 squashFrom = Vector2.one;
    AirSlam airSlam;
    readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        sync = GetComponentInChildren<PlayerLayerSync>();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        airSlam = GetComponent<AirSlam>();

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
        movement.Jumped += OnJumped;
        movement.Landed += OnLanded;
    }

    void OnDisable()
    {
        if (movement != null)
        {
            movement.RollStarted -= OnRollStarted; movement.RollEnded -= OnRollEnded;
            movement.Jumped -= OnJumped; movement.Landed -= OnLanded; movement.VisualSquash = Vector2.one;
        }
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

    // ------------------------------------------------------------------ jump, landing, footsteps, turning (small)
    void OnJumped()
    {
        if (squashAndStretch) Squash(new Vector2(0.9f, 1.14f), 0.14f);           // a little stretch up
        if (!jumpDust || dustFrames == null || dustFrames.Length == 0) return;
        Vector2 f = FeetPosition(); float u = UnitsPerPixel();
        for (int i = -1; i <= 1; i += 2)                                           // two small puffs, one to each side
            Puff(f + new Vector2(i * 4f * u, 0f), 0.42f + 0.15f * Random.value, new Vector2(i * Random.Range(0.4f, 0.9f), Random.Range(0.15f, 0.35f)));
    }

    void OnLanded(float impact)
    {
        if (airSlam != null && airSlam.IsActive) return;                           // the slam has its own, bigger landing
        float k = Mathf.Clamp01((impact - 2.5f) / 12f);                            // 0 = a tiny drop, 1 = a long fall
        if (squashAndStretch) Squash(new Vector2(Mathf.Lerp(1.06f, 1.16f, k), Mathf.Lerp(0.94f, 0.82f, k)), Mathf.Lerp(0.11f, 0.2f, k));
        if (landShake > 0f && impact > 11f) CombatFeel.Shake(landShake * Mathf.Lerp(0.5f, 1f, k), 0.12f);
        if (!jumpDust || dustFrames == null || dustFrames.Length == 0) return;
        Vector2 f = FeetPosition(); float u = UnitsPerPixel();
        int n = k > 0.5f ? 4 : 2;
        for (int i = 0; i < n; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            Puff(f + new Vector2(side * (3f + i * 2f) * u, 0f), 0.4f + 0.45f * k + 0.12f * Random.value,
                 new Vector2(side * Random.Range(0.6f, 1.4f), Random.Range(0.1f, 0.4f)));
        }
    }

    void Squash(Vector2 target, float time)
    {
        squashFrom = target; squash = target; squashTimer = time; squashTime = time;
    }

    void LateUpdate()
    {
        if (movement == null) return;
        if (squashTimer > 0f)
        {
            squashTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(squashTimer / Mathf.Max(0.01f, squashTime));
            float e = t * t * (3f - 2f * t);                                         // smooth back to 1
            squash = Vector2.Lerp(squashFrom, Vector2.one, e);
        }
        else squash = Vector2.one;
        movement.VisualSquash = squashAndStretch ? squash : Vector2.one;
    }

    void FootAndTurnDust()
    {
        if (sync == null || movement == null || dustFrames == null || dustFrames.Length == 0) return;
        // a tiny puff each time a foot comes down: Walk and Run both have 8 pictures, the feet land on pictures 0 and 4
        string anim = sync.CurrentAnimation; int frame = sync.CurrentFrame;
        bool stepAnim = anim == "Walk" || anim == "Run";
        if (footstepDust && stepAnim && movement.IsGrounded && (anim != lastAnim || frame != lastFrame) && frame % 4 == 0 && Random.value < (anim == "Run" ? 0.9f : 0.65f))
        {
            int dir = movement.Facing; float u = UnitsPerPixel();
            Puff(FeetPosition() + new Vector2(-dir * 2f * u, 0f), (anim == "Run" ? 0.34f : 0.26f) + 0.08f * Random.value,
                 new Vector2(-dir * Random.Range(0.1f, 0.45f), Random.Range(0.05f, 0.2f)));
        }
        lastAnim = anim; lastFrame = frame;

        // turning around at speed: a small skid puff behind
        float vx = rb != null ? rb.linearVelocity.x : 0f;
        float s = Mathf.Abs(vx) > 1.5f ? Mathf.Sign(vx) : 0f;
        turnCooldown -= Time.deltaTime;
        float input = Mathf.Sign(movement.HorizontalInput) * (Mathf.Abs(movement.HorizontalInput) > 0.1f ? 1f : 0f);
        if (turnDust && movement.IsGrounded && turnCooldown <= 0f && s != 0f && input != 0f && input != s)
        {
            turnCooldown = 0.35f;
            float u = UnitsPerPixel();
            Puff(FeetPosition() + new Vector2(s * 3f * u, 0f), 0.4f + 0.12f * Random.value, new Vector2(s * Random.Range(0.3f, 0.8f), Random.Range(0.1f, 0.25f)));
        }
    }

    // ------------------------------------------------------------------ sprint
    void Update()
    {
        FootAndTurnDust();
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
