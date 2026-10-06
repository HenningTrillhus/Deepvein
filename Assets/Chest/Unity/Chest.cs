using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using DeepVain.Items;

/// <summary>
/// A treasure chest (one tile). Closed it shimmers; walk up to it and press the interact key to open it.
/// The lid opens, light pours out in the colour of the BEST item inside (Common / Uncommon / Rare / Epic / Legendary),
/// and the items go into the player's inventory (until the chest has its own window in the inventory UI).
/// Made by DeepVain > Chest > Create chest prefab.
/// </summary>
public class Chest : MonoBehaviour
{
    [Serializable]
    public class LootEntry
    {
        public ItemData item;
        public int amount = 1;
    }

    public enum State { Closed, Opening, Open }

    [Header("Innhold")]
    public LootEntry[] loot;
    [Tooltip("På: tingene legges rett i sekken når kista åpnes (til kista får sitt eget vindu i inventory).")]
    public bool giveToInventory = true;
    [Tooltip("Pause mellom hver ting som dukker opp (stripene nede til høyre).")]
    public float lootInterval = 0.25f;

    [Header("Åpne")]
    public Key interactKey = Key.G;
    [Tooltip("Lite tekstobjekt som vises når spilleren står nær.")]
    public GameObject prompt;

    [Header("Bilder")]
    public SpriteRenderer chestRenderer;
    public SpriteRenderer glowRenderer;
    public SpriteRenderer burstRenderer;
    public Sprite[] idleFrames;
    public Sprite[] openFrames;
    public Sprite[] glowFrames;
    public Sprite[] burstFrames;
    public float idleFrameTime = 0.12f;
    public float openFrameTime = 0.07f;
    public float glowFrameTime = 0.1f;
    public float burstFrameTime = 0.06f;
    [Tooltip("Hvilket bilde i åpne-animasjonen lyset starter på (lokket er da løftet).")]
    public int glowStartFrame = 3;
    [Tooltip("Hvor lenge lyset tones inn.")]
    public float glowFadeTime = 0.35f;
    [Tooltip("Hvor lenge lyset står på (sekunder etter at det er tonet inn) før det slukker.")]
    public float glowHoldTime = 0.65f;
    [Tooltip("Hvor lenge det tar å tone lyset ut.")]
    public float glowFadeOutTime = 0.5f;

    [Header("Styrke per sjeldenhet (Common, Uncommon, Rare, Epic, Legendary)")]
    [Tooltip("Hvor stort lyset blir. Legendary gir mest.")]
    public float[] glowScale = { 0.7f, 0.85f, 1f, 1.15f, 1.3f };
    [Tooltip("Hvor sterkt lyset i scenen er.")]
    public float[] lightScale = { 0.6f, 0.8f, 1f, 1.25f, 1.6f };

    [Header("Lys i scenen (valgfritt, Light2D)")]
    public Behaviour sceneLight;
    public float lightIntensity = 1.4f;

    public State Current { get; private set; } = State.Closed;
    public ItemRarity BestRarity { get; private set; } = ItemRarity.Common;
    public Color GlowColor { get; private set; } = Color.white;
    public event Action<Chest> Opened;

    bool playerNear;
    Inventory playerInventory;
    float idleTimer, glowTimer;
    float glowAlpha;

    // same colours as the inventory UI
    public static Color RarityColor(ItemRarity r)
    {
        switch (r)
        {
            case ItemRarity.Uncommon: return new Color32(0x5F, 0xD0, 0x68, 255);
            case ItemRarity.Rare: return new Color32(0x4A, 0x9C, 0xF0, 255);
            case ItemRarity.Epic: return new Color32(0xB0, 0x62, 0xE8, 255);
            case ItemRarity.Legendary: return new Color32(0xFF, 0xC9, 0x4A, 255);
            default: return new Color32(0xE8, 0xE0, 0xD0, 255);
        }
    }

    static float Pick(float[] a, ItemRarity r, float fallback)
    {
        int i = (int)r;
        return a != null && i >= 0 && i < a.Length ? a[i] : fallback;
    }

    /// <summary>The highest rarity among the items in the chest.</summary>
    public ItemRarity ComputeBestRarity()
    {
        ItemRarity best = ItemRarity.Common;
        if (loot != null)
            foreach (var l in loot)
                if (l != null && l.item != null && l.amount > 0 && l.item.rarity > best) best = l.item.rarity;
        return best;
    }

