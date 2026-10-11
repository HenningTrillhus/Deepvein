using UnityEngine;

/// <summary>
/// Lar hver kopi av en animert blomst (eller gress, busk ...) starte på en tilfeldig plass i animasjonen og gå litt ulikt fort,
/// så en eng ikke svaier i takt. Legges på objektet med Animator (blomst-prefabene fra menyen har den allerede).
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimatorStartOffset : MonoBehaviour
{
    [Tooltip("Hvor mye farten kan avvike, f.eks. 0.15 = +/- 15 %.")]
    [Range(0f, 0.5f)] public float speedVariation = 0.15f;

    void Start()
    {
        var a = GetComponent<Animator>();
        a.Play(0, 0, Random.value);
        a.speed = 1f + Random.Range(-speedVariation, speedVariation);
    }
}
