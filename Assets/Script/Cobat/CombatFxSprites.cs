using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pikselkunst for kamp-effektene, tegnet i koden (ingen bildefiler): en tydelig månesigd-sveipe i flere lag
/// (hvit kjerne, lys midt, farget kant og en svak glød) som vokser fram og toner ut, og en stjerne-krasj med lange stråler.
/// Rammene lages én gang per palett og gjenbrukes.
/// </summary>
public static class FxSprites
{
    public enum Pal { Player, Enemy, Hot }

    struct Colors { public Color32 core, mid, edge, glow; }

    static Colors Of(Pal p)
    {
        switch (p)
        {
            case Pal.Enemy: return new Colors { core = new Color32(255, 246, 222, 255), mid = new Color32(255, 200, 124, 255), edge = new Color32(232, 92, 62, 255), glow = new Color32(200, 60, 52, 150) };
            case Pal.Hot:   return new Colors { core = new Color32(255, 255, 235, 255), mid = new Color32(255, 220, 110, 255), edge = new Color32(255, 128, 40, 255), glow = new Color32(230, 70, 40, 160) };
            default:        return new Colors { core = new Color32(255, 255, 255, 255), mid = new Color32(222, 238, 255, 255), edge = new Color32(148, 184, 232, 255), glow = new Color32(126, 158, 214, 150) };
        }
    }

    static readonly Dictionary<Pal, Sprite[]> slash = new Dictionary<Pal, Sprite[]>();
    static readonly Dictionary<Pal, Sprite[]> impact = new Dictionary<Pal, Sprite[]>();

    public const int SlashSize = 72;
    public const float SlashRadiusPx = 27f;          // the middle of the crescent, in sprite pixels
    public const float SlashArcDegrees = 120f;        // from +60 (over the aim line) to -60 (under it)
    public const int ImpactSize = 48;

    static Sprite MakeSprite(Color32[] px, int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    // ------------------------------------------------------------------------------------------ the sweep
    public static Sprite[] Slash(Pal pal)
    {
        Sprite[] r;
        if (slash.TryGetValue(pal, out r) && r != null && r[0] != null) return r;
        const int S = SlashSize, F = 7;
        var col = Of(pal);
        r = new Sprite[F];
        float c = (S - 1) * 0.5f;
        for (int f = 0; f < F; f++)
        {
            var px = new Color32[S * S];
            bool sweeping = f <= 3;
            float p = sweeping ? (f + 1) / 4f : 1f;
            float ease = 1f - (1f - p) * (1f - p);
            float head = Mathf.Lerp(60f, -60f, ease);
            float trail = Mathf.Lerp(40f, 100f, ease);
            float erode = sweeping ? 0f : (f - 3) / 4f;                   // the tail eats away from the far end
            float widthMul = sweeping ? 1f : 1f - (f - 3) * 0.27f;
            const float Tmax = 14f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - c, dy = y - c;
                    float rad = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    float a = ang - head;
                    Color32 outc = default(Color32);
                    if (a >= 0f && a <= trail)
                    {
                        float u = 1f - a / trail;                          // 1 at the head, 0 at the tail
                        if (u >= erode)
                        {
                            float w = Tmax * widthMul * Mathf.Pow(u, 0.7f) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(u, 8f)));
                            float rm = SlashRadiusPx + 2.5f * Mathf.Sin(u * Mathf.PI);          // a little bulge in the middle: a crescent, not a ring
                            float d = Mathf.Abs(rad - rm);
                            if (w > 0.6f)
                            {
                                if (d <= w * 0.2f) outc = col.core;
                                else if (d <= w * 0.42f) outc = col.mid;
                                else if (d <= w * 0.5f) outc = col.edge;
                                else if (d <= w * 0.66f && ((x + y) & 1) == 0) outc = col.glow;
                            }
                            else if (d <= 0.7f) outc = col.core;
                        }
                    }
                    // thin speed lines outside and inside the main arc
                    if (outc.a == 0 && f >= 1 && f <= 4)
                    {
                        float a2 = ang - head;
                        if (a2 >= trail * 0.25f && a2 <= trail * 0.85f && ((x * 3 + y) % 3) != 0)
                        {
                            float d1 = Mathf.Abs(rad - (SlashRadiusPx + 12.5f)), d2 = Mathf.Abs(rad - (SlashRadiusPx - 13f));
                            if (d1 <= 0.5f || (d2 <= 0.5f && a2 > trail * 0.45f)) outc = col.glow;
                        }
                    }
                    px[y * S + x] = outc;
                }
            r[f] = MakeSprite(px, S, S);
        }
        slash[pal] = r;
        return r;
    }

    // ------------------------------------------------------------------------------------------ the crash star
    public static Sprite[] Impact(Pal pal)
    {
        Sprite[] r;
        if (impact.TryGetValue(pal, out r) && r != null && r[0] != null) return r;
        const int S = ImpactSize, F = 6;
        var col = Of(pal);
        float[] longLen = { 15f, 22f, 23f, 20f, 15f, 8f };
        float[] shortLen = { 8f, 12f, 13f, 11f, 8f, 3f };
        float[] disc = { 7f, 6f, 4.5f, 3f, 2f, 0f };
        r = new Sprite[F];
        float c = (S - 1) * 0.5f;
        for (int f = 0; f < F; f++)
        {
            var px = new Color32[S * S];
            Color32 body = f <= 1 ? col.core : f == 2 ? col.mid : f == 3 ? col.edge : col.glow;
            Color32 inner = f <= 2 ? col.core : col.mid;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - c, dy = y - c;
                    float rad = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    Color32 o = default(Color32);
                    if (rad <= disc[f]) o = col.core;
                    else
                    {
                        // 4 long rays (the cross), 4 diagonal ones that are a bit shorter, and 8 thin short ones in between
                        float a90 = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 90f) * 90f));
                        float a45 = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round((ang - 45f) / 90f) * 90f + 45f));
                        float a22 = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round((ang - 22.5f) / 45f) * 45f + 22.5f));
                        float lenC = longLen[f], lenD = longLen[f] * 0.72f, lenS = shortLen[f];
                        float perpC = rad * Mathf.Sin(a90 * Mathf.Deg2Rad), perpD = rad * Mathf.Sin(a45 * Mathf.Deg2Rad), perpS = rad * Mathf.Sin(a22 * Mathf.Deg2Rad);
                        float hC = 2.6f * (1f - rad / Mathf.Max(1f, lenC)) + 0.35f;
                        float hD = 2.2f * (1f - rad / Mathf.Max(1f, lenD)) + 0.35f;
                        float hS = 1.1f * (1f - rad / Mathf.Max(1f, lenS)) + 0.25f;
                        if (rad < lenC && perpC <= hC) o = perpC <= hC * 0.4f ? inner : body;
                        else if (rad < lenD && perpD <= hD) o = perpD <= hD * 0.4f ? inner : body;
                        else if (rad < lenS && perpS <= hS) o = body;
                    }
                    px[y * S + x] = o;
                }
            r[f] = MakeSprite(px, S, S);
        }
        impact[pal] = r;
        return r;
    }
}

