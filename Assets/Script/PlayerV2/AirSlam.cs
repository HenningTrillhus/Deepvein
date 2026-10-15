using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AIR SLAM: attack while you are high in the air and the sword goes up over the head, you drop fast and bring it down on the ground
/// with weight: a critical hit on everything close by.
///
///   Windup  (sword up)  ->  Fall (loops, fast)  ->  Strike (the big arc, a little above the ground)  ->  Land (impact, then recover)
///
/// The air time varies with the jump height, so Fall just loops until the ground is reached. The animations are only the picture; this
/// script is the logic and drives the Animator through PlayerAnimator.SetAirSlamPhase (0 none, 1 windup, 2 fall, 3 strike, 4 land).
/// PlayerAttack starts it (a click in the air), PlayerMovement stands still while it is active, PlayerHealth cancels it when you are hit.
/// Put it on the player (next to PlayerMovement).
/// </summary>
[DisallowMultipleComponent]
public class AirSlam : MonoBehaviour
{
    [Header("Start")]
    [Tooltip("Hvor høyt over bakken (units) du må være for at et slag i lufta skal bli et slam. Lavere enn dette = vanlig slag. Et vanlig hopp er ca. 0.8 høyt, så 0.3 gir slam i det meste av hoppet.")]
    public float startHeight = 0.3f;
    [Tooltip("Sekunder før du kan gjøre det igjen.")]
    public float cooldown = 0.6f;
    [Tooltip("Stamina det koster (0 = gratis). Har du ikke nok, blir det et vanlig slag.")]
    public float staminaCost = 20f;

    [Header("Fall")]
    [Tooltip("Hvor fort du faller (units per sekund). Vanlig fall er ca. 8-12.")]
    public float fallSpeed = 20f;
    [Tooltip("Hvor mye du kan styre til siden mens du faller (units per sekund).")]
    public float airControl = 2.5f;
    [Tooltip("Buen starter når bakken er så nær (units). Landingen kommer uansett når du treffer bakken.")]
    public float strikeHeight = 0.7f;

    [Header("Pansring")]
    [Tooltip("På: et treff mens du slammer gjør fortsatt skade, men kaster deg ikke ut av slammet (ingen stun, ingen knockback). Av: et treff avbryter det.")]
    public bool hyperArmor = true;

    [Header("Landing")]
    [Tooltip("Skaden = vanlig slag-skade x dette (et kritisk treff).")]
    public float damageMultiplier = 3f;
    [Tooltip("Hvor langt ut fiender tas (units).")]
    public float radius = 2.4f;
    [Tooltip("Hvor lenge du står i landingen før du får kontroll igjen (Land-animasjonen er 0.45 s).")]
    public float landTime = 0.45f;
    [Tooltip("Skjermristing og et lite stopp i bildet ved landing (0 = av).")]
    public float shake = 0.12f;
    public float hitStop = 0.09f;

    [Header("Feilsøking")]
    [Tooltip("Skriver i Console hvorfor et slag i lufta ikke ble et slam (for lavt, stamina osv.).")]
    public bool debugLog = true;

    public enum Phase { None, Windup, Fall, Strike, Land }
    public Phase Current { get; private set; } = Phase.None;
    public bool IsActive { get { return Current != Phase.None; } }

    /// <summary>Kommer ved landing, med antall fiender som ble truffet.</summary>
    public event System.Action<int> Landed;

    PlayerMovement movement;
    PlayerAttack attack;
    PlayerAnimator anim;
    PlayerHealth health;
    PlayerStamina stamina;
    PlayerBlock block;
    Rigidbody2D rb;
    float phaseTimer, nextAllowed;
    CollisionDetectionMode2D savedDetection;
    bool detectionSaved;
    GameObject speedLines;
    int facing = 1;
    const float WindupTime = 0.175f;      // = AirSlamStart (55 + 55 + 65 ms)
    const float StrikeMax = 0.12f;        // = AirSlamStrike (40 + 40 ms) plus a little

    void Awake()
    {
        if (debugLog) Debug.Log("[AirSlam] klar (v3): Start Height " + startHeight.ToString("0.0") + ", Fall Speed " + fallSpeed.ToString("0") + ", Damage x" + damageMultiplier.ToString("0.0"), this);
        movement = GetComponent<PlayerMovement>();
        attack = GetComponent<PlayerAttack>();
        anim = GetComponent<PlayerAnimator>();
        health = GetComponent<PlayerHealth>();
        stamina = GetComponent<PlayerStamina>();
        block = GetComponent<PlayerBlock>();
        rb = GetComponent<Rigidbody2D>();
    }

    void OnDisable() { Cancel(); }

