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

    private int health;
    private bool invincible;
    private Rigidbody2D rb;
    private PlayerMovement movement;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public bool IsStunned { get; private set; }

    void Awake()
    {
        health = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void TakeDamage(int amount, Vector2 knockback)
    {
        if (invincible || health <= 0) return;

        health = Mathf.Max(0, health - amount);

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(knockback, ForceMode2D.Impulse);

        StartCoroutine(StunRoutine());
        StartCoroutine(InvincibilityRoutine());

        if (health <= 0)
            Die();
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
        // Senere: respawn, dødsskjerm, animasjon
    }
}