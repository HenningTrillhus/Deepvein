using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAttack : MonoBehaviour
{
    [Header("Referanser")]
    [Tooltip("Tomt barn-objekt midt på fienden. Roteres mot spilleren.")]
    [SerializeField] private Transform attackPivot;
    [Tooltip("Barn av pivot med SpriteRenderer. Selve slaget som vises.")]
    [SerializeField] private GameObject hitboxVisual;

    [Header("Hitbox")]
    [SerializeField] private float attackRange = 0.9f;
    [SerializeField] private float hitboxRadius = 0.5f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackForce = 6f;

    [Header("Timing")]
    [Tooltip("Hvor nær spilleren må være før den slår.")]
    [SerializeField] private float triggerRange = 1.5f;
    [Tooltip("Varsel før boksen blir aktiv.")]
    [SerializeField] private float windupTime = 0.35f;
    [Tooltip("Hvor lenge boksen er synlig og aktiv.")]
    [SerializeField] private float activeTime = 0.2f;
    [Tooltip("Hvile etter slaget.")]
    [SerializeField] private float recoveryTime = 0.8f;

    private Rigidbody2D rb;
    private Transform player;
    private bool isAttacking;
    private float lastAttackEnd = -999f;
    private EnemyBlock block;
    private EnemyStateMachine states;

    public bool IsAttacking => isAttacking;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        block = GetComponent<EnemyBlock>();
        states = GetComponent<EnemyStateMachine>();

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null) player = found.transform;

        if (hitboxVisual != null)
            hitboxVisual.SetActive(false);
    }

    void Update()
    {
        if (player == null) return;

        AimAtPlayer();

        if (states != null && !states.CanAct) return;
        if (isAttacking) return;
        if (Time.time < lastAttackEnd + recoveryTime) return;

        float dist = Vector2.Distance(transform.position, player.position);
        Debug.Log($"dist={dist:F2}, pivotAngle={attackPivot.eulerAngles.z:F0}, hitbox={GetHitboxCenter()}, rootScaleX={transform.localScale.x}");
        Debug.Log($"player={player.name}, playerPos={player.position}, myPos={transform.position}");

        if (dist <= triggerRange)
            StartCoroutine(AttackRoutine());
    }

    private void AimAtPlayer()
    {
        if (attackPivot == null) return;

        Vector2 dir = (Vector2)player.position - (Vector2)attackPivot.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        attackPivot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private IEnumerator AttackRoutine()
    {
       isAttacking = true;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // Animasjonen starter samtidig som windupen, ikke etterpå
        if (states != null)
            states.EnterAttack(windupTime + activeTime);
        // Windup: står stille, boksen er ikke aktiv enda
        yield return new WaitForSeconds(windupTime);

        // Boksen blir synlig og farlig
        if (hitboxVisual != null)
            hitboxVisual.SetActive(true);

        float t = 0f;
        bool hasHit = false;

        while (t < activeTime)
        {
            if (!hasHit && TryHit())
                hasHit = true;

            t += Time.deltaTime;
            yield return null;
        }

        if (hitboxVisual != null)
            hitboxVisual.SetActive(false);

        lastAttackEnd = Time.time;
        isAttacking = false;
    }

    private bool TryHit()
    {
        Collider2D hit = Physics2D.OverlapCircle(GetHitboxCenter(), hitboxRadius, playerLayer);
        if (hit == null) return false;

        IDamageable target = hit.GetComponentInParent<IDamageable>();
        if (target == null) return false;

        Vector2 dir = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
        dir.y = Mathf.Max(dir.y, 0.4f);

        target.TakeDamage(damage, dir.normalized * knockbackForce);
        return true;
    }

    private Vector2 GetHitboxCenter()
    {
        if (attackPivot == null) return transform.position;
        return (Vector2)attackPivot.position + (Vector2)attackPivot.right * attackRange;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(GetHitboxCenter(), hitboxRadius);
        Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, triggerRange);
    }
}