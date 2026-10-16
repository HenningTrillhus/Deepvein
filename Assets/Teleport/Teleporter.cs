using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Teleporterer spilleren til et sted når han trykker E nær objektet. Legg den på en dør, en port, en stige, en stein, hva som helst med en collider eller bare et sted.
///
/// Slik bruker du den:
///   1. Legg Teleporter på objektet (Add Component).
///   2. Skriv inn Target Position (verdens-koordinater, der føttene til spilleren havner). Eller dra et tomt objekt inn i Destination.
///      Tips: marker objektet og dra i den blå ringen i Scene-vinduet, så fylles posisjonen ut for deg.
///   3. Ferdig. Trykk E nær objektet.
///
/// Ligger det også en GoblinGate på samme objekt, er det GoblinGate som tar seg av døra (åpne/lukke, gå inn, sirkel-overgang)
/// og Teleporter bare gir den målet. Da gjelder posisjonen bare når Use Position er på. Er den av, bruker porten Pair Id / Linked Gate som før.
/// Uten GoblinGate gjør Teleporter hele jobben selv (valgfri gå-inn-animasjon og svart sirkel).
/// </summary>
[DisallowMultipleComponent]
public class Teleporter : MonoBehaviour
{
    public enum Transition { Iris, Instant }

    [Header("Koble to dører")]
    [Tooltip("Den andre døra (har også en Teleporter). Du går inn her med ryggen mot kamera og kommer ut av den andre døra gående mot kamera. Og motsatt. Kobler du den ene, kobles den andre tilbake automatisk.")]
    public Teleporter linkedDoor;
    [Tooltip("Hvor spilleren står når han kommer UT av denne døra, målt fra dørens posisjon (føttene). Vanligvis 0, 0.")]
    public Vector2 exitOffset;

    [Header("Eller et fritt sted")]
    [Tooltip("Verdens-koordinater der spilleren havner (føttene). Dra i den blå ringen i Scene-vinduet for å sette den.")]
    public Vector2 targetPosition;
    [Tooltip("Valgfritt: et tomt objekt som står der spilleren skal havne. Brukes i stedet for Target Position hvis satt.")]
    public Transform destination;
    [Tooltip("På: bruker posisjonen/destination. Av: gjør ingenting (på en GoblinGate betyr det at porten bruker Pair Id / Linked Gate som før).")]
    public bool usePosition = true;

    [Header("Tast og rekkevidde")]
    public Key interactKey = Key.E;
    [Tooltip("Hvor langt til siden (units) fra objektet spilleren kan stå.")]
    public float reach = 1.5f;
    [Tooltip("Hvor langt opp/ned (units).")]
    public float reachY = 2f;

    [Header("Overgang (bare uten GoblinGate)")]
    public Transition transition = Transition.Iris;
    [Tooltip("Gå til midten av objektet og spill gå-inn-animasjonen først (krever DoorEnterDriver på spilleren).")]
    public bool walkInFirst = true;
    public float irisClose = 0.45f;
    public float irisHold = 0.15f;
    public float irisOpen = 0.45f;
    [Tooltip("Gir opp å gå mot midten etter så mange sekunder.")]
    public float approachTimeout = 2f;
    [Tooltip("Spilleren settes på bakken under målet, selv om målpunktet ligger litt høyere enn føttene.")]
    public bool snapToGround = true;

    [Header("[E]-merket (bare uten GoblinGate)")]
    public bool showPrompt = true;
    public string promptSortingLayer = "Default";
    public int promptSortingOrder = 50;
    public float promptHeight = 1.9f;

    [Header("Når den har teleportert")]
    public UnityEvent onTeleported;

    PlayerMovement player;
    GoblinGate gate;
    GameObject gateTarget;
    GameObject prompt;
    bool busy, abort;

    /// <summary>Hvor spilleren havner akkurat nå: den koblede døra, ellers Destination, ellers Target Position.</summary>
    public Vector3 Target
    {
        get
        {
            if (linkedDoor != null) return linkedDoor.transform.position + (Vector3)linkedDoor.exitOffset;
            return destination != null ? destination.position : new Vector3(targetPosition.x, targetPosition.y, 0f);
        }
    }

