using System.Collections;
using UnityEngine;

/// <summary>
/// Flytende tall når noen mister liv (eller får liv): "-2" i rødt over spilleren, "-3" i hvitt/gult over en fiende, "+2" i grønt ved helbredelse.
/// Tegnet med et lite pikselsett (5 x 7) som lages i koden, så ingen bilder trengs. Bruk:
///     DamagePopup.Spawn(worldPosition, 2, DamagePopup.EnemyHit);
/// Tallet spretter litt frem, stiger og toner ut. Størrelsen vokser litt med hvor mye skade det er.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    public static readonly Color EnemyHit = new Color(1f, 0.93f, 0.55f);
    public static readonly Color PlayerHit = new Color(1f, 0.28f, 0.26f);
    public static readonly Color Heal = new Color(0.45f, 1f, 0.5f);

    // 5 x 7 digits, 1 = ink
    static readonly string[] Glyphs =
    {
        "01110 10001 10011 10101 11001 10001 01110",   // 0
        "00100 01100 00100 00100 00100 00100 01110",   // 1
        "01110 10001 00001 00010 00100 01000 11111",   // 2
        "11110 00001 00001 01110 00001 00001 11110",   // 3
        "00010 00110 01010 10010 11111 00010 00010",   // 4
        "11111 10000 11110 00001 00001 10001 01110",   // 5
        "00110 01000 10000 11110 10001 10001 01110",   // 6
        "11111 00001 00010 00100 01000 01000 01000",   // 7
        "01110 10001 10001 01110 10001 10001 01110",   // 8
        "01110 10001 10001 01111 00001 00010 01100",   // 9
        "00000 00000 00000 11111 00000 00000 00000",   // - (10)
        "00000 00100 00100 11111 00100 00100 00000",   // + (11)
    };
    const int GW = 7, GH = 9;                          // 5 x 7 + a 1 px outline round it

    static Sprite[] glyphSprites;
    static Texture2D atlas;

    static void Build()
    {
        if (glyphSprites != null && glyphSprites[0] != null) return;
        int n = Glyphs.Length;
        atlas = new Texture2D(GW * n, GH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[GW * n * GH];
        var clear = new Color32(0, 0, 0, 0); var ink = new Color32(255, 255, 255, 255); var edge = new Color32(20, 8, 12, 255);
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        for (int g = 0; g < n; g++)
        {
            var rows = Glyphs[g].Split(' ');
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 5; x++)
                {
                    if (rows[y][x] != '1') continue;
                    int gx = g * GW + 1 + x, gy = GH - 2 - y;                    // flip: texture y goes up
                    px[gy * (GW * n) + gx] = ink;
                }
            // outline round every ink pixel
            for (int y = 0; y < GH; y++)
                for (int x = 0; x < GW; x++)
                {
                    int idx = y * (GW * n) + g * GW + x;
                    if (px[idx].a != 0) continue;
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1 && !near; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || xx >= GW || yy < 0 || yy >= GH) continue;
                            if (px[yy * (GW * n) + g * GW + xx].a == 255 && px[yy * (GW * n) + g * GW + xx].r == 255) near = true;
                        }
                    if (near) px[idx] = edge;
                }
        }
        atlas.SetPixels32(px); atlas.Apply();
        glyphSprites = new Sprite[n];
        for (int g = 0; g < n; g++) glyphSprites[g] = Sprite.Create(atlas, new Rect(g * GW, 0, GW, GH), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Shows a number. amount > 0 with sign: "-" for damage, "+" for healing (heal = true).</summary>
    public static void Spawn(Vector3 position, int amount, Color color, bool heal = false, string sortingLayer = "Default")
    {
        if (amount <= 0) return;
        Build();
        var go = new GameObject("DamagePopup");
        position.y += 0.28f * active;                                     // a number that is still rising makes the next one start higher
        go.transform.position = position;
        active++;
        string text = (heal ? "+" : "-") + amount.ToString();
        float step = GW * 0.01f;
        for (int i = 0; i < text.Length; i++)
        {
            int g = text[i] == '-' ? 10 : text[i] == '+' ? 11 : text[i] - '0';
            var c = new GameObject("c" + i, typeof(SpriteRenderer));
            c.transform.SetParent(go.transform, false);
            c.transform.localPosition = new Vector3((i - (text.Length - 1) / 2f) * step, 0f, 0f);
            var sr = c.GetComponent<SpriteRenderer>();
            sr.sprite = glyphSprites[g]; sr.color = color; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 300;
        }
        var p = go.AddComponent<DamagePopup>();
        p.baseScale = 3f + Mathf.Min(amount, 6) * 0.35f;
        p.drift = Random.Range(-0.45f, 0.45f);
    }

    static int active;
    float baseScale = 3f, drift;
    const float Life = 0.95f;

    IEnumerator Start()
    {
        Vector3 start = transform.position;
        var rends = GetComponentsInChildren<SpriteRenderer>();
        float t = 0f;
        while (t < Life)
        {
            t += Time.deltaTime;
            float k = t / Life;
            float rise = 1.15f * (1f - (1f - k) * (1f - k));                      // fast at first, then slower
            transform.position = start + new Vector3(drift * k, rise, 0f);
            float pop = k < 0.12f ? Mathf.Lerp(0.6f, 1.35f, k / 0.12f) : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((k - 0.12f) / 0.15f));
            float s = baseScale * pop;
            transform.localScale = new Vector3(s, s, 1f);
            float a = k < 0.65f ? 1f : 1f - (k - 0.65f) / 0.35f;
            foreach (var r in rends) if (r != null) { var c = r.color; c.a = a; r.color = c; }
            yield return null;
        }
        Destroy(gameObject);
    }

    void OnDestroy() { active = Mathf.Max(0, active - 1); }
}
