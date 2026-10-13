using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeepVain.Items;

namespace DeepVain.Levels
{
    /// <summary>
    /// Level-siden i boka. Oversikt (hovednivå + kort for hvert skill) og, når man klikker et skill, skillets egen side med skill-treet.
    ///   [I]            åpner / lukker level-siden (lukker sekken hvis den er åpen)
    ///   Tab            åpner sekken (lukker level-siden)
    ///   fanene         høyre kant av boka bytter mellom sekk og levels
    ///   Esc            tilbake fra skill-siden, så lukker boka
    /// Settes opp av menyen DeepVain > Levels > 2 - Build level book. Selve sideinnholdet bygges av koden mens spillet går.
    /// </summary>
    public class LevelBookUI : MonoBehaviour
    {
        public static LevelBookUI Instance { get; private set; }

        [Header("Data")]
        public PlayerLevels levels;
        public BookInventoryUI inventoryUi;

        [Header("Book")]
        public GameObject window;
        public RectTransform bookRoot;
        public RectTransform content;
        public RectTransform hud;

        [Header("Sprites")]
        public Sprite slotEmpty, slotLocked, slotSelected, panelInset, buttonBrass, popupFrame, seal, ownedFrame, availableFrame;
        public Sprite[] figureFrames;
        public float figureFrameTime = 0.15f;

        [Header("Layout (som i forhåndsvisningene)")]
        public float treeX = 358f, treeY = 54f, columnGap = 80f, rowGap = 56f;

        enum Page { Overview, Skill }
        Page page = Page.Overview;
        SkillType current = SkillType.Mining;
        string selNode;
        Image figureImage;

        static readonly Color Ink = new Color32(0x46, 0x28, 0x1A, 255);
        static readonly Color Dim = new Color32(0x8A, 0x64, 0x40, 255);
        static readonly Color Title = new Color32(0x4A, 0x28, 0x14, 255);
        static readonly Color Cream = new Color32(0xF8, 0xE4, 0xA8, 255);
        static readonly Color Gold = new Color32(0xC8, 0x98, 0x28, 255);
        static readonly Color Trough = new Color32(0x3A, 0x24, 0x14, 255);
        static readonly Color TroughEdge = new Color32(0x6A, 0x4A, 0x2A, 255);
        static readonly Color Green = new Color32(0x2A, 0x8A, 0x40, 255);
        static readonly Color Badge = new Color32(0x40, 0x26, 0x14, 255);
        static readonly Color Line = new Color32(0x8A, 0x70, 0x48, 255);
        static readonly Color LineOwned = new Color32(0xC8, 0x98, 0x28, 255);
        static readonly Color LineBack = new Color32(0x30, 0x1C, 0x10, 255);

        public bool IsOpen { get { return window != null && window.activeSelf; } }

        // ------------------------------------------------------------------ life
        [Header("Animasjon")]
        [Tooltip("Boka åpnes, lukkes og sidene blas med en rask animasjon (BookFx).")]
        public bool animate = true;
        [Tooltip("Sekunder det tar å bla en side.")]
        public float flipTime = 0.26f;
        [Tooltip("Sekunder det tar å åpne / lukke boka.")]
        public float openTime = 0.32f;
        BookFx fx;
        Canvas levelCanvas, inventoryCanvas;
        bool invWas;

        void Awake()
        {
            Instance = this;
            fx = GetComponent<BookFx>();
            if (fx == null) fx = gameObject.AddComponent<BookFx>();
            fx.flipTime = flipTime; fx.openTime = openTime;
            levelCanvas = GetComponent<Canvas>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; if (levels != null) { levels.OnChanged -= OnLevelsChanged; levels.OnXpGained -= OnXp; levels.OnSkillLevelUp -= OnSkillUp; levels.OnMainLevelUp -= OnMainUp; } }

        void Start()
        {
            if (levels == null) levels = FindFirstObjectByType<PlayerLevels>();
            if (inventoryUi == null) inventoryUi = FindFirstObjectByType<BookInventoryUI>();
            if (inventoryUi != null) inventoryCanvas = inventoryUi.GetComponentInParent<Canvas>();
            if (levels != null)
            {
                levels.OnChanged += OnLevelsChanged; levels.OnXpGained += OnXp; levels.OnSkillLevelUp += OnSkillUp; levels.OnMainLevelUp += OnMainUp;
            }
            if (window != null) window.SetActive(false);
            invWas = InvOpen;
        }

