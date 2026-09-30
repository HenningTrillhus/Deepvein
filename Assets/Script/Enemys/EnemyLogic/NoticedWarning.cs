using System.Collections;
using UnityEngine;

public class NoticedWarning : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float popTime = 0.15f;
    [SerializeField] private float holdTime = 0.35f;
    [SerializeField] private float fadeTime = 0.1f;

    [Header("Scale")]
    [SerializeField] private float overshoot = 1.3f;

    [Header("Look")]
    [SerializeField] private Color color = Color.red;
    [SerializeField] private float riseHeight = 0.15f;

    private SpriteRenderer[] renderers;
    private Vector3 baseScale;
    private Vector3 basePos;
    private Coroutine routine;

    void Awake()
    {
        // Picks up both squares, however they're nested
        renderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (renderers.Length == 0)
        {
            Debug.LogError($"{name}: no SpriteRenderers found", this);
            enabled = false;
            return;
        }

        baseScale = transform.localScale;
        basePos = transform.localPosition;

        SetAlpha(1f);
        Hide();
    }

    public void Show()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        SetVisible(true);

        float t = 0f;
        while (t < popTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / popTime);

            transform.localScale = baseScale * Mathf.LerpUnclamped(0f, 1f, EaseOutBack(p));
            transform.localPosition = basePos + Vector3.up * (riseHeight * p);
            SetAlpha(1f);
            yield return null;
        }

        transform.localScale = baseScale;
        transform.localPosition = basePos + Vector3.up * riseHeight;

        yield return new WaitForSeconds(holdTime);

        if (fadeTime > 0f)
        {
            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                SetAlpha(1f - Mathf.Clamp01(t / fadeTime));
                yield return null;
            }
        }

        Hide();
        routine = null;
    }

    private void Hide()
    {
        SetVisible(false);
        transform.localScale = baseScale;
        transform.localPosition = basePos;
        SetAlpha(1f);
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in renderers)
            r.enabled = visible;
    }

    private void SetAlpha(float a)
    {
        Color c = color;
        c.a = a;
        foreach (var r in renderers)
            r.color = c;
    }

    private float EaseOutBack(float x)
    {
        float c1 = (overshoot - 1f) * 3f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    void LateUpdate()
    {
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);
    }
}