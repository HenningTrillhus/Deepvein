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
    [Tooltip("Hvor lenge spilleren er stunnet når slaget hans blir blokkert/parert av denne fienden (0 = ingen stun).")]
    [SerializeField] private float stunPlayerOnBlock = 0.7f;
    [Tooltip("Hvor mye spilleren dyttes tilbake når slaget hans treffer skjoldet (andel av Parry Knockback på EnemyBlock). Lite = bare et lite dytt.")]
    [Range(0f, 1f)] [SerializeField] private float blockPush = 0.1f;
    [Tooltip("Hvor mye fienden dyttes tilbake når den treffes (1 = som før). Lavere = roligere.")]
    [Range(0f, 1f)] [SerializeField] private float hitKnockbackScale = 0.55f;
    [Header("Effekter")]
    [SerializeField] private bool combatEffects = true;
    

    private int health;
    private int baseMaxHealth;
    private bool isDead;
    private Rigidbody2D rb;
    private EnemyBlock block;
    private EnemyAnimator enemyAnimator;
    private EnemyStateMachine states;

    void Awake()
    {
        baseMaxHealth = maxHealth;
        health = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        block = GetComponent<EnemyBlock>();
        enemyAnimator = GetComponent<EnemyAnimator>();
        states = GetComponent<EnemyStateMachine>();

        if (healthBar != null)
            healthBar.SetHealth(health, maxHealth);
    }

    /// <summary>Level-skalering (settes ut av EnemySpawner): maxliv = grunnliv x multiplier (minst 1), og fienden starter med fullt liv.</summary>
    public void ScaleHealth(float multiplier)
    {
        if (baseMaxHealth <= 0) baseMaxHealth = maxHealth;
        maxHealth = Mathf.Max(1, (int)System.Math.Round(baseMaxHealth * Mathf.Max(0.1f, multiplier), System.MidpointRounding.AwayFromZero));
        health = maxHealth;
        if (healthBar != null) healthBar.InitHealth(health, maxHealth);
    }

    public int MaxHealth { get { return maxHealth; } }

    public void TakeDamage(int amount, Vector2 knockbackDirection)
    {
        // Eneste ting som stopper et treff er at fienden allerede er død.
        // Ingen state, ingen animasjon, ingen lås kan svelge et slag.
        if (isDead) return;

        Vector2 attackOrigin = (Vector2)transform.position - knockbackDirection.normalized * 1f;

        if (block != null && block.CanBlockFrom(attackOrigin))
        {
            block.RegisterBlockedHit();
            if (combatEffects) BlockFx(attackOrigin);
            if (stunPlayerOnBlock > 0f)
            {
                var ph = FindFirstObjectByType<PlayerHealth>();
                if (ph != null) ph.Stun(stunPlayerOnBlock);                // parried: the player is stunned
            }
            amount = Mathf.RoundToInt(amount * block.DamageMultiplier);
            PushBackAttacker(attackOrigin);

            if (amount <= 0) return;
        }

        health = Mathf.Max(0, health - amount);

        if (combatEffects) HitFx(knockbackDirection, health <= 0);

        if (healthBar != null)
            healthBar.SetHealth(health, maxHealth);

        if (health <= 0)
        {
            Die(knockbackDirection);
            return;
        }

        if (states != null) states.EnterHurt();

        if (rb != null)
            rb.AddForce(knockbackDirection * knockbackForce * hitKnockbackScale, ForceMode2D.Impulse);
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

    // ---- effects
    private Vector3 BodyCenter()
    {
        var c = GetComponent<Collider2D>();
        return c != null ? c.bounds.center : transform.position + Vector3.up * 0.5f;
    }

    private Vector3 FeetPoint()
    {
        var c = GetComponent<Collider2D>();
        return c != null ? new Vector3(c.bounds.center.x, c.bounds.min.y, 0f) : transform.position;
    }

    /// <summary>A hit on the shield: a crash with sparks on the side the player came from, and a little dust where the player is pushed.</summary>
    private void BlockFx(Vector2 attackOrigin)
    {
        Vector2 toward = attackOrigin - (Vector2)transform.position;
        float dirX = toward.x >= 0f ? 1f : -1f;
        Vector3 at = BodyCenter() + new Vector3(dirX * 0.45f, 0.1f, 0f);
        CombatFx.Parry(at, new Vector2(dirX, 0.35f));
        CombatFx.Dust(FeetPoint() + new Vector3(dirX * 0.5f, 0f, 0f), (int)dirX, 3);
    }

    /// <summary>A normal hit: a crash in the body; the dust follows the knockback.</summary>
    private void HitFx(Vector2 knockbackDirection, bool killed)
    {
        Vector3 at = BodyCenter();
        CombatFx.Impact(at, killed ? 1.6f : 1f, killed);
        CombatFx.Sparks(at, knockbackDirection, killed ? 10 : 5, 100f, 2.8f);
        int sign = knockbackDirection.x >= 0f ? 1 : -1;
        CombatFx.Dust(FeetPoint(), sign, killed ? 5 : 3);
    }

    private void PushBackAttacker(Vector2 attackOrigin)
    {
        Collider2D hit = Physics2D.OverlapCircle(attackOrigin, attackerSearchRadius, playerLayer);
        if (hit == null) return;

        Rigidbody2D playerRb = hit.attachedRigidbody;
        if (playerRb == null) return;

        Vector2 pushDir = ((Vector2)playerRb.transform.position - (Vector2)transform.position).normalized;
        pushDir.y = Mathf.Max(pushDir.y, 0.1f);

        playerRb.linearVelocity = Vector2.zero;
        playerRb.AddForce(pushDir.normalized * block.ParryKnockback * blockPush, ForceMode2D.Impulse);
    }
}