using UnityEngine;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("Referanser")]
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Selve livbaren.")]
    [SerializeField] private RectTransform fillRect;
    [Tooltip("Stripen som viser tapt liv. Legg den OVER Fill i Hierarchy.")]
    [SerializeField] private RectTransform lostRect;

    [Header("Animasjon")]
    [Tooltip("Pause før den tapte stripen begynner å krympe.")]
    [SerializeField] private float lostDelay = 0.4f;
    [Tooltip("Hvor fort den tapte stripen forsvinner. 0.5 = hele baren på 2 sek.")]
    [SerializeField] private float lostSpeed = 0.5f;

    private float currentPercent = 1f;   // følger livet direkte
    private float lostPercent = 1f;      // høyre kant av den tapte stripen
    private float delayTimer;

    void Awake()
    {
        if (playerHealth == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerHealth = p.GetComponent<PlayerHealth>();
        }
    }

    void Update()
    {
        if (playerHealth == null) return;

        float target = playerHealth.MaxHealth > 0
            ? (float)playerHealth.Health / playerHealth.MaxHealth
            : 0f;

        // Tok vi skade? Frys stripen der den er og start nedtellingen på nytt.
        // Blir vi truffet igjen mens den krymper, blir den stående og dekker
        // begge tapene samlet.
        if (target < currentPercent - 0.0001f)
            delayTimer = lostDelay;

        // Baren hopper rett dit livet er - ingen smoothing
        currentPercent = target;

        if (delayTimer > 0f)
            delayTimer -= Time.deltaTime;
        else
            lostPercent = Mathf.MoveTowards(lostPercent, currentPercent, lostSpeed * Time.deltaTime);

        // Heling: stripen skal aldri ligge bak baren
        if (lostPercent < currentPercent)
            lostPercent = currentPercent;

        SetAnchors(fillRect, 0f, currentPercent);
        SetAnchors(lostRect, currentPercent, lostPercent);

        if (lostRect != null)
            lostRect.gameObject.SetActive(lostPercent > currentPercent + 0.001f);
    }

    private void SetAnchors(RectTransform rect, float left, float right)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(left, rect.anchorMin.y);
        rect.anchorMax = new Vector2(right, rect.anchorMax.y);
        rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
        rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
    }
}