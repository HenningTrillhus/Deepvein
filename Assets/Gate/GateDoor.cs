using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using DeepVain.Items;

/// <summary>
/// A big double gate. Walk up and press the interact key: the leaves swing open (8 pictures) and the blocker is switched off,
/// so the player can walk through. Press again to close it (not possible while someone stands in the doorway).
/// Optional: a key item that is needed to open it.
/// Made by DeepVain > Gate > 2 - Create gate prefabs.
/// </summary>
public class GateDoor : MonoBehaviour
{
    public enum State { Closed, Opening, Open, Closing }

    [Header("Bilder")]
    public SpriteRenderer gateRenderer;
    public Sprite closedSprite;
    public Sprite[] openFrames;                 // first = just ajar, last = fully open
    public float frameTime = 0.07f;

    [Header("Kollisjon")]
    [Tooltip("Døra selv. Slås av når porten er åpen (stolpene har egne colliders som alltid er på).")]
    public Collider2D blocker;

    [Header("Åpne")]
    public Key interactKey = Key.G;
    public bool startOpen = false;
    public bool canClose = true;
    [Tooltip("Valgfritt: nøkkel-item som trengs for å låse opp. La stå tom for en vanlig port.")]
    public ItemData requiredKey;
    public bool consumeKey = false;
    [Tooltip("Lite tekstobjekt som vises når spilleren står nær.")]
    public TMP_Text prompt;

    public State Current { get; private set; } = State.Closed;
    public bool Locked { get; private set; }
    public event Action<GateDoor> Opened, Closed;

    Collider2D playerCol;
    Inventory playerInv;
    bool playerNear;

    void Awake()
    {
        Locked = requiredKey != null;
        if (startOpen) SetOpenImmediately();
        else if (gateRenderer != null && closedSprite != null) gateRenderer.sprite = closedSprite;
        if (prompt != null) prompt.gameObject.SetActive(false);
    }

    void SetOpenImmediately()
    {
        Current = State.Open; Locked = false;
        if (blocker != null) blocker.enabled = false;
        if (gateRenderer != null && openFrames != null && openFrames.Length > 0) gateRenderer.sprite = openFrames[openFrames.Length - 1];
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var inv = other.GetComponentInParent<Inventory>();
        if (inv == null) return;
        playerInv = inv; playerCol = other; playerNear = true;
        RefreshPrompt();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<Inventory>() == null) return;
        playerNear = false;
        if (prompt != null) prompt.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!playerNear) return;
        var kb = Keyboard.current;
        if (kb != null && kb[interactKey].wasPressedThisFrame) Toggle();
    }

    void RefreshPrompt()
    {
        if (prompt == null) return;
        bool busy = Current == State.Opening || Current == State.Closing;
        bool show = playerNear && !busy && (Current == State.Closed || (Current == State.Open && canClose));
        prompt.gameObject.SetActive(show);
        if (!show) return;
        string k = "[" + interactKey.ToString().Replace("Digit", "") + "] ";
        prompt.text = Current == State.Closed ? (Locked && !HasKey() ? k + "Locked" : k + "Open") : k + "Close";
    }

    bool HasKey() { return requiredKey == null || (playerInv != null && playerInv.Count(requiredKey) > 0); }

    /// <summary>Opens a closed gate or closes an open one (also callable from other scripts, e.g. a lever).</summary>
    public void Toggle()
    {
        if (Current == State.Closed) Open();
        else if (Current == State.Open && canClose) Close();
    }

    public void Open()
    {
        if (Current != State.Closed) return;
        if (Locked)
        {
            if (!HasKey()) { Debug.Log("[Gate] Låst. Trenger: " + requiredKey.displayName); return; }
            if (consumeKey) playerInv.RemoveItem(requiredKey, 1);
            Locked = false;
        }
        StartCoroutine(Play(true));
    }

    public void Close()
    {
        if (Current != State.Open) return;
        // never close it on top of the player
        if (blocker != null && playerCol != null)
        {
            bool wasOn = blocker.enabled; blocker.enabled = true;
            bool overlap = blocker.bounds.Intersects(playerCol.bounds);
            blocker.enabled = wasOn;
            if (overlap) { Debug.Log("[Gate] Noen står i døra."); return; }
        }
        StartCoroutine(Play(false));
    }

    IEnumerator Play(bool opening)
    {
        Current = opening ? State.Opening : State.Closing;
        RefreshPrompt();
        if (!opening && blocker != null) blocker.enabled = true;      // solid as soon as it starts closing
        int n = openFrames != null ? openFrames.Length : 0;
        for (int k = 0; k < n; k++)
        {
            int i = opening ? k : n - 1 - k;
            gateRenderer.sprite = openFrames[i];
            yield return new WaitForSeconds(frameTime);
        }
        if (opening)
        {
            Current = State.Open;
            if (blocker != null) blocker.enabled = false;
            if (Opened != null) Opened(this);
        }
        else
        {
            gateRenderer.sprite = closedSprite;
            Current = State.Closed;
            if (Closed != null) Closed(this);
        }
        RefreshPrompt();
    }
}
