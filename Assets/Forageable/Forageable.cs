using System.Collections.Generic;
using UnityEngine;
using DeepVain.Items;

/// <summary>
/// Noe spilleren kan plukke ved å trykke E rett ved (bær, sopp, urter, bomull ...). Legges på prop-prefaben.
/// Mens det ikke er plukket går et hvitt skinn (et skrått bånd) over det nå og da, så man ser at det er spesielt.
/// Skinnet slukker når det er plukket. Skinnet er en shader (DeepVain/Sprite Shine), ikke et eget bilde.
/// Plukket tilstand: planter bytter til animasjonen "Picked" (samme vind, så den ikke hopper). Sopp har ingen plukket tilstand
/// og forsvinner (Hide When Picked). Det gror ikke tilbake.
/// ForagePicker (på spilleren) finner disse, og bestemmer når noe plukkes.
/// </summary>
[DisallowMultipleComponent]
public class Forageable : MonoBehaviour
{
    /// <summary>Alle plukkbare som er i scenen akkurat nå (ingen fysikk nødvendig).</summary>
    public static readonly List<Forageable> All = new List<Forageable>();

    [Header("Hva man får")]
    public ItemData item;
    [Min(1)] public int minAmount = 2;
    [Min(1)] public int maxAmount = 4;

    [Header("Plukket")]
    [Tooltip("På: forsvinner helt når den er plukket (sopp). Av: bytter til animasjonen Picked Animation State.")]
    public bool hideWhenPicked = false;
    [Tooltip("Navnet på tilstanden i Animator som vises når den er plukket.")]
    public string pickedState = "Picked";
    [Tooltip("Hvor høyt over foten [E]-merket vises (units).")]
    public float promptHeight = 1.4f;

    [Header("Skinn (hvit sweep)")]
    [Tooltip("Et hvitt bånd går over det nå og da, helt til det er plukket.")]
    public bool shine = true;
    [Tooltip("Materialet med shaderen DeepVain/Sprite Shine. Tomt = finner shaderen selv.")]
    public Material shineMaterial;
    [Tooltip("Sekunder fra en sweep til neste.")]
    public float shinePeriod = 2.9f;
    [Range(0.1f, 1f), Tooltip("Hvor stor del av tiden båndet er på vei over (resten hviler det).")]
    public float shineRun = 0.4f;

    static readonly int pPhase = Shader.PropertyToID("_ShinePhase"), pPeriod = Shader.PropertyToID("_ShinePeriod"),
        pRun = Shader.PropertyToID("_ShineRun"), pStrength = Shader.PropertyToID("_ShineStrength");
    static Material sharedShine;

    bool picked;
    Animator animator;
    SpriteRenderer plantRenderer;
    MaterialPropertyBlock mpb;

    public bool IsPicked { get { return picked; } }
    public int RollAmount() { return Random.Range(Mathf.Min(minAmount, maxAmount), Mathf.Max(minAmount, maxAmount) + 1); }

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        plantRenderer = GetComponentInChildren<SpriteRenderer>();
        if (shine) SetupShine();
    }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void SetupShine()
    {
        if (plantRenderer == null) return;
        var mat = shineMaterial;
        if (mat == null)
        {
            if (sharedShine == null)
            {
                var shader = Shader.Find("DeepVain/Sprite Shine");
                if (shader != null) sharedShine = new Material(shader) { name = "Sprite Shine (auto)" };
            }
            mat = sharedShine;
        }
        if (mat == null) { Debug.LogWarning("[Forageable] Fant ikke shaderen \"DeepVain/Sprite Shine\", så det blir ikke noe skinn på " + name + ". Kopier SpriteShine.shader inn i Assets.", this); return; }
        plantRenderer.sharedMaterial = mat;

        mpb = new MaterialPropertyBlock();
        plantRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(pPhase, Random.value * shinePeriod);                         // each prop shines at its own time
        mpb.SetFloat(pPeriod, shinePeriod);
        mpb.SetFloat(pRun, shineRun);
        mpb.SetFloat(pStrength, 1f);
        plantRenderer.SetPropertyBlock(mpb);
    }

    void SetShine(float strength)
    {
        if (plantRenderer == null || mpb == null) return;
        plantRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(pStrength, strength);
        plantRenderer.SetPropertyBlock(mpb);
    }

    /// <summary>Gjør den plukket: skinnet slukker, og den bytter animasjon (samme tidspunkt i vinden), eller skjules.</summary>
    public void Pick()
    {
        if (picked) return;
        picked = true;
        SetShine(0f);
        if (hideWhenPicked || animator == null || string.IsNullOrEmpty(pickedState))
        {
            gameObject.SetActive(false);
            return;
        }
        float t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
        animator.Play(pickedState, 0, t);
    }
}
