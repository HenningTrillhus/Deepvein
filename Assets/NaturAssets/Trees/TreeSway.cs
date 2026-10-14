using UnityEngine;

/// <summary>
/// Makes a tree (or a bush, a banner, tall grass ...) sway in the wind without any animation frames.
///   Bend:   a shader bends the picture sideways, nothing at the foot and more and more towards the top, in whole sprite pixels.
///           Needs the "DeepVain/Sprite Sway" material (made by the menu DeepVain > Trees > 1 - Create sway material).
///   Rotate: no shader needed. The whole object rocks around its pivot (the feet) a few degrees. Stiffer, but works everywhere.
/// v2: also knows "selected" (SetHighlighted): the shader draws a white outline and brightens the picture. Used by TreeHover.
/// Put it on the object with the SpriteRenderer. Every tree gets its own random phase, so a forest never sways in step.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class TreeSway : MonoBehaviour
{
    public enum Style { Bend, Rotate }

    /// <summary>One knob for all trees at once: 0 = dead calm, 1 = normal, 2 = storm. Can be changed from anywhere.</summary>
    public static float WindScale = 1f;

    [Header("Valgt (musa over treet)")]
    public Color outlineColor = Color.white;
    [Tooltip("Hvor mye lysere treet blir når det er valgt (0-1).")]
    [Range(0f, 1f)] public float highlightBrighten = 0.22f;

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
        pUVPerUnit = Shader.PropertyToID("_UVPerUnit"), pUVMinMax = Shader.PropertyToID("_UVMinMaxX"), pPPU = Shader.PropertyToID("_PPU"), pSnap = Shader.PropertyToID("_Snap"),
        pUVPerUnitY = Shader.PropertyToID("_UVPerUnitY"), pUVMinMaxY = Shader.PropertyToID("_UVMinMaxY"), pOutline = Shader.PropertyToID("_Outline"),
        pOutlineColor = Shader.PropertyToID("_OutlineColor"), pBrighten = Shader.PropertyToID("_Brighten");

    bool highlighted;
    Color baseColor = Color.white;

    // shake (a hit with the axe) and still (a tree that is falling)
    float shakeStart = -100f, shakeDuration = 0.5f, shakeStrength = 1f;
    bool shakingLastFrame, still;

    /// <summary>The tree shudders: the crown whips back and forth and settles. Used when the axe hits.</summary>
    public void Shake(float strength = 1f, float duration = 0.45f)
    {
        shakeStart = Time.time; shakeDuration = Mathf.Max(0.05f, duration); shakeStrength = strength;
    }
    public bool IsShaking { get { return Time.time < shakeStart + shakeDuration; } }
    float ShakeK()
    {
        float t = (Time.time - shakeStart) / shakeDuration;
        if (t < 0f || t > 1f) return 0f;
        return (1f - t) * (1f - t) * shakeStrength;                      // starts hard, dies out
    }
    /// <summary>
    /// How far (object-space units, sideways) the wind bends the sprite at a given height: 0 = bottom of the sprite, 1 = top.
    /// The same maths as the shader, so things hanging in the tree (apples) can follow the leaves. A shake is not included.
    /// </summary>
    public float SwayDx(float h)
    {
        if (still || style != Style.Bend) return 0f;
        float t = Time.timeSinceLevelLoad * speed * speedMul + phase;
        float s = Mathf.Sin(t + h * 0.9f) * 0.75f + Mathf.Sin(t * 2.17f + phase * 1.7f + h * 2f) * 0.25f;
        float a = amount * WindScale;
        float dx = s * a * h * h;
        dx += Mathf.Sin(Time.timeSinceLevelLoad * 7f * speed * speedMul + phase * 3f + h * 9f) * a * 0.12f * h * h * h;
        if (snapToPixels && sr != null && sr.sprite != null) dx = Mathf.Round(dx * sr.sprite.pixelsPerUnit) / sr.sprite.pixelsPerUnit;
        return dx;
    }

    /// <summary>On = no wind sway at all (the tree is falling and is moved by script instead).</summary>
    public void Still(bool on) { still = on; if (style == Style.Bend && mpb != null) ApplySprite(); }

    public bool Highlighted { get { return highlighted; } }

    /// <summary>Selected look on/off. Bend: outline + brighter through the shader. Rotate (no shader): only a brighter tint.</summary>
    public void SetHighlighted(bool on)
    {
        if (highlighted == on) return;
        highlighted = on;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (style == Style.Bend && mpb != null) ApplySprite();
        else if (sr != null) sr.color = on ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseRotation = transform.localRotation;
        baseColor = sr.color;

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
        float vMin = r.yMin / tex.height, vMax = r.yMax / tex.height;
        mpb.SetFloat(pUVPerUnit, (uMax - uMin) / Mathf.Max(0.0001f, b.size.x));
        mpb.SetFloat(pUVPerUnitY, (vMax - vMin) / Mathf.Max(0.0001f, b.size.y));
        mpb.SetVector(pUVMinMax, new Vector4(uMin, uMax, 0f, 0f));
        mpb.SetVector(pUVMinMaxY, new Vector4(vMin, vMax, 0f, 0f));
        mpb.SetFloat(pOutline, highlighted ? 1f : 0f);
        mpb.SetColor(pOutlineColor, outlineColor);
        mpb.SetFloat(pBrighten, highlightBrighten);
        mpb.SetFloat(pPPU, sprite.pixelsPerUnit);
        mpb.SetFloat(pSnap, snapToPixels ? 1f : 0f);
        float k = ShakeK();
        mpb.SetFloat(pAmount, still ? 0f : amount);
        mpb.SetFloat(pSpeed, speed * speedMul * (1f + 2.5f * k));
        mpb.SetFloat(pPhase, phase);
        mpb.SetFloat(pWind, still ? 0f : WindScale + 3.5f * k);           // a shake works even in dead calm
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
            float kk = ShakeK();
            transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, still ? 0f : w * angle * WindScale + Mathf.Sin(Time.time * 45f) * kk * 3f);
            return;
        }

        if (sr == null || sr.sprite == null) return;
        bool shaking = IsShaking;
        if (sr.sprite != lastSprite || !Mathf.Approximately(lastWind, WindScale) || shaking || shakingLastFrame) ApplySprite();       // a new picture, a new wind strength, or a shake
        shakingLastFrame = shaking;
    }
}
