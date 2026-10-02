using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Liv")]
    [SerializeField] private int maxHealth = 10;

    [Header("Usårbarhet etter treff")]
    [SerializeField] private float invincibleTime = 1f;
    [SerializeField] private float blinkInterval = 0.08f;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Knockback")]
    [Tooltip("Hvor lenge spilleren mister kontrollen etter et treff.")]
    [SerializeField] private float stunTime = 0.2f;

    [Header("Blokkering")]
    [Tooltip("Brukes for å finne angriperen som skal dyttes tilbake.")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float attackerSearchRadius = 1.5f;

    private int health;
    private bool invincible;
    private Rigidbody2D rb;
    private PlayerBlock block;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public bool IsStunned { get; private set; }

    void Awake()
    {
        health = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        block = GetComponent<PlayerBlock>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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

        StartCoroutine(StunRoutine());
        StartCoroutine(InvincibilityRoutine());

        if (health <= 0)
            Die();
    }

    private void HandleBlockedHit(Vector2 attackOrigin, ref int amount, ref Vector2 knockback)
    {
        block.RegisterBlockedHit();

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

        while (t < invincibleTime)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;

            yield return new WaitForSeconds(blinkInterval);
            t += blinkInterval;
        }

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        invincible = false;
    }

    private void Die()
    {
        Debug.Log("Spilleren døde");
    }
}