    /// <summary>The player arrives here: open this door for a moment (used when the other door sends him).</summary>
    public void ArrivedHere() { if (gate != null) gate.OpenFor(1.2f); }

    // ------------------------------------------------------------------------------------------------ setup
    void Awake()
    {
        gate = GetComponent<GoblinGate>();
        if (linkedDoor != null && linkedDoor.linkedDoor == null) linkedDoor.linkedDoor = this;       // two-way without extra work
    }

    void Start()
    {
        // after every Awake, so both doors know about each other
        if (gate == null || (!usePosition && linkedDoor == null)) return;
        if (linkedDoor != null)
        {
            var g2 = linkedDoor.GetComponent<GoblinGate>();
            if (g2 != null) { gate.linkedGate = g2; gate.destination = null; }
            else
            {
                gateTarget = new GameObject("TeleportTarget (" + name + ")"); gateTarget.hideFlags = HideFlags.HideInHierarchy;
                gateTarget.transform.position = Target; gate.destination = gateTarget.transform; gate.linkedGate = null;
            }
            gate.pairId = ""; return;
        }
        // GoblinGate does the whole door show; we just give it the place. A hidden empty object stands at the target.
        if (destination != null) gate.destination = destination;
        else
        {
            gateTarget = new GameObject("TeleportTarget (" + name + ")");
            gateTarget.hideFlags = HideFlags.HideInHierarchy;
            gateTarget.transform.position = Target;
            gate.destination = gateTarget.transform;
        }
        gate.linkedGate = null; gate.pairId = "";
    }

    void OnDestroy() { if (gateTarget != null) Destroy(gateTarget); }

    /// <summary>Flytter målet mens spillet går (også for GoblinGate-modus).</summary>
    public void SetTarget(Vector2 pos)
    {
        targetPosition = pos;
        if (gateTarget != null) gateTarget.transform.position = Target;
    }

    // ------------------------------------------------------------------------------------------------ own door logic (no GoblinGate)
    void Update()
    {
        if (gate != null || !usePosition) { SetPrompt(false); return; }
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();
        bool near = !busy && InReach(player);
        SetPrompt(near && showPrompt);
        if (!near || Keyboard.current == null) return;
        if (Keyboard.current[interactKey].wasPressedThisFrame) TryTeleport();
    }

    bool InReach(PlayerMovement p)
    {
        if (p == null) return false;
        Vector3 d = p.transform.position - transform.position;
        return Mathf.Abs(d.x) <= reach && Mathf.Abs(d.y) <= reachY;
    }

    static bool PlayerBusy(PlayerMovement p)
    {
        if (p.IsClimbing || p.IsLedgeClimbing || p.IsRolling || p.IsCrouching || !p.IsGrounded) return true;
        var atk = p.GetComponent<PlayerAttack>(); if (atk != null && atk.IsAttacking) return true;
        var blk = p.GetComponent<PlayerBlock>(); if (blk != null && blk.IsBlocking) return true;
        var hp = p.GetComponent<PlayerHealth>(); if (hp != null && (hp.IsStunned || hp.IsDead)) return true;
        var dd = p.GetComponent<DoorEnterDriver>(); if (dd != null && dd.IsPlaying) return true;
        return false;
    }

    /// <summary>Samme som å trykke E. Kan kalles fra andre script (en spak, en knapp). true = det startet.</summary>
    public bool TryTeleport()
    {
        if (gate != null) return gate.TryEnter();
        if (!usePosition || busy) return false;
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();
        if (player == null || !InReach(player) || PlayerBusy(player)) return false;
        StartCoroutine(Go(player));
        return true;
    }

    void OnPlayerInput() { abort = true; }

