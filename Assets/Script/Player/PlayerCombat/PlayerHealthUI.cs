using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    public System.Action<int, int> OnHealthChanged;

    [Header("Referanser")]
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Image med Type = Filled, Fill Method = Horizontal, Origin = Left.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Valgfritt: hvit/gul bar bak som henger etter. Gir 'damage flash'.")]
    [SerializeField] private Image delayedFillImage;
    [Tooltip("Valgfritt: tekst som viser 8 / 10.")]
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Animasjon")]
    [SerializeField] private float fillSpeed = 10f;
    [Tooltip("Hvor lenge den etterslepende baren venter før den tar igjen.")]
    [SerializeField] private float delayBeforeCatchUp = 0.4f;
    [SerializeField] private float delayedFillSpeed = 3f;

    [Header("Farge")]
    [SerializeField] private bool colorByHealth = true;
    [SerializeField] private Color fullColor = new Color(0.3f, 0.85f, 0.3f);
    [SerializeField] private Color lowColor = new Color(0.85f, 0.2f, 0.2f);
    

    private float targetPercent = 1f;
    private float currentPercent = 1f;
    private float delayedPercent = 1f;
    private float catchUpTimer;

    void Awake()
    {
        if (playerHealth == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerHealth = p.GetComponent<PlayerHealth>();
        }
    }

    void Start()
    {
        if (playerHealth != null)
        {
            targetPercent = currentPercent = delayedPercent =
                (float)playerHealth.Health / playerHealth.MaxHealth;
            ApplyImmediate();
        }
    }

    void Update()
    {
        if (playerHealth == null) return;

        OnHealthChanged?.Invoke(playerHealth.Health, playerHealth.MaxHealth);

        float newTarget = playerHealth.MaxHealth > 0
            ? (float)playerHealth.Health / playerHealth.MaxHealth
            : 0f;

        // Tok vi skade? Start nedtelling for den etterslepende baren
        if (newTarget < targetPercent)
            catchUpTimer = delayBeforeCatchUp;

        targetPercent = newTarget;

        // Hovedbaren følger raskt
        currentPercent = Mathf.MoveTowards(currentPercent, targetPercent, fillSpeed * Time.deltaTime);

        // Den bak venter litt, så glir ned
        if (catchUpTimer > 0f)
            catchUpTimer -= Time.deltaTime;
        else
            delayedPercent = Mathf.MoveTowards(delayedPercent, currentPercent, delayedFillSpeed * Time.deltaTime);

        // Heling: la den bakre følge med med en gang
        if (delayedPercent < currentPercent)
            delayedPercent = currentPercent;

        ApplyImmediate();
    }

    private void ApplyImmediate()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = currentPercent;

            if (colorByHealth)
                fillImage.color = Color.Lerp(lowColor, fullColor, currentPercent);
        }

        if (delayedFillImage != null)
            delayedFillImage.fillAmount = delayedPercent;

        if (healthText != null && playerHealth != null)
            healthText.text = $"{playerHealth.Health}";
    }
}