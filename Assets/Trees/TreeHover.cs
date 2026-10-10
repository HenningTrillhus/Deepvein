using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DeepVain.Items;

/// <summary>
/// Gives the tree under the mouse a "selected" look: a white outline around the whole silhouette and a slightly brighter picture
/// (made by the Sprite Sway shader through TreeSway.SetHighlighted, so no new pictures are needed).
/// Put ONE of these on any object in the scene (for example the same object as WorldBuilder). It finds the trees by itself:
/// everything the TreeSpawner made, and everything that has a TreeSway.
/// Other scripts (the chopping) can read:  TreeHover.Hovered  (the tree under the mouse or null)  and  TreeHover.HoveredInRange.
/// </summary>
[DisallowMultipleComponent]
public class TreeHover : MonoBehaviour
{
    /// <summary>The tree object under the mouse right now (or null).</summary>
    public static GameObject Hovered { get; private set; }
    /// <summary>True when that tree is also close enough to the player to be chopped.</summary>
    public static bool HoveredInRange { get; private set; }

    [Header("Når")]
    [Tooltip("Bare vis det når spilleren har en øks i hånden (Equipment-siden). Av = alltid.")]
    public bool onlyWithAxe = true;
    [Tooltip("Ikke vis det når musa er over boka/menyene.")]
    public bool ignoreWhenOverUI = true;

    [Header("Rekkevidde")]
    [Tooltip("Hvor nær spilleren treet må stå for å kunne hogges (units, sidelengs). Brukes av hogge-scriptet.")]
    public float chopRange = 3f;
    [Tooltip("Vis utlinjen bare på trær innen rekkevidde. Av = alle trær under musa lyser, også de som er langt unna.")]
    public bool onlyInRange = false;

    [Header("Referanser (valgfritt)")]
    [Tooltip("Tomt = finner spilleren selv (PlayerMovement).")]
    public Transform player;
    [Tooltip("Tomt = Camera.main.")]
    public Camera cam;

    [Header("Finne trær")]
    [Tooltip("Hvor ofte listen med trær oppdateres (sekunder). Trær som spawnes senere blir med automatisk.")]
    public float refreshEvery = 0.5f;

    static readonly HashSet<GameObject> excluded = new HashSet<GameObject>();
    /// <summary>A tree that must no longer be hovered or highlighted (it is falling / gone).</summary>
    public static void Exclude(GameObject tree) { if (tree != null) excluded.Add(tree); }

    class Target { public GameObject go; public SpriteRenderer sr; public TreeSway sway; public Color baseColor; }

    readonly List<Target> targets = new List<Target>();
    readonly List<Vector2> shape = new List<Vector2>();
    float nextRefresh;
    Target current;
    PlayerEquipment equipment;

    void OnDisable() { Clear(); }

    /// <summary>Look for trees again right now (call it after the world is made, if you like).</summary>
    public void Refresh()
    {
        nextRefresh = Time.unscaledTime + refreshEvery;
        var seen = new HashSet<GameObject>();
        targets.RemoveAll(t => t.go == null);
        foreach (var t in targets) seen.Add(t.go);

        foreach (var m in FindObjectsByType<SpawnedTreeMarker>(FindObjectsSortMode.None)) Add(m.gameObject, seen);
        foreach (var s in FindObjectsByType<TreeSway>(FindObjectsSortMode.None)) Add(s.gameObject, seen);
    }

    void Add(GameObject go, HashSet<GameObject> seen)
    {
        if (!seen.Add(go)) return;
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        targets.Add(new Target { go = go, sr = sr, sway = go.GetComponent<TreeSway>(), baseColor = sr.color });
    }

    void Clear()
    {
        if (current != null) SetLook(current, false);
        current = null; Hovered = null; HoveredInRange = false;
    }

    void Update()
    {
        if (Time.unscaledTime >= nextRefresh) Refresh();
        if (cam == null) cam = Camera.main;
        if (player == null) { var pm = FindFirstObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform; }
        if (equipment == null) equipment = FindFirstObjectByType<PlayerEquipment>();

        Target hit = null;
        bool allowed = cam != null && Mouse.current != null;
        if (allowed && onlyWithAxe && (equipment == null || !equipment.HasAxe)) allowed = false;
        if (allowed && ignoreWhenOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) allowed = false;

        if (allowed)
        {
            Vector3 sp = Mouse.current.position.ReadValue();
            sp.z = Mathf.Abs(cam.transform.position.z);
            Vector2 world = cam.ScreenToWorldPoint(sp);
            hit = Pick(world);
        }

        bool inRange = hit != null && InRange(hit);
        if (hit != null && onlyInRange && !inRange) hit = null;
        if (current != null && excluded.Contains(current.go)) { SetLook(current, false); current = null; }

        if (hit != current)
        {
            if (current != null) SetLook(current, false);
            current = hit;
            if (current != null) SetLook(current, true);
        }
        Hovered = current != null ? current.go : null;
        HoveredInRange = current != null && inRange;
    }

    bool InRange(Target t)
    {
        if (player == null) return true;
        return Mathf.Abs(t.go.transform.position.x - player.position.x) <= chopRange;
    }

    void SetLook(Target t, bool on)
    {
        if (t.go == null) return;
        if (t.sway != null) t.sway.SetHighlighted(on);
        else if (t.sr != null) t.sr.color = on ? Color.Lerp(t.baseColor, Color.white, 0.35f) : t.baseColor;     // no TreeSway on it: only a lighter tint
    }

    /// <summary>The tree whose drawn shape contains the point. When trees overlap, the one drawn in front wins.</summary>
    Target Pick(Vector2 world)
    {
        Target best = null; int bestLayer = int.MinValue, bestOrder = int.MinValue;
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t.go == null || t.sr == null || !t.sr.enabled || !t.go.activeInHierarchy || t.sr.sprite == null) continue;
            if (excluded.Contains(t.go)) continue;
            if (!t.sr.bounds.Contains(new Vector3(world.x, world.y, t.sr.bounds.center.z))) continue;
            if (!Inside(t, world)) continue;
            int layer = SortingLayer.GetLayerValueFromID(t.sr.sortingLayerID), order = t.sr.sortingOrder;
            if (best == null || layer > bestLayer || (layer == bestLayer && order > bestOrder)) { best = t; bestLayer = layer; bestOrder = order; }
        }
        return best;
    }

    // point in the sprite's outline (Unity makes one from the transparent pixels when the picture is imported); falls back to the picture's box
    bool Inside(Target t, Vector2 world)
    {
        var sprite = t.sr.sprite;
        Vector3 l3 = t.sr.transform.InverseTransformPoint(world);
        Vector2 p = new Vector2(l3.x, l3.y);
        if (t.sr.flipX) p.x = -p.x;
        if (t.sr.flipY) p.y = -p.y;

        int n = sprite.GetPhysicsShapeCount();
        if (n == 0) return sprite.bounds.Contains(new Vector3(p.x, p.y, 0f));
        for (int s = 0; s < n; s++)
        {
            shape.Clear();
            sprite.GetPhysicsShape(s, shape);
            if (PointInPolygon(p, shape)) return true;
        }
        return false;
    }

    static bool PointInPolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }
}
