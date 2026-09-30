using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("Referanser")]
    [Tooltip("Tomt barn-objekt i midten av spilleren. Roteres mot musa.")]
    public Transform attackPivot;
    [Tooltip("Barn av pivot, med SpriteRenderer. Selve slaget som vises.")]
    public GameObject hitboxVisual;

    [Header("Hitbox")]
    [Tooltip("Hvor langt fra spilleren treffområdet ligger.")]
    public float attackRange = 0.8f;
    [Tooltip("Størrelsen på treffområdet.")]
    public float hitboxRadius = 0.5f;
    public LayerMask enemyLayer;

    [Header("Timing")]
    [Tooltip("Hvor lenge slaget er aktivt og synlig.")]
    public float attackDuration = 0.5f;
    [Tooltip("Ventetid før du kan slå igjen (fra slaget startet).")]
    public float attackCooldown = 0.7f;

    [Header("Skade")]
    public int damage = 1;

    private PlayerControls controls;
    private Camera cam;
    private bool isAttacking;
    private float lastAttackTime = -999f;

    // Så hver fiende bare tar skade én gang per slag
    private readonly HashSet<Collider2D> alreadyHit = new HashSet<Collider2D>();

    void Awake()
    {
        cam = Camera.main;

        controls = new PlayerControls();
        controls.Player.Attack.performed += ctx => TryAttack();

        if (hitboxVisual != null)
            hitboxVisual.SetActive(false);
    }

    void OnEnable() => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();

    void Update()
    {
        // Pek pivoten mot musa, også når vi ikke slår
        AimAtMouse();

        // Mens slaget er aktivt sjekker vi treff hver frame
        if (isAttacking)
            CheckHits();
    }

    private void AimAtMouse()
    {
        if (attackPivot == null || cam == null) return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));

        Vector2 dir = (Vector2)mouseWorld - (Vector2)attackPivot.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        attackPivot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void TryAttack()
    {
        if (isAttacking) return;
        if (Time.time < lastAttackTime + attackCooldown) return;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        alreadyHit.Clear();

        if (hitboxVisual != null)
            hitboxVisual.SetActive(true);

        yield return new WaitForSeconds(attackDuration);

        if (hitboxVisual != null)
            hitboxVisual.SetActive(false);

        isAttacking = false;
    }

    private void CheckHits()
    {
        Vector2 center = GetHitboxCenter();
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, hitboxRadius, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            if (alreadyHit.Contains(hit)) continue;
            alreadyHit.Add(hit);

            // Leter på objektet selv OG oppover i hierarkiet
            IDamageable target = hit.GetComponentInParent<IDamageable>();

            if (target != null)
            {
                Vector2 knockbackDir = ((Vector2)hit.transform.position - (Vector2)attackPivot.position).normalized;
                target.TakeDamage(damage, knockbackDir);
            }
            else
            {
                Debug.Log($"{hit.name} har ingen IDamageable");
            }
        }
    }

    private Vector2 GetHitboxCenter()
    {
        // attackPivot.right peker mot musa etter rotasjonen
        return (Vector2)attackPivot.position + (Vector2)attackPivot.right * attackRange;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPivot == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetHitboxCenter(), hitboxRadius);
    }
}