using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// En port/dør du går inn i med E. Ingen collider: du kan gå rett forbi den.
///   1. Står spilleren nær porten kommer et [E]-merke over den. Trykk E.
///   2. Porten åpner seg, og spilleren går mot midten av porten (som mot trærne) og snur ryggen til.
///   3. EnterDoor-animasjonen spilles (ryggen mot oss, inn i skjermen), og en svart sirkel lukker seg fra kantene mot midten.
///   4. Spilleren flyttes til målet (en annen port, eller et tomt punkt), sirkelen åpner seg igjen, og porten lukker seg.
///
/// Mål: sett Linked Gate (en annen GoblinGate), eller Destination (et tomt objekt), eller gi to porter samme Pair Id, så finner de hverandre
/// (praktisk når porten ligger i en chunk-prefab og den andre i en annen). Spilleren dukker opp midt i målporten, som da åpner seg og lukker seg.
/// Legges på portobjektet sammen med en SpriteRenderer. Settes opp av menyen DeepVain > Gate > Build goblin gate.
/// Krever DoorEnterDriver på spilleren (menyen legger den på). Mangler den, hopper den over gå-inn-animasjonen og kjører bare overgangen.
/// </summary>
[DisallowMultipleComponent]
public class GoblinGate : MonoBehaviour
{
    [Header("Tast og rekkevidde")]
    public Key interactKey = Key.E;
    [Tooltip("Hvor langt til siden (units) fra midten av porten spilleren kan stå for at E virker.")]
    public float reach = 1.5f;
    [Tooltip("Hvor langt opp/ned (units).")]
    public float reachY = 2f;

    [Header("Hvor går den (en av dem)")]
    [Tooltip("Porten spilleren kommer ut av.")]
    public GoblinGate linkedGate;
    [Tooltip("Eller et tomt objekt der spilleren dukker opp (føttene).")]
    public Transform destination;
    [Tooltip("Eller: to porter med samme Pair Id (ikke tomt) finner hverandre selv.")]
    public string pairId = "";

    [Header("Bilder")]
    public SpriteRenderer spriteRenderer;
    public Sprite closedSprite;
    [Tooltip("Åpningen, fra litt åpen til helt åpen (8 bilder).")]
    public Sprite[] openSprites;
    [Tooltip("Sekunder per bilde i åpne/lukke-animasjonen.")]
    public float frameTime = 0.045f;

    [Header("Gå inn")]
    [Tooltip("Hvor nøyaktig spilleren stiller seg i midten av porten (units).")]
    public float standTolerance = 0.06f;
    [Tooltip("Gir opp å gå mot midten etter så mange sekunder.")]
    public float approachTimeout = 2f;

    [Header("Overgang (svart sirkel)")]
    [Tooltip("Sekunder fra E (og spilleren står i midten) til sirkelen begynner å lukke seg.")]
    public float irisDelay = 0.1f;
    public float irisClose = 0.55f;
    [Tooltip("Hvor lenge det er helt svart etter flyttingen.")]
    public float irisHold = 0.2f;
    public float irisOpen = 0.55f;

    [Header("[E]-merket")]
    public bool showPrompt = true;
    public string promptSortingLayer = "Default";
    public int promptSortingOrder = 50;
    public float promptHeight = 1.9f;

    PlayerMovement player;
    bool busy, isOpen, abort;
    GameObject prompt;
    Coroutine anim;
    int promptOn;

    public bool IsBusy { get { return busy; } }
    /// <summary>True when the doors stand open (or are opening).</summary>
    public bool IsOpen { get { return isOpen; } }

    // ------------------------------------------------------------------------------------------------ finding things
    PlayerMovement FindPlayer()
    {
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();
        return player;
    }

    bool InReach(PlayerMovement p)
    {
        if (p == null) return false;
        Vector3 d = p.transform.position - transform.position;
        return Mathf.Abs(d.x) <= reach && Mathf.Abs(d.y) <= reachY;
    }

