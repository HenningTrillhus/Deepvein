#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DeepVain.TeleportTools
{
    /// <summary>
    /// Scene-vinduet: marker et objekt med Teleporter og dra i den blå ringen, så fylles Target Position ut.
    /// Inspector: knapper for å hente posisjonen fra et annet objekt, og for å hoppe til målet.
    /// Meny: DeepVain > Gate > Legg Teleporter på goblin-port-prefabene.
    /// </summary>
    [CustomEditor(typeof(Teleporter))]
    public class TeleporterEditor : Editor
    {
        void OnSceneGUI()
        {
            var t = (Teleporter)target;
            if (!t.usePosition || t.destination != null || t.linkedDoor != null) return;
            EditorGUI.BeginChangeCheck();
            Vector3 p = Handles.PositionHandle(new Vector3(t.targetPosition.x, t.targetPosition.y, 0f), Quaternion.identity);
            Handles.Label(p + Vector3.up * 0.6f, "Teleport-mål");
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Flytt teleport-mål");
                t.targetPosition = new Vector2(Mathf.Round(p.x * 100f) / 100f, Mathf.Round(p.y * 100f) / 100f);
                EditorUtility.SetDirty(t);
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var t = (Teleporter)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mål = valgt annet objekt"))
                {
                    var other = Selection.activeGameObject;
                    if (other != null && other != t.gameObject)
                    {
                        Undo.RecordObject(t, "Sett teleport-mål");
                        t.targetPosition = other.transform.position; EditorUtility.SetDirty(t);
                    }
                    else EditorUtility.DisplayDialog("Teleporter", "Marker et annet objekt i Hierarchy først (hold Ctrl mens du markerer for å beholde denne), eller dra i ringen i Scene-vinduet.", "OK");
                }
                if (GUILayout.Button("Vis målet i Scene"))
                {
                    SceneView.lastActiveSceneView?.LookAt(t.Target);
                }
            }
            if (t.GetComponent<GoblinGate>() != null)
                EditorGUILayout.HelpBox(t.usePosition
                    ? "GoblinGate på samme objekt: porten går til posisjonen over (Pair Id og Linked Gate ignoreres)."
                    : "Use Position er av: porten bruker Pair Id / Linked Gate som før.", MessageType.Info);
        }
    }

    public static class TeleporterMenu
    {
        [MenuItem("DeepVain/Gate/Legg Teleporter på goblin-port-prefabene")]
        static void AddToGatePrefabs()
        {
            int n = 0;
            foreach (var path in new[] { "Assets/Gate/Gate_Goblin.prefab", "Assets/Gate/Gate_Goblin_Small.prefab" })
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root.GetComponent<GoblinGate>() != null && root.GetComponent<Teleporter>() == null)
                {
                    var tp = root.AddComponent<Teleporter>();
                    tp.usePosition = false;          // the existing gates keep using their Pair Id until you tick this on one of them
                    n++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[Teleporter] Lagt på " + n + " port-prefab(er). Use Position er AV som standard: marker en port i scenen, huk av Use Position og skriv inn Target Position (eller dra ringen i Scene).");
        }
    }
}
#endif
