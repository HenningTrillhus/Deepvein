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

    [Header("Ground checks")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float checkRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Visuals")]
    [Tooltip("The sprite child, flipped when turning. Leave empty to flip the whole object.")]
    [SerializeField] private Transform spriteToFlip;

    [Tooltip("Senses the player within this radius even when facing away.")]
    [SerializeField] private float senseRange = 1.5f;

    [SerializeField] private NoticedWarning noticedWarning;

    private enum State { Patrol, Chase }
    private State state = State.Patrol;

    private EnemyAttack attack;
    public int FacingDirection => direction;

    private Rigidbody2D rb;
    private Vector2 startPos;
    private int direction = 1;
    private float pauseTimer;
    private float memoryTimer;
    private float knockbackTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        attack = GetComponent<EnemyAttack>();     // <- ny linje
        rb.freezeRotation = true;
        startPos = transform.position;

        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
    }

    void FixedUpdate()
    {
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        if (attack != null && attack.IsAttacking) return;

        UpdateState();

        if (state == State.Chase) Chase();
        else Patrol();
    }
    

    // ---------- State ----------

    private void UpdateState()
    {
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

            // Give up if we ran out of memory or the player got far away
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

        // Something solid in between blocks both sight and sense
        if (Physics2D.Raycast(transform.position, toPlayer.normalized, dist, sightBlockers))
            return false;

        // Close enough to notice regardless of which way we're facing
        if (dist <= senseRange) return true;

        // Otherwise only sees what's in front
        return Mathf.Sign(toPlayer.x) == direction || Mathf.Abs(toPlayer.x) <= 0.3f;
    }

    // ---------- Behaviour ----------

    private void Patrol()
    {
        //Debug.Log($"ledge={AtLedge()}, wall={AtWall()}, pause={pauseTimer:F2}, state={state}");
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

        // Face the player
        if (Mathf.Abs(dx) > 0.1f)
            SetDirection(dx > 0 ? 1 : -1);

        // Close enough, or about to walk off an edge
        if (Mathf.Abs(dx) <= stopDistance || (useEdgeDetection && AtLedge()))
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(direction * chaseSpeed, rb.linearVelocity.y);
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

    private void SetDirection(int dir)
    {
        if (dir == direction) return;
        direction = dir;

        Transform t = spriteToFlip != null ? spriteToFlip : transform;
        Vector3 s = t.localScale;
        s.x = Mathf.Abs(s.x) * direction;
        t.localScale = s;

        // The checks sit on the sprite's side, so mirror them too
        MirrorCheck(groundCheck);
        MirrorCheck(wallCheck);
    }

    private void MirrorCheck(Transform check)
    {
        if (check == null || check.IsChildOf(spriteToFlip != null ? spriteToFlip : transform)) return;
        Vector3 p = check.localPosition;
        p.x = Mathf.Abs(p.x) * direction;
        check.localPosition = p;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, loseRange);

        Vector3 origin = Application.isPlaying ? (Vector3)startPos : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin + Vector3.left * patrolDistance, origin + Vector3.right * patrolDistance);

        Gizmos.color = Color.green;
        if (groundCheck != null) Gizmos.DrawWireSphere(groundCheck.position, checkRadius);
        if (wallCheck != null) Gizmos.DrawWireSphere(wallCheck.position, checkRadius);
    }

    public void ApplyKnockback(Vector2 force, float stunDuration = 0.3f)
    {
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);
        StartCoroutine(KnockbackStun(stunDuration));
    }

    private IEnumerator KnockbackStun(float duration)
    {
        knockbackTimer = duration;
        yield return new WaitForSeconds(duration);
        knockbackTimer = 0f;
    }
}