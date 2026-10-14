using System;
using System.Collections;
using UnityEngine;
using DeepVain.Player;

/// <summary>
/// Plays "EnterDoor": the player turns his back to us and walks a few steps into the screen (smaller and a bit higher up in the picture),
/// fades out, then calls onHidden (load the next room / teleport) and gets control back.
/// Works for any door or gate: the door script just starts  StartCoroutine(driver.Play(() => { ... }))  when the player uses it.
///
/// Put this on the same object as PlayerMovement. Needs the EnterDoor animation in the built player (DeepVain > Player Setup > Build).
/// The walk is drawn into the pictures, so nothing moves from code; only the control lock and the fade are done here.
/// </summary>
[DisallowMultipleComponent]
public class DoorEnterDriver : MonoBehaviour
{
    [Tooltip("1 = as drawn (0.72 seconds). 1.3 = faster, 0.8 = slower.")]
    public float playbackSpeed = 1f;
    [Tooltip("When the fade starts, as a share of the animation (0.55 = after the first steps).")]
    [Range(0f, 1f)] public float fadeStart = 0.5f;
    [Tooltip("Sword, shield and other loose objects that should be hidden while he walks through the door.")]
    public GameObject[] hideDuring;

    static readonly int[] Ms = { 90, 90, 90, 90, 90, 90, 90, 90 };      // from the sprite script (8 frames)
    const string AnimName = "EnterDoor";

    PlayerLayerSync sync;
    Animator animator;
    Rigidbody2D rb;
    public bool IsPlaying { get; private set; }

    void Awake()
    {
        sync = GetComponentInChildren<PlayerLayerSync>();
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    int FindAnimIndex()
    {
        if (sync == null || sync.animationNames == null) return -1;
        for (int i = 0; i < sync.animationNames.Length; i++)
            if (sync.animationNames[i] == AnimName) return i;
        return -1;
    }

    /// <summary>True when the EnterDoor animation exists in the built player.</summary>
    public bool Ready { get { return sync != null && FindAnimIndex() >= 0; } }

    void SetAlpha(float a)
    {
        if (sync == null || sync.slots == null) return;
        foreach (var s in sync.slots)
            if (s != null && s.renderer != null) { var c = s.renderer.color; c.a = a; s.renderer.color = c; }
    }

    /// <summary>Run it from a coroutine. onHidden is called when the player is invisible (swap the room here).</summary>
    public IEnumerator Play(Action onHidden = null)
    {
        if (IsPlaying) yield break;
        int idx = FindAnimIndex();
        if (idx < 0)
        {
            Debug.LogWarning("[DoorEnterDriver] Fant ikke animasjonen EnterDoor. Bygg spilleren på nytt (DeepVain > Player Setup > Build) etter at EnterDoor er lagt til.");
            if (onHidden != null) onHidden();
            yield break;
        }
        IsPlaying = true;

        // lock the player
        var locked = new System.Collections.Generic.List<Behaviour>();
        foreach (var b in new Behaviour[] { GetComponent<PlayerMovement>(), GetComponent<PlayerAttack>(), GetComponent<PlayerBlock>() })
            if (b != null && b.enabled) { b.enabled = false; locked.Add(b); }
        var oldType = RigidbodyType2D.Dynamic;
        if (rb != null) { oldType = rb.bodyType; rb.linearVelocity = Vector2.zero; rb.bodyType = RigidbodyType2D.Kinematic; }
        if (animator != null) animator.enabled = false;
        var hidden = new System.Collections.Generic.List<GameObject>();
        if (hideDuring != null) foreach (var g in hideDuring) if (g != null && g.activeSelf) { g.SetActive(false); hidden.Add(g); }

        sync.animId = idx;
        float speed = Mathf.Max(0.1f, playbackSpeed);
        float total = 0f; foreach (int m in Ms) total += m / 1000f;
        total /= speed;

        float t = 0f;
        while (t < total)
        {
            float tm = t * speed * 1000f, acc = 0f; int f = Ms.Length - 1;
            for (int i = 0; i < Ms.Length; i++) { acc += Ms[i]; if (tm < acc) { f = i; break; } }
            sync.frame = f;
            float k = Mathf.InverseLerp(fadeStart, 1f, t / total);
            SetAlpha(1f - Mathf.SmoothStep(0f, 1f, k));
            t += Time.deltaTime;
            yield return null;
        }
        sync.frame = Ms.Length - 1; SetAlpha(0f);

        if (onHidden != null) onHidden();
        yield return null;

        // give everything back
        SetAlpha(1f);
        if (animator != null) animator.enabled = true;
        if (rb != null) rb.bodyType = oldType;
        foreach (var g in hidden) if (g != null) g.SetActive(true);
        foreach (var b in locked) if (b != null) b.enabled = true;
        IsPlaying = false;
    }
}
