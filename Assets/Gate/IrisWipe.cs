using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overgangen mellom to steder: en svart sirkel lukker seg fra kantene av skjermen mot midten (og åpner seg igjen).
/// Tegnes som et lite pikselbilde (rundt 216 piksler høyt) strukket over skjermen, så sirkelen får pikselkant som resten av spillet.
/// Lager sitt eget Canvas selv første gang det trengs. Bruk fra en coroutine:
///     yield return IrisWipe.CloseRoutine(0.55f);   ...flytt spilleren...   yield return IrisWipe.OpenRoutine(0.55f);
/// </summary>
public static class IrisWipe
{
    const int TexH = 216;

    static GameObject root;
    static RawImage image;
    static Texture2D tex;
    static Color32[] px;
    static int w;
    static float current = 1f;                // 1 = fully open (no black), 0 = fully black

    /// <summary>1 = no black, 0 = all black (for scripts that want to know).</summary>
    public static float Amount { get { return current; } }

    static void Ensure()
    {
        int wantW = Mathf.Max(64, Mathf.RoundToInt(TexH * (Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f)));
        if (root != null && image != null && tex != null && w == wantW) return;
        if (root != null) Object.Destroy(root);
        w = wantW;
        root = new GameObject("IrisWipe");
        Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var go = new GameObject("Image", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(root.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        image = go.GetComponent<RawImage>();
        image.raycastTarget = false;
        tex = new Texture2D(w, TexH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        px = new Color32[w * TexH];
        image.texture = tex;
        Set(current);
    }

    /// <summary>k = 1 nothing is black, 0 = everything black. In between: a circle of that size (1 = reaches the corners).</summary>
    public static void Set(float k)
    {
        Ensure();
        current = k;
        if (k >= 0.999f) { image.enabled = false; return; }
        image.enabled = true;
        float cx = w * 0.5f, cy = TexH * 0.5f;
        float maxR = Mathf.Sqrt(cx * cx + cy * cy) + 1f;
        float r = maxR * Mathf.Max(0f, k);
        float r2 = r * r;
        var black = new Color32(8, 4, 10, 255); var clear = new Color32(0, 0, 0, 0);
        for (int y = 0; y < TexH; y++)
        {
            float dy = y + 0.5f - cy;
            float rem = r2 - dy * dy;
            int row = y * w;
            if (rem <= 0f) { for (int x = 0; x < w; x++) px[row + x] = black; continue; }
            float half = Mathf.Sqrt(rem);
            int x0 = Mathf.CeilToInt(cx - half - 0.5f), x1 = Mathf.FloorToInt(cx + half - 0.5f);
            for (int x = 0; x < w; x++) px[row + x] = (x >= x0 && x <= x1) ? clear : black;
        }
        tex.SetPixels32(px); tex.Apply(false);
    }

    public static void Hide() { Set(1f); }

    public static IEnumerator CloseRoutine(float duration, float delay = 0f)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float t = 0f;
        float from = current;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
            Set(Mathf.Lerp(from, 0f, k * k * (3f - 2f * k)));
            yield return null;
        }
        Set(0f);
    }

    public static IEnumerator OpenRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
            Set(Mathf.Lerp(0f, 1f, k * k * (3f - 2f * k)));
            yield return null;
        }
        Set(1f);
    }
}
