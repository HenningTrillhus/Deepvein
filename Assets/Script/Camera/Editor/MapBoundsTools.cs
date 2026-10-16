#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// DeepVain > World > Add map bounds (camera + walls) to selected
///   Select the root object of the map (for instance the inside goblin chunk) and run it: a MapBounds is added, fitted to the tilemaps below, and the walls are built
///   so you can see them in the Hierarchy and the Scene view (cyan = the map, red = the walls). The camera (CameraFollow) uses it by itself.
/// </summary>
public static class MapBoundsMenu
{
    [MenuItem("DeepVain/World/Add map bounds (camera + walls) to selected")]
    static void Add()
    {
        var go = Selection.activeGameObject;
        if (go == null) { EditorUtility.DisplayDialog("Map bounds", "Marker først objektet som er rota til kartet (for eksempel inside-goblin-chunken) i Hierarchy.", "OK"); return; }
        var mb = go.GetComponent<MapBounds>();
        if (mb == null) mb = Undo.AddComponent<MapBounds>(go);
        mb.Refresh();
        var so = new SerializedObject(mb);
        so.FindProperty("min").vector2Value = mb.Area.min; so.FindProperty("max").vector2Value = mb.Area.max;
        so.ApplyModifiedPropertiesWithoutUndo();
        mb.BuildWalls();
        EditorUtility.SetDirty(mb);
        Debug.Log("[MapBounds] " + go.name + ": området er x " + mb.Area.xMin.ToString("0.0") + " til " + mb.Area.xMax.ToString("0.0") + " (" + mb.Area.width.ToString("0.0") + " units), y " +
                  mb.Area.yMin.ToString("0.0") + " til " + mb.Area.yMax.ToString("0.0") + ". Vegger bygget (MapWalls). Er tallene feil: huk av Manual Area og skriv dem selv.", mb);
    }
}
#endif