/// <summary>Spiller en rekke bilder én gang på en SpriteRenderer og fjerner seg selv.</summary>
public class FxFlip : MonoBehaviour
{
    Sprite[] frames; float fps; SpriteRenderer sr; float t;

    public static FxFlip Spawn(Sprite[] frames, float fps, string layer, int order, Vector3 worldPos, float scale, float rotationZ = 0f, Transform parent = null)
    {
        var go = new GameObject("FxFlip", typeof(SpriteRenderer));
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        var f = go.AddComponent<FxFlip>();
        f.frames = frames; f.fps = Mathf.Max(1f, fps); f.sr = go.GetComponent<SpriteRenderer>();
        f.sr.sortingLayerName = layer; f.sr.sortingOrder = order; f.sr.sprite = frames[0];
        return f;
    }

    void Update()
    {
        t += Time.deltaTime;
        int i = Mathf.FloorToInt(t * fps);
        if (i >= frames.Length) { Destroy(gameObject); return; }
        sr.sprite = frames[i];
    }
}

/// <summary>
/// Kamp-følelse: et lite stopp i bildet ved treff (hit-stop) og et kort risting av kameraet. Begge er korte og kan skrus av/ned.
/// </summary>
public static class CombatFeel
{
    public static bool Enabled = true;
    /// <summary>0 = av, 1 = som standard, 2 = dobbelt så sterkt.</summary>
    public static float Strength = 1f;

    static Runner runner;
    static float freezeUntil;          // real time
    static bool frozen;
    static float shakeAmount, shakeUntil, shakeStart;
    static Vector3 lastOffset;

    class Runner : MonoBehaviour
    {
        void Update()
        {
            if (frozen && Time.unscaledTime >= freezeUntil) { Time.timeScale = 1f; frozen = false; }
        }
    }

    static void EnsureRunner()
    {
        if (runner != null) return;
        var go = new GameObject("CombatFeel");
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    /// <summary>Stopper spillet i så mange sekunder (det går i nesten stillstand).</summary>
    public static void HitStop(float seconds)
    {
        if (!Enabled || Strength <= 0f) return;
        seconds *= Strength;
        if (seconds <= 0.005f) return;
        EnsureRunner();
        if (!frozen)
        {
            if (Time.timeScale < 0.99f) return;            // the game is paused or in slow motion for another reason: leave it
            Time.timeScale = 0.04f; frozen = true;
        }
        freezeUntil = Mathf.Max(freezeUntil, Time.unscaledTime + seconds);
    }

    // ----- camera shake: two small components on the camera, one that removes last frame's offset before the follow script, one that adds the new
    [DefaultExecutionOrder(-1000)] class ShakeBegin : MonoBehaviour { void LateUpdate() { transform.position -= lastOffset; lastOffset = Vector3.zero; } }
    [DefaultExecutionOrder(1000)]
    class ShakeEnd : MonoBehaviour
    {
        void LateUpdate()
        {
            if (Time.unscaledTime >= shakeUntil) return;
            float k = 1f - (Time.unscaledTime - shakeStart) / Mathf.Max(0.01f, shakeUntil - shakeStart);
            float a = shakeAmount * k * k;
            var o = new Vector3(Random.Range(-1f, 1f) * a, Random.Range(-1f, 1f) * a, 0f);
            transform.position += o; lastOffset = o;
        }
    }

    public static void Shake(float amount, float seconds)
    {
        if (!Enabled || Strength <= 0f) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (cam.GetComponent<ShakeBegin>() == null) { cam.gameObject.AddComponent<ShakeBegin>(); cam.gameObject.AddComponent<ShakeEnd>(); }
        amount *= Strength;
        if (Time.unscaledTime < shakeUntil && shakeAmount > amount) return;      // a bigger shake is already going
        shakeAmount = amount; shakeStart = Time.unscaledTime; shakeUntil = Time.unscaledTime + seconds;
    }
}