        void OnLevelsChanged() { if (IsOpen) Rebuild(); }

        void PlayFx(BookFx.Kind kind, Texture oldT, Texture newT)
        {
            fx.Play(kind, oldT, newT, window, inventoryUi != null ? inventoryUi.window : null);
        }

        bool InvOpen { get { return inventoryUi != null && inventoryUi.window != null && inventoryUi.window.activeSelf; } }
        bool CanAnimate { get { return animate && fx != null && levelCanvas != null; } }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.iKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); }
                else if (IsOpen && kb.escapeKey.wasPressedThisFrame)
                {
                    if (page == Page.Skill) GoOverview(); else Close();
                }
            }

            if (IsOpen && figureImage != null && figureFrames != null && figureFrames.Length > 0)
                figureImage.sprite = figureFrames[(int)(Time.unscaledTime / Mathf.Max(0.02f, figureFrameTime)) % figureFrames.Length];
        }

        /// <summary>Sekken åpnes og lukkes av BookInventoryUI (Tab / Esc). Her ser vi det og legger animasjonen på.</summary>
        void LateUpdate()
        {
            bool inv = InvOpen, lev = IsOpen;
            if (inv != invWas)
            {
                if (inv && lev)
                {
                    // Tab with the level page open: the bag takes over (the pages turn back)
                    if (CanAnimate && inventoryCanvas != null)
                    {
                        var oldT = fx.Capture(levelCanvas, hud != null ? hud.gameObject : null);
                        CloseSilently();
                        var newT = fx.Capture(inventoryCanvas);
                        PlayFx(BookFx.Kind.TurnBackward, oldT, newT);
                    }
                    else CloseSilently();
                }
                else if (inv && !lev)
                {
                    if (CanAnimate && inventoryCanvas != null) PlayFx(BookFx.Kind.Open, null, fx.Capture(inventoryCanvas));
                }
                else if (!inv && !lev)
                {
                    // the bag was closed (Tab / Esc): put it back for a moment to draw it, then close it with the animation
                    if (CanAnimate && inventoryCanvas != null)
                    {
                        inventoryUi.window.SetActive(true);
                        var oldT = fx.Capture(inventoryCanvas);
                        inventoryUi.window.SetActive(false);
                        PlayFx(BookFx.Kind.Close, oldT, null);
                    }
                }
            }
            invWas = InvOpen;
        }

        // ------------------------------------------------------------------ open / close / pages
        public void Open()
        {
            if (window == null || IsOpen) return;
            bool wasInv = InvOpen;
            RenderTexture oldT = null;
            if (CanAnimate && wasInv && inventoryCanvas != null) oldT = fx.Capture(inventoryCanvas);
            CloseInventory();
            window.SetActive(true);
            page = Page.Overview; selNode = null;
            Rebuild();
            if (CanAnimate)
            {
                var newT = fx.Capture(levelCanvas, hud != null ? hud.gameObject : null);
                if (wasInv) PlayFx(BookFx.Kind.TurnForward, oldT, newT);
                else PlayFx(BookFx.Kind.Open, null, newT);
            }
        }

        public void Close()
        {
            if (!IsOpen) return;
            RenderTexture oldT = CanAnimate ? fx.Capture(levelCanvas, hud != null ? hud.gameObject : null) : null;
            CloseSilently();
            if (oldT != null) PlayFx(BookFx.Kind.Close, oldT, null);
        }

        void CloseSilently() { if (window != null) window.SetActive(false); }

        void CloseInventory()
        {
            if (inventoryUi == null || inventoryUi.window == null || !inventoryUi.window.activeSelf) return;
            inventoryUi.window.SetActive(false);
            inventoryUi.ClearSelection();
            invWas = false;
        }

        /// <summary>Faner: true = levels, false = sekken.</summary>
        public void ShowPage(bool levelsPage)
        {
            if (levelsPage) { Open(); return; }
            if (!IsOpen) return;
            RenderTexture oldT = CanAnimate ? fx.Capture(levelCanvas, hud != null ? hud.gameObject : null) : null;
            CloseSilently();
            if (inventoryUi != null && inventoryUi.window != null)
            {
                inventoryUi.window.SetActive(true); inventoryUi.Refresh();
                invWas = true;
                if (oldT != null && inventoryCanvas != null) PlayFx(BookFx.Kind.TurnBackward, oldT, fx.Capture(inventoryCanvas));
            }
        }

        void OpenSkill(SkillType s)
        {
            Flip(() => { page = Page.Skill; current = s; selNode = null; Rebuild(); }, true);
        }

        void GoOverview()
        {
            Flip(() => { page = Page.Overview; selNode = null; Rebuild(); }, false);
        }

        /// <summary>Bytter innhold på level-siden med en side som blas.</summary>
        void Flip(Action change, bool forward)
        {
            if (!CanAnimate || !IsOpen) { change(); return; }
            var hide = hud != null ? hud.gameObject : null;
            var oldT = fx.Capture(levelCanvas, hide);
            change();
            var newT = fx.Capture(levelCanvas, hide);
            PlayFx(forward ? BookFx.Kind.TurnForward : BookFx.Kind.TurnBackward, oldT, newT);
        }

        // ------------------------------------------------------------------ small ui helpers
        RectTransform NewRT(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        Image Img(Transform parent, Sprite s, float x, float y, float w, float h, bool sliced = false, bool raycast = false, Color? color = null)
        {
            var rt = NewRT(s != null ? s.name : "Box", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s; img.raycastTarget = raycast; img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color ?? Color.white;
            Place(rt, x, y, w, h);
            return img;
        }

        Image Box(Transform parent, float x, float y, float w, float h, Color c) { return Img(parent, null, x, y, w, h, false, false, c); }

        TMP_Text Txt(Transform parent, string text, float x, float y, float w, float h, TextAlignmentOptions align, Color col, float size = 8f, bool bold = false)
        {
            var rt = NewRT("Text", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            if (bold) t.fontStyle = FontStyles.Bold;
            Place(rt, x, y, w, h);
            return t;
        }

        TMP_Text TxtC(Transform parent, string text, float cx, float cy, float w, Color col, float size = 8f, bool bold = false)
        {
            return Txt(parent, text, cx - w / 2f, cy - 6f, w, 12f, TextAlignmentOptions.Center, col, size, bold);
        }

        TMP_Text TxtR(Transform parent, string text, float rightX, float cy, float w, Color col, float size = 8f, bool bold = false)
        {
            return Txt(parent, text, rightX - w, cy - 6f, w, 12f, TextAlignmentOptions.MidlineRight, col, size, bold);
        }

        TMP_Text TxtL(Transform parent, string text, float x, float cy, float w, Color col, float size = 8f, bool bold = false)
        {
            return Txt(parent, text, x, cy - 6f, w, 12f, TextAlignmentOptions.MidlineLeft, col, size, bold);
        }

        void Bar(Transform parent, float x, float y, float w, float h, float frac, Color fill)
        {
            Box(parent, x, y, w, h, TroughEdge);
            Box(parent, x + 1, y + 1, w - 2, h - 2, Trough);
            int fw = Mathf.RoundToInt((w - 2) * Mathf.Clamp01(frac));
            if (fw <= 0) return;
            Box(parent, x + 1, y + 1, fw, h - 2, fill);
            Box(parent, x + 1, y + 1, fw, 1, Color.Lerp(fill, Color.white, 0.35f));
            Box(parent, x + 1, y + h - 2, fw, 1, Color.Lerp(fill, Color.black, 0.4f));
        }

        void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>Gjør et objekt klikkbart (med valgfri ramme som vises når musa er over).</summary>
        void Clickable(Image target, Action onClick, GameObject hoverFrame = null)
        {
            var c = target.gameObject.AddComponent<UiClick>();
            c.onClick = onClick; c.hover = hoverFrame;
            target.raycastTarget = true;
            if (hoverFrame != null) hoverFrame.SetActive(false);
        }

        static string N(int n) { return n.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' '); }

        // ------------------------------------------------------------------ drawing
        void Rebuild()
        {
            if (content == null || levels == null) return;
            Clear(content);
            figureImage = null;
            if (page == Page.Overview) BuildOverview(); else BuildSkill();
        }

        void BuildOverview()
        {
            var c = content;
            TxtC(c, "ADVENTURER", 166f, 32f, 200f, Title, 8f, true);
            TxtC(c, "SKILLS", 474f, 32f, 200f, Title, 8f, true);

            // figure + seal + main level
            figureImage = Img(c, figureFrames != null && figureFrames.Length > 0 ? figureFrames[0] : null, 112f, 52f, 108f, 156f);
            Img(c, seal, 140f, 198f, 52f, 52f);
            TxtC(c, levels.MainLevel.ToString(), 166f, 224f, 40f, Cream, 14f, true);
            TxtC(c, "LEVEL " + levels.MainLevel, 166f, 266f, 200f, Title, 12f, true);
            float mf = levels.MainXpToNextLevel <= 0 ? 1f : levels.MainXpInto / (float)levels.MainXpToNextLevel;
            Bar(c, 66f, 284f, 200f, 9f, mf, Gold);
            TxtC(c, levels.MainXpToNextLevel <= 0 ? "MAX" : N(levels.MainXpInto) + " / " + N(levels.MainXpToNextLevel) + " XP", 166f, 302f, 200f, Ink);

            // the skill cards
            var order = new[] { SkillType.Foraging, SkillType.Woodcutting, SkillType.Mining, SkillType.Combat, SkillType.Running,
                                SkillType.Fishing, SkillType.Swordsmanship, SkillType.Magic, SkillType.Archery };
            for (int i = 0; i < order.Length; i++)
                BuildCard(c, order[i], 352f + (i % 3) * 86f, 54f + (i / 3) * 82f);
            TxtC(c, "Click a skill to open its tree", 478f, 306f, 250f, Dim);
        }

        void BuildCard(Transform parent, SkillType s, float x, float y)
        {
            var def = levels.Def(s);
            bool avail = def != null && def.available;
            string name = def != null ? def.displayName : s.ToString();
            var bg = Img(parent, panelInset, x, y, 80f, 76f, true, true);
            var hover = BuildFrame(bg.transform, -2f, -2f, 84f, 80f, new Color32(0xE0, 0x9A, 0x20, 255));
            if (avail) Clickable(bg, () => OpenSkill(s), hover); else hover.SetActive(false);

            Img(parent, avail ? slotEmpty : slotLocked, x + 22f, y + 5f, 36f, 36f);
            if (def != null && def.icon != null)
            {
                var ic = Img(parent, def.icon, x + 25f, y + 8f, 30f, 30f);
                ic.preserveAspect = true;
                if (!avail) ic.color = new Color(1f, 1f, 1f, 0.45f);
            }
            if (!avail)
            {
                TxtC(parent, name, x + 40f, y + 50f, 78f, Dim);
                TxtC(parent, "Locked", x + 40f, y + 62f, 78f, Dim);
                return;
            }
            TxtC(parent, name, x + 40f, y + 50f, 78f, Title, 8f, true);
            Bar(parent, x + 8f, y + 58f, 64f, 7f, levels.GetProgress(s), def.color);
            Box(parent, x + 56f, y + 3f, 20f, 12f, Badge);
            TxtC(parent, levels.GetLevel(s).ToString(), x + 66f, y + 9f, 20f, Cream, 8f, true);
            int pts = levels.PointsFree(s);
            if (pts > 0)
            {
                Box(parent, x + 3f, y + 3f, 18f, 12f, Green);
                TxtC(parent, "+" + pts, x + 12f, y + 9f, 18f, Color.white, 8f, true);
            }
        }

        GameObject BuildFrame(Transform parent, float x, float y, float w, float h, Color col)
        {
            var root = NewRT("HoverFrame", parent);
            Place(root, x, y, w, h);
            Box(root, 0, 0, w, 2, col); Box(root, 0, h - 2, w, 2, col); Box(root, 0, 0, 2, h, col); Box(root, w - 2, 0, 2, h, col);
            return root.gameObject;
        }

        // ---------------------------------------------------------------- skill page
        void BuildSkill()
        {
            var c = content;
            var def = levels.Def(current);
            if (def == null) { page = Page.Overview; BuildOverview(); return; }
            TxtC(c, def.displayName.ToUpperInvariant(), 166f, 32f, 200f, Title, 8f, true);
            TxtC(c, "SKILL TREE", 474f, 32f, 200f, Title, 8f, true);

            // back button
            var back = Img(c, buttonBrass, 40f, 48f, 70f, 16f, true, true);
            Clickable(back, GoOverview);
            TxtC(back.transform, "< SKILLS", 35f, 8f, 70f, Title, 8f, true);

            // the big icon
            Img(c, slotEmpty, 130f, 74f, 72f, 72f);
            if (def.icon != null) { var ic = Img(c, def.icon, 136f, 80f, 60f, 60f); ic.preserveAspect = true; }
            TxtC(c, def.displayName.ToUpperInvariant(), 166f, 158f, 200f, Title, 12f, true);
            int lvl = levels.GetLevel(current);
            TxtC(c, "Level " + lvl, 166f, 176f, 200f, Ink, 8f, true);
            Bar(c, 66f, 188f, 200f, 10f, levels.GetProgress(current), def.color);
            int to = levels.GetXpToNext(current);
            TxtC(c, to <= 0 ? "MAX" : N(levels.GetXpInto(current)) + " / " + N(to) + " XP", 166f, 208f, 200f, Ink);

            Box(c, 56f, 222f, 220f, 1f, Dim);
            int free = levels.PointsFree(current);
            TxtL(c, "Skill points", 60f, 236f, 120f, Dim); TxtR(c, free.ToString(), 272f, 236f, 60f, free > 0 ? Green : Ink, 8f, true);
            int total = 0; foreach (var n in def.nodes) if (n != null && n.cost > 0) total++;
            int owned = levels.UnlockedCount(current) - 1;
            TxtL(c, "Perks unlocked", 60f, 252f, 120f, Dim); TxtR(c, Mathf.Max(0, owned) + " / " + total, 272f, 252f, 60f, Ink, 8f, true);
            TxtL(c, "Total XP", 60f, 268f, 120f, Dim); TxtR(c, N(levels.GetXp(current)), 272f, 268f, 80f, Ink, 8f, true);
            Box(c, 56f, 286f, 220f, 1f, Dim);
            string active = ActiveSummary(def);
            TxtC(c, active, 166f, 300f, 230f, Dim);
            if (free > 0) TxtC(c, free + " point" + (free == 1 ? "" : "s") + " to spend", 166f, 320f, 200f, Green, 8f, true);

            // the tree
            DrawTree(c, def);
            DrawDetail(c, def);
        }

        string ActiveSummary(SkillDef def)
        {
            var parts = new List<string>();
            foreach (var n in def.nodes)
                if (n != null && n.cost > 0 && levels.IsUnlocked(def.skill, n)) parts.Add(n.displayName);
            if (parts.Count == 0) return "No perks yet";
            string s = string.Join(", ", parts.ToArray());
            return s.Length > 44 ? s.Substring(0, 41) + "..." : s;
        }

        Vector2 NodePos(SkillNode n) { return new Vector2(treeX + n.column * columnGap, treeY + n.row * rowGap); }

        void DrawTree(Transform c, SkillDef def)
        {
            const float S = 36f;
            // connectors first (under the nodes)
            foreach (var n in def.nodes)
            {
                if (n == null || string.IsNullOrEmpty(n.parentId)) continue;
                var p = def.Find(n.parentId); if (p == null) continue;
                Vector2 a = NodePos(p), b = NodePos(n);
                bool own = levels.IsUnlocked(def.skill, n);
                Color col = own ? LineOwned : Line;
                float ax = a.x + S / 2f, ay = a.y + S, bx = b.x + S / 2f, by = b.y;
                float midY = Mathf.Round((ay + by) / 2f) - 2f;
                Seg(c, ax, ay, ax, midY, LineBack, 4f); Seg(c, ax, midY, bx, midY, LineBack, 4f); Seg(c, bx, midY, bx, by, LineBack, 4f);
                Seg(c, ax, ay, ax, midY, col, 2f); Seg(c, ax, midY, bx, midY, col, 2f); Seg(c, bx, midY, bx, by, col, 2f);
            }
            foreach (var n in def.nodes)
            {
                if (n == null) continue;
                Vector2 p = NodePos(n);
                var st = levels.StateOf(def.skill, n);
                bool selected = n.id == selNode;
                var bgSprite = selected ? slotSelected : (st == PlayerLevels.NodeState.Locked ? slotLocked : slotEmpty);
                var bg = Img(c, bgSprite, p.x, p.y, S, S, false, true);
                string id = n.id;
                Clickable(bg, () => { selNode = id; Rebuild(); });
                if (n.icon != null)
                {
                    var ic = Img(c, n.icon, p.x + 3f, p.y + 3f, 30f, 30f);
                    ic.preserveAspect = true;
                    if (st == PlayerLevels.NodeState.Locked) ic.color = new Color(1f, 1f, 1f, 0.45f);
                }
                if (st == PlayerLevels.NodeState.Owned && ownedFrame != null) Img(c, ownedFrame, p.x, p.y, S, S);
                else if (st == PlayerLevels.NodeState.Available && availableFrame != null) Img(c, availableFrame, p.x, p.y, S, S);
                TxtL(c, "Lv " + n.requiredLevel, p.x + S + 3f, p.y + 19f, 30f, st == PlayerLevels.NodeState.Locked ? Dim : Ink);
                if (st == PlayerLevels.NodeState.Owned) { Box(c, p.x + S - 10f, p.y - 2f, 12f, 11f, Badge); TxtC(c, "v", p.x + S - 4f, p.y + 3f, 12f, Cream, 8f, true); }
                else if (st == PlayerLevels.NodeState.Available) { Box(c, p.x + S - 10f, p.y - 2f, 12f, 11f, Green); TxtC(c, "+", p.x + S - 4f, p.y + 3f, 12f, Color.white, 8f, true); }
            }
        }

        void Seg(Transform c, float x1, float y1, float x2, float y2, Color col, float w)
        {
            float x = Mathf.Min(x1, x2) - (x1 == x2 ? w / 2f : 0f), y = Mathf.Min(y1, y2) - (y1 == y2 ? w / 2f : 0f);
            float ww = x1 == x2 ? w : Mathf.Abs(x2 - x1), hh = y1 == y2 ? w : Mathf.Abs(y2 - y1);
            Box(c, x, y, ww, hh, col);
        }

        void DrawDetail(Transform c, SkillDef def)
        {
            const float X = 350f, Y = 270f, W = 254f, H = 66f;
            var panel = Img(c, popupFrame, X, Y, W, H, true, true);
            var n = def.Find(selNode);
            if (n == null)
            {
                TxtC(c, "Click a node to read it.", X + W / 2f, Y + 20f, W - 10f, Dim);
                TxtC(c, "gold = owned   green + = can unlock   grey = locked", X + W / 2f, Y + 38f, W - 10f, Dim);
                return;
            }
            var st = levels.StateOf(def.skill, n);
            Img(c, slotEmpty, X + 8f, Y + 8f, 36f, 36f);
            if (n.icon != null) { var ic = Img(c, n.icon, X + 11f, Y + 11f, 30f, 30f); ic.preserveAspect = true; }
            TxtL(c, n.displayName, X + 50f, Y + 14f, 196f, Title, 8f, true);
            TxtL(c, "Requires Lv " + n.requiredLevel + (n.cost > 0 ? "   Cost " + n.cost + " pt" : ""), X + 50f, Y + 26f, 196f, Dim);
            TxtL(c, n.description, X + 50f, Y + 38f, 196f, Ink);
            if (st == PlayerLevels.NodeState.Available)
            {
                var btn = Img(c, buttonBrass, X + 8f, Y + H - 22f, W - 16f, 16f, true, true);
                var node = n; var skill = def.skill;
                Clickable(btn, () => { levels.Unlock(skill, node); });
                TxtC(btn.transform, "Unlock  (" + n.cost + " point" + (n.cost == 1 ? "" : "s") + ")", (W - 16f) / 2f, 8f, W - 20f, Title, 8f, true);
            }
            else if (st == PlayerLevels.NodeState.Owned)
                TxtC(c, "Owned", X + W / 2f, Y + H - 14f, 100f, Green, 8f, true);
            else
                TxtC(c, levels.WhyLocked(def.skill, n), X + W / 2f, Y + H - 14f, W - 20f, Dim, 8f, false);
        }

        // ------------------------------------------------------------------ toasts (always visible, also when the book is closed)
        readonly List<GameObject> toasts = new List<GameObject>();

        // ---- XP numbers next to the player: a small tool icon and the number in the skill's colour, on a random side, floats up and fades
        [Header("XP-tall ved spilleren")]
        [Tooltip("Hvor langt fra midten av spilleren, til siden (units, tilfeldig venstre/høyre). Større = lenger fra kroppen.")]
        public Vector2 xpSideRange = new Vector2(0.60f, 0.90f);
        [Tooltip("Hvor høyt over foten tallet starter (units).")]
        public float xpStartHeight = 0.85f;
        [Tooltip("Hvor fort det stiger (units per sekund).")]
        public float xpRiseSpeed = 0.30f;
        public float xpLifetime = 1.5f;
        public float xpNumberSize = 8f;
        int livePops;

        static readonly Dictionary<SkillType, Sprite> xpIcons = new Dictionary<SkillType, Sprite>();

        static Color XpColor(SkillType s)
        {
            switch (s)
            {
                case SkillType.Mining: return new Color32(0x78, 0xB0, 0xF0, 255);
                case SkillType.Woodcutting: return new Color32(0x7A, 0xE0, 0x50, 255);
                case SkillType.Combat: return new Color32(0xF0, 0x70, 0x60, 255);
                case SkillType.Foraging: return new Color32(0xC8, 0x90, 0xF0, 255);
            }
            return new Color32(0x7A, 0xE0, 0x50, 255);
        }

        // 7x7 pictures drawn by the code (no files needed): m metal, d dark metal, s shine, w wood, g gold
        static string[] IconRows(SkillType s)
        {
            switch (s)
            {
                case SkillType.Mining:      // pickaxe
                    return new[] { ".smmmm.", "m..w..m", "d..w..d", "...w...", "...w...", "...w...", "...w..." };
                case SkillType.Woodcutting: // axe
                    return new[] { "..wsmd.", "..wmmmd", "..wmmd.", "..wmd..", "..w....", "..w....", "..w...." };
                case SkillType.Combat:      // sword
                    return new[] { "...s...", "...m...", "...m...", "...m...", "...m...", ".ggggg.", "...w..." };
                case SkillType.Foraging:    // a red mushroom
                    return new[] { "..rrr..", ".rsrrr.", "rrrrsrr", "ddddddd", "..mmm..", "..mmm..", "..ddd.." };
            }
            return null;
        }

        static Color32 IconColor(char c)
        {
            switch (c)
            {
                case 'm': return new Color32(0xD8, 0xDE, 0xEC, 255);
                case 'd': return new Color32(0x8C, 0x96, 0xB0, 255);
                case 's': return new Color32(255, 255, 255, 255);
                case 'w': return new Color32(0xB4, 0x80, 0x44, 255);
                case 'g': return new Color32(0xF0, 0xC0, 0x30, 255);
                case 'r': return new Color32(0xE0, 0x40, 0x50, 255);
            }
            return new Color32(0, 0, 0, 0);
        }

        static Sprite XpIcon(SkillType s)
        {
            Sprite sp;
            if (xpIcons.TryGetValue(s, out sp) && sp != null) return sp;
            var rows = IconRows(s);
            if (rows == null) { xpIcons[s] = null; return null; }
            var tex = new Texture2D(9, 9, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "XpIcon_" + s };
            var outline = new Color32(0x10, 0x14, 0x10, 255);
            var px = new Color32[81];
            bool[,] on = new bool[7, 7];
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++) on[x, y] = rows[y][x] != '.';
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                {
                    if (!on[x, y]) continue;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            bool filled = nx >= 0 && nx < 7 && ny >= 0 && ny < 7 && on[nx, ny];
                            if (!filled) px[(8 - (ny + 1)) * 9 + (nx + 1)] = outline;       // the outline sits one pixel outside the picture
                        }
                }
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                    if (on[x, y]) px[(8 - (y + 1)) * 9 + (x + 1)] = IconColor(rows[y][x]);
            tex.SetPixels32(px); tex.Apply(false, false);
            sp = Sprite.Create(tex, new Rect(0, 0, 9, 9), new Vector2(0.5f, 0.5f), 1f);
            xpIcons[s] = sp;
            return sp;
        }

        void OnXp(SkillType s, int amount)
        {
            if (s == SkillType.Running) return;                  // running gives XP all the time: no number for it
            if (hud == null || levels == null || Camera.main == null || livePops >= 12) return;
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float dx = side * UnityEngine.Random.Range(xpSideRange.x, xpSideRange.y);
            Vector3 p = levels.transform.position + new Vector3(dx, xpStartHeight + UnityEngine.Random.Range(-0.08f, 0.08f), 0f);
            StartCoroutine(XpPop(p, "+" + amount, XpIcon(s), XpColor(s)));
        }

        IEnumerator XpPop(Vector3 world, string number, Sprite icon, Color col)
        {
            livePops++;
            var rt = NewRT("XpPop", hud);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(60f, 12f);
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;

            var num = NewRT("Number", rt);
            var nt = num.gameObject.AddComponent<TextMeshProUGUI>();
            nt.text = number; nt.fontSize = xpNumberSize * 1.5f; nt.color = col; nt.fontStyle = FontStyles.Bold; nt.alignment = TextAlignmentOptions.Center;
            nt.raycastTarget = false; nt.textWrappingMode = TextWrappingModes.NoWrap; nt.outlineWidth = 0.28f; nt.outlineColor = new Color32(0x10, 0x14, 0x10, 255);
            num.anchorMin = num.anchorMax = new Vector2(0.5f, 0.5f); num.pivot = new Vector2(0.5f, 0.5f);
            float textW = Mathf.Ceil(nt.GetPreferredValues(number).x) + 2f;
            num.sizeDelta = new Vector2(textW, 12f);
            float iconW = icon != null ? 9f : 0f, gap = icon != null ? 1f : 0f;
            float total = iconW + gap + textW;
            num.anchoredPosition = new Vector2(-total / 2f + iconW + gap + textW / 2f, 0f);
            if (icon != null)
            {
                var ic = NewRT("Icon", rt);
                var im = ic.gameObject.AddComponent<Image>(); im.sprite = icon; im.raycastTarget = false;
                ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 0.5f); ic.pivot = new Vector2(0.5f, 0.5f); ic.sizeDelta = new Vector2(9f, 9f);
                ic.anchoredPosition = new Vector2(-total / 2f + 4.5f, 0f);
            }

            var canvas = hud.GetComponentInParent<Canvas>();
            Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            float age = 0f;
            while (age < xpLifetime && rt != null)
            {
                age += Time.unscaledDeltaTime;
                if (age > 0.1f && nt.fontSize > xpNumberSize) nt.fontSize = xpNumberSize;          // the pop: big for a moment
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 wp = world + new Vector3(0f, xpRiseSpeed * age, 0f);
                    Vector2 local;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(hud, cam.WorldToScreenPoint(wp), uiCam, out local))
                        rt.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y));
                }
                float fade = age < xpLifetime - 0.5f ? 1f : Mathf.Clamp01((xpLifetime - age) / 0.5f);
                group.alpha = Mathf.Ceil(fade * 4f) / 4f;                                           // stepped, so it fades like pixel art
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
            livePops--;
        }

        void OnSkillUp(SkillType s, int level)
        {
            var def = levels.Def(s);
            Toast((def != null ? def.displayName.ToUpperInvariant() : s.ToString()) + " LEVEL " + level + "!  +1 skill point", new Color32(0xFF, 0xE0, 0x70, 255), 10f, 3.2f);
        }

        void OnMainUp(int level) { Toast("LEVEL " + level + "!", new Color32(0xFF, 0xE0, 0x70, 255), 12f, 3.5f); }

        void Toast(string text, Color col, float size, float life)
        {
            if (hud == null) return;
            toasts.RemoveAll(t => t == null);
            while (toasts.Count >= 4) { Destroy(toasts[0]); toasts.RemoveAt(0); }
            var rt = NewRT("Toast", hud);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(300f, 14f);
            var shadow = rt.gameObject.AddComponent<CanvasGroup>();
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = col; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            t.fontStyle = FontStyles.Bold; t.textWrappingMode = TextWrappingModes.NoWrap; t.outlineWidth = 0.3f; t.outlineColor = new Color32(0x1A, 0x0E, 0x08, 255);
            toasts.Add(rt.gameObject);
            StartCoroutine(ToastRoutine(rt, shadow, life));
        }

        IEnumerator ToastRoutine(RectTransform rt, CanvasGroup g, float life)
        {
            float t = 0f;
            while (t < life && rt != null)
            {
                t += Time.unscaledDeltaTime;
                int slot = Mathf.Max(0, toasts.IndexOf(rt.gameObject));
                float targetY = -22f - slot * 13f - Mathf.Min(6f, t * 10f);
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, new Vector2(0f, targetY), 0.35f);
                g.alpha = Mathf.Clamp01((life - t) / 0.5f);
                yield return null;
            }
            if (rt != null) { toasts.Remove(rt.gameObject); Destroy(rt.gameObject); }
        }

        // ------------------------------------------------------------------ click + hover on a picture
        public class UiClick : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public Action onClick;
            public GameObject hover;
            public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && onClick != null) onClick(); }
            public void OnPointerEnter(PointerEventData e) { if (hover != null) hover.SetActive(true); }
            public void OnPointerExit(PointerEventData e) { if (hover != null) hover.SetActive(false); }
            void OnDisable() { if (hover != null) hover.SetActive(false); }
        }
    }
}
