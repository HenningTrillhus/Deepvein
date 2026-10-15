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
    private int baseDamage = -1;
    [SerializeField] private float knockbackForce = 6f;
    [Tooltip("Hvor mye spilleren dyttes når han treffes (1 = som før). Lavere = roligere.")]
    [Range(0f, 1f)] [SerializeField] private float knockbackScale = 0.55f;
    [Tooltip("En sveipe-effekt bak slaget.")]
    [SerializeField] private bool slashEffect = true;
    [Tooltip("Hvor stor buen er i grader.")]
    [SerializeField] private float slashArc = 110f;

    [Header("Timing")]
    [Tooltip("Hvor nær spilleren må være før den slår.")]
    [SerializeField] private float triggerRange = 1.5f;
    [Tooltip("Varsel før boksen blir aktiv.")]
    [SerializeField] private float windupTime = 0.35f;
    [Tooltip("Hvor lenge boksen er synlig og aktiv.")]
    [SerializeField] private float activeTime = 0.2f;
    [Tooltip("Hvile etter slaget.")]
    [SerializeField] private float recoveryTime = 0.8f;

    [Header("Presisjon")]
    [Tooltip("Slår ikke hvis spilleren er mer enn så langt over eller under (units). Hindrer slag gjennom gulv og tak.")]
    [SerializeField] private float maxVerticalGap = 1.6f;
    [Tooltip("Blir slaget avbrutt av et treff, tåler fienden å bli truffet så lenge uten å avbrytes igjen (sekunder). Hindrer at den kan stun-låses.")]
    [SerializeField] private float armorTime = 2f;

    private Rigidbody2D rb;
    private Transform player;
    private PlayerHealth playerHealth;
    private bool isAttacking;
    private bool aimLocked;
    private Coroutine routine;
    private float lastAttackEnd = -999f;
    private float armorUntil;
    private EnemyBlock block;
    private EnemyStateMachine states;
    private EnemyAI ai;

    public bool IsAttacking => isAttacking;
    /// <summary>True en stund etter at et slag ble avbrutt av et treff: da tåler fienden treff uten å bli avbrutt.</summary>
    public bool HasArmor => Time.time < armorUntil;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        block = GetComponent<EnemyBlock>();
        states = GetComponent<EnemyStateMachine>();
        ai = GetComponent<EnemyAI>();

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
        {
            player = found.transform;
            playerHealth = found.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null) playerHealth = found.GetComponentInChildren<PlayerHealth>();
        }

        if (hitboxVisual != null)
            hitboxVisual.SetActive(false);
    }

    void OnDisable()
    {
        // Coroutines fortsetter selv om scriptet skrus av (det er det som gjorde at døde fiender kunne slå): stopp alt her.
        StopAllCoroutines();
        if (isAttacking) EndAttack();
    }

    void Update()
    {
        if (player == null) return;
        if (states != null && states.IsDead) return;

        if (!aimLocked) AimAtPlayer();           // den sikter mens den varsler, men låser seg når slaget går av (kan unnvikes)

        if (!CanStartAttack()) return;
        routine = StartCoroutine(AttackRoutine());
    }

    private bool CanStartAttack()
    {
        if (isAttacking) return false;
        if (states != null && !states.CanAct) return false;                              // lever, ikke stunnet, ikke midt i en reaksjon
        if (block != null && block.IsBlocking) return false;                             // skjoldet er oppe: ingen slag
        if (playerHealth != null && playerHealth.IsDead) return false;                   // ingen slag mot et lik
        if (Time.time < lastAttackEnd + recoveryTime) return false;

        Vector2 d = (Vector2)player.position - (Vector2)transform.position;
        if (d.magnitude > triggerRange) return false;
        if (Mathf.Abs(d.y) > maxVerticalGap) return false;
        if (ai != null && !EnemyDirector.HasToken(ai)) return false;                     // bare den det er tur til slår
        return true;
    }

    private void AimAtPlayer()
    {
        if (attackPivot == null) return;

        Vector2 dir = (Vector2)player.position - (Vector2)attackPivot.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        attackPivot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>Er fienden fortsatt i stand til å fullføre slaget? (Lever, ikke stunnet, ikke truffet, fortsatt i angreps-staten.)</summary>
    private bool StillAttacking()
    {
        if (!isActiveAndEnabled) return false;
        if (states == null) return true;
        return !states.IsDead && !states.IsStunned && states.Current == EnemyState.Attack;
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        aimLocked = false;
        if (ai != null) ai.FaceTowards(player.position.x);          // snur seg mot spilleren før slaget
        AimAtPlayer();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // Animasjonen starter samtidig som windupen, ikke etterpå (litt ekstra lås så staten ikke går ut før siste bilde)
        if (states != null) states.EnterAttack(windupTime + activeTime + 0.05f);
        if (states != null && states.Current != EnemyState.Attack)   // staten ble ikke med: ikke slå uten animasjon
        {
            EndAttack();
            yield break;
        }

        // Windup: står stille, boksen er ikke aktiv enda
        for (float t = 0f; t < windupTime; t += Time.deltaTime)
        {
            if (!StillAttacking()) { EndAttack(); yield break; }
            yield return null;
        }
        if (!StillAttacking()) { EndAttack(); yield break; }

        AimAtPlayer();
        aimLocked = true;                                           // slaget går dit spilleren STO da

        // Drawing order: body, then the slash, then the weapon on top (as on the player), all on the body's sorting layer.
        // (Before, the slash was on the "Default" layer and could cover the weapon.)
        var body = GetComponent<SpriteRenderer>();
        if (body == null) body = GetComponentInChildren<SpriteRenderer>();
        SpriteRenderer weapon = hitboxVisual != null ? hitboxVisual.GetComponent<SpriteRenderer>() : null;
        if (weapon == null && hitboxVisual != null) weapon = hitboxVisual.GetComponentInChildren<SpriteRenderer>(true);
        if (weapon != null && body != null)
        {
            weapon.sortingLayerID = body.sortingLayerID;
            weapon.sortingOrder = body.sortingOrder + 2;
        }

        if (hitboxVisual != null)
            hitboxVisual.SetActive(true);

        if (slashEffect && attackPivot != null)
        {
            float aim = attackPivot.eulerAngles.z;
            int order = body != null ? body.sortingOrder + 1 : CombatFx.SortingOrder - 10;
            CombatFx.Slash(transform, (Vector2)(attackPivot.position - transform.position), aim + slashArc * 0.5f, aim - slashArc * 0.5f,
                           activeTime, attackRange, null, CombatFx.EnemySlash, order);
            if (body != null)
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform c = transform.GetChild(i);
                    if (c.name != "FxFlip") continue;
                    var sr = c.GetComponent<SpriteRenderer>();
                    if (sr != null) { sr.sortingLayerID = body.sortingLayerID; sr.sortingOrder = body.sortingOrder + 1; }
                }
        }

        bool hasHit = false;
        for (float t = 0f; t < activeTime; t += Time.deltaTime)
        {
            if (!StillAttacking()) { EndAttack(); yield break; }
            if (!hasHit && TryHit()) hasHit = true;
            yield return null;
        }

        EndAttack();
    }

    /// <summary>Avbryter slaget akkurat nå (treff, stun, død, blokk). Ingen skade, effekten fjernes.</summary>
    public void Interrupt()
    {
        if (!isAttacking) return;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        armorUntil = Time.time + armorTime;
        EndAttack();
    }

    private void EndAttack()
    {
        routine = null;
        if (hitboxVisual != null) hitboxVisual.SetActive(false);
        aimLocked = false;
        bool wasAttacking = isAttacking;
        isAttacking = false;
        lastAttackEnd = Time.time;

        // sveipe-effekten som henger på fienden forsvinner med slaget
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c.name == "FxFlip") Destroy(c.gameObject);
        }

        if (wasAttacking && ai != null) EnemyDirector.AttackFinished(ai);       // the turn goes on to the next enemy
    }

    private bool TryHit()
    {
        if (states != null && states.IsDead) return false;

        Collider2D hit = Physics2D.OverlapCircle(GetHitboxCenter(), hitboxRadius, playerLayer);
        if (hit == null) return false;

        IDamageable target = hit.GetComponentInParent<IDamageable>();
        if (target == null) return false;

        Vector2 dir = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
        dir.y = Mathf.Max(dir.y, 0.4f);

        CombatTrace.Hit(target, gameObject, damage, dir.normalized * knockbackForce * knockbackScale);
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

    /// <summary>Level-skalering (settes ut av EnemySpawner): skade = grunnskade x multiplier (minst 1).</summary>
    public void ScaleDamage(float multiplier)
    {
        if (baseDamage < 0) baseDamage = damage;
        damage = Mathf.Max(1, (int)System.Math.Round(baseDamage * Mathf.Max(0.1f, multiplier), System.MidpointRounding.AwayFromZero));
    }

    public int Damage { get { return damage; } }
}
