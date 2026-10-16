using System;
using System.Collections;
using UnityEngine;
using DeepVain.Player;

/// <summary>
/// Plays the door animations on the player:
///   EnterDoor: the player turns his back to us and walks a few steps into the screen (smaller and a bit higher up in the picture) and fades out.
///   ExitDoor : the other way: he walks OUT of a door towards us, looking straight at us (starts small and high, ends at normal size).
/// Works for any door or gate. The door script starts  StartCoroutine(driver.Play(() => { /* move the player */ }))  when the player uses it.
/// With holdBeforeExit >= 0 and an ExitDoor animation in the built player, Play also does the arrival: after the move the player stays locked for
/// holdBeforeExit seconds (the black circle is shut), onExitStart is called (open the circle), then he walks out. Without ExitDoor it ends right after onHidden, as before.
///
/// Put this on the same object as PlayerMovement. Needs EnterDoor (and ExitDoor) in the built player (DeepVain > Player Setup > Build).
/// The walks are drawn into the pictures, so nothing moves from code; only the control lock and the fade are done here.
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

    static readonly int[] Ms = { 90, 90, 90, 90, 90, 90, 90, 90 };      // from the sprite scripts (8 frames each)
    const string EnterName = "EnterDoor";
    const string ExitName = "ExitDoor";

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

    int FindAnimIndex(string name)
    {
        if (sync == null || sync.animationNames == null) return -1;
        for (int i = 0; i < sync.animationNames.Length; i++)
            if (sync.animationNames[i] == name) return i;
        return -1;
    }

    /// <summary>True when the EnterDoor animation exists in the built player.</summary>
    public bool Ready { get { return sync != null && FindAnimIndex(EnterName) >= 0; } }
    /// <summary>True when the ExitDoor animation (walking out, facing us) exists in the built player.</summary>
    public bool ReadyExit { get { return sync != null && FindAnimIndex(ExitName) >= 0; } }

    void SetAlpha(float a)
    {
        if (sync == null || sync.slots == null) return;
        foreach (var s in sync.slots)
            if (s != null && s.renderer != null) { var c = s.renderer.color; c.a = a; s.renderer.color = c; }
    }

    // ------------------------------------------------------------------------------------------------ lock / unlock
    readonly System.Collections.Generic.List<Behaviour> locked = new System.Collections.Generic.List<Behaviour>();
    readonly System.Collections.Generic.List<GameObject> hidden = new System.Collections.Generic.List<GameObject>();
    RigidbodyType2D oldType = RigidbodyType2D.Dynamic;

    void Lock()
    {
        locked.Clear(); hidden.Clear();
        foreach (var b in new Behaviour[] { GetComponent<PlayerMovement>(), GetComponent<PlayerAttack>(), GetComponent<PlayerBlock>() })
            if (b != null && b.enabled) { b.enabled = false; locked.Add(b); }
        if (rb != null) { oldType = rb.bodyType; rb.linearVelocity = Vector2.zero; rb.bodyType = RigidbodyType2D.Kinematic; }
        if (animator != null) animator.enabled = false;
        var hp = GetComponent<PlayerHealth>(); if (hp != null) hp.LockInvulnerable();            // cannot be hurt while the door scene runs
        if (hideDuring != null) foreach (var g in hideDuring) if (g != null && g.activeSelf) { g.SetActive(false); hidden.Add(g); }
    }

    void Unlock()
    {
        SetAlpha(1f);
        if (animator != null) animator.enabled = true;
        if (rb != null) rb.bodyType = oldType;
        foreach (var g in hidden) if (g != null) g.SetActive(true);
        foreach (var b in locked) if (b != null) b.enabled = true;
        var hp = GetComponent<PlayerHealth>(); if (hp != null) hp.StartDoorGrace();              // and a few safe seconds afterwards (PlayerHealth.doorGraceTime)
        IsPlaying = false;
    }

    float Total(float speed) { float t = 0f; foreach (int m in Ms) t += m / 1000f; return t / speed; }

    int FrameAt(float t, float speed)
    {
        float tm = t * speed * 1000f, acc = 0f;
        for (int i = 0; i < Ms.Length; i++) { acc += Ms[i]; if (tm < acc) return i; }
        return Ms.Length - 1;
    }

    // ------------------------------------------------------------------------------------------------ walking in
    /// <summary>Run it from a coroutine. onHidden is called when the player is invisible (swap the room here).
    /// holdBeforeExit >= 0: also walk out of the other side afterwards (see the class comment). onExitStart is called just before he starts walking out.</summary>
    public IEnumerator Play(Action onHidden = null, float holdBeforeExit = -1f, Action onExitStart = null)
    {
        if (IsPlaying) yield break;
        int idx = FindAnimIndex(EnterName);
        if (idx < 0)
        {
            Debug.LogWarning("[DoorEnterDriver] Fant ikke animasjonen EnterDoor. Bygg spilleren på nytt (DeepVain > Player Setup > Build) etter at EnterDoor er lagt til.");
            if (onHidden != null) onHidden();
            yield break;
        }
        IsPlaying = true;
        Lock();

        sync.animId = idx;
        float speed = Mathf.Max(0.1f, playbackSpeed);
        float total = Total(speed);
        float t = 0f;
        while (t < total)
        {
            sync.frame = FrameAt(t, speed);
            float k = Mathf.InverseLerp(fadeStart, 1f, t / total);
            SetAlpha(1f - Mathf.SmoothStep(0f, 1f, k));
            t += Time.deltaTime;
            yield return null;
        }
        sync.frame = Ms.Length - 1; SetAlpha(0f);

        if (onHidden != null) onHidden();
        yield return null;

        if (holdBeforeExit >= 0f && ReadyExit)
        {
            SetAlpha(0f);
            yield return new WaitForSeconds(holdBeforeExit);
            yield return WalkOut(onExitStart);
        }
        Unlock();
    }

    /// <summary>Only the walk OUT of a door towards us (the player must already stand at the door). For doors that do their own timing.</summary>
    public IEnumerator PlayExit(Action onExitStart = null)
    {
        if (IsPlaying) yield break;
        if (!ReadyExit)
        {
            Debug.LogWarning("[DoorEnterDriver] Fant ikke animasjonen ExitDoor. Bygg spilleren på nytt (DeepVain > Player Setup > Build).");
            if (onExitStart != null) onExitStart();
            yield break;
        }
        IsPlaying = true;
        Lock();
        yield return WalkOut(onExitStart);
        Unlock();
    }

    IEnumerator WalkOut(Action onExitStart)
    {
        sync.animId = FindAnimIndex(ExitName); sync.frame = 0;
        SetAlpha(1f);
        if (onExitStart != null) onExitStart();
        float speed = Mathf.Max(0.1f, playbackSpeed);
        float total = Total(speed), t = 0f;
        while (t < total)
        {
            sync.frame = FrameAt(t, speed);
            t += Time.deltaTime;
            yield return null;
        }
        sync.frame = Ms.Length - 1;
        yield return null;
    }
}
