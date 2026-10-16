using System.Collections;
using UnityEngine;
using DeepVain.Player;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Liv")]
    [SerializeField] private int maxHealth = 10;

    [Header("Usårbarhet etter treff")]
    [SerializeField] private float invincibleTime = 1f;
    [SerializeField] private float blinkInterval = 0.08f;

    [Header("Recoil (kastet litt bakover)")]
    [Tooltip("På: et treff som tar liv spiller Recoil-animasjonen (kastet bakover). Av: den gamle Hit-animasjonen.")]
    [SerializeField] private bool recoilOnDamage = true;

    [Header("Knockback")]
    [Tooltip("Hvor lenge spilleren mister kontrollen etter et treff. Hit-animasjonen varer ca. 0.3 s.")]
    [SerializeField] private float stunTime = 0.3f;

    [Header("Blokkering")]
    [Tooltip("Brukes for å finne angriperen som skal dyttes tilbake.")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float attackerSearchRadius = 1.5f;

    [Header("Død")]
    [Tooltip("Hvor lenge etter døden utseendet skjules (Death + DeathFade varer ca. 1.6 s).")]
    [SerializeField] private float hideAfterDeath = 1.7f;

    private int health;
    private bool invincible;

    [Header("Dører")]
    [Tooltip("Sekunder spilleren ikke kan skades etter at han har gått gjennom en dør (regnes fra han får kontrollen tilbake).")]
    public float doorGraceTime = 3f;
    private float graceUntil;
    /// <summary>True mens spilleren er trygg etter en dør (eller midt i overgangen).</summary>
    public bool HasDoorGrace { get { return Time.time < graceUntil; } }
    /// <summary>Trygg så lenge overgangen pågår (kalles av DoorEnterDriver).</summary>
    public void LockInvulnerable() { graceUntil = float.MaxValue; }
    /// <summary>Starter de trygge sekundene etter døra.</summary>
    public void StartDoorGrace() { graceUntil = Time.time + doorGraceTime; }
    public void SetDodging(bool on) { dodging = on && !IsDead; }
    private bool dodging;
    private Rigidbody2D rb;
    private PlayerBlock block;
    private PlayerAnimator anim;
    private PlayerLayerSync layers;
    private PlayerAttack attackRef;
    private AirSlam airSlam;
    private AirSlam Slam { get { if (airSlam == null) { airSlam = GetComponent<AirSlam>(); if (airSlam == null) airSlam = gameObject.AddComponent<AirSlam>(); } return airSlam; } }

    public int Health => health;
    public int MaxHealth => maxHealth;
    public bool IsStunned { get; private set; }
    public bool IsDead { get; private set; }

    void Awake()
    {
        health = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        block = GetComponent<PlayerBlock>();
        anim = GetComponent<PlayerAnimator>();
        layers = GetComponentInChildren<PlayerLayerSync>();
        attackRef = GetComponent<PlayerAttack>();
        airSlam = GetComponent<AirSlam>();
    }

    public void TakeDamage(int amount, Vector2 knockback)
    {
        // invincible after a hit, during the roll's i-frames (dodging was set by the roll but never read before), and when dead
        if (invincible || dodging || health <= 0 || Time.time < graceUntil) return;

        // Hvor kom slaget fra? Knockback peker bort fra angriperen.
        Vector2 attackOrigin = (Vector2)transform.position - knockback.normalized * 1f;

        if (block != null && block.CanBlockFrom(attackOrigin))
        {
            HandleBlockedHit(attackOrigin, ref amount, ref knockback);

            // Full blokk: ingen skade, ingen stun, ingen blinking
            if (amount <= 0)
            {
                rb.linearVelocity = Vector2.zero;
                rb.AddForce(knockback, ForceMode2D.Impulse);
                return;
            }
        }

        health = Mathf.Max(0, health - amount);
        // an air slam has hyper armor: a hit still hurts, but it does not knock him out of the slam (no stun, no knockback)
        bool armored = Slam.IsActive && Slam.hyperArmor;
        if (amount > 0) HitFx(attackOrigin, knockback);
        if (amount > 0 && attackRef != null && !armored) attackRef.CancelSwing();     // a hit knocks the swing out of your hand
        if (amount > 0 && !armored) Slam.Cancel();              // ... and stops an air slam (unless it has armor)

        if (!armored)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(knockback, ForceMode2D.Impulse);
        }

        if (health > 0)
        {
            if (!armored)
            {
                if (anim != null) { if (recoilOnDamage) anim.Recoil(); else anim.Hit(); }
                StartCoroutine(StunRoutine());
            }
            StartCoroutine(InvincibilityRoutine());
        }
        else
        {
            Die();
        }
    }

    // ---- effects (dust, sparks and a crash)
    private Vector3 ChestPoint() { return transform.position + new Vector3(0f, 0.7f, 0f); }

    private void HitFx(Vector2 attackOrigin, Vector2 knockback)
    {
        CombatFx.Impact(ChestPoint(), 1f, false);
        CombatFx.Sparks(ChestPoint(), knockback, 4, 100f, 2.4f);
        CombatFx.Dust(transform.position, knockback.x >= 0f ? 1 : -1, 3);
    }

    private void BlockFx(Vector2 attackOrigin)
    {
        float dirX = attackOrigin.x >= transform.position.x ? 1f : -1f;
        Vector3 at = ChestPoint() + new Vector3(dirX * 0.45f, 0f, 0f);
        CombatFx.Clang(at, new Vector2(dirX, 0.4f));
        CombatFx.Dust(transform.position + new Vector3(dirX * 0.1f, 0f, 0f), -(int)dirX, 3);       // the push goes the other way
    }

    private void HandleBlockedHit(Vector2 attackOrigin, ref int amount, ref Vector2 knockback)
    {
        block.RegisterBlockedHit();   // spiller også ShieldHit-animasjonen
        BlockFx(attackOrigin);

        amount = Mathf.RoundToInt(amount * block.DamageMultiplier);
        knockback *= block.KnockbackMultiplier;

        // the enemy that really swung (EnemyAttack says so); only when that is not known the closest one is used
        Component attacker = CombatTrace.Attacker != null ? (Component)CombatTrace.Attacker.transform : null;
        if (attacker == null)
        {
            Collider2D c = Physics2D.OverlapCircle(attackOrigin, attackerSearchRadius, enemyLayer);
            if (c == null) return;
            attacker = c;
        }

        // Parry: traff innenfor vinduet -> stun i stedet for vanlig knockback
        if (block.IsInParryWindow)
        {
            EnemyStateMachine enemyStates = attacker.GetComponentInParent<EnemyStateMachine>();
            if (enemyStates != null)
            {
                enemyStates.EnterStun();
                CombatFx.Parry(enemyStates.transform.position + new Vector3(0f, 0.7f, 0f), (Vector2)(enemyStates.transform.position - transform.position));
                return;
            }
        }

        EnemyAI enemyAI = attacker.GetComponentInParent<EnemyAI>();
        if (enemyAI == null) return;

        Vector2 pushDir = ((Vector2)enemyAI.transform.position - (Vector2)transform.position).normalized;
        enemyAI.ApplyKnockback(pushDir * block.ParryKnockback);
    }

    /// <summary>
    /// The sword bounced off a shield (or he is thrown a little back): the Recoil animation, no control for a moment, a little dust where
    /// he skids. A swing or an air slam that was going is stopped.
    /// </summary>
    public void Recoil(float seconds, bool clang = false)
    {
        if (IsDead || seconds <= 0f) return;
        if (attackRef != null) attackRef.CancelSwing();
        Slam.Cancel();
        if (anim != null) anim.Recoil();
        if (clang) CombatFx.Impact(ChestPoint() + new Vector3(0.45f, 0f, 0f), 1.1f, false);
        StartCoroutine(StunFor(seconds));
        StartCoroutine(SkidDust(0.25f));
    }

    private IEnumerator SkidDust(float after)
    {
        yield return new WaitForSeconds(after);
        if (!IsDead) CombatFx.Dust(transform.position, -1, 4);
    }

    /// <summary>Stunner spilleren i så mange sekunder (brukes når et slag blir parert av en fiende).</summary>
    public void Stun(float seconds)
    {
        if (IsDead || seconds <= 0f) return;
        if (anim != null) anim.Hit();
        StartCoroutine(StunFor(seconds));
    }

    private IEnumerator StunFor(float seconds)
    {
        IsStunned = true;
        yield return new WaitForSeconds(seconds);
        IsStunned = false;
    }

    private IEnumerator StunRoutine()
    {
        IsStunned = true;
        yield return new WaitForSeconds(stunTime);
        IsStunned = false;
    }

    private IEnumerator InvincibilityRoutine()
    {
        invincible = true;
        float t = 0f;

        while (t < invincibleTime && !IsDead)
        {
            // blink via PlayerLayerSync slik at ALLE lagene blinker sammen
            if (layers != null) layers.blinkHidden = !layers.blinkHidden;

            yield return new WaitForSeconds(blinkInterval);
            t += blinkInterval;
        }

        if (layers != null) layers.blinkHidden = false;

        invincible = false;
    }

    private void Die()
    {
        IsDead = true;
        IsStunned = false;
        invincible = true;
        if (layers != null) layers.blinkHidden = false;

        rb.linearVelocity = Vector2.zero;
        if (anim != null) anim.Die();             // Death -> DeathFade (kroppen løses opp)

        // ingen angrep / blokkering mens vi er døde
        var attack = GetComponent<PlayerAttack>(); if (attack != null) attack.enabled = false;
        if (block != null) { block.enabled = false; }

        StartCoroutine(HideAfterDeath());
        Debug.Log("Spilleren døde");
    }

    private IEnumerator HideAfterDeath()
    {
        yield return new WaitForSeconds(hideAfterDeath);
        if (layers != null) layers.gameObject.SetActive(false);
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        health = Mathf.Min(maxHealth, health + amount);
    }
}
