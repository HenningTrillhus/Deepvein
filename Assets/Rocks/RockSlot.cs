using UnityEngine;

/// <summary>
/// Et sted der en stein KAN stå, på samme måte som en TreeSlot for trær. Legges på et tomt objekt i en chunk, på bakken
/// (GameObject > DeepVain > Rock Slot, eller DeepVain > Rocks > Scatter rock slots on selected).
/// RockSpawner bestemmer om det faktisk kommer en stein (Chance), og om den har kull i seg (Coal Chance).
/// </summary>
[DisallowMultipleComponent]
public class RockSlot : MonoBehaviour
{
    [Tooltip("Sjansen for at det kommer en stein her i det hele tatt (1 = alltid, 0.3 = hver tredje gang).")]
    [Range(0f, 1f)] public float chance = 0.35f;
    [Tooltip("Hvis det kommer en stein: sjansen for at den har kull i seg.")]
    [Range(0f, 1f)] public float coalChance = 0.25f;
    [Tooltip("Tomt = bruk listen til RockSpawner. Fyll inn for å bruke en annen liste akkurat her.")]
    public RockSet rockSet;
    [Tooltip("Speil tilfeldig (halve gangene).")]
    public bool randomFlip = true;
    [Tooltip("Flytt tilfeldig til siden, +/- så mange units.")]
    [Min(0f)] public float jitterX = 0.4f;

    void OnDrawGizmos()
    {
        Vector3 p = transform.position;
        Gizmos.color = new Color(0.55f, 0.7f, 1f, 0.35f + 0.6f * chance);
        Gizmos.DrawLine(p + Vector3.left * 0.4f, p + Vector3.right * 0.4f);
        Gizmos.DrawWireCube(p + Vector3.up * 0.3f, new Vector3(0.5f, 0.35f, 0f));
        if (jitterX > 0f) { Gizmos.color = new Color(0.55f, 0.7f, 1f, 0.3f); Gizmos.DrawLine(p + Vector3.left * jitterX, p + Vector3.right * jitterX); }
    }
}

/// <summary>Merker en stein laget av RockSpawner (så den kan fjernes og lages på nytt).</summary>
public class SpawnedRockMarker : MonoBehaviour { }
