using UnityEngine;

/// <summary>
/// Effects for the air slam, drawn in code (no picture files), each its own small object so they can be turned off or changed:
///   Windup(follow)     a little swoosh as the sword goes up
///   SpeedLines(follow) vertical streaks on the player while he falls (returns the object; destroy it when the fall ends)
///   Strike(follow)     the big crescent as the sword comes down
///   Land(feet)         impact star, sparks, a flat shock wave along the ground, steam of dust, flying rubble and a crack that fades
/// Uses CombatFx for the star, sparks and dust, so it follows CombatFx.Enabled / Intensity.
/// </summary>
public static class AirSlamFx
{
    static Sprite[] speedFrames, shockFrames;
    static Sprite crack, square;

    static Sprite Make(Color32[] px, int w, int h, Vector2 pivot)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, w, h), pivot, 100f);
    }
    static Color32[] Blank(int w, int h) { var a = new Color32[w * h]; return a; }
    static void Put(Color32[] px, int w, int h, int x, int y, Color32 c) { if (x >= 0 && x < w && y >= 0 && y < h) px[y * w + x] = c; }

    static Sprite Square()
    {
        if (square != null) return square;
        square = Make(new[] { new Color32(255, 255, 255, 255) }, 1, 1, new Vector2(0.5f, 0.5f));
        return square;
    }

    // ------------------------------------------------------------------------------------------ the pictures
    static Sprite[] SpeedFrames()
    {
        if (speedFrames != null) return speedFrames;
        const int W = 26, H = 44;
        speedFrames = new Sprite[3];
        var rnd = new System.Random(5);
        for (int f = 0; f < 3; f++)
        {
            var px = Blank(W, H);
            for (int k = 0; k < 7; k++)
            {
                int x = 1 + (k * 4 + f * 2) % (W - 2);
                int y0 = (int)((k * 13 + f * 9) % (H - 14));
                int len = 7 + (k * 5 + f) % 6;
                var c = k % 2 == 0 ? new Color32(240, 246, 255, 235) : new Color32(190, 214, 255, 170);
                for (int y = y0; y < y0 + len; y++) Put(px, W, H, x, y, c);
            }
            speedFrames[f] = Make(px, W, H, new Vector2(0.5f, 0.5f));
        }
        return speedFrames;
    }

    static Sprite[] ShockFrames()
    {
        if (shockFrames != null) return shockFrames;
        const int W = 96, H = 18, N = 6;
        shockFrames = new Sprite[N];
        for (int f = 0; f < N; f++)
        {
            var px = Blank(W, H);
            float age = f / (float)(N - 1);
            float rx = 6f + 40f * age, ry = 2f + 7f * (1f - age * 0.8f);
            byte a = (byte)(255 * (1f - age * 0.85f));
            for (int k = 0; k <= 80; k++)
            {
                float t = Mathf.PI * k / 80f;
                int x = W / 2 + Mathf.RoundToInt(Mathf.Cos(t) * rx);
                int y = Mathf.RoundToInt(Mathf.Sin(t) * ry);
                Put(px, W, H, x, y, new Color32(255, 246, 214, a));
                if (f < N - 2) Put(px, W, H, x, y + 1, new Color32(255, 200, 120, (byte)(a * 0.8f)));
            }
            for (int s = -1; s <= 1; s += 2) { Put(px, W, H, W / 2 + s * Mathf.RoundToInt(rx), 0, new Color32(255, 255, 255, a)); Put(px, W, H, W / 2 + s * Mathf.RoundToInt(rx) - s, 0, new Color32(255, 255, 255, a)); }
            shockFrames[f] = Make(px, W, H, new Vector2(0.5f, 0f));
        }
        return shockFrames;
    }

    static Sprite Crack()
    {
        if (crack != null) return crack;
        const int W = 44, H = 7;
        var px = Blank(W, H);
        var dark = new Color32(36, 26, 28, 255);
        int[,] pts = { {22,0},{21,1},{19,1},{17,2},{15,2},{13,3},{10,3},{8,4},{5,4},{2,5},{23,1},{25,1},{27,2},{29,2},{31,3},{34,3},{36,4},{39,4},{42,5},{18,3},{16,4},{26,3},{28,4} };
        for (int i = 0; i < pts.GetLength(0); i++) Put(px, W, H, pts[i, 0], H - 1 - pts[i, 1], dark);
        crack = Make(px, W, H, new Vector2(0.5f, 1f));
        return crack;
    }

    // ------------------------------------------------------------------------------------------ the calls
    /// <summary>
    /// The sword rising: no swoosh (the crescent effect only goes from top to bottom, so it would look like the wrong direction).
    /// The speed lines and the sword in the animation carry this part.
    /// </summary>
    public static void Windup(Transform follow, int facing) { }

    /// <summary>
    /// The big arc as the sword comes down. Same convention as the normal swing: aim = where the sword ends up (forward and down),
    /// the crescent goes from aim + 60 to aim - 60, always from above and down (also when facing left).
    /// </summary>
    public static void Strike(Transform follow, int facing)
    {
        if (!CombatFx.Enabled || follow == null) return;
        float aim = facing >= 0 ? -50f : 230f;
        CombatFx.Slash(follow, new Vector2(0.12f * facing, 0.85f), aim + 60f, aim - 60f, 0.09f, 1.15f, null, new Color(1f, 1f, 0.9f, 1f), CombatFx.SortingOrder - 8, 0.1f);
    }

    public static GameObject SpeedLines(Transform follow)
    {
        if (!CombatFx.Enabled || follow == null) return null;
        var go = new GameObject("AirSlamSpeedLines");
        go.transform.SetParent(follow, false);
        go.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        float s = CombatFx.Pixel * 100f * 0.9f;
        go.transform.localScale = new Vector3(s, s, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = CombatFx.SortingLayer; sr.sortingOrder = CombatFx.SortingOrder - 12;
        var loop = go.AddComponent<LoopFrames>();
        loop.frames = SpeedFrames(); loop.fps = 16f; loop.sr = sr;
        return go;
    }

    public static void Land(Vector3 feet, int facing)
    {
        if (!CombatFx.Enabled) return;
        float u = CombatFx.Pixel * 100f;
        Vector3 ground = new Vector3(feet.x + facing * 0.55f, feet.y, 0f);

        FxFlip.Spawn(ShockFrames(), 20f, CombatFx.SortingLayer, CombatFx.SortingOrder - 6, ground, u * 1.1f);

        // the crack stays a moment, then fades
        var c = new GameObject("AirSlamCrack");
        c.transform.position = ground + new Vector3(0f, 0.01f, 0f);
        c.transform.localScale = new Vector3(u, u, 1f);
        var csr = c.AddComponent<SpriteRenderer>();
        csr.sprite = Crack(); csr.sortingLayerName = CombatFx.SortingLayer; csr.sortingOrder = CombatFx.SortingOrder - 14;
        var fade = c.AddComponent<FadeAway>(); fade.hold = 2.2f; fade.time = 1.4f; fade.sr = csr;

        // rubble
        var d = new GameObject("AirSlamRubble");
        d.transform.position = ground;
        d.AddComponent<Rubble>();

        CombatFx.Impact(ground + new Vector3(0f, 0.35f, 0f), 1.9f, true);
        CombatFx.Sparks(ground + new Vector3(0f, 0.15f, 0f), Vector2.up, 16, 170f, 4.6f);
        CombatFx.Dust(ground, -1, 5);
        CombatFx.Dust(ground, 1, 5);
    }

    // ------------------------------------------------------------------------------------------ little components
    class LoopFrames : MonoBehaviour
    {
        public Sprite[] frames; public float fps = 16f; public SpriteRenderer sr; float t;
        void Update()
        {
            if (frames == null || frames.Length == 0 || sr == null) return;
            t += Time.deltaTime;
            sr.sprite = frames[Mathf.FloorToInt(t * fps) % frames.Length];
        }
    }

    class FadeAway : MonoBehaviour
    {
        public float hold = 2f, time = 1.2f; public SpriteRenderer sr; float t;
        void Update()
        {
            t += Time.deltaTime;
            if (t > hold + time) { Destroy(gameObject); return; }
            if (t > hold && sr != null) { var c = sr.color; c.a = 1f - (t - hold) / time; sr.color = c; }
        }
    }

    class Rubble : MonoBehaviour
    {
        class Bit { public Transform t; public Vector2 v; public float spin; }
        readonly System.Collections.Generic.List<Bit> bits = new System.Collections.Generic.List<Bit>();
        float age;
        void Start()
        {
            float u = CombatFx.Pixel * 100f;
            int n = 12;
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("rubble");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(Random.Range(-0.4f, 0.4f), 0.05f, 0f);
                float s = Random.Range(1.2f, 2.2f) * u;
                go.transform.localScale = new Vector3(s, s, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Square(); sr.sortingLayerName = CombatFx.SortingLayer; sr.sortingOrder = CombatFx.SortingOrder - 5;
                sr.color = Random.value < 0.5f ? new Color(0.50f, 0.38f, 0.28f) : new Color(0.38f, 0.28f, 0.2f);
                float side = (i % 2 == 0 ? 1f : -1f);
                bits.Add(new Bit { t = go.transform, v = new Vector2(side * Random.Range(0.8f, 3.4f), Random.Range(2.2f, 5.2f)) });
            }
        }
        void Update()
        {
            age += Time.deltaTime;
            if (age > 0.75f) { Destroy(gameObject); return; }
            foreach (var b in bits)
            {
                b.v.y -= 16f * Time.deltaTime;
                b.t.position += (Vector3)(b.v * Time.deltaTime);
            }
        }
    }
}