    /// <summary>Where the player will stand on the other side (feet).</summary>
    bool Resolve(out Vector3 pos, out GoblinGate other)
    {
        other = linkedGate;
        if (other == null && destination == null && !string.IsNullOrEmpty(pairId))
            foreach (var g in FindObjectsByType<GoblinGate>(FindObjectsSortMode.None))
                if (g != this && g.pairId == pairId) { other = g; break; }
        if (other != null) { pos = other.transform.position; return true; }
        if (destination != null) { pos = destination.position; return true; }
        pos = Vector3.zero;
        return false;
    }

    // ------------------------------------------------------------------------------------------------ update
    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && closedSprite == null) closedSprite = spriteRenderer.sprite;
    }

    void Update()
    {
        var p = FindPlayer();
        bool near = !busy && InReach(p);
        SetPrompt(near && showPrompt);
        if (!near || Keyboard.current == null) return;
        if (!Keyboard.current[interactKey].wasPressedThisFrame) return;
        TryEnter();
    }

    /// <summary>Same as pressing E (spilleren må stå innen rekkevidde og ikke være opptatt). true = overgangen startet.</summary>
    public bool TryEnter()
    {
        var p = FindPlayer();
        if (busy || p == null || !InReach(p) || PlayerBusy(p)) return false;
        Vector3 pos; GoblinGate other;
        if (!Resolve(out pos, out other)) { Debug.LogWarning("[GoblinGate] \"" + name + "\" har ikke noe mål. Sett Linked Gate, Destination eller et Pair Id.", this); return false; }
        StartCoroutine(Enter(p, pos, other));
        return true;
    }

    bool PlayerBusy(PlayerMovement p)
    {
        if (p.IsClimbing || p.IsLedgeClimbing || p.IsRolling || p.IsCrouching || !p.IsGrounded) return true;
        var atk = p.GetComponent<PlayerAttack>(); if (atk != null && atk.IsAttacking) return true;
        var blk = p.GetComponent<PlayerBlock>(); if (blk != null && blk.IsBlocking) return true;
        var hp = p.GetComponent<PlayerHealth>(); if (hp != null && (hp.IsStunned || hp.IsDead)) return true;
        var dd = p.GetComponent<DoorEnterDriver>(); if (dd != null && dd.IsPlaying) return true;
        return false;
    }

    void OnDisable() { SetPrompt(false); }

    // ------------------------------------------------------------------------------------------------ the walk in
    void OnPlayerInput() { abort = true; }

    IEnumerator Enter(PlayerMovement p, Vector3 targetPos, GoblinGate other)
    {
        busy = true; abort = false;
        p.PlayerInput += OnPlayerInput;
        SetPrompt(false);
        Open();

        // walk to the middle of the gate and turn the back to us
        float start = Time.time;
        int lastDir = 0;
        while (true)
        {
            float dx = transform.position.x - p.transform.position.x;
            if (Mathf.Abs(dx) <= standTolerance) break;
            if (abort || Time.time - start > approachTimeout) { Finish(p, false); yield break; }
            int dir = dx < 0f ? -1 : 1;
            if (lastDir != 0 && dir != lastDir) break;                        // went past the middle: good enough
            lastDir = dir;
            p.SetForcedMove(dir); p.SetForcedFacing(dir);
            yield return null;
        }
        p.SetForcedMove(0f); p.SetForcedFacing(0);
        var rb = p.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        p.PlayerInput -= OnPlayerInput;

        // walk in + the black circle
        bool teleported = false;
        System.Action doTeleport = () =>
        {
            if (teleported) return;
            teleported = true;
            Teleport(p, targetPos);
            if (other != null) other.OpenFor(irisHold + irisOpen + 0.3f);
        };

        StartCoroutine(IrisWipe.CloseRoutine(irisClose, irisDelay));
        var driver = p.GetComponent<DoorEnterDriver>();
        if (driver != null && driver.Ready)
            yield return driver.Play(doTeleport);
        else
            yield return new WaitForSeconds(irisDelay + irisClose);
        doTeleport();
        // the iris must be shut before the next step
        yield return new WaitForSeconds(irisHold);
        StartCoroutine(IrisWipe.OpenRoutine(irisOpen));
        Close();
        yield return new WaitForSeconds(irisOpen);
        Finish(p, true);
    }

    void Finish(PlayerMovement p, bool ok)
    {
        if (p != null)
        {
            p.PlayerInput -= OnPlayerInput;
            if (!ok) { p.SetForcedMove(0f); p.SetForcedFacing(0); }
        }
        if (!ok) Close();
        busy = false;
    }

    void Teleport(PlayerMovement p, Vector3 pos)
    {
        var rb = p.GetComponent<Rigidbody2D>();
        Vector3 cur = p.transform.position;
        Vector3 np = new Vector3(pos.x, pos.y + 0.02f, cur.z);
        p.transform.position = np;
        if (rb != null) { rb.position = new Vector2(np.x, np.y); rb.linearVelocity = Vector2.zero; }
        Physics2D.SyncTransforms();
        var cam = Camera.main;                                              // the camera follows with a delay: snap it, so it is not seen flying across
        if (cam != null) { var cp = cam.transform.position; cam.transform.position = new Vector3(np.x, np.y, cp.z); }
    }

    // ------------------------------------------------------------------------------------------------ the door itself
    public void Open()
    {
        if (openSprites == null || openSprites.Length == 0 || spriteRenderer == null) return;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Play(true));
    }

    public void Close()
    {
        if (spriteRenderer == null) return;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Play(false));
    }

    /// <summary>Open for a while, then close again (used for the gate the player comes out of).</summary>
    public void OpenFor(float seconds) { StartCoroutine(OpenForRoutine(seconds)); }
    IEnumerator OpenForRoutine(float seconds) { Open(); yield return new WaitForSeconds(seconds); if (!busy) Close(); }

    IEnumerator Play(bool open)
    {
        if (openSprites == null || openSprites.Length == 0) yield break;
        if (open)
        {
            for (int i = 0; i < openSprites.Length; i++) { spriteRenderer.sprite = openSprites[i]; yield return new WaitForSeconds(frameTime); }
            isOpen = true;
        }
        else
        {
            for (int i = openSprites.Length - 1; i >= 0; i--) { spriteRenderer.sprite = openSprites[i]; yield return new WaitForSeconds(frameTime); }
            if (closedSprite != null) spriteRenderer.sprite = closedSprite;
            isOpen = false;
        }
    }

    // ------------------------------------------------------------------------------------------------ the [E] mark
    static Sprite promptSprite;

    static Sprite MakePromptSprite()
    {
        if (promptSprite != null) return promptSprite;
        const int w = 11, h = 11;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var clear = new Color32(0, 0, 0, 0); var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        Color32 dark = new Color32(34, 18, 28, 255), pale = new Color32(244, 234, 208, 255);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            px[y * w + x] = (x == 0 || y == 0 || x == w - 1 || y == h - 1) ? dark : pale;
        string[] e = { "EEEEE", "E....", "E....", "EEEE.", "E....", "E....", "EEEEE" };       // the letter E, 5 x 7
        for (int j = 0; j < 7; j++) for (int i = 0; i < 5; i++)
            if (e[j][i] == 'E') px[(h - 2 - j) * w + (3 + i)] = dark;
        t.SetPixels32(px); t.Apply();
        promptSprite = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        return promptSprite;
    }

    void SetPrompt(bool on)
    {
        if (on)
        {
            if (prompt == null)
            {
                prompt = new GameObject("[E]", typeof(SpriteRenderer));
                prompt.transform.SetParent(transform, false);
                prompt.transform.localPosition = new Vector3(0f, promptHeight / Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y)), 0f);
                float k = 2f / Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
                prompt.transform.localScale = new Vector3(k, k, 1f);
                var sr = prompt.GetComponent<SpriteRenderer>();
                sr.sprite = MakePromptSprite(); sr.sortingLayerName = promptSortingLayer; sr.sortingOrder = promptSortingOrder;
            }
            if (!prompt.activeSelf) prompt.SetActive(true);
        }
        else if (prompt != null && prompt.activeSelf) prompt.SetActive(false);
    }
}
