using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBlock : MonoBehaviour
{
    [Header("Referanser")]
    [Tooltip("Tomt barn-objekt midt på spilleren. Roteres mot musa.")]
    [SerializeField] private Transform blockPivot;
    [Tooltip("Skjold-sprite som vises mens du blokkerer.")]
    [SerializeField] private GameObject shieldVisual;

    [Header("Blokk-sone")]
    [Tooltip("Hvor langt fra spilleren skjoldet står.")]
    [SerializeField] private float blockRange = 0.7f;
    [Tooltip("Hvor bred blokk-vinkelen er. 180 = alt foran deg.")]
    [Range(30f, 360f)]
    [SerializeField] private float blockAngle = 120f;

    [Header("Utholdenhet")]
    [SerializeField] private float maxStamina = 100f;
    [Tooltip("Hvor fort den tappes mens skjoldet holdes oppe.")]
    [SerializeField] private float drainPerSecond = 15f;
    [Tooltip("Ekstra kostnad når et slag faktisk blokkeres.")]
    [SerializeField] private float costPerBlockedHit = 20f;
    [SerializeField] private float regenPerSecond = 25f;
    [Tooltip("Pause før den begynner å fylles igjen.")]
    [SerializeField] private float regenDelay = 0.8f;
    [Tooltip("Hvor mye som må fylles opp igjen etter at du gikk helt tom.")]
    [Range(0f, 1f)]
    [SerializeField] private float exhaustedRecovery = 0.3f;

    [Header("Kostnad ved treff")]
    [Tooltip("Hvor lenge du ikke kan blokkere etter at et slag ble blokkert.")]
    [SerializeField] private float blockCooldown = 0.3f;
    [Tooltip("Skade som slipper gjennom. 0 = full blokk.")]
    [Range(0f, 1f)]
    [SerializeField] private float damageThrough = 0f;
    [Tooltip("Hvor mye knockback du tar når du blokkerer.")]
    [Range(0f, 1f)]
    [SerializeField] private float knockbackThrough = 0.3f;

    [Tooltip("Hvor hardt angriperen dyttes tilbake ved blokk.")]
    [SerializeField] private float parryKnockback = 8f;

    private PlayerControls controls;
    private Camera cam;
    private bool holdingBlock;
    private float cooldownUntil;

    private float stamina;
    private float regenTimer;
    private bool exhausted;

    // Lesbart utenfra
    public bool IsBlocking => holdingBlock && !exhausted && stamina > 0f && Time.time >= cooldownUntil;
    public float Stamina => stamina;
    public float MaxStamina => maxStamina;
    public float StaminaPercent => maxStamina > 0f ? stamina / maxStamina : 0f;
    public bool IsExhausted => exhausted;
    public float DamageMultiplier => damageThrough;
    public float KnockbackMultiplier => knockbackThrough;
    public float ParryKnockback => parryKnockback;

    void Awake()
    {
        cam = Camera.main;
        stamina = maxStamina;

        controls = new PlayerControls();
        controls.Player.Block.performed += ctx => holdingBlock = true;
        controls.Player.Block.canceled += ctx => holdingBlock = false;

        if (shieldVisual != null)
            shieldVisual.SetActive(false);
    }

    void OnEnable() => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();

    void Update()
    {
        bool active = IsBlocking;

        if (active)
        {
            AimAtMouse();
            DrainStamina(drainPerSecond * Time.deltaTime);
        }
        else
        {
            RegenStamina();
        }

        if (shieldVisual != null && shieldVisual.activeSelf != active)
            shieldVisual.SetActive(active);
    }

    private void DrainStamina(float amount)
    {
        stamina -= amount;
        regenTimer = regenDelay;

        if (stamina <= 0f)
        {
            stamina = 0f;
            exhausted = true;   // må slippe og vente før skjoldet virker igjen
        }
    }

    private void RegenStamina()
    {
        if (regenTimer > 0f)
        {
            regenTimer -= Time.deltaTime;
            return;
        }

        stamina = Mathf.Min(maxStamina, stamina + regenPerSecond * Time.deltaTime);

        if (exhausted && stamina >= maxStamina * exhaustedRecovery)
            exhausted = false;
    }

    /// <summary>Kan vi blokkere et slag som kommer fra denne retningen?</summary>
    public bool CanBlockFrom(Vector2 attackOrigin)
    {
        if (!IsBlocking || blockPivot == null) return false;

        Vector2 toAttacker = (attackOrigin - (Vector2)transform.position).normalized;
        float angle = Vector2.Angle(blockPivot.right, toAttacker);

        return angle <= blockAngle * 0.5f;
    }

    /// <summary>Kalles fra PlayerHealth når et slag faktisk ble blokkert.</summary>
    public void RegisterBlockedHit()
    {
        cooldownUntil = Time.time + blockCooldown;
        DrainStamina(costPerBlockedHit);
    }

    private void AimAtMouse()
    {
        if (blockPivot == null || cam == null) return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = cam.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));

        Vector2 dir = (Vector2)mouseWorld - (Vector2)blockPivot.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        blockPivot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDrawGizmosSelected()
    {
        if (blockPivot == null) return;

        Gizmos.color = Color.cyan;
        Vector3 center = blockPivot.position;

        for (float a = -blockAngle * 0.5f; a <= blockAngle * 0.5f; a += 10f)
        {
            Vector3 d = Quaternion.Euler(0f, 0f, a) * blockPivot.right;
            Gizmos.DrawLine(center, center + d * blockRange);
        }
    }
}