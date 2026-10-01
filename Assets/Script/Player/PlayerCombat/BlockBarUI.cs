using UnityEngine;
using UnityEngine.UI;

public class BlockBarUI : MonoBehaviour
{
    [SerializeField] private PlayerBlock playerBlock;
    [Tooltip("Fill-objektet. Trenger ingen sprite, bare en farge.")]
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Image fillImage;

    [Header("Farger")]
    [SerializeField] private Color normalColor = new Color(0.3f, 0.6f, 0.95f);
    [SerializeField] private Color exhaustedColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private float exhaustedBlinkSpeed = 6f;

    [SerializeField] private float fillSpeed = 12f;

    private float current = 1f;

    void Awake()
    {
        if (playerBlock == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerBlock = p.GetComponent<PlayerBlock>();
        }

        if (fillImage == null && fillRect != null)
            fillImage = fillRect.GetComponent<Image>();
    }

    void Update()
    {
        if (playerBlock == null || fillRect == null) return;

        current = Mathf.MoveTowards(current, playerBlock.StaminaPercent, fillSpeed * Time.deltaTime);

        // Styrer bredden via anchors i stedet for fillAmount.
        // anchorMin.x = 0 betyr at venstre kant alltid står stille.
        fillRect.anchorMin = new Vector2(0f, fillRect.anchorMin.y);
        fillRect.anchorMax = new Vector2(current, fillRect.anchorMax.y);
        fillRect.offsetMin = new Vector2(0f, fillRect.offsetMin.y);
        fillRect.offsetMax = new Vector2(0f, fillRect.offsetMax.y);

        if (fillImage == null) return;

        if (playerBlock.IsExhausted)
        {
            float t = (Mathf.Sin(Time.time * exhaustedBlinkSpeed) + 1f) * 0.5f;
            fillImage.color = Color.Lerp(exhaustedColor, normalColor, t * 0.4f);
        }
        else
        {
            fillImage.color = normalColor;
        }
    }
}