    IEnumerator Go(PlayerMovement p)
    {
        busy = true; abort = false;
        SetPrompt(false);
        Vector3 target = Target;

        if (transition == Transition.Instant)
        {
            Move(p, target);
            busy = false;
            yield break;
        }

        var driver = p.GetComponent<DoorEnterDriver>();
        bool walk = walkInFirst && driver != null && driver.Ready;
        if (walk)
        {
            p.PlayerInput += OnPlayerInput;
            float start = Time.time; int lastDir = 0;
            while (true)                                                     // walk to the middle of the door
            {
                float dx = transform.position.x - p.transform.position.x;
                if (Mathf.Abs(dx) <= 0.06f) break;
                if (abort || Time.time - start > approachTimeout) { p.SetForcedMove(0f); p.SetForcedFacing(0); p.PlayerInput -= OnPlayerInput; busy = false; yield break; }
                int dir = dx < 0f ? -1 : 1;
                if (lastDir != 0 && dir != lastDir) break;
                lastDir = dir;
                p.SetForcedMove(dir); p.SetForcedFacing(dir);
                yield return null;
            }
            p.SetForcedMove(0f); p.SetForcedFacing(0);
            p.PlayerInput -= OnPlayerInput;
        }

        bool moved = false;
        System.Action doMove = () => { if (moved) return; moved = true; Move(p, target); if (linkedDoor != null) linkedDoor.ArrivedHere(); };
        StartCoroutine(IrisWipe.CloseRoutine(irisClose, 0.05f));
        bool walkOut = walk && driver.ReadyExit;
        if (walkOut)
        {
            // in with the back to us, then (still locked) the circle opens and he walks out of the other door facing us
            yield return driver.Play(doMove, irisHold, () => StartCoroutine(IrisWipe.OpenRoutine(irisOpen)));
        }
        else
        {
            if (walk) yield return driver.Play(doMove);
            else yield return new WaitForSeconds(0.05f + irisClose);
            doMove();
            yield return new WaitForSeconds(irisHold);
            StartCoroutine(IrisWipe.OpenRoutine(irisOpen));
            yield return new WaitForSeconds(irisOpen);
        }
        busy = false;
    }

    /// <summary>The ground below the door (the player's own ground layers), so he stands on it even if the door object sits higher than his feet.</summary>
    static float GroundY(PlayerMovement p, Vector3 pos)
    {
        var hits = Physics2D.RaycastAll(new Vector2(pos.x, pos.y + 0.5f), Vector2.down, 12f, p.groundLayer);
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger || h.collider.transform.root == p.transform.root) continue;
            return h.point.y;
        }
        return pos.y;
    }

    void Move(PlayerMovement p, Vector3 pos)
    {
        var rb = p.GetComponent<Rigidbody2D>();
        float gy = snapToGround ? GroundY(p, pos) : pos.y;
        Vector3 np = new Vector3(pos.x, gy + 0.02f, p.transform.position.z);
        p.transform.position = np;
        if (rb != null) { rb.position = new Vector2(np.x, np.y); rb.linearVelocity = Vector2.zero; }
        var dd = p.GetComponent<DoorEnterDriver>(); var hp = p.GetComponent<PlayerHealth>();
        if (hp != null && (dd == null || !dd.IsPlaying)) hp.StartDoorGrace();      // no door animation running: the safe seconds start now
        Physics2D.SyncTransforms();
        var cam = Camera.main;                                               // snap the camera so it is not seen flying across
        if (cam != null) { var cp = cam.transform.position; cam.transform.position = new Vector3(np.x, np.y, cp.z); }
        if (onTeleported != null) onTeleported.Invoke();
    }

    // ------------------------------------------------------------------------------------------------ [E] mark
    static Sprite promptSprite;

    static Sprite MakePromptSprite()
    {
        if (promptSprite != null) return promptSprite;
        const int w = 11, h = 11;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[w * h];
        Color32 dark = new Color32(34, 18, 28, 255), pale = new Color32(244, 234, 208, 255);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            px[y * w + x] = (x == 0 || y == 0 || x == w - 1 || y == h - 1) ? dark : pale;
        string[] e = { "EEEEE", "E....", "E....", "EEEE.", "E....", "E....", "EEEEE" };
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

    void OnDisable() { SetPrompt(false); }

    // ------------------------------------------------------------------------------------------------ Scene view
    void OnDrawGizmosSelected()
    {
        if (!usePosition) return;
        Vector3 t = Target;
        if (linkedDoor != null) { Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f); }
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, t);
        Gizmos.DrawWireSphere(t, 0.35f);
        Gizmos.DrawLine(t + Vector3.left * 0.5f, t + Vector3.right * 0.5f);
        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);                              // where the player has to stand to use it
        Gizmos.DrawWireCube(transform.position, new Vector3(reach * 2f, reachY * 2f, 0f));
    }
}
