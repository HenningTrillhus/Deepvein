using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float knockbackForce = 300f;
    [SerializeField] private HealthBar healthBar;

    private int health;
    private Rigidbody2D rb;

    void Awake()
    {
        health = maxHealth;
        rb = GetComponent<Rigidbody2D>();

        if (healthBar != null)
            healthBar.SetHealth(health, maxHealth);
    }

    public void TakeDamage(int amount, Vector2 knockbackDirection)
    {
        health = Mathf.Max(0, health - amount);
        Debug.Log($"TakeDamage: health={health}/{maxHealth}, healthBar={(healthBar == null ? "NULL" : healthBar.name)}");

        if (healthBar != null)
            healthBar.SetHealth(health, maxHealth);

        if (rb != null)
            rb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);

        if (health <= 0)
            Destroy(gameObject);
    }
}