    // ------------------------------------------------------------------------------------------ start
    /// <summary>Distance straight down to the ground (units), 999 if there is none within 30.</summary>
    public float HeightAboveGround()
    {
        if (movement == null || movement.groundCheck == null) return 0f;
        Vector2 o = movement.groundCheck.position;
        var hits = Physics2D.RaycastAll(o, Vector2.down, 30f, movement.groundLayer);
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            return h.distance;
        }
        return 999f;
    }

    /// <summary>Called by PlayerAttack when the attack button is pressed. Returns true if the slam started (then no normal swing).</summary>
    public bool TryStart()
    {
        if (IsActive || movement == null || rb == null) return false;
        if (movement.IsGrounded) return false;                             // on the ground: a normal swing
        if (Time.time < nextAllowed) return Refuse("venter (cooldown " + (nextAllowed - Time.time).ToString("0.0") + " s)");
        if (movement.IsClimbing || movement.IsLedgeClimbing || movement.IsRolling) return Refuse("klatrer / ruller");
        if (health != null && (health.IsStunned || health.IsDead)) return Refuse("stunnet");
        if (block != null && block.IsBlocking) return Refuse("skjoldet er oppe");
        float height = HeightAboveGround();
        if (height < startHeight) return Refuse("for lavt over bakken (" + height.ToString("0.0") + " units, trenger " + startHeight.ToString("0.0") + ")");
        if (staminaCost > 0f && stamina != null)
        {
            if (stamina.IsExhausted || stamina.Current < staminaCost) return Refuse("ikke nok stamina (" + stamina.Current.ToString("0") + ", trenger " + staminaCost.ToString("0") + ")");
            stamina.Spend(staminaCost);
        }

        facing = movement.Facing != 0 ? movement.Facing : 1;
        Current = Phase.Windup; phaseTimer = 0f;
        savedDetection = rb.collisionDetectionMode; detectionSaved = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;      // a fast fall must not go through the ground
        if (attack != null) attack.ForceSword(true);
        if (anim != null) anim.SetAirSlamPhase(1);
        AirSlamFx.Windup(transform, facing);
        return true;
    }

    bool Refuse(string why)
    {
        if (debugLog) Debug.Log("[AirSlam] ble ikke til et slam: " + why + ". (Vanlig slag i stedet.)", this);
        return false;
    }

    // ------------------------------------------------------------------------------------------ the phases (physics)
    void FixedUpdate()
    {
        if (!IsActive) return;
        phaseTimer += Time.fixedDeltaTime;
        float vx = movement != null ? movement.HorizontalInput * airControl : 0f;

        switch (Current)
        {
            case Phase.Windup:
                // almost hovers while the sword goes up (a rising jump is stopped)
                rb.linearVelocity = new Vector2(vx, Mathf.Min(rb.linearVelocity.y, 0f) * 0.3f);
                if (movement != null && movement.IsGrounded) { Land(); break; }
                if (phaseTimer >= WindupTime)
                {
                    Current = Phase.Fall; phaseTimer = 0f;
                    if (anim != null) anim.SetAirSlamPhase(2);
                    speedLines = AirSlamFx.SpeedLines(transform);
                }
                break;

            case Phase.Fall:
                rb.linearVelocity = new Vector2(vx, -fallSpeed);
                if (movement != null && movement.IsGrounded) { Land(); break; }
                if (HeightAboveGround() <= strikeHeight)
                {
                    Current = Phase.Strike; phaseTimer = 0f;
                    if (anim != null) anim.SetAirSlamPhase(3);
                    AirSlamFx.Strike(transform, facing);
                    if (speedLines != null) { Destroy(speedLines); speedLines = null; }
                }
                break;

            case Phase.Strike:
                rb.linearVelocity = new Vector2(vx * 0.5f, -fallSpeed);
                if ((movement != null && movement.IsGrounded) || HeightAboveGround() < 0.08f) Land();
                else if (phaseTimer > 0.6f) Land();           // never stay in the air
                break;

            case Phase.Land:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (phaseTimer >= landTime) End();
                break;
        }
    }

    void Land()
    {
        Current = Phase.Land; phaseTimer = 0f;
        rb.linearVelocity = Vector2.zero;
        if (anim != null) anim.SetAirSlamPhase(4);
        if (speedLines != null) { Destroy(speedLines); speedLines = null; }

        Vector3 feet = movement != null && movement.groundCheck != null ? movement.groundCheck.position : transform.position;
        int hitCount = DealDamage(feet);
        AirSlamFx.Land(feet, facing);
        CombatFeel.Shake(shake, 0.32f);
        CombatFeel.HitStop(hitStop);
        if (Landed != null) Landed(hitCount);
    }

    int DealDamage(Vector3 feet)
    {
        if (attack == null) return 0;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(attack.Damage * damageMultiplier));
        var done = new HashSet<GameObject>();
        int n = 0;
        Vector2 centre = new Vector2(feet.x + facing * 0.5f, feet.y + 0.5f);
        foreach (var c in Physics2D.OverlapCircleAll(centre, radius, attack.EnemyLayer))
        {
            if (c == null) continue;
            var target = c.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            var comp = target as Component;
            if (comp != null && !done.Add(comp.gameObject)) continue;
            var enemy = c.GetComponentInParent<Enemy>();
            if (enemy != null && enemy.IsDead) continue;
            float dx = c.bounds.center.x - transform.position.x;
            float side = Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : facing;
            CombatTrace.Hit(target, gameObject, dmg, new Vector2(side, 0.7f).normalized);       // up and away: a big shove
            if (comp != null) attack.NotifyEnemyHit(comp.gameObject);
            n++;
        }
        return n;
    }

    // ------------------------------------------------------------------------------------------ end / cancel
    void RestoreDetection()
    {
        if (detectionSaved && rb != null) rb.collisionDetectionMode = savedDetection;
        detectionSaved = false;
    }

    void End()
    {
        RestoreDetection();
        Current = Phase.None; phaseTimer = 0f;
        nextAllowed = Time.time + cooldown;
        if (anim != null) anim.SetAirSlamPhase(0);
        if (attack != null) attack.ForceSword(false);
    }

    /// <summary>The slam is stopped (hit, stunned, dead, disabled). No damage is dealt.</summary>
    public void Cancel()
    {
        if (!IsActive) return;
        RestoreDetection();
        Current = Phase.None; phaseTimer = 0f;
        nextAllowed = Time.time + cooldown;
        if (speedLines != null) { Destroy(speedLines); speedLines = null; }
        if (anim != null) anim.SetAirSlamPhase(0);
        if (attack != null) attack.ForceSword(false);
    }
}
