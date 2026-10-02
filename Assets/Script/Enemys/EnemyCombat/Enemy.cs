using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Liv")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float knockbackForce = 300f;
    [SerializeField] private HealthBar healthBar;

    [Header("Død")]
    [Tooltip("Hvor lenge liket blir liggende før fade starter.")]
    [SerializeField] private float deathLingerTime = 1f;
    [Tooltip("Lengden på fade-animasjonen.")]
    [SerializeField] private float fadeDuration = 0.6f;

    [Header("Blokkering")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float attackerSearchRadius = 2f;
    

    private int health;
    private bool isDead;
    private Rigidbody2D rb;
    private EnemyBlock block;
    private EnemyAnimator enemyAnimator;
    private EnemyStateMachine states;

    void Awake()
    {
        health = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        block = GetComponent<EnemyBlock>();
        enemyAnimator = GetComponent<EnemyAnimator>();
        states = GetComponent<EnemyStateMachine>();

        if (healthBar != null)
            healthBar.SetHealth(health, maxHealth);
    }

    public void TakeDamage(int amount, Vector2 knockbackDirection)
    {
        // Eneste ting som stopper et treff er at fienden allerede er død.
        // Ingen state, ingen animasjon, ingen lås kan svelge et slag.
        if (isDead) return;

        Vector2 attackOrigin = (Vector2)transform.position - knockbackDirection.normalized * 1f;

        if (block != null && block.CanBlockFrom(attackOrigin))
        {
            block.RegisterBlockedHit();
            amount = Mathf.RoundToInt(amount * block.DamageMultiplier);
            PushBackAttacker(attackOrigin);

            if (amount <= 0) return;
        }

        health = Mathf.Max(0, health - amount);

        if (healthBar != null)
            healthBar.SetHealth(health, maxHealth);

        if (health <= 0)
        {
            Die(knockbackDirection);
            return;
        }

        if (states != null) states.EnterHurt();

        if (rb != null)
            rb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);
    }

    private void Die(Vector2 knockbackDirection)
    {
        isDead = true;

        if (states != null)
            states.EnterDead();

        // Stopp all oppførsel
        if (TryGetComponent(out EnemyAI ai)) ai.enabled = false;
        if (TryGetComponent(out EnemyAttack atk)) atk.enabled = false;
        if (block != null) block.enabled = false;

        // Frys kroppen der den står - faller ikke, dytter ingen
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        Debug.Log("Die kjørt, Dead satt til true");

        // La collideren være, men gjør den til trigger så spilleren går gjennom liket
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>())
            c.enabled = false;

        //if (healthBar != null)
        //    foreach (SpriteRenderer r in healthBar.GetComponentsInChildren<SpriteRenderer>())
        //        r.enabled = false;

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        yield return new WaitForSeconds(deathLingerTime);

        if (states != null)
            states.EnterFade();

        yield return new WaitForSeconds(fadeDuration);

        Destroy(gameObject);
    }

    private void PushBackAttacker(Vector2 attackOrigin)
    {
        Collider2D hit = Physics2D.OverlapCircle(attackOrigin, attackerSearchRadius, playerLayer);
        if (hit == null) return;

        Rigidbody2D playerRb = hit.attachedRigidbody;
        if (playerRb == null) return;

        Vector2 pushDir = ((Vector2)playerRb.transform.position - (Vector2)transform.position).normalized;
        pushDir.y = Mathf.Max(pushDir.y, 0.3f);

        playerRb.linearVelocity = Vector2.zero;
        playerRb.AddForce(pushDir.normalized * block.ParryKnockback, ForceMode2D.Impulse);
    }
}