    void Awake()
    {
        BestRarity = ComputeBestRarity();
        GlowColor = RarityColor(BestRarity);
        if (glowRenderer != null) glowRenderer.enabled = false;
        if (burstRenderer != null) burstRenderer.enabled = false;
        if (prompt != null) prompt.SetActive(false);
        SetLight(0f);
        if (chestRenderer != null && idleFrames != null && idleFrames.Length > 0) chestRenderer.sprite = idleFrames[0];
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var inv = other.GetComponentInParent<Inventory>();
        if (inv == null) return;
        playerInventory = inv; playerNear = true;
        if (prompt != null && Current == State.Closed) prompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<Inventory>() == null) return;
        playerNear = false;
        if (prompt != null) prompt.SetActive(false);
    }

    void Update()
    {
        if (Current == State.Closed)
        {
            Animate(idleFrames, ref idleTimer, idleFrameTime, chestRenderer, true);
            var kb = Keyboard.current;
            if (playerNear && kb != null && kb[interactKey].wasPressedThisFrame) Open();
        }
        else if (glowRenderer != null && glowRenderer.enabled)
        {
            Animate(glowFrames, ref glowTimer, glowFrameTime, glowRenderer, true);
            glowRenderer.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, glowAlpha);
        }
    }

    static void Animate(Sprite[] frames, ref float timer, float frameTime, SpriteRenderer target, bool loop)
    {
        if (frames == null || frames.Length == 0 || target == null) return;
        timer += Time.deltaTime;
        int i = (int)(timer / Mathf.Max(0.01f, frameTime));
        if (loop) i %= frames.Length; else i = Mathf.Min(i, frames.Length - 1);
        target.sprite = frames[i];
    }

    /// <summary>Opens the chest (also callable from other scripts).</summary>
    public void Open()
    {
        if (Current != State.Closed) return;
        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        Current = State.Opening;
        if (prompt != null) prompt.SetActive(false);
        BestRarity = ComputeBestRarity();
        GlowColor = RarityColor(BestRarity);
        float gs = Pick(glowScale, BestRarity, 1f);
        if (glowRenderer != null) glowRenderer.transform.localScale = Vector3.one * gs;
        if (burstRenderer != null) burstRenderer.transform.localScale = Vector3.one * gs;

        for (int i = 0; i < openFrames.Length; i++)
        {
            chestRenderer.sprite = openFrames[i];
            if (i == glowStartFrame) { StartCoroutine(GlowRoutine()); StartCoroutine(BurstRoutine()); }
            yield return new WaitForSeconds(openFrameTime);
        }

        Current = State.Open;
        StartCoroutine(GiveLootRoutine());
        if (Opened != null) Opened(this);
    }

    IEnumerator GlowRoutine()
    {
        if (glowRenderer == null) yield break;
        glowRenderer.enabled = true; glowTimer = 0f; glowAlpha = 0f;
        for (float t = 0f; t < glowFadeTime; t += Time.deltaTime)
        {
            glowAlpha = Mathf.Clamp01(t / glowFadeTime);
            SetLight(glowAlpha);
            yield return null;
        }
        glowAlpha = 1f; SetLight(1f);
        yield return new WaitForSeconds(glowHoldTime);
        for (float t = 0f; t < glowFadeOutTime; t += Time.deltaTime)
        {
            glowAlpha = 1f - Mathf.Clamp01(t / glowFadeOutTime);
            SetLight(glowAlpha);
            yield return null;
        }
        glowAlpha = 0f; SetLight(0f);
        glowRenderer.enabled = false;
    }

    IEnumerator BurstRoutine()
    {
        if (burstRenderer == null || burstFrames == null || burstFrames.Length == 0) yield break;
        burstRenderer.enabled = true; burstRenderer.color = GlowColor;
        for (int i = 0; i < burstFrames.Length; i++)
        {
            burstRenderer.sprite = burstFrames[i];
            yield return new WaitForSeconds(burstFrameTime);
        }
        burstRenderer.enabled = false;
    }

    void SetLight(float k)
    {
        if (sceneLight == null) return;
        var t = sceneLight.GetType();
        var pc = t.GetProperty("color"); var pi = t.GetProperty("intensity");
        if (pc != null) pc.SetValue(sceneLight, GlowColor);
        if (pi != null) pi.SetValue(sceneLight, lightIntensity * Pick(lightScale, BestRarity, 1f) * k);
        sceneLight.enabled = k > 0.001f;
    }

    IEnumerator GiveLootRoutine()
    {
        if (!giveToInventory || playerInventory == null || loot == null) yield break;
        foreach (var l in loot)
        {
            if (l == null || l.item == null || l.amount <= 0) continue;
            int left = playerInventory.Add(l.item, l.amount);
            int got = l.amount - left;
            if (got > 0) LootToastUI.Show(l.item, got);               // the strip in the corner (if the scene has one)
            if (left > 0) Debug.Log("[Chest] " + l.item.displayName + ": " + left + " fikk ikke plass");
            yield return new WaitForSeconds(lootInterval);
        }
    }
}
