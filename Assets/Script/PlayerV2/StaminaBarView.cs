using DeepVain.Player;
using UnityEngine;

/// <summary>
/// The thin stamina bar under the player's feet. It only shows when stamina is below Show Below (85 %), and fades out when
/// nothing has been spent for Hide After seconds (3). Drawn by swapping between 29 pictures (0..28 pixels filled) so the bar
/// is always pixel sharp. Put it on the same object as PlayerStamina (the builder does it).
/// </summary>
[DisallowMultipleComponent]
public class StaminaBarView : MonoBehaviour
{
    [Header("Bilder (settes av byggeren)")]
    public Sprite[] amber = new Sprite[29];
    public Sprite[] red = new Sprite[29];

    [Header("Når den vises")]
    [Range(0f, 1f)] public float showBelow = 0.85f;
    [Tooltip("Sekunder uten at stamina brukes før baren fader bort.")]
    public float hideAfter = 3f;
    public float fadeIn = 0.15f;
    public float fadeOut = 0.5f;
    [Tooltip("Under denne andelen blir baren rød. Er du utmattet blinker den.")]
    [Range(0f, 1f)] public float redBelow = 0.26f;

    [Header("Plassering")]
    [Tooltip("Hvor mange tegnede piksler under føttene baren henger.")]
    public float pixelsBelowFeet = 2f;
    [Tooltip("Units per tegnede piksel. Finnes PlayerLayerSync leses den automatisk.")]
    public float fallbackUnitsPerPixel = 0.025f;

    PlayerStamina stamina;
    PlayerLayerSync sync;
    Collider2D col;
    SpriteRenderer sr;
    float alpha;
    Vector2 feetOffset;           // where the feet are, relative to the player's transform (measured once, standing)

    void Awake()
    {
        stamina = GetComponent<PlayerStamina>();
        sync = GetComponentInChildren<PlayerLayerSync>();
        col = GetComponent<Collider2D>();

        // where the feet are relative to the transform: measured once while standing, so the bar follows the (interpolated) transform
        // smoothly instead of the physics collider, which steps at 50 Hz and made the bar shake while running
        feetOffset = col != null ? new Vector2(col.bounds.center.x - transform.position.x, col.bounds.min.y - transform.position.y) : Vector2.zero;

        // not a child of the picture object, so it never flips with the player
        var go = new GameObject("StaminaBar");
        go.transform.SetParent(transform, false);
        sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = sync != null ? sync.sortingLayer : "Default";
        sr.sortingOrder = (sync != null ? sync.baseSortingOrder : 10) - 3;
        sr.color = new Color(1f, 1f, 1f, 0f);
        sr.enabled = false;
    }

    float UnitsPerPixel()
    {
        if (sync != null && sync.slots != null)
            foreach (var s in sync.slots)
                if (s != null && s.renderer != null && s.renderer.sprite != null)
                    return Mathf.Abs(s.renderer.transform.lossyScale.y) / s.renderer.sprite.pixelsPerUnit;
        return fallbackUnitsPerPixel;
    }

    /// <summary>True when the bar should be showing right now.</summary>
    public bool WantsToShow
    {
        get { return stamina != null && stamina.Percent < showBelow && stamina.TimeSinceSpent < hideAfter; }
    }
    public float Alpha { get { return alpha; } }

    void LateUpdate()
    {
        if (stamina == null || sr == null) return;

        float target = WantsToShow ? 1f : 0f;
        float speed = target > alpha ? 1f / Mathf.Max(0.01f, fadeIn) : 1f / Mathf.Max(0.01f, fadeOut);
        alpha = Mathf.MoveTowards(alpha, target, speed * Time.deltaTime);
        sr.enabled = alpha > 0.01f;
        if (!sr.enabled) return;

        int n = Mathf.Clamp(Mathf.RoundToInt(stamina.Percent * 28f), 0, 28);
        if (stamina.Current > 0f && n == 0) n = 1;
        bool isRed = stamina.Percent < redBelow || stamina.IsExhausted;
        var set = isRed ? red : amber;
        if (set != null && n < set.Length) sr.sprite = set[n];

        float a = alpha;
        if (stamina.IsExhausted) a *= 0.6f + 0.4f * (Mathf.Sin(Time.time * 14f) * 0.5f + 0.5f);       // blinks when empty
        sr.color = new Color(1f, 1f, 1f, a);

        // hangs just under the feet, centred on the player (the picture has its pivot at the top centre)
        float u = UnitsPerPixel();
        Vector3 p = transform.position;
        sr.transform.position = new Vector3(p.x + feetOffset.x, p.y + feetOffset.y - pixelsBelowFeet * u, p.z);
        Vector3 ps = transform.lossyScale;                                                       // cancel the player's own scale AND its sign, so the bar never mirrors
        sr.transform.localScale = new Vector3(u / (Mathf.Abs(ps.x) < 0.0001f ? 1f : ps.x), u / (Mathf.Abs(ps.y) < 0.0001f ? 1f : ps.y), 1f);
        sr.transform.rotation = Quaternion.identity;
    }
}
