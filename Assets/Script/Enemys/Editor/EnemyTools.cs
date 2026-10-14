#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepVain.EnemyTools
{
    /// <summary>
    /// DeepVain > Enemies > 1 - Make enemy prefab from selected
    ///   Velg goblin-objektet du har i scenen (det med EnemyAI) i Hierarchy. Lager en prefab av det i Assets/Prefabs/Enemies
    ///   (objektet i scenen beholdes og kobles til prefaben).
    /// DeepVain > Enemies > 2 - Add enemy spawner
    ///   Legger EnemySpawner på WorldBuilder-objektet og gir den enemy-prefabene i Assets/Prefabs/Enemies.
    /// </summary>
    public static class EnemyMenu
    {
        const string Dir = "Assets/Prefabs/Enemies";

        [MenuItem("DeepVain/Enemies/1 - Make enemy prefab from selected")]
        public static void MakePrefab()
        {
            var go = Selection.activeGameObject;
            if (go == null) { EditorUtility.DisplayDialog("Enemy", "Velg goblin-objektet i Hierarchy først.", "OK"); return; }
            if (go.GetComponent<EnemyAI>() == null) { EditorUtility.DisplayDialog("Enemy", "Objektet \"" + go.name + "\" har ingen EnemyAI. Velg selve goblin-objektet (rot-objektet).", "OK"); return; }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Enemies");
            string path = Dir + "/" + go.name + ".prefab";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction);
            Debug.Log("[Enemy] Prefab " + path + " laget fra \"" + go.name + "\". Kjør neste: DeepVain > Enemies > 2 - Add enemy spawner.", prefab);
        }

        [MenuItem("DeepVain/Enemies/2 - Add enemy spawner")]
        public static void AddSpawner()
        {
            var wb = Object.FindFirstObjectByType<WorldBuilder>();
            if (wb == null) { EditorUtility.DisplayDialog("Enemy", "Fant ingen WorldBuilder i scenen.", "OK"); return; }
            var sp = wb.GetComponent<EnemySpawner>();
            if (sp == null) sp = Undo.AddComponent<EnemySpawner>(wb.gameObject);
            Undo.RecordObject(sp, "Enemy spawner");
            var list = new System.Collections.Generic.List<EnemySpawner.Entry>();
            if (AssetDatabase.IsValidFolder(Dir))
                foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { Dir }))
                {
                    var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
                    if (p != null && p.GetComponent<EnemyAI>() != null) list.Add(new EnemySpawner.Entry { label = p.name, prefab = p, weight = 1f });
                }
            if (list.Count > 0) sp.enemies = list.ToArray();
            EditorUtility.SetDirty(sp);
            Debug.Log("[Enemy] EnemySpawner på \"" + wb.name + "\" med " + list.Count + " fiende-prefab(er)."
                      + (list.Count == 0 ? " Ingen funnet: lag først en prefab (meny 1) eller dra prefaben inn i Enemies-listen selv." : " Lagre scenen og spill (eller høyreklikk EnemySpawner > Populate now)."), sp);
        }
    }
}
#endif
