using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// The edges of a map (for instance the inside of the goblin camp):
///   - the CAMERA follows the player but stops at the edges: it never shows anything outside the map, and when the player walks towards an edge he simply
///     walks on while the camera stands still (CameraFollow asks this script; it only counts while the player is inside the area, so you can have several maps in one scene)
///   - invisible WALLS stand at the left and right edge (and, if you want, a ceiling), so the player cannot walk or jump out of the map.
/// The area is found from the tiles: put this script on the root object of the map and it takes the width and height of the ground tilemaps below it
/// (Tilemaps that have a collider). Or tick Manual Area and type the numbers. Right-click the script header: Fit to tilemaps / Build walls now / Clear walls.
/// </summary>
[DisallowMultipleComponent]
public class MapBounds : MonoBehaviour
{
    public static readonly List<MapBounds> All = new List<MapBounds>();

    [Header("Området (verdens-koordinater)")]
    [Tooltip("På: bruker Min og Max under, som du skriver selv. Av: finner området fra tilemapene under dette objektet når spillet starter.")]
    public bool manualArea = false;
    public Vector2 min = new Vector2(-20f, -10f);
    public Vector2 max = new Vector2(20f, 20f);
    [Tooltip("Bare tilemaps som har en collider regnes med (bakken), ikke bakgrunns-tilemaps. Av: alle tilemaps.")]
    public bool onlyTilemapsWithColliders = true;

    [Header("Kamera")]
    [Tooltip("Kameraet stopper også oppe og nede (ellers bare til sidene).")]
    public bool limitCameraY = false;
    [Tooltip("Hvor langt utenfor området (til sidene) spilleren kan være og kameraet fortsatt bruker disse kantene (units).")]
    public float insideMargin = 2f;

    [Header("Usynlige vegger")]
    public bool createWalls = true;
    [Tooltip("Fysikk-laget veggene får. Samme som bakken, så spilleren og fiendene stopper mot dem.")]
    public string wallLayerName = "Ground";
    public float wallThickness = 1f;
    [Tooltip("Hvor høyt over og under området veggene går (units), så man ikke hopper over.")]
    public float wallExtraHeight = 60f;
    [Tooltip("Et usynlig tak også.")]
    public bool createCeiling = false;
    public float ceilingHeight = 12f;

    public Rect Area { get; private set; }

    Transform wallsRoot;

