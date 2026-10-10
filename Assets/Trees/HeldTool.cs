using UnityEngine;

/// <summary>
/// Viser øksa i hånda mens spilleren hugger, på samme måte som sverdet vises mens han slår.
/// Øksa er et eget SpriteRenderer-objekt som ligger som barn av samme HandSocket (Front) som sverdet,
/// så den følger hånda og underarmen i alle animasjonsbilder.
/// Legges på spilleren (samme objekt som TreeChopper). Lages automatisk av menyen DeepVain > Player > Add held axe.
/// </summary>
[DisallowMultipleComponent]
public class HeldTool : MonoBehaviour
{
    [Header("Øks")]
    [Tooltip("SpriteRenderer-objektet for øksa (barn av HandSocket Front, ved siden av sverdet).")]
    public SpriteRenderer axeRenderer;
    [Tooltip("Bildet av øksa i hånda (Axe_Held.png: 30x30, grepet i midten, peker mot høyre).")]
    public Sprite axeSprite;

    [Header("Hakke (senere)")]
    public SpriteRenderer pickaxeRenderer;
    public Sprite pickaxeSprite;

    [Header("Visning")]
    [Tooltip("Hvor lenge øksa blir synlig etter at hoggingen stopper, så animasjonen får gjøre seg ferdig.")]
    public float linger = 0.12f;

    TreeChopper chopper;
    float axeHideAt;

    void Awake()
    {
        chopper = GetComponent<TreeChopper>();
        if (axeRenderer != null)
        {
            if (axeSprite != null) axeRenderer.sprite = axeSprite;
            axeRenderer.enabled = false;
        }
        if (pickaxeRenderer != null)
        {
            if (pickaxeSprite != null) pickaxeRenderer.sprite = pickaxeSprite;
            pickaxeRenderer.enabled = false;
        }
    }

    void Update()
    {
        if (axeRenderer == null) return;
        bool chopping = chopper != null && chopper.IsChopping;
        if (chopping) axeHideAt = Time.time + linger;
        axeRenderer.enabled = chopping || Time.time < axeHideAt;
    }

    /// <summary>Er øksa tegnet i hånda akkurat nå?</summary>
    public bool AxeVisible { get { return axeRenderer != null && axeRenderer.enabled; } }
}
