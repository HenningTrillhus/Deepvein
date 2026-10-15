using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Små kamp-effekter tegnet i koden (ingen bilder): støv, gnister og "krasj" (en kort stjerne ved treff).
///   CombatFx.Impact(pos)        krasj: en lys stjerne som flasker ut og forsvinner (vanlig treff)
///   CombatFx.Sparks(pos, dir)   gnister som spruter ut og faller
///   CombatFx.Dust(feet, away)   støvskyer langs bakken som drifter bort fra dyttet
///   CombatFx.Clang(pos, dir)    skjoldtreff: liten krasj + gnister
///   CombatFx.Parry(pos, dir)    parry: stor krasj + ring + mange gnister
/// Alt er pikselfirkanter i samme størrelse som en tegnet piksel på fiendene (Pixel = 0.03 units).
/// </summary>
public static class CombatFx
{
    public static string SortingLayer = "Default";
    public static int SortingOrder = 250;
    public static float Pixel = 0.03f;
    public static bool Enabled = true;
    /// <summary>1 = som tegnet først, lavere = færre og mindre partikler. 0.7 er rolig.</summary>
    public static float Intensity = 0.7f;

    static Sprite square;

    static Sprite Square()
    {
        if (square != null) return square;
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        t.SetPixel(0, 0, Color.white); t.Apply();
        square = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);     // 0.01 unit per pixel, scaled up
        return square;
    }

    // ------------------------------------------------------------------------------------------ the public calls
    /// <summary>Crash: a star of rays flashing out from the hit point (a drawn animation), with a short hit-stop and a little shake.</summary>
    public static void Impact(Vector3 pos, float size = 1f, bool hot = false)
    {
        if (!Enabled) return;
        var pal = hot ? FxSprites.Pal.Hot : FxSprites.Pal.Player;
        float scale = Pixel * 100f * 0.85f * Mathf.Lerp(1f, size, 0.9f) * (0.8f + 0.2f * Intensity);        // one drawn pixel = about one Pixel
        FxFlip.Spawn(FxSprites.Impact(pal), 26f, SortingLayer, SortingOrder + 5, pos, scale, Random.Range(-12f, 12f));
        CombatFeel.HitStop(hot ? 0.07f : 0.04f);
        CombatFeel.Shake(hot ? 0.07f : 0.035f, hot ? 0.16f : 0.1f);
    }

    public static void Sparks(Vector3 pos, Vector2 dir, int count = 8, float coneDegrees = 80f, float speed = 3.2f)
    {
        if (!Enabled) return;
        count = Mathf.Max(1, Mathf.RoundToInt(count * Intensity));
        speed *= 0.85f;
        var b = NewBurst(pos);
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
        float baseAng = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Color[] cols = { new Color(1f, 0.95f, 0.6f), new Color(1f, 0.75f, 0.25f), new Color(1f, 0.5f, 0.15f), Color.white };
        for (int i = 0; i < count; i++)
        {
            float a = (baseAng + Random.Range(-coneDegrees, coneDegrees) * 0.5f) * Mathf.Deg2Rad;
            float s = speed * Random.Range(0.55f, 1.2f);
            b.Add(new P
            {
                size = Random.value < 0.4f ? 2.4f : 1.5f, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, gravity = -7f, drag = 1.5f,
                life = Random.Range(0.35f, 0.65f), color = cols[Random.Range(0, cols.Length)], fade = true
            });
        }
    }

    /// <summary>Dust puffs at the feet, drifting away. awaySign = +1 right, -1 left (the way the thing was pushed).</summary>
    public static void Dust(Vector3 feet, int awaySign, int count = 4)
    {
        if (!Enabled) return;
        count = Mathf.Max(1, Mathf.RoundToInt(count * Intensity));
        var b = NewBurst(feet);
        Color[] cols = { new Color(0.78f, 0.7f, 0.58f, 0.85f), new Color(0.66f, 0.58f, 0.48f, 0.85f), new Color(0.88f, 0.82f, 0.7f, 0.8f) };
        for (int i = 0; i < count; i++)
        {
            float s = Random.Range(0.4f, 1.1f);
            b.Add(new P
            {
                size = Random.Range(2f, 3.2f) * Intensity + 0.8f, grow = Random.Range(2f, 3.5f),
                pos = new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(0f, 0.05f)),
                vel = new Vector2(awaySign * s * Random.Range(0.4f, 1f) + Random.Range(-0.25f, 0.25f), Random.Range(0.3f, 0.7f)), drag = 3f,
                life = Random.Range(0.5f, 0.8f), color = cols[Random.Range(0, cols.Length)], fade = true
            });
        }
    }

    /// <summary>A shield hit: a small crash and a spray of sparks flying back along dir.</summary>
    public static void Clang(Vector3 pos, Vector2 dir)
    {
        Impact(pos, 0.8f, false);
        Sparks(pos, dir, 8, 90f, 3.4f);
    }

    /// <summary>A parry: a big crash, a ring and plenty of sparks.</summary>
    public static void Parry(Vector3 pos, Vector2 dir)
    {
        if (!Enabled) return;
        Impact(pos, 1.2f, true);
        Sparks(pos, dir, 10, 150f, 3.6f);
        var b = NewBurst(pos);
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            b.Add(new P { size = 1.5f, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f, drag = 6f, life = 0.28f, color = new Color(1f, 0.98f, 0.8f), fade = true });
        }
    }

    // ------------------------------------------------------------------------------------------ the slash
    public static Color PlayerSlash = new Color(0.9f, 0.96f, 1f, 0.9f);
    public static Color EnemySlash = new Color(1f, 0.58f, 0.42f, 0.9f);

    /// <summary>
    /// A swoosh behind the sword: a crescent that follows the sword along its swing (start -> end angle, degrees), thick at the sword and
    /// thinning into a tail. It is a child of follow (the player or enemy), so it moves with them.
    /// </summary>
    public static void Slash(Transform follow, Vector2 originLocal, float startAngle, float endAngle, float duration, float radius,
                             AnimationCurve curve, Color color, int order, float tail = 0.11f)
    {
        if (!Enabled || follow == null) return;
        // the warm palette for the enemy colour, the cool one otherwise
        var pal = color.r > color.b + 0.15f ? FxSprites.Pal.Enemy : FxSprites.Pal.Player;
        var frames = FxSprites.Slash(pal);
        float aim = (startAngle + endAngle) * 0.5f;
        // always from above and down, also when it swings to the left (the picture is mirrored then)
        bool down = Mathf.Cos(aim * Mathf.Deg2Rad) >= 0f;
        Vector3 origin = follow.position + new Vector3(originLocal.x, originLocal.y, 0f);        // originLocal is a world offset from follow
        // the picture is FxSprites.SlashRadiusPx big in the middle of the crescent: scale it so that is the wanted radius (world units)
        float scale = radius * 100f / FxSprites.SlashRadiusPx;
        float total = Mathf.Max(0.12f, duration + 0.07f);
        // NOT a child of the player/enemy: if that object is mirrored (scale x = -1 when it looks left) a child would be mirrored as well,
        // and the slash to the left was drawn as if it was a slash to the right. It follows the position instead.
        var fx = FxFlip.Spawn(frames, frames.Length / total, SortingLayer, order, origin, scale, aim, null);
        var t = fx.transform;
        t.localScale = new Vector3(scale, (down ? 1f : -1f) * scale, 1f);
        t.position = origin;
        t.rotation = Quaternion.Euler(0f, 0f, aim);
        var f = fx.gameObject.AddComponent<SlashFollow>();
        f.target = follow; f.offset = (Vector3)originLocal;
    }

    class SlashFollow : MonoBehaviour
    {
        public Transform target; public Vector3 offset;
        void LateUpdate() { if (target != null) transform.position = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z); }
    }

    // ------------------------------------------------------------------------------------------ the engine
    struct P
    {
        public float size, grow, life, gravity, drag, angle, length, maxLength, thick;
        public Vector2 pos, vel;
        public Color color;
        public bool fade, ray, flash;
    }

    static Burst NewBurst(Vector3 pos)
    {
        var go = new GameObject("CombatFx");
        go.transform.position = pos;
        return go.AddComponent<Burst>();
    }

    class Burst : MonoBehaviour
    {
        class Live { public P p; public Transform t; public SpriteRenderer sr; public float age; public Vector2 pos, vel; }
        readonly List<Live> live = new List<Live>();
        float maxLife;

        public void Add(P p)
        {
            var go = new GameObject("p", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = Square(); sr.sortingLayerName = SortingLayer; sr.sortingOrder = SortingOrder; sr.color = p.color;
            var l = new Live { p = p, t = go.transform, sr = sr, pos = p.pos, vel = p.vel };
            live.Add(l);
            maxLife = Mathf.Max(maxLife, p.life);
            Apply(l, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < live.Count; i++)
            {
                var l = live[i];
                l.age += dt;
                if (l.age >= l.p.life) { if (l.t != null) l.t.gameObject.SetActive(false); continue; }
                l.vel.y += l.p.gravity * dt;
                if (l.p.drag > 0f) l.vel *= Mathf.Max(0f, 1f - l.p.drag * dt);
                l.pos += l.vel * dt;
                Apply(l, l.age / l.p.life);
            }
            if (live.Count > 0)
            {
                bool any = false;
                foreach (var l in live) if (l.age < l.p.life) { any = true; break; }
                if (!any) Destroy(gameObject);
            }
        }

        void Apply(Live l, float k)
        {
            float u = Pixel * 100f;                                   // the 1 px sprite is 0.01 units; this is the scale for one art pixel
            var q = l.p;
            if (q.ray)
            {
                float len = k < 0.5f ? Mathf.Lerp(0f, q.maxLength, k / 0.5f) : Mathf.Lerp(q.maxLength, 0f, (k - 0.5f) / 0.5f);
                l.t.localPosition = new Vector3(Mathf.Cos(q.angle * Mathf.Deg2Rad) * (len * 0.5f + 1f) * Pixel, Mathf.Sin(q.angle * Mathf.Deg2Rad) * (len * 0.5f + 1f) * Pixel, 0f);
                l.t.localRotation = Quaternion.Euler(0f, 0f, q.angle);
                l.t.localScale = new Vector3(Mathf.Max(0.01f, len) * u, q.thick * u, 1f);
                return;
            }
            float size = Mathf.Max(0.1f, q.size + q.grow * k * q.life);
            l.t.localPosition = new Vector3(l.pos.x, l.pos.y, 0f);
            l.t.localScale = new Vector3(size * u, size * u, 1f);
            if (q.fade || q.flash)
            {
                var c = q.color; c.a *= 1f - k * k;
                l.sr.color = c;
            }
        }
    }
}