    void Awake()
    {
        Refresh();
        if (createWalls && Application.isPlaying) BuildWalls();
    }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>Works out Area (from the numbers or from the tilemaps).</summary>
    public void Refresh()
    {
        if (manualArea) { Area = Rect.MinMaxRect(min.x, min.y, max.x, max.y); return; }
        Rect r;
        if (FitRect(out r)) Area = r;
        else Area = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    bool FitRect(out Rect r)
    {
        r = new Rect();
        bool any = false; float x0 = 0, y0 = 0, x1 = 0, y1 = 0;
        foreach (var tm in GetComponentsInChildren<Tilemap>(false))
        {
            if (onlyTilemapsWithColliders && tm.GetComponent<Collider2D>() == null) continue;
            tm.CompressBounds();
            var b = tm.cellBounds;
            if (b.size.x <= 0 || b.size.y <= 0) continue;
            Vector3 lo = tm.CellToWorld(new Vector3Int(b.xMin, b.yMin, 0));
            Vector3 hi = tm.CellToWorld(new Vector3Int(b.xMax, b.yMax, 0));
            if (!any) { x0 = Mathf.Min(lo.x, hi.x); y0 = Mathf.Min(lo.y, hi.y); x1 = Mathf.Max(lo.x, hi.x); y1 = Mathf.Max(lo.y, hi.y); any = true; }
            else { x0 = Mathf.Min(x0, lo.x, hi.x); y0 = Mathf.Min(y0, lo.y, hi.y); x1 = Mathf.Max(x1, lo.x, hi.x); y1 = Mathf.Max(y1, lo.y, hi.y); }
        }
        if (any) r = Rect.MinMaxRect(x0, y0, x1, y1);
        return any;
    }

    /// <summary>True if a point is inside the map (a little extra to the sides, Inside Margin).</summary>
    public bool Contains(Vector2 p)
    {
        return p.x >= Area.xMin - insideMargin && p.x <= Area.xMax + insideMargin && p.y >= Area.yMin - wallExtraHeight && p.y <= Area.yMax + wallExtraHeight;
    }

    /// <summary>The map the point is inside (or null). The camera uses this.</summary>
    public static MapBounds Find(Vector2 p)
    {
        for (int i = 0; i < All.Count; i++) if (All[i] != null && All[i].Contains(p)) return All[i];
        return null;
    }

    // ------------------------------------------------------------------------------------------------ walls
    [ContextMenu("Fit to tilemaps")]
    void FitNow()
    {
        Rect r;
        if (FitRect(out r)) { min = r.min; max = r.max; Area = r; Debug.Log("[MapBounds] Området er nå x " + r.xMin.ToString("0.0") + " til " + r.xMax.ToString("0.0") + ", y " + r.yMin.ToString("0.0") + " til " + r.yMax.ToString("0.0") + " (" + r.width.ToString("0.0") + " units bredt).", this); }
        else Debug.LogWarning("[MapBounds] Fant ingen tilemaps (med collider) under dette objektet.", this);
    }

    [ContextMenu("Build walls now")]
    public void BuildWalls()
    {
        ClearWalls();
        Refresh();
        var go = new GameObject("MapWalls");
        go.transform.SetParent(transform, false);
        wallsRoot = go.transform;
        int layer = LayerMask.NameToLayer(wallLayerName); if (layer < 0) { Debug.LogWarning("[MapBounds] Fant ikke laget " + wallLayerName + "; veggene havner på Default.", this); layer = 0; }
        var mat = new PhysicsMaterial2D("MapWall") { friction = 0f, bounciness = 0f };
        float h = Area.height + 2f * wallExtraHeight, cy = Area.center.y;
        Wall("Wall_Left", new Vector2(Area.xMin - wallThickness / 2f, cy), new Vector2(wallThickness, h), layer, mat);
        Wall("Wall_Right", new Vector2(Area.xMax + wallThickness / 2f, cy), new Vector2(wallThickness, h), layer, mat);
        if (createCeiling) Wall("Ceiling", new Vector2(Area.center.x, Area.yMax + ceilingHeight + wallThickness / 2f), new Vector2(Area.width + 2f * wallThickness, wallThickness), layer, mat);
    }

    void Wall(string name, Vector2 worldCentre, Vector2 size, int layer, PhysicsMaterial2D mat)
    {
        var w = new GameObject(name);
        w.transform.SetParent(wallsRoot, false);
        w.transform.position = new Vector3(worldCentre.x, worldCentre.y, 0f);
        w.layer = layer;
        var bc = w.AddComponent<BoxCollider2D>(); bc.size = size; bc.sharedMaterial = mat;
    }

    [ContextMenu("Clear walls")]
    public void ClearWalls()
    {
        var old = transform.Find("MapWalls");
        if (old == null) return;
        if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
        wallsRoot = null;
    }

    void OnDrawGizmosSelected()
    {
        Rect r;
        if (manualArea) r = Rect.MinMaxRect(min.x, min.y, max.x, max.y); else if (!FitRect(out r)) r = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawLine(new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin)); Gizmos.DrawLine(new Vector3(r.xMax, r.yMin), new Vector3(r.xMax, r.yMax));
        Gizmos.DrawLine(new Vector3(r.xMax, r.yMax), new Vector3(r.xMin, r.yMax)); Gizmos.DrawLine(new Vector3(r.xMin, r.yMax), new Vector3(r.xMin, r.yMin));
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);                                                       // the walls
        Gizmos.DrawLine(new Vector3(r.xMin, r.yMin - 4f), new Vector3(r.xMin, r.yMax + 12f)); Gizmos.DrawLine(new Vector3(r.xMax, r.yMin - 4f), new Vector3(r.xMax, r.yMax + 12f));
    }
}
