using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DeepVain.Items;

/// <summary>
/// Gives the tree (or rock) under the mouse a "selected" look: a white outline around the whole silhouette and a slightly brighter picture
/// (made by the Sprite Sway shader through TreeSway.SetHighlighted, so no new pictures are needed).
/// v3: the look only shows when the player is within reach of the tree/rock (Chop Reach / Mine Reach), and with the right tool in hand
///     (axe for trees, pickaxe for rocks). Rocks (they have a Mineable) are found as well as trees.
/// Put ONE of these on any object in the scene (for example the same object as WorldBuilder).
/// Other scripts can read:  TreeHover.Hovered / HoveredInRange  (the tree)  and  TreeHover.HoveredRock / HoveredRockInRange  (the rock).
/// </summary>
[DisallowMultipleComponent]
public class TreeHover : MonoBehaviour
{
    /// <summary>The tree object under the mouse right now (or null). Only set when it is within reach.</summary>
    public static GameObject Hovered { get; private set; }
    /// <summary>True when that tree is within reach (always true when Hovered is set).</summary>
    public static bool HoveredInRange { get; private set; }
    /// <summary>The rock under the mouse right now (or null). Only set when it is within reach.</summary>
    public static GameObject HoveredRock { get; private set; }
    public static bool HoveredRockInRange { get; private set; }

    [Header("Når")]
    [Tooltip("Bare vis det når spilleren har riktig verktøy i hånden: øks for trær, hakke for steiner. Av = alltid.")]
    public bool onlyWithAxe = true;
    [Tooltip("Ikke vis det når musa er over boka/menyene.")]
    public bool ignoreWhenOverUI = true;

    [Header("Rekkevidde (utlinjen vises og man kan hakke bare innen disse)")]
    [Tooltip("Hvor nær treet spilleren må stå (units, sidelengs, målt til stammen). Lavt = må stå rett ved siden av.")]
    public float chopReach = 1.6f;
    [Tooltip("Hvor nær steinen spilleren må stå (units, sidelengs).")]
    public float mineReach = 1.6f;
    [Tooltip("Hvor mye høyere/lavere enn spilleren det kan være (units).")]
    public float reachHeight = 2f;

    [Header("Referanser (valgfritt)")]
    [Tooltip("Tomt = finner spilleren selv (PlayerMovement).")]
    public Transform player;
    [Tooltip("Tomt = Camera.main.")]
    public Camera cam;

    [Header("Finne trær og steiner")]
    [Tooltip("Hvor ofte listen oppdateres (sekunder). Ting som spawnes senere blir med automatisk.")]
    public float refreshEvery = 0.5f;

    static readonly HashSet<GameObject> excluded = new HashSet<GameObject>();
    /// <summary>A tree or rock that must no longer be hovered or highlighted (it is falling / breaking / gone).</summary>
    public static void Exclude(GameObject go) { if (go != null) excluded.Add(go); }

    class Target { public GameObject go; public SpriteRenderer sr; public TreeSway sway; public Color baseColor; public bool rock; }

    readonly List<Target> targets = new List<Target>();
    readonly List<Vector2> shape = new List<Vector2>();
    float nextRefresh;
    Target current;
    PlayerEquipment equipment;

    void OnDisable() { Clear(); }

    /// <summary>Look for trees and rocks again right now.</summary>
    public void Refresh()
    {
        nextRefresh = Time.unscaledTime + refreshEvery;
        var seen = new HashSet<GameObject>();
        targets.RemoveAll(t => t.go == null);
        foreach (var t in targets) seen.Add(t.go);

        foreach (var m in FindObjectsByType<SpawnedTreeMarker>(FindObjectsSortMode.None)) Add(m.gameObject, seen, false);
        foreach (var s in FindObjectsByType<TreeSway>(FindObjectsSortMode.None)) Add(s.gameObject, seen, s.GetComponent<Mineable>() != null);
        foreach (var r in FindObjectsByType<Mineable>(FindObjectsSortMode.None)) Add(r.gameObject, seen, true);
    }

    void Add(GameObject go, HashSet<GameObject> seen, bool rock)
    {
        if (!seen.Add(go)) return;
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        targets.Add(new Target { go = go, sr = sr, sway = go.GetComponent<TreeSway>(), baseColor = sr.color, rock = rock });
    }

    void Clear()
    {
        if (current != null) SetLook(current, false);
        current = null; Hovered = null; HoveredInRange = false; HoveredRock = null; HoveredRockInRange = false;
    }

    void Update()
    {
        if (Time.unscaledTime >= nextRefresh) Refresh();
        if (cam == null) cam = Camera.main;
        if (player == null) { var pm = FindFirstObjectByType<PlayerMovement>(); if (pm != null) player = pm.transform; }
        if (equipment == null) equipment = FindFirstObjectByType<PlayerEquipment>();

        Target hit = null;
        bool allowed = cam != null && Mouse.current != null;
        if (allowed && ignoreWhenOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) allowed = false;

        if (allowed)
        {
            Vector3 sp = Mouse.current.position.ReadValue();
            sp.z = Mathf.Abs(cam.transform.position.z);
            Vector2 world = cam.ScreenToWorldPoint(sp);
            hit = Pick(world);
        }

        // the look and the click only count within reach
        if (current != null && excluded.Contains(current.go)) { SetLook(current, false); current = null; }

        if (hit != current)
        {
            if (current != null) SetLook(current, false);
            current = hit;
            if (current != null) SetLook(current, true);
        }
        Hovered = current != null && !current.rock ? current.go : null;
        HoveredInRange = Hovered != null;
        HoveredRock = current != null && current.rock ? current.go : null;
        HoveredRockInRange = HoveredRock != null;
    }

    bool ToolOk(Target t)
    {
        if (!onlyWithAxe) return true;
        if (equipment == null) return false;
        return t.rock ? equipment.HasPickaxe : equipment.HasAxe;
    }

    bool InRange(Target t)
    {
        if (player == null) return true;
        float range = t.rock ? mineReach : chopReach;
        Vector3 d = t.go.transform.position - player.position;
        return Mathf.Abs(d.x) <= range && Mathf.Abs(d.y) <= reachHeight;
    }

    void SetLook(Target t, bool on)
    {
        if (t.go == null) return;
        if (t.sway != null) t.sway.SetHighlighted(on);
        else if (t.sr != null) t.sr.color = on ? Color.Lerp(t.baseColor, Color.white, 0.35f) : t.baseColor;     // no TreeSway on it: only a lighter tint
    }

    /// <summary>The tree/rock whose drawn shape contains the point, within reach and with the right tool. The one drawn in front wins.</summary>
    Target Pick(Vector2 world)
    {
        Target best = null; int bestLayer = int.MinValue, bestOrder = int.MinValue;
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t.go == null || t.sr == null || !t.sr.enabled || !t.go.activeInHierarchy || t.sr.sprite == null) continue;
            if (excluded.Contains(t.go)) continue;
            if (!ToolOk(t) || !InRange(t)) continue;
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
