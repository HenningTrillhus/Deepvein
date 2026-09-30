using UnityEngine;

public class HealthBar : MonoBehaviour
{
    [Header("Referanser")]
    [SerializeField] private Transform fill;
    [SerializeField] private SpriteRenderer fillRenderer;

    [Header("Oppførsel")]
    [SerializeField] private bool hideWhenFull = true;
    [SerializeField] private float smoothSpeed = 8f;

    [Header("Farge")]
    [SerializeField] private bool colorByHealth = true;
    [SerializeField] private Color fullColor = new Color(0.3f, 0.85f, 0.3f);
    [SerializeField] private Color lowColor = new Color(0.85f, 0.2f, 0.2f);

    private float targetPercent = 1f;
    private float currentPercent = 1f;
    private Vector3 baseScale;
    private float leftEdge;

    void Awake()
    {
        baseScale = fill.localScale;
        leftEdge = fill.localPosition.x - baseScale.x * 0.5f;

        currentPercent = 1f;
        targetPercent = 1f;
        ApplyFill();          // draw a full bar immediately
    }

    void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);

        currentPercent = smoothSpeed <= 0f
            ? targetPercent
            : Mathf.Lerp(currentPercent, targetPercent, smoothSpeed * Time.deltaTime);

        ApplyFill();
    }

    [SerializeField] private SpriteRenderer backgroundRenderer;

    public void SetHealth(int current, int max)
    {
        targetPercent = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
    }

    private void ApplyFill()
    {
        float newWidth = baseScale.x * currentPercent;

        fill.localScale = new Vector3(newWidth, baseScale.y, baseScale.z);

        Vector3 p = fill.localPosition;
        p.x = leftEdge + newWidth * 0.5f;
        fill.localPosition = p;

        if (colorByHealth && fillRenderer != null)
            fillRenderer.color = Color.Lerp(lowColor, fullColor, currentPercent);

        // Skjul rendererne, ikke objektet
        bool visible = !hideWhenFull || currentPercent < 0.999f;
        if (fillRenderer != null) fillRenderer.enabled = visible;
        if (backgroundRenderer != null) backgroundRenderer.enabled = visible;
    }
}