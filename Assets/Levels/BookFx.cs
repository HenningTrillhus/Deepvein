using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeepVain.Levels
{
    /// <summary>
    /// Åpne-bok- og side-bla-animasjoner. Boka (sekken eller level-siden) tegnes til et bilde rett før og rett etter en endring,
    /// og så spilles en rask animasjon oppå av disse bildene:
    ///   Open          dekselet svinger opp og boka glir til midten
    ///   Close         det samme baklengs
    ///   TurnForward   høyre side snus over til venstre (sekk -> levels, oversikt -> skill-siden)
    ///   TurnBackward  venstre side snus over til høyre (levels -> sekk, skill-siden -> oversikt)
    /// Den ekte UI-en bytter med en gang, så knapper virker umiddelbart; animasjonen ligger bare oppå og blokkerer klikk i et øyeblikk.
    /// Brukes av LevelBookUI (legges på samme objekt).
    /// </summary>
    public class BookFx : MonoBehaviour
    {
        public enum Kind { Open, Close, TurnForward, TurnBackward }

        [Header("Tid (sekunder) - kort, så det går fort")]
        public float flipTime = 0.26f;
        public float openTime = 0.32f;
        [Tooltip("Test: 1 = normal. Mindre = saktere.")]
        public float speed = 1f;

        const float PW = 320f, BH = 360f;                // one page (half of the 640 picture, with the book's edge), and the height

        Camera cam;
        RectTransform overlay;
        RawImage staticLeft, staticRight, flipping, coverImg;
        Image shadow;
        GameObject overlayRoot;
        Texture2D coverTex;
        readonly List<RenderTexture> live = new List<RenderTexture>();
        Coroutine running;

        public bool IsPlaying { get { return running != null; } }

        void OnDestroy() { if (overlayRoot != null) Destroy(overlayRoot); if (cam != null) Destroy(cam.gameObject); ReleaseTextures(); }

        // ------------------------------------------------------------------ capture
        /// <summary>Tegner en canvas (boka) til et bilde 1280x720 akkurat nå. hide = ting som ikke skal være med (XP-tall).</summary>
        public RenderTexture Capture(Canvas canvas, params GameObject[] hide)
        {
            if (canvas == null) return null;
            if (cam == null)
            {
                var go = new GameObject("BookFxCamera");
                // a root object: a child of the canvas would inherit the canvas scale
                cam = go.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = 5f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.cullingMask = 1 << LayerMask.NameToLayer("UI");
                cam.enabled = false;
                go.transform.position = new Vector3(0f, -5000f, -10f);                     // far away from everything else
            }
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            live.Add(rt);

            var oldMode = canvas.renderMode; var oldCam = canvas.worldCamera; float oldDist = canvas.planeDistance;
            var hidden = new List<GameObject>();
            if (hide != null) foreach (var h in hide) if (h != null && h.activeSelf) { h.SetActive(false); hidden.Add(h); }

            var scaler = canvas.GetComponent<CanvasScaler>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f;
            cam.targetTexture = rt;
            Rescale(scaler);                                   // the scaler only updates in Update: do it now, for the size of the texture
            Canvas.ForceUpdateCanvases();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = null;
            canvas.renderMode = oldMode; canvas.worldCamera = oldCam; canvas.planeDistance = oldDist;
            Rescale(scaler);
            foreach (var h in hidden) h.SetActive(true);
            return rt;
        }

        static void Rescale(CanvasScaler scaler)
        {
            if (scaler == null) return;
            scaler.enabled = false; scaler.enabled = true;     // OnEnable runs the scale calculation straight away
        }

        // ------------------------------------------------------------------ the overlay
        void EnsureOverlay()
        {
            if (overlayRoot != null) return;
            overlayRoot = new GameObject("BookFxCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlayRoot.layer = LayerMask.NameToLayer("UI");
            // a canvas of its own (not a child of the book canvas, or it would just be part of that one)
            var c = overlayRoot.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 40;
            var sc = overlayRoot.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(640f, 360f);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0.5f; sc.referencePixelsPerUnit = 1f;

            // a see-through layer that eats the clicks while the animation runs
            var blocker = NewImage("Blocker", overlayRoot.transform);
            blocker.color = new Color(0f, 0f, 0f, 0f); blocker.raycastTarget = true;
            var br = blocker.rectTransform; br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;

            var root = new GameObject("Book", typeof(RectTransform)); root.layer = LayerMask.NameToLayer("UI");
            overlay = (RectTransform)root.transform; overlay.SetParent(overlayRoot.transform, false);
            overlay.anchorMin = overlay.anchorMax = new Vector2(0.5f, 0.5f); overlay.pivot = new Vector2(0.5f, 0.5f);
            overlay.sizeDelta = new Vector2(640f, BH); overlay.anchoredPosition = Vector2.zero;

            staticLeft = NewRaw("StaticLeft", new Vector2(1f, 0.5f));
            staticRight = NewRaw("StaticRight", new Vector2(0f, 0.5f));
            shadow = NewImage("Shadow", overlay); shadow.color = new Color(0f, 0f, 0f, 0.28f); shadow.raycastTarget = false;
            flipping = NewRaw("Flipping", new Vector2(0f, 0.5f));
            coverImg = NewRaw("Cover", new Vector2(0f, 0.5f));
            coverTex = MakeCover();
            coverImg.texture = coverTex;
            var t = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI)); t.layer = LayerMask.NameToLayer("UI");
            t.transform.SetParent(coverImg.transform, false);
            var tt = t.GetComponent<TextMeshProUGUI>();
            tt.text = "DEEPVAIN"; tt.fontSize = 18; tt.fontStyle = FontStyles.Bold; tt.alignment = TextAlignmentOptions.Center; tt.color = new Color32(0xE8, 0xC0, 0x50, 255); tt.raycastTarget = false;
            var tr = tt.rectTransform; tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f); tr.anchoredPosition = new Vector2(0f, -78f); tr.sizeDelta = new Vector2(0f, 26f);
            overlayRoot.SetActive(false);
        }

        Image NewImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        RawImage NewRaw(string name, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage)); go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(overlay, false);
            var r = go.GetComponent<RawImage>(); r.raycastTarget = false;
            var rt = r.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = pivot; rt.sizeDelta = new Vector2(PW, BH);
            return r;
        }

        static Texture2D MakeCover()
        {
            int w = 320, h = 360;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            var rnd = new System.Random(5);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float k = Mathf.Abs(y - h / 2f) / (h / 2f);
                    int r = (int)(0x5A - 16 * k), g = (int)(0x34 - 10 * k), b = (int)(0x1C - 6 * k);
                    int n = rnd.Next(0, 24);
                    if (n == 0) { r += 28; g += 18; b += 10; } else if (n == 1) { r -= 20; g -= 12; b -= 8; }
                    bool edge = x < 2 || y < 2 || x >= w - 2 || y >= h - 2;
                    bool inner = (x >= 8 && x < 10 || x >= w - 10 && x < w - 8 || y >= 8 && y < 10 || y >= h - 10 && y < h - 8) && x >= 8 && x < w - 8 && y >= 8 && y < h - 8;
                    if (edge) { r = 0x20; g = 0x10; b = 0x08; }
                    else if (inner) { r = 0x86; g = 0x58; b = 0x30; }
                    else if ((y == 13 || y == h - 14) && x > 12 && x < w - 12 && x % 6 == 0) { r = 0xC8; g = 0xA0; b = 0x60; }
                    px[y * w + x] = new Color32((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255), (byte)Mathf.Clamp(b, 0, 255), 255);
                }
            // a gold plate and a star
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int cx = w / 2, cy = h - 128;
                    bool plate = Mathf.Abs(x - cx) <= 72 && Mathf.Abs(y - (h - 100)) <= 28 && (Mathf.Abs(x - cx) >= 70 || Mathf.Abs(y - (h - 100)) >= 26);
                    float dx = x - cx, dy = y - (h - 230); float d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool ring = d >= 22 && d <= 24;
                    bool star = d < 14 && (Mathf.Abs(dx) < 3 || Mathf.Abs(dy) < 3 || Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) < 3);
                    if (plate || ring || star) px[y * w + x] = new Color32(0xC8, 0x98, 0x28, 255);
                }
            tex.SetPixels32(px); tex.Apply(false, false);
            return tex;
        }

        // ------------------------------------------------------------------ playing
        readonly List<CanvasGroup> hiddenGroups = new List<CanvasGroup>();
        readonly List<float> hiddenAlpha = new List<float>();

        void RestoreLive()
        {
            for (int i = 0; i < hiddenGroups.Count; i++) if (hiddenGroups[i] != null) hiddenGroups[i].alpha = hiddenAlpha[i];
            hiddenGroups.Clear(); hiddenAlpha.Clear();
        }

        /// <summary>live = de ekte bok-vinduene: de gjemmes (alpha 0) mens animasjonen går, så bare animasjonen sees.</summary>
        public void Play(Kind kind, Texture oldTex, Texture newTex, params GameObject[] live)
        {
            EnsureOverlay();
            if (running != null) { StopCoroutine(running); running = null; }
            RestoreLive();
            if (live != null)
                foreach (var go in live)
                {
                    if (go == null) continue;
                    var g = go.GetComponent<CanvasGroup>();
                    if (g == null) g = go.AddComponent<CanvasGroup>();
                    hiddenGroups.Add(g); hiddenAlpha.Add(g.alpha);
                    g.alpha = 0f;
                }
            running = StartCoroutine(Run(kind, oldTex, newTex));
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        IEnumerator Run(Kind kind, Texture oldTex, Texture newTex)
        {
            overlayRoot.SetActive(true);
            bool isOpen = kind == Kind.Open || kind == Kind.Close;
            float dur = (isOpen ? openTime : flipTime) / Mathf.Max(0.01f, speed);
            float t = 0f;
            while (t < dur)
            {
                float p = Mathf.Clamp01(t / dur);
                if (kind == Kind.Close) p = 1f - p;
                Frame(kind, Ease(p), oldTex, newTex);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            overlayRoot.SetActive(false);
            RestoreLive();
            ReleaseTextures();
            running = null;
        }

        void ReleaseTextures()
        {
            foreach (var rt in live) if (rt != null) { rt.Release(); Destroy(rt); }
            live.Clear();
        }

        static Rect LeftUv = new Rect(0f, 0f, 0.5f, 1f), RightUv = new Rect(0.5f, 0f, 0.5f, 1f);

        void Setup(RawImage r, Texture tex, Rect uv, float x, float width, float tilt, float scaleY)
        {
            r.gameObject.SetActive(tex != null && width > 0.5f);
            if (tex == null) return;
            r.texture = tex; r.uvRect = uv;
            var rt = r.rectTransform;
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(Mathf.Max(0.5f, width), BH);
            rt.localScale = new Vector3(1f, scaleY, 1f);
            float k = 1f - 0.4f * tilt;
            r.color = new Color(k, k, k, 1f);
        }

        /// <summary>One picture of the animation. s runs 0..1 (flipping: the old page turns, then the new one comes down).</summary>
        void Frame(Kind kind, float s, Texture oldTex, Texture newTex)
        {
            float spine = kind == Kind.Open || kind == Kind.Close ? -PW / 2f + PW / 2f * s : 0f;      // x of the spine, from the middle of the screen
            bool firstHalf = s < 0.5f;
            float frac = firstHalf ? 1f - 2f * s : 2f * s - 1f;                                  // how wide the turning page is (1 = flat)
            float tilt = 1f - frac;
            float sy = 1f + 0.05f * tilt;

            coverImg.gameObject.SetActive(false);
            shadow.gameObject.SetActive(false);

            switch (kind)
            {
                case Kind.Open:
                case Kind.Close:
                {
                    var spread = kind == Kind.Open ? newTex : oldTex;
                    staticLeft.gameObject.SetActive(false);
                    Setup(staticRight, spread, RightUv, spine, PW, 0f, 1f);
                    if (firstHalf)
                    {
                        coverImg.gameObject.SetActive(true);
                        flipping.gameObject.SetActive(false);
                        var cr = coverImg.rectTransform; cr.pivot = new Vector2(0f, 0.5f);
                        cr.anchoredPosition = new Vector2(spine, 0f); cr.sizeDelta = new Vector2(Mathf.Max(0.5f, PW * frac), BH); cr.localScale = new Vector3(1f, sy, 1f);
                        coverImg.color = new Color(1f - 0.4f * tilt, 1f - 0.4f * tilt, 1f - 0.4f * tilt, 1f);
                        PlaceShadow(spine + PW * frac, 1);
                    }
                    else
                    {
                        flipping.rectTransform.pivot = new Vector2(1f, 0.5f);
                        Setup(flipping, spread, LeftUv, spine, PW * frac, tilt, sy);
                        PlaceShadow(spine - PW * frac, -1);
                    }
                    break;
                }
                case Kind.TurnForward:
                    Setup(staticLeft, oldTex, LeftUv, 0f, PW, 0f, 1f);
                    Setup(staticRight, newTex, RightUv, 0f, PW, 0f, 1f);
                    if (firstHalf) { flipping.rectTransform.pivot = new Vector2(0f, 0.5f); Setup(flipping, oldTex, RightUv, 0f, PW * frac, tilt, sy); PlaceShadow(PW * frac, 1); }
                    else { flipping.rectTransform.pivot = new Vector2(1f, 0.5f); Setup(flipping, newTex, LeftUv, 0f, PW * frac, tilt, sy); PlaceShadow(-PW * frac, -1); }
                    break;
                case Kind.TurnBackward:
                    Setup(staticLeft, newTex, LeftUv, 0f, PW, 0f, 1f);
                    Setup(staticRight, oldTex, RightUv, 0f, PW, 0f, 1f);
                    if (firstHalf) { flipping.rectTransform.pivot = new Vector2(1f, 0.5f); Setup(flipping, oldTex, LeftUv, 0f, PW * frac, tilt, sy); PlaceShadow(-PW * frac, -1); }
                    else { flipping.rectTransform.pivot = new Vector2(0f, 0.5f); Setup(flipping, newTex, RightUv, 0f, PW * frac, tilt, sy); PlaceShadow(PW * frac, 1); }
                    break;
            }
            // the picture lying on top must be above the static ones
            flipping.transform.SetAsLastSibling();
            coverImg.transform.SetAsLastSibling();
        }

        void PlaceShadow(float edgeX, int side)
        {
            shadow.gameObject.SetActive(true);
            var r = shadow.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(side == 1 ? 0f : 1f, 0.5f);
            r.anchoredPosition = new Vector2(edgeX, 0f);
            r.sizeDelta = new Vector2(10f, BH - 20f);
        }
    }
}
