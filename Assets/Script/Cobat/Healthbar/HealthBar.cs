using UnityEngine;

/// <summary>
/// Livbaren over fiendene, v3: stilen "Thin" (slank lilla bar med spisse ender, et skjold med level og navnet over).
/// Alt tegnes i koden som små pikselbilder (samme piksel-størrelse som fienden), så ingen bildefiler trengs:
///   - baren: ramme + rød fylling + en blek "tapt liv"-stripe som krymper etter et treff + hvit blink ved treff
///   - skjoldet til venstre viser level, navnet står over baren ("GOBLIN")
///   - flytende skadetall ("-2") ved treff (DamagePopup)
/// Scriptet ligger på selve fienden (som før). De gamle delene (fill/bakgrunn) skjules. Sett Display Name og Level i Inspector, eller
/// kall Setup("Goblin", 3) fra kode (en spawner kan gi nivå).
/// Feltene fra v1 er beholdt, så eksisterende prefaber virker uendret.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [Header("Gamle deler (skjules, beholdt så prefaben ikke mister koblingene)")]
    [SerializeField] private Transform fill;
    [SerializeField] private SpriteRenderer fillRenderer;
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Header("Navn og level")]
    [SerializeField] private string displayName = "Goblin";
    [Min(1)] [SerializeField] private int level = 1;
    [Tooltip("Farge på level-skjoldet (hvit = lilla som tegnet). Gi sterkere fiender en annen farge.")]
    [SerializeField] private Color badgeTint = Color.white;
    [Tooltip("Farge på navnet.")]
    [SerializeField] private Color nameColor = new Color(0.91f, 0.85f, 0.97f);
    [Tooltip("Farge på tallet i level-skjoldet.")]
    [SerializeField] private Color levelColor = new Color(1f, 0.32f, 0.26f);

    [Header("Vises")]
    [Tooltip("Skjul baren når fienden har fullt liv (og det er gått Show After Hit sekunder siden sist treff).")]
    [SerializeField] private bool hideWhenFull = true;
    [Tooltip("Navn og level står alltid over fienden, også når baren er skjult (full liv).")]
    [SerializeField] private bool alwaysShowName = true;
    [SerializeField] private float showAfterHit = 3f;
    [Tooltip("Hvor raskt fyllingen følger livet (0 = rett dit).")]
    [SerializeField] private float smoothSpeed = 10f;

    [Header("Plassering")]
    [Tooltip("Ekstra avstand opp fra toppen av fienden (i tegnede piksler).")]
    [SerializeField] private float liftPixels = 3f;
    [Tooltip("Sorting Order over fienden.")]
    [SerializeField] private int orderAbove = 20;

    [Header("Tapt liv og tall")]
    [SerializeField] private float lostDelay = 0.35f;
    [SerializeField] private float lostSpeed = 0.9f;
    [SerializeField] private bool damageNumbers = true;

    // -------------------------------------------------------------------------------- the art, drawn in code
    const int InnerW = 35, FrameW = 46, FrameH = 7;                   // the fill area is 35 x 3 art pixels, the frame (with its outline) 46 x 7
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    static readonly Color32 Edge = new Color32(0x16, 0x0d, 0x1c, 255);
    static readonly Color32 Plate = new Color32(0x44, 0x30, 0x60, 255);
    static readonly Color32 Slot = new Color32(0x1e, 0x0e, 0x16, 255);
    static readonly Color32 Gold = new Color32(0xf0, 0xc8, 0x40, 255);

    static readonly string[] Font =
    {
        "111 101 101 101 111", "010 110 010 010 111", "110 001 010 100 111", "110 001 010 001 110", "101 101 111 001 001",
        "111 100 110 001 110", "011 100 111 101 111", "111 001 010 100 100", "111 101 111 101 111", "111 101 111 001 110"
    };
    static readonly string[] Letters =
    {
        "010 101 111 101 101", "110 101 110 101 110", "011 100 100 100 011", "110 101 101 101 110", "111 100 110 100 111", "111 100 110 100 100",
        "011 100 101 101 011", "101 101 111 101 101", "111 010 010 010 111", "001 001 001 101 010", "101 101 110 101 101", "100 100 100 100 111",
        "101 111 111 101 101", "110 101 101 101 101", "010 101 101 101 010", "110 101 110 100 100", "010 101 101 111 011", "110 101 110 101 101",
        "011 100 010 001 110", "111 010 010 010 010", "101 101 101 101 111", "101 101 101 101 010", "101 101 111 111 101", "101 101 010 101 101",
        "101 101 010 010 010", "111 001 010 100 111"
    };

    static Sprite frameSprite, fillSprite, flashSprite, lostSprite;

    static Sprite MakeSprite(Color32[] px, int w, int h, Vector2 pivot)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, w, h), pivot, 100f);
    }

    static Color32[] Blank(int w, int h) { var a = new Color32[w * h]; for (int i = 0; i < a.Length; i++) a[i] = Clear; return a; }

    static void Outline(Color32[] px, int w, int h)
    {
        var add = new System.Collections.Generic.List<int>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a != 0) continue;
                bool n = (x > 0 && px[y * w + x - 1].a != 0) || (x < w - 1 && px[y * w + x + 1].a != 0) || (y > 0 && px[(y - 1) * w + x].a != 0) || (y < h - 1 && px[(y + 1) * w + x].a != 0);
                if (n) add.Add(y * w + x);
            }
        foreach (var i in add) px[i] = Edge;
    }

    static void BuildShared()
    {
        if (frameSprite != null) return;
        // the frame: a flat bar with pointed ends, a dark slot in it, a gold tip at both ends. y is up (row 0 = the bottom).
        var px = Blank(FrameW, FrameH);
        int[] left = { 3, 1, 0, 1, 3 }, right = { 40, 42, 43, 42, 40 };
        for (int r = 0; r < 5; r++)
            for (int x = left[r]; x <= right[r]; x++) px[(1 + r) * FrameW + (1 + x)] = Plate;
        for (int r = 1; r <= 3; r++)
            for (int x = 4; x <= 4 + InnerW; x++) px[(1 + r) * FrameW + (1 + x)] = Slot;
        px[(1 + 2) * FrameW + 1] = Gold; px[(1 + 2) * FrameW + (1 + 43)] = Gold;
        Outline(px, FrameW, FrameH);
        frameSprite = MakeSprite(px, FrameW, FrameH, Vector2.zero);

        // one pixel wide, three rows: the fill (light on top, dark at the bottom), the flash, the lost strip. They are scaled sideways.
        fillSprite = MakeSprite(new[] { new Color32(0xb0, 0x24, 0x28, 255), new Color32(0xe0, 0x40, 0x3a, 255), new Color32(0xff, 0x7a, 0x6a, 255) }, 1, 3, Vector2.zero);
        flashSprite = MakeSprite(new[] { new Color32(0xe8, 0xe8, 0xf0, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) }, 1, 3, Vector2.zero);
        lostSprite = MakeSprite(new[] { new Color32(0xc8, 0xb0, 0x70, 255), new Color32(0xf0, 0xe0, 0xa0, 255), new Color32(0xff, 0xf6, 0xd0, 255) }, 1, 3, Vector2.zero);
    }

    static void DrawGlyph(Color32[] px, int w, int h, int ox, int oy, string rows, Color32 c)
    {
        var r = rows.Split(' ');
        for (int j = 0; j < 5; j++)
            for (int i = 0; i < 3; i++)
                if (r[j][i] == '1')
                {
                    int x = ox + i, y = oy + (4 - j);
                    if (x >= 0 && x < w && y >= 0 && y < h) px[y * w + x] = c;
                }
    }

    static string Glyph(char ch)
    {
        ch = char.ToUpperInvariant(ch);
        if (ch >= '0' && ch <= '9') return Font[ch - '0'];
        if (ch >= 'A' && ch <= 'Z') return Letters[ch - 'A'];
        return null;
    }

    /// <summary>The name as a small picture: 3 x 5 letters with a dark edge.</summary>
    static Sprite MakeNameSprite(string text, Color32 col)
    {
        int n = Mathf.Max(1, text.Length);
        int w = n * 4 + 1, h = 7;
        var px = Blank(w, h);
        int cx = 1;
        foreach (char ch in text)
        {
            var g = Glyph(ch);
            if (g != null) DrawGlyph(px, w, h, cx, 1, g, col);
            cx += 4;
        }
        Outline(px, w, h);
        return MakeSprite(px, w, h, Vector2.zero);
    }

    /// <summary>The level shield: a purple shield with the number in gold. Wider for two digits.</summary>
    static Sprite MakeBadgeSprite(int level, Color32 digit)
    {
        string t = Mathf.Clamp(level, 1, 99).ToString();
        int inner = t.Length * 4 - 1;
        int sw = Mathf.Max(7, inner + 4);
        if (sw % 2 == 0) sw++;
        int w = sw + 2, h = 11;
        var px = Blank(w, h);
        var hi = new Color32(0x6a, 0x50, 0x88, 255); var dk = new Color32(0x2c, 0x1c, 0x40, 255);
        int half = sw / 2;
        for (int y = 0; y < 9; y++)
        {
            int rowFromTop = 8 - y;
            int span = rowFromTop <= 5 ? half : Mathf.Max(0, half - (rowFromTop - 5));
            for (int x = -span; x <= span; x++)
            {
                int xx = 1 + half + x;
                bool border = x == -span || x == span || rowFromTop == 0;
                px[(1 + y) * w + xx] = border ? hi : dk;
            }
        }
        int ox = 1 + (sw - inner) / 2;
        for (int i = 0; i < t.Length; i++) DrawGlyph(px, w, h, ox + i * 4, 4, Font[t[i] - '0'], digit);
        Outline(px, w, h);
        return MakeSprite(px, w, h, Vector2.zero);
    }

    // -------------------------------------------------------------------------------- the live bar
    private SpriteRenderer frameR, fillR, lostR, nameR, badgeR;
    private Transform plate;
    private float targetPercent = 1f, currentPercent = 1f, lostPercent = 1f;
    private float lostTimer, flashTimer, shownUntil;
    private int lastHealth = -1;
    private bool built;

    public string DisplayName { get { return displayName; } }
    public int Level { get { return level; } }

    void Awake() { Build(); }

    /// <summary>Gives the enemy another name and level (call from a spawner, for example).</summary>
    public void Setup(string name, int newLevel)
    {
        displayName = name; level = Mathf.Max(1, newLevel);
        if (built) Rebuild();
    }

    void Build()
    {
        if (built) return;
        built = true;
        BuildShared();
        // hide the old parts (unless one of them is this very object)
        if (fill != null && fill.gameObject != gameObject) fill.gameObject.SetActive(false);
        if (backgroundRenderer != null && backgroundRenderer.gameObject != gameObject) backgroundRenderer.gameObject.SetActive(false);
        if (fillRenderer != null && fillRenderer.gameObject != gameObject) fillRenderer.gameObject.SetActive(false);

        var host = new GameObject("Nameplate");
        plate = host.transform;
        plate.SetParent(transform, false);

        // the sorting layer of the enemy's own picture
        SpriteRenderer own = null;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>())
            if (r != null && r.gameObject.activeInHierarchy && !r.transform.IsChildOf(plate)) { own = r; break; }
        int layerId = own != null ? own.sortingLayerID : 0;
        int baseOrder = (own != null ? own.sortingOrder : 0) + orderAbove;

        frameR = NewRenderer("Frame", frameSprite, layerId, baseOrder);
        lostR = NewRenderer("Lost", lostSprite, layerId, baseOrder + 1);
        fillR = NewRenderer("Fill", fillSprite, layerId, baseOrder + 2);
        nameR = NewRenderer("Name", null, layerId, baseOrder + 3);
        badgeR = NewRenderer("Level", null, layerId, baseOrder + 3);
        Rebuild();
        Place(own);
        Apply();
    }

    SpriteRenderer NewRenderer(string n, Sprite s, int layerId, int order)
    {
        var go = new GameObject(n, typeof(SpriteRenderer));
        go.transform.SetParent(plate, false);
        var r = go.GetComponent<SpriteRenderer>();
        r.sprite = s; r.sortingLayerID = layerId; r.sortingOrder = order;
        return r;
    }

    void Rebuild()
    {
        nameR.sprite = MakeNameSprite(displayName, nameColor);
        nameR.color = Color.white;
        badgeR.sprite = MakeBadgeSprite(level, levelColor);
        badgeR.color = badgeTint;
        PlaceParts();
    }

    /// <summary>Parts inside the plate, in art pixels (0.01 units). The plate's origin is the left end of the bar.</summary>
    void PlaceParts()
    {
        const float U = 0.01f;
        frameR.transform.localPosition = Vector3.zero;
        fillR.transform.localPosition = new Vector3(5f * U, 2f * U, 0f);
        lostR.transform.localPosition = new Vector3(5f * U, 2f * U, 0f);
        float bw = badgeR.sprite != null ? badgeR.sprite.rect.width : 9f;
        badgeR.transform.localPosition = new Vector3(-(bw - 1f) * U, -2f * U, 0f);
        nameR.transform.localPosition = new Vector3(5f * U, FrameH * U, 0f);
    }

    /// <summary>Puts the plate above the top of the enemy's picture, centred on it.</summary>
    void Place(SpriteRenderer own)
    {
        const float U = 0.01f;
        float topLocal = 1.2f, centerX = 0f;
        if (own != null)
        {
            Bounds b = own.bounds;
            Vector3 top = transform.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
            topLocal = top.y; centerX = top.x;
        }
        plate.localPosition = new Vector3(centerX - (FrameW * 0.5f) * U, topLocal + liftPixels * U, 0f);
    }

    void LateUpdate()
    {
        // never turned or mirrored with the enemy
        transform.rotation = Quaternion.identity;
        Vector3 s = transform.localScale;
        if (s.x < 0f) transform.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);
        if (!built) return;

        currentPercent = smoothSpeed <= 0f ? targetPercent : Mathf.MoveTowards(currentPercent, targetPercent, smoothSpeed * Time.deltaTime);
        if (lostTimer > 0f) lostTimer -= Time.deltaTime;
        else lostPercent = Mathf.MoveTowards(lostPercent, currentPercent, lostSpeed * Time.deltaTime);
        if (lostPercent < currentPercent) lostPercent = currentPercent;
        if (flashTimer > 0f) flashTimer -= Time.deltaTime;
        Apply();
    }

    /// <summary>Sets the health without any hit effect (used when the enemy gets its level and max health at spawn).</summary>
    public void InitHealth(int current, int max)
    {
        lastHealth = current;
        targetPercent = currentPercent = lostPercent = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        lostTimer = 0f; flashTimer = 0f;
    }

    public void SetHealth(int current, int max)
    {
        float newTarget = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        if (lastHealth >= 0 && current < lastHealth)
        {
            lostTimer = lostDelay;
            lostPercent = Mathf.Max(lostPercent, targetPercent);
            flashTimer = 0.1f;
            shownUntil = Time.time + showAfterHit;
            if (damageNumbers && plate != null)
                DamagePopup.Spawn(plate.position + new Vector3(FrameW * 0.5f * 0.01f * Mathf.Abs(transform.lossyScale.x), 0.35f, 0f), lastHealth - current, DamagePopup.EnemyHit);
        }
        else if (lastHealth >= 0 && current > lastHealth) shownUntil = Time.time + showAfterHit;
        lastHealth = current;
        targetPercent = newTarget;
    }

    void Apply()
    {
        const float U = 0.01f;
        int fw = Mathf.RoundToInt(InnerW * currentPercent);
        int lw = Mathf.RoundToInt(InnerW * lostPercent);
        fillR.transform.localScale = new Vector3(Mathf.Max(0, fw), 1f, 1f);
        fillR.enabled = fw > 0;
        fillR.sprite = flashTimer > 0f ? flashSprite : fillSprite;
        bool showLost = lw > fw;
        lostR.enabled = showLost;
        if (showLost)
        {
            lostR.transform.localPosition = new Vector3((5f + fw) * U, 2f * U, 0f);
            lostR.transform.localScale = new Vector3(lw - fw, 1f, 1f);
        }
        bool visible = !hideWhenFull || currentPercent < 0.999f || Time.time < shownUntil || lostPercent > currentPercent + 0.001f;
        frameR.enabled = visible;
        if (!visible) { fillR.enabled = false; lostR.enabled = false; }
        bool nameOn = visible || alwaysShowName;
        nameR.enabled = nameOn; badgeR.enabled = nameOn;
        if (plate.gameObject.activeSelf != nameOn) plate.gameObject.SetActive(nameOn);
    }
}
