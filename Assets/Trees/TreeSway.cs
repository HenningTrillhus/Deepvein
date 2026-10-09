using UnityEngine;

/// <summary>
/// Makes a tree (or a bush, a banner, tall grass ...) sway in the wind without any animation frames.
///   Bend:   a shader bends the picture sideways, nothing at the foot and more and more towards the top, in whole sprite pixels.
///           Needs the "DeepVain/Sprite Sway" material (made by the menu DeepVain > Trees > 1 - Create sway material).
///   Rotate: no shader needed. The whole object rocks around its pivot (the feet) a few degrees. Stiffer, but works everywhere.
/// Put it on the object with the SpriteRenderer. Every tree gets its own random phase, so a forest never sways in step.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class TreeSway : MonoBehaviour
{
    public enum Style { Bend, Rotate }

    /// <summary>One knob for all trees at once: 0 = dead calm, 1 = normal, 2 = storm. Can be changed from anywhere.</summary>
    public static float WindScale = 1f;

    public Style style = Style.Bend;
    [Tooltip("Bend: the Sprite Sway material. Tomt = finner shaderen selv (virker i editor og i bygg hvis den er brukt av et material).")]
    public Material swayMaterial;

    [Header("Bevegelse")]
    [Tooltip("Bend: hvor langt toppen flytter seg sideveis, i units (1 tile = 1). 0.05-0.12 er mye.")]
    public float amount = 0.08f;
    [Tooltip("Rotate: hvor mange grader den vugger.")]
    public float angle = 1.2f;
    [Tooltip("Hvor fort (1 = rolig vind).")]
    public float speed = 1f;
    [Tooltip("Hvor ulike trærne er hverandre (0 = alle lik, 1 = helt ulik fart).")]
    [Range(0f, 1f)] public float variation = 0.25f;
    [Tooltip("Hver kopi får tilfeldig fase fra posisjonen. Av = alle starter likt.")]
    public bool randomPhase = true;

    [Header("Piksler")]
    [Tooltip("Bend: flytt bare hele sprite-piksler, så det blir skarpt pikselkunst.")]
    public bool snapToPixels = true;

    SpriteRenderer sr;
    MaterialPropertyBlock mpb;
    float phase, speedMul = 1f, lastWind = -1f;
    Quaternion baseRotation;
    Sprite lastSprite;
    bool warned;

    static readonly int pAmount = Shader.PropertyToID("_SwayAmount"), pSpeed = Shader.PropertyToID("_SwaySpeed"), pPhase = Shader.PropertyToID("_SwayPhase"),
        pWind = Shader.PropertyToID("_Wind"), pBaseY = Shader.PropertyToID("_BaseY"), pHeight = Shader.PropertyToID("_Height"), pCenterX = Shader.PropertyToID("_CenterX"),
        pUVPerUnit = Shader.PropertyToID("_UVPerUnit"), pUVMinMax = Shader.PropertyToID("_UVMinMaxX"), pPPU = Shader.PropertyToID("_PPU"), pSnap = Shader.PropertyToID("_Snap");

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseRotation = transform.localRotation;

        // a stable random per tree (from where it stands), so the same tree always starts in the same place in the sway
        Vector3 p = transform.position;
        float h = Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.y * 78.233f) * 43758.5453f); h -= Mathf.Floor(h);
        phase = randomPhase ? h * Mathf.PI * 2f : 0f;
        speedMul = 1f + (h * 2f - 1f) * variation * 0.4f;
    }

    void OnEnable()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (style == Style.Bend) SetupBend();
    }

    void OnDisable()
    {
        if (sr != null && mpb != null) sr.SetPropertyBlock(null);
        transform.localRotation = baseRotation;
    }

    void SetupBend()
    {
        if (swayMaterial == null)
        {
            var shader = Shader.Find("DeepVain/Sprite Sway");
            if (shader != null) swayMaterial = new Material(shader) { name = "Sprite Sway (auto)" };
        }
        if (swayMaterial == null)
        {
            if (!warned) { warned = true; Debug.LogWarning("[TreeSway] Fant ikke Sprite Sway-materialet/shaderen på " + name + ". Bruker Rotate. Lag materialet: DeepVain > Trees > 1 - Create sway material.", this); }
            style = Style.Rotate;
            return;
        }
        sr.sharedMaterial = swayMaterial;
        ApplySprite();
    }

    void ApplySprite()
    {
        var sprite = sr.sprite;
        lastSprite = sprite;
        if (sprite == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);

        Bounds b = sprite.bounds;                                   // object space, units
        Rect r = sprite.textureRect;                                // pixels in the texture
        var tex = sprite.texture;
        float uMin = r.xMin / tex.width, uMax = r.xMax / tex.width;
        mpb.SetFloat(pBaseY, b.min.y);
        mpb.SetFloat(pHeight, b.size.y);
        mpb.SetFloat(pCenterX, b.center.x);
        mpb.SetFloat(pUVPerUnit, (uMax - uMin) / Mathf.Max(0.0001f, b.size.x));
        mpb.SetVector(pUVMinMax, new Vector4(uMin, uMax, 0f, 0f));
        mpb.SetFloat(pPPU, sprite.pixelsPerUnit);
        mpb.SetFloat(pSnap, snapToPixels ? 1f : 0f);
        mpb.SetFloat(pAmount, amount);
        mpb.SetFloat(pSpeed, speed * speedMul);
        mpb.SetFloat(pPhase, phase);
        mpb.SetFloat(pWind, WindScale);
        lastWind = WindScale;
        sr.SetPropertyBlock(mpb);

        if (!warned && sprite.vertices.Length > 4)
        {
            warned = true;
            Debug.LogWarning("[TreeSway] Spriten på " + name + " har Mesh Type = Tight, så toppen kan bli kuttet når den svaier. Sett Mesh Type = Full Rect (menyen DeepVain > Trees > 2 gjør det for deg).", this);
        }
    }

    void Update()
    {
        if (style == Style.Rotate)
        {
            float t = Time.time * speed * speedMul + phase;
            float w = Mathf.Sin(t) * 0.75f + Mathf.Sin(t * 2.17f + phase * 1.7f) * 0.25f;
            transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, w * angle * WindScale);
            return;
        }

        if (sr == null || sr.sprite == null) return;
        if (sr.sprite != lastSprite || !Mathf.Approximately(lastWind, WindScale)) ApplySprite();       // a new picture or a new wind strength
    }
}
