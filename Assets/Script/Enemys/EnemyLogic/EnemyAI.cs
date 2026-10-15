using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask playerLayer;

    [Header("Speeds")]
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3f;

    [Header("Patrol")]
    [Tooltip("How far from the start point it walks before turning around.")]
    [SerializeField] private float patrolDistance = 4f;
    [Tooltip("Pause at each end of the route.")]
    [SerializeField] private float turnPause = 0.5f;
    [Tooltip("Turn around at walls and ledges instead of walking off.")]
    [SerializeField] private bool useEdgeDetection = true;

    [Header("Detection")]
    [SerializeField] private float sightRange = 6f;
    [Tooltip("How far it keeps chasing once it has seen you.")]
    [SerializeField] private float loseRange = 9f;
    [Tooltip("Keeps chasing this long after losing sight.")]
    [SerializeField] private float memoryTime = 2f;
    [Tooltip("Blocks line of sight. Set this to Ground.")]
    [SerializeField] private LayerMask sightBlockers;
    [Tooltip("Stop this close to the player so it doesn't shove into you.")]
    [SerializeField] private float stopDistance = 0.8f;
    [Tooltip("Senses the player within this radius even when facing away.")]
    [SerializeField] private float senseRange = 1.5f;

    [Header("Ground checks")]
    [Tooltip("Plasser denne på HØYRE side av fienden - retningen regnes ut derfra.")]
    [SerializeField] private Transform groundCheck;
    [Tooltip("Plasser denne på HØYRE side av fienden.")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float checkRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Visuals")]
    [SerializeField] private NoticedWarning noticedWarning;

    [Header("Kamp: ikke alle samtidig")]
    [Tooltip("Fiender som venter på tur holder denne avstanden til spilleren (units).")]
    [SerializeField] private float waitDistance = 3.5f;
    [Tooltip("Etter et slag trekker fienden seg tilbake til denne avstanden (units).")]
    [SerializeField] private float retreatDistance = 2.6f;
    [Tooltip("Hvor lenge den holder seg unna etter et slag (sekunder, tilfeldig mellom disse).")]
    [SerializeField] private float retreatMin = 0.5f;
    [SerializeField] private float retreatMax = 1.3f;
    [Tooltip("Hvor nøyaktig den holder ønsket avstand (units).")]
    [SerializeField] private float distanceTolerance = 0.35f;
    [Tooltip("Hvor fort den går fram og tilbake mens den venter (andel av jagefart).")]
    [Range(0f, 1f)] [SerializeField] private float paceSpeed = 0.4f;

    private enum State { Patrol, Chase }
    private State state = State.Patrol;

    private EnemyAttack attack;
    private Rigidbody2D rb;
    private Vector2 startPos;
    private int direction = 1;
    private float pauseTimer;
    private float memoryTimer;
    private float knockbackTimer;

    // Startposisjonene til sjekkene, så speilvendingen alltid regnes fra samme punkt
    private Vector3 groundCheckStart;
    private Vector3 wallCheckStart;

    public int FacingDirection => direction;
    /// <summary>Avstand til spilleren (units), 999 hvis ukjent.</summary>
    public float DistanceToPlayer { get { return player != null ? Vector2.Distance(transform.position, player.position) : 999f; } }
    private float retreatTimer, paceTimer, idleTimer;
    private int paceDir;
    private bool wasAttacking;
    private EnemyBlock blockRef;
    private EnemyStateMachine statesRef;
    private PlayerHealth playerHealth;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        attack = GetComponent<EnemyAttack>();
        rb.freezeRotation = true;
        startPos = transform.position;

        if (groundCheck != null) groundCheckStart = groundCheck.localPosition;
        if (wallCheck != null) wallCheckStart = wallCheck.localPosition;

        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }

        blockRef = GetComponent<EnemyBlock>();
        statesRef = GetComponent<EnemyStateMachine>();
        if (player != null)
        {
            playerHealth = player.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null) playerHealth = player.GetComponentInChildren<PlayerHealth>();
        }
        EnemyPhysics.IgnoreOthers(gameObject);          // verken spiller eller fiender dytter på hverandre
    }

    void FixedUpdate()
    {
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        bool attackingNow = attack != null && attack.IsAttacking;
        if (wasAttacking && !attackingNow) retreatTimer = Random.Range(retreatMin, retreatMax);     // after a swing: back off a little
        wasAttacking = attackingNow;
        if (retreatTimer > 0f) retreatTimer -= Time.fixedDeltaTime;

        if (attackingNow) return;

        if (statesRef != null)
        {
            if (statesRef.IsDead) return;

            // hurt, parried or stunned: it does not walk, the knockback slides out and brakes
            if (!statesRef.CanAct)
            {
                float bx = Mathf.MoveTowards(rb.linearVelocity.x, 0f, 14f * Time.fixedDeltaTime);
                rb.linearVelocity = new Vector2(bx, rb.linearVelocity.y);
                return;
            }

            // the shield is up: it stands, but keeps looking at the player
            if (statesRef.Current == EnemyState.Block)
            {
                if (player != null)
                {
                    float bdx = player.position.x - transform.position.x;
                    if (Mathf.Abs(bdx) > 0.1f) SetDirection(bdx >= 0f ? 1 : -1);
                }
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                return;
            }
        }

        UpdateState();

        if (state == State.Chase) Chase();
        else Patrol();
    }

    // ---------- State ----------

    private void UpdateState()
    {
        // the player is dead: it gives up and goes back to its route
        if (playerHealth != null && playerHealth.IsDead)
        {
            state = State.Patrol;
            return;
        }

        bool sees = CanSeePlayer();

        if (sees)
        {
            if (state != State.Chase && noticedWarning != null)
                noticedWarning.Show();

            state = State.Chase;
            memoryTimer = memoryTime;
            return;
        }

        if (state == State.Chase)
        {
            memoryTimer -= Time.fixedDeltaTime;

            float dist = player != null ? Vector2.Distance(transform.position, player.position) : 999f;
            if (memoryTimer <= 0f || dist > loseRange)
                state = State.Patrol;
        }
    }

    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        float dist = toPlayer.magnitude;

        if (dist > sightRange) return false;

        if (Physics2D.Raycast(transform.position, toPlayer.normalized, dist, sightBlockers))
            return false;

        if (dist <= senseRange) return true;

        return Mathf.Sign(toPlayer.x) == direction || Mathf.Abs(toPlayer.x) <= 0.3f;
    }

    // ---------- Behaviour ----------

    private void Patrol()
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        bool reachedEnd = Mathf.Abs(transform.position.x - startPos.x) >= patrolDistance
                          && Mathf.Sign(transform.position.x - startPos.x) == direction;

        if (reachedEnd || (useEdgeDetection && (AtLedge() || AtWall())))
        {
            Turn();
            return;
        }

        rb.linearVelocity = new Vector2(direction * patrolSpeed, rb.linearVelocity.y);
    }

    private void Chase()
    {
        float dx = player.position.x - transform.position.x;
        float dist = Mathf.Abs(dx);
        int toward = dx >= 0f ? 1 : -1;
        if (dist > 0.1f) SetDirection(toward);

        EnemyDirector.Join(this);                                   // tell the others we are chasing
        bool token = EnemyDirector.HasToken(this);                  // only the one with the token goes all the way in

        float want;
        if (!token) want = waitDistance;                            // waits its turn
        else if (retreatTimer > 0f) want = retreatDistance;         // just swung: keeps away for a moment
        else want = stopDistance;                                   // its turn: right up to the player

        float vx = 0f;
        if (dist > want + distanceTolerance)
        {
            if (!(useEdgeDetection && AtLedge())) vx = toward * chaseSpeed;
        }
        else if (dist < want - distanceTolerance)
        {
            if (!BackBlocked()) vx = -toward * chaseSpeed * 0.8f;   // backs away (still looking at the player)
        }
        else if (!token || retreatTimer > 0f)
        {
            // waiting (or keeping away after a swing): shuffles a little back and forth, never stands still like a post
            float center = token ? retreatDistance : waitDistance;
            paceTimer -= Time.fixedDeltaTime;
            if (paceTimer <= 0f)
            {
                paceDir = Random.value < 0.5f ? -1 : 1;               // never 0: it is always on its way somewhere
                paceTimer = Random.Range(0.5f, 1.3f);
            }
            float next = dist - paceDir * 0.05f;                    // paceDir +1 = towards the player
            bool inside = next > center - 0.9f && next < center + 0.9f;
            if (!inside) paceDir = dist > center ? 1 : -1;    // too far out or too close: turn back towards the middle
            bool blockedForward = paceDir > 0 && useEdgeDetection && AtLedge();
            bool blockedBack = paceDir < 0 && BackBlocked();
            if (blockedForward) paceDir = -1;
            else if (blockedBack) paceDir = 1;
            vx = toward * paceDir * chaseSpeed * paceSpeed;
        }

        // never just standing there: when it would stand still (and is not blocking, stunned or swinging), it backs off or shuffles
        bool busy = (blockRef != null && blockRef.IsBlocking) || (statesRef != null && !statesRef.CanAct);
        if (Mathf.Abs(vx) < 0.01f && !busy)
        {
            idleTimer += Time.fixedDeltaTime;
            if (idleTimer > 0.2f)
            {
                if (token) retreatTimer = Mathf.Max(retreatTimer, Random.Range(retreatMin, retreatMax));      // its turn but waiting: step back first
                else { paceDir = Random.value < 0.5f ? -1 : 1; paceTimer = Random.Range(0.5f, 1f); }
                idleTimer = 0f;
            }
        }
        else idleTimer = 0f;

        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    /// <summary>True when there is a wall or a drop right behind the enemy (so it will not back into it).</summary>
    private bool BackBlocked()
    {
        if (!useEdgeDetection) return false;
        if (groundCheck != null)
        {
            Vector2 g = groundCheck.position; g.x = 2f * transform.position.x - g.x;
            if (!Physics2D.OverlapCircle(g, checkRadius, groundLayer)) return true;
        }
        if (wallCheck != null)
        {
            Vector2 w = wallCheck.position; w.x = 2f * transform.position.x - w.x;
            if (Physics2D.OverlapCircle(w, checkRadius, groundLayer)) return true;
        }
        return false;
    }

    // ---------- Helpers ----------

    private bool AtLedge()
    {
        if (groundCheck == null) return false;
        return !Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
    }

    private bool AtWall()
    {
        if (wallCheck == null) return false;
        return Physics2D.OverlapCircle(wallCheck.position, checkRadius, groundLayer);
    }

    private void Turn()
    {
        SetDirection(-direction);
        pauseTimer = turnPause;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    /// <summary>Snur fienden mot et punkt (brukes når et slag starter, så den aldri slår bakover).</summary>
    public void FaceTowards(float worldX)
    {
        float dx = worldX - transform.position.x;
        if (Mathf.Abs(dx) > 0.05f) SetDirection(dx >= 0f ? 1 : -1);
    }

    private void SetDirection(int dir)
    {
        if (dir == direction) return;
        direction = dir;

        // Ingen localScale her - EnemyAnimator styrer flipX alene,
        // så attackPivot aldri blir speilvendt.
        MirrorCheck(groundCheck, groundCheckStart);
        MirrorCheck(wallCheck, wallCheckStart);
    }

    private void MirrorCheck(Transform check, Vector3 startLocal)
    {
        if (check == null) return;

        Vector3 p = startLocal;
        p.x = Mathf.Abs(startLocal.x) * direction;
        check.localPosition = p;
    }

    public void ApplyKnockback(Vector2 force, float stunDuration = 0.3f)
    {
        knockbackTimer = stunDuration;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, loseRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, senseRange);

        Vector3 origin = Application.isPlaying ? (Vector3)startPos : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin + Vector3.left * patrolDistance, origin + Vector3.right * patrolDistance);

        Gizmos.color = Color.green;
        if (groundCheck != null) Gizmos.DrawWireSphere(groundCheck.position, checkRadius);
        if (wallCheck != null) Gizmos.DrawWireSphere(wallCheck.position, checkRadius);
    }
}