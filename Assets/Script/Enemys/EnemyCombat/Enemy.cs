using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float knockbackForce = 300f;
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float attackerSearchRadius = 2f;

    private int health;
    private Rigidbody2D rb;
    private EnemyBlock block;

    void Awake()
    {
        health = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        block = GetComponent<EnemyBlock>();

        if (healthBar != null)
            healthBar.SetHealth(health, maxHealth);
    }

    public void TakeDamage(int amount, Vector2 knockbackDirection)
    {
        Vector2 attackOrigin = (Vector2)transform.position - knockbackDirection.normalized * 1f;

        if (block != null && block.CanBlockFrom(attackOrigin))
        {
            block.RegisterBlockedHit();
            amount = Mathf.RoundToInt(amount * block.DamageMultiplier);

            PushBackAttacker(attackOrigin);

            if (amount <= 0)
                return;    // full blokk, ingen skade og ingen knockback
        }

        health = Mathf.Max(0, health - amount);

        if (healthBar != null)
            healthBar.SetHealth(health, maxHealth);

        if (rb != null)
            rb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);

        if (health <= 0)
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