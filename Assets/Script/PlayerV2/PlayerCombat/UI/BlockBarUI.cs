using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skjold-baren (blå) + en ring bak skjold-ikonet som viser ventetiden før skjoldet kan løftes igjen.
/// Når spilleren har senket skjoldet, snurrer en lys ring rundt ikonet, og buen blir kortere og kortere til skjoldet er klart.
/// Da blinker ringen til og forsvinner. Ringen bygges av scriptet selv (ingen bilder trengs) og ligger like bak rammen.
/// </summary>
public class BlockBarUI : MonoBehaviour
{
    [SerializeField] private PlayerBlock playerBlock;
    [Tooltip("Fill-objektet. Trenger ingen sprite, bare en farge.")]
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Image fillImage;

    [Header("Farger")]
    [SerializeField] private Color normalColor = new Color(0.3f, 0.6f, 0.95f);
    [SerializeField] private Color exhaustedColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private float exhaustedBlinkSpeed = 6f;

    [SerializeField] private float fillSpeed = 12f;

    [Header("Ring bak skjold-ikonet")]
    [SerializeField] private bool showRaiseRing = true;
    [Tooltip("Rammen med skjold-ikonet (BlockBarBG). Tomt = to nivåer over fill.")]
    [SerializeField] private RectTransform frame;
    [Tooltip("Hvor ikonet sitter i rammen, som andel av bredde og høyde (0,0 = nede til venstre).")]
    [SerializeField] private Vector2 iconCenter = new Vector2(0.102f, 0.5f);
    [Tooltip("Ringens størrelse som andel av rammens høyde.")]
    [SerializeField] private float ringSize = 1.15f;
    [SerializeField] private Color ringColor = new Color(0.55f, 0.82f, 1f, 1f);
    [SerializeField] private Color ringBackColor = new Color(0.08f, 0.1f, 0.22f, 0.6f);
    [Tooltip("Grader per sekund ringen snurrer.")]
    [SerializeField] private float spinSpeed = 280f;

    private float current = 1f;
    private Image ringBack, ring, flash;
    private bool wasCooling;
    private float flashTimer;
    private static Sprite ringSprite;

    void Awake()
    {
        if (playerBlock == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerBlock = p.GetComponent<PlayerBlock>();
        }

        if (fillImage == null && fillRect != null)
            fillImage = fillRect.GetComponent<Image>();
    }

    void Update()
    {
        if (playerBlock == null || fillRect == null) return;

        current = Mathf.MoveTowards(current, playerBlock.StaminaPercent, fillSpeed * Time.deltaTime);

        // Styrer bredden via anchors i stedet for fillAmount.
        // anchorMin.x = 0 betyr at venstre kant alltid står stille.
        fillRect.anchorMin = new Vector2(0f, fillRect.anchorMin.y);
        fillRect.anchorMax = new Vector2(current, fillRect.anchorMax.y);
        fillRect.offsetMin = new Vector2(0f, fillRect.offsetMin.y);
        fillRect.offsetMax = new Vector2(0f, fillRect.offsetMax.y);

        if (fillImage != null)
        {
            if (playerBlock.IsExhausted)
            {
                float t = (Mathf.Sin(Time.time * exhaustedBlinkSpeed) + 1f) * 0.5f;
                fillImage.color = Color.Lerp(exhaustedColor, normalColor, t * 0.4f);
            }
            else
            {
                fillImage.color = normalColor;
            }
        }

        if (showRaiseRing) UpdateRing();
    }

    // ------------------------------------------------------------------------------------------ the ring
    static Sprite MakeRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int S = 64;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                px[y * S + x] = (d <= 31f && d >= 25f) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        t.SetPixels32(px); t.Apply();
        ringSprite = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }

    Image NewImage(string n, Transform parent, int sibling)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(sibling);
        var img = go.GetComponent<Image>();
        img.sprite = MakeRingSprite();
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return img;
    }

    void BuildRing()
    {
        if (frame == null && fillRect != null && fillRect.parent != null)
        {
            var up = fillRect.parent.parent as RectTransform;
            frame = up != null ? up : fillRect.parent as RectTransform;
        }
        if (frame == null || frame.parent == null) return;
        int idx = frame.GetSiblingIndex();
        ringBack = NewImage("ShieldRingBack", frame.parent, idx);
        ring = NewImage("ShieldRing", frame.parent, idx + 1);          // all just before the frame, so they are drawn behind the icon
        flash = NewImage("ShieldRingReady", frame.parent, idx + 2);
        ringBack.color = ringBackColor;
        ring.color = ringColor; ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360; ring.fillOrigin = 2; ring.fillClockwise = false;
        flash.color = new Color(ringColor.r, ringColor.g, ringColor.b, 0f);
        ringBack.enabled = ring.enabled = flash.enabled = false;
    }

    void PlaceRing(Image img, float scale)
    {
        var rt = (RectTransform)img.transform;
        Vector3 local = new Vector3(frame.rect.xMin + frame.rect.width * iconCenter.x, frame.rect.yMin + frame.rect.height * iconCenter.y, 0f);
        rt.position = frame.TransformPoint(local);
        float size = frame.rect.height * ringSize * scale;
        rt.sizeDelta = new Vector2(size, size);
    }

    void UpdateRing()
    {
        if (ring == null) { BuildRing(); if (ring == null) return; }
        float left = playerBlock.RaiseCooldownLeft, total = playerBlock.RaiseCooldownTotal;
        bool cooling = total > 0.001f && left > 0.001f;

        if (cooling)
        {
            ringBack.enabled = ring.enabled = true;
            ring.fillAmount = Mathf.Clamp01(left / total);
            ring.rectTransform.Rotate(0f, 0f, -spinSpeed * Time.unscaledDeltaTime);       // the arc spins and gets shorter
            PlaceRing(ringBack, 1f);
            PlaceRing(ring, 1f);
        }
        else
        {
            if (wasCooling) flashTimer = 0.25f;                                             // ready: a quick blink
            ringBack.enabled = ring.enabled = false;
        }
        wasCooling = cooling;

        if (flashTimer > 0f)
        {
            flashTimer -= Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(flashTimer / 0.25f);                              // 0 -> 1
            flash.enabled = true;
            PlaceRing(flash, Mathf.Lerp(1f, 1.35f, k));
            flash.color = new Color(ringColor.r, ringColor.g, ringColor.b, 1f - k);
        }
        else if (flash.enabled) flash.enabled = false;
    }
}
