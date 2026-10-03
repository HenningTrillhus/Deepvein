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
    private Rigidbody2D rb;
    private PlayerBlock block;
    private PlayerAnimator anim;
    private PlayerLayerSync layers;

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
    }

    public void TakeDamage(int amount, Vector2 knockback)
    {
        if (invincible || health <= 0) return;

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

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(knockback, ForceMode2D.Impulse);

        if (health > 0)
        {
            if (anim != null) anim.Hit();
            StartCoroutine(StunRoutine());
            StartCoroutine(InvincibilityRoutine());
        }
        else
        {
            Die();
        }
    }

    private void HandleBlockedHit(Vector2 attackOrigin, ref int amount, ref Vector2 knockback)
    {
        block.RegisterBlockedHit();   // spiller også ShieldHit-animasjonen

        amount = Mathf.RoundToInt(amount * block.DamageMultiplier);
        knockback *= block.KnockbackMultiplier;

        Collider2D attacker = Physics2D.OverlapCircle(attackOrigin, attackerSearchRadius, enemyLayer);
        if (attacker == null) return;

        // Parry: traff innenfor vinduet -> stun i stedet for vanlig knockback
        if (block.IsInParryWindow)
        {
            EnemyStateMachine enemyStates = attacker.GetComponentInParent<EnemyStateMachine>();
            if (enemyStates != null)
            {
                enemyStates.EnterStun();
                return;
            }
        }

        EnemyAI enemyAI = attacker.GetComponentInParent<EnemyAI>();
        if (enemyAI == null) return;

        Vector2 pushDir = ((Vector2)enemyAI.transform.position - (Vector2)transform.position).normalized;
        enemyAI.ApplyKnockback(pushDir * block.ParryKnockback);
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
}
