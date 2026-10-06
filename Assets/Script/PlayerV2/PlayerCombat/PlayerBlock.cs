using UnityEngine;
using UnityEngine.InputSystem;
using DeepVain.Player;

public class PlayerBlock : MonoBehaviour
{
    [Header("Referanser")]
    [Tooltip("Tomt barn-objekt midt på spilleren. Roteres mot musa.")]
    [SerializeField] private Transform blockPivot;
    [Tooltip("Skjold-sprite som vises mens du blokkerer.")]
    [SerializeField] private GameObject shieldVisual;
    [Tooltip("Skjoldet sitter i spillerens hånd (HandSocket) og vises mens skjold-animasjonene spilles.")]
    [SerializeField] private bool shieldFollowsHand = true;

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

    [Header("Skjold-timer")]
    [Tooltip("Hvor lenge du må vente etter at du senket skjoldet før du kan løfte det igjen.")]
    [SerializeField] private float raiseCooldown = 1f;
    private float raiseReadyTime;

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

    [Header("Parry")]
    [Tooltip("Hvor lenge etter knappetrykket et treff teller som parry.")]
    [SerializeField] private float parryWindow = 0.5f;
    private float blockPressedTime = -999f;

    private PlayerControls controls;
    private Camera cam;
    private PlayerAnimator anim;
    private PlayerAttack attack;
    private PlayerLayerSync layerSync;
    private SpriteRenderer[] shieldRenderers;
    private bool holdingBlock;
    private float cooldownUntil;

    private float stamina;
    private float regenTimer;
    private bool exhausted;

    // Lesbart utenfra
    public bool IsBlocking => holdingBlock && !exhausted && stamina > 0f && Time.time >= cooldownUntil
                              && !(attack != null && attack.IsAttacking);   // skjoldet er nede mens du slår
    public float Stamina => stamina;
    public float MaxStamina => maxStamina;
    public float StaminaPercent => maxStamina > 0f ? stamina / maxStamina : 0f;
    public bool IsExhausted => exhausted;
    public float DamageMultiplier => damageThrough;
    public float KnockbackMultiplier => knockbackThrough;
    public float ParryKnockback => parryKnockback;
    public bool IsInParryWindow => Time.time <= blockPressedTime + parryWindow;

    void Awake()
    {
        cam = Camera.main;
        stamina = maxStamina;
        anim = GetComponent<PlayerAnimator>();
        attack = GetComponent<PlayerAttack>();
        layerSync = GetComponentInChildren<PlayerLayerSync>();
        if (shieldVisual != null) shieldRenderers = shieldVisual.GetComponentsInChildren<SpriteRenderer>(true);

        controls = new PlayerControls();
        controls.Player.Block.performed += ctx =>
        {
            if (Time.time < raiseReadyTime) return;    // skjoldet er ikke klart ennå
            holdingBlock = true;
            blockPressedTime = Time.time;              // starter parry-vinduet
        };
        controls.Player.Block.canceled += ctx =>
        {
            if (holdingBlock) raiseReadyTime = Time.time + raiseCooldown;   // timeren starter når du slipper
            holdingBlock = false;
        };

        SetShield(false);
    }

    void OnEnable() => controls.Player.Enable();

    void OnDisable()
    {
        controls.Player.Disable();
        holdingBlock = false;
        if (anim != null) anim.SetBlocking(false);
        SetShield(false);
    }

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

        // spilleren løfter skjoldet (ShieldRaise -> ShieldHold) så lenge du blokkerer
        if (anim != null) anim.SetBlocking(active);

        bool show = active;
        if (shieldFollowsHand && layerSync != null)
        {
            // synlig akkurat mens spillerens skjold-animasjon går
            string a = layerSync.CurrentAnimation;
            show = a == "ShieldRaise" || a == "ShieldHold" || a == "ShieldHit";
        }
        SetShield(show);
    }

    private void SetShield(bool on)
    {
        if (shieldVisual == null) return;
        if (shieldFollowsHand && shieldRenderers != null)
        {
            // skjoldet er barn av HandSocket: skru bare tegningen av og på
            foreach (var r in shieldRenderers) if (r != null) r.enabled = on;
            return;
        }
        if (shieldVisual.activeSelf != on) shieldVisual.SetActive(on);
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
        if (anim != null) anim.ShieldHit();      // støt i skjoldet
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