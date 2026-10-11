using UnityEngine;

/// <summary>
/// A place where a tree MAY grow. Put it on an empty object inside a chunk, on the ground where a tree can stand
/// (use GameObject > DeepVain > Tree Slot to make one, or copy/paste). The TreeSpawner picks a random tree for it when the world is made.
/// The green mark in the Scene view shows where the slot is; the stronger the green, the surer the tree.
/// </summary>
[DisallowMultipleComponent]
public class TreeSlot : MonoBehaviour
{
    [Tooltip("Sjansen for at det faktisk kommer et tre her (1 = alltid, 0.5 = annenhver gang).")]
    [Range(0f, 1f)] public float chance = 1f;
    [Tooltip("Tomt = bruk treslisten til TreeSpawner. Fyll inn for å bruke en annen liste akkurat her (f.eks. bare busker, bare gran).")]
    public TreeSet treeSet;
    [Tooltip("Speil treet tilfeldig (halve gangene), for mer variasjon.")]
    public bool randomFlip = true;
    [Tooltip("Flytt treet tilfeldig til siden, +/- så mange units.")]
    [Min(0f)] public float jitterX = 0f;

    void OnDrawGizmos()
    {
        Vector3 p = transform.position;
        Color c = new Color(0.25f, 0.9f, 0.3f, 0.35f + 0.6f * chance);
        Gizmos.color = c;
        Gizmos.DrawLine(p + Vector3.left * 0.35f, p + Vector3.right * 0.35f);
        Gizmos.DrawLine(p, p + Vector3.up * 1.2f);
        Gizmos.DrawWireSphere(p + Vector3.up * 1.5f, 0.3f);
        if (jitterX > 0f) { Gizmos.color = new Color(c.r, c.g, c.b, 0.3f); Gizmos.DrawLine(p + Vector3.left * jitterX, p + Vector3.right * jitterX); }
    }
}

/// <summary>Marks a tree made by the TreeSpawner, so it can be found and removed again.</summary>
public class SpawnedTreeMarker : MonoBehaviour { }
