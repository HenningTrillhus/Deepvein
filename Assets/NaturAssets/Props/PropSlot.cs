using UnityEngine;

/// <summary>
/// Et sted der en prop (blomst, plante, sopp) KAN vokse. Legges på et tomt objekt i en chunk, på bakken
/// (bruk GameObject > DeepVain > Prop Slot, eller menyen DeepVain > Props > Scatter prop slots on selected).
/// PropSpawner velger en tilfeldig prop til den når verden lages. Det gule merket i Scene-vinduet viser hvor plassen er.
/// </summary>
[DisallowMultipleComponent]
public class PropSlot : MonoBehaviour
{
    [Tooltip("Sjansen for at det faktisk kommer noe her (1 = alltid, 0.5 = annenhver gang).")]
    [Range(0f, 1f)] public float chance = 0.8f;
    [Tooltip("Tomt = bruk listen til PropSpawner. Fyll inn for å bruke en annen liste akkurat her (f.eks. bare sopp i en grotte).")]
    public PropSet propSet;
    [Tooltip("Speil tilfeldig (halve gangene).")]
    public bool randomFlip = true;
    [Tooltip("Flytt tilfeldig til siden, +/- så mange units.")]
    [Min(0f)] public float jitterX = 0.3f;

    void OnDrawGizmos()
    {
        Vector3 p = transform.position;
        Color c = new Color(1f, 0.85f, 0.2f, 0.35f + 0.6f * chance);
        Gizmos.color = c;
        Gizmos.DrawLine(p + Vector3.left * 0.3f, p + Vector3.right * 0.3f);
        Gizmos.DrawWireSphere(p + Vector3.up * 0.2f, 0.15f);
        if (jitterX > 0f) { Gizmos.color = new Color(c.r, c.g, c.b, 0.3f); Gizmos.DrawLine(p + Vector3.left * jitterX, p + Vector3.right * jitterX); }
    }
}

/// <summary>Merker en prop laget av PropSpawner. Den er bare pynt: ingen collider, kan ikke pekes på, slås eller hugges.</summary>
public class SpawnedPropMarker : MonoBehaviour { }
