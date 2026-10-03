using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("Ground check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundLayer;

    [Header("Step over small ledges")]
    public float stepHeight = 0.2f;
    public float stepCheckDistance = 0.08f;
    public float stepSmooth = 8f;

    [Header("Ladder")]
    public LayerMask ladderLayer;
    [Tooltip("Fart opp/ned når du holder W eller S.")]
    public float climbSpeed = 3.5f;
    [Tooltip("Fart nedover når du ikke holder noe.")]
    public float slideSpeed = 1f;
    public float ladderCheckRadius = 0.2f;
    [Tooltip("Hvor mye du kan bevege deg sidelengs mens du henger i stigen.")]
    [Range(0f, 1f)] public float climbSideControl = 0.5f;

    [Header("Blocking")]
    [Tooltip("Lag som stopper spilleren. Huk av Enemy her.")]
    public LayerMask blockingLayer;
    public float blockCheckDistance = 0.05f;

    [Header("Visual (the layered player prefab, child of this object)")]
    [Tooltip("Barn-objektet med Player.prefab. Det blir speilvendt når du går mot venstre.")]
    [SerializeField] private Transform visual;

    private PlayerHealth playerHealth;
    private PlayerAnimator anim;
    private PlayerAttack attack;
    private PlayerBlock block;
    private Camera cam;

    private Rigidbody2D rb;
    private Collider2D col;
    private PlayerControls controls;

    private float moveInput;
    private float climbInput;
    private bool jumpPressed;
    private bool isGrounded;
    private bool onLadder;
    private bool isClimbing;
    private float defaultGravity;
    private Vector3 visualBaseScale = Vector3.one;
    private int facing = 1;

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        anim = GetComponent<PlayerAnimator>();
        attack = GetComponent<PlayerAttack>();
        block = GetComponent<PlayerBlock>();
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        defaultGravity = rb.gravityScale;

        if (visual == null)
        {
            var a = GetComponentInChildren<Animator>();
            if (a != null) visual = a.transform;
        }
        if (visual != null) visualBaseScale = new Vector3(Mathf.Abs(visual.localScale.x), visual.localScale.y, visual.localScale.z);

        controls = new PlayerControls();
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<float>();
        controls.Player.Move.canceled += ctx => moveInput = 0f;
        controls.Player.Climb.performed += ctx => climbInput = ctx.ReadValue<float>();
        controls.Player.Climb.canceled += ctx => climbInput = 0f;
        controls.Player.Jump.performed += ctx => jumpPressed = true;
    }

    void OnEnable() => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();

    void Update()
    {
        // Mens du slår eller blokkerer ser spilleren mot musa. Ellers mot gå-retningen.
        bool aiming = (attack != null && attack.IsAttacking) || (block != null && block.IsBlocking);
        if (aiming && cam != null && Mouse.current != null)
        {
            Vector2 ms = Mouse.current.position.ReadValue();
            float dx = cam.ScreenToWorldPoint(new Vector3(ms.x, ms.y, -cam.transform.position.z)).x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f) facing = dx < 0 ? -1 : 1;
        }
        else if (moveInput != 0)
        {
            facing = moveInput < 0 ? -1 : 1;
        }

        // speilvend hele det lagdelte utseendet (alle lag følger med)
        if (visual != null)
            visual.localScale = new Vector3(visualBaseScale.x * facing, visualBaseScale.y, visualBaseScale.z);

        if (anim != null)
            anim.SetMotion(Mathf.Abs(rb.linearVelocity.x), rb.linearVelocity.y, isGrounded, isClimbing, climbInput);
    }

    void FixedUpdate()
    {
        if (playerHealth != null && (playerHealth.IsStunned || playerHealth.IsDead)) return;

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        onLadder = Physics2D.OverlapCircle(transform.position, ladderCheckRadius, ladderLayer);

        UpdateClimbState();

        if (isClimbing)
        {
            HandleClimbing();
            return;
        }

        float h = moveInput;
        if (IsBlocked(h)) h = 0f;

        rb.linearVelocity = new Vector2(h * moveSpeed, rb.linearVelocity.y);

        if (jumpPressed)
        {
            if (isGrounded)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                if (anim != null) anim.Jump();
            }

            jumpPressed = false;
        }

        HandleStep();
    }

    // ---------- Stige ----------

    private void UpdateClimbState()
    {
        // Gikk vi av stigen? Slå på tyngdekraften igjen
        if (!onLadder)
        {
            StopClimbing();
            return;
        }

        if (!isClimbing && Mathf.Abs(climbInput) > 0.01f)
        {
            // Trykker S mens vi står på bakken -> vi vil bare stå, ikke klatre
            if (climbInput < 0f && isGrounded) return;

            isClimbing = true;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void HandleClimbing()
    {
        // Hopp av stigen
        if (jumpPressed)
        {
            jumpPressed = false;
            StopClimbing();
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, jumpForce);
            if (anim != null) anim.Jump();
            return;
        }

        float vertical;
        if (climbInput > 0.01f)       vertical = climbSpeed;      // W = opp
        else if (climbInput < -0.01f) vertical = -climbSpeed;     // S = ned
        else                          vertical = -slideSpeed;     // slipper -> sklir sakte ned

        // Nådd bakken på vei ned -> gå ut av klatremodus
        if (vertical < 0f && isGrounded)
        {
            StopClimbing();
            return;
        }

        rb.linearVelocity = new Vector2(moveInput * moveSpeed * climbSideControl, vertical);
    }

    private void StopClimbing()
    {
        if (!isClimbing) return;
        isClimbing = false;
        rb.gravityScale = defaultGravity;
    }

    // ---------- Steg over lave kanter ----------

    private void HandleStep()
    {
        if (!isGrounded || Mathf.Abs(moveInput) < 0.01f) return;

        Vector2 dir = new Vector2(Mathf.Sign(moveInput), 0f);
        Bounds b = col.bounds;

        float rayLength = b.extents.x + stepCheckDistance;
        const float skin = 0.02f;

        Vector2 lowerOrigin = new Vector2(b.center.x, b.min.y + skin);
        Vector2 upperOrigin = new Vector2(b.center.x, b.min.y + stepHeight + skin);

        RaycastHit2D lower = Physics2D.Raycast(lowerOrigin, dir, rayLength, groundLayer);
        RaycastHit2D upper = Physics2D.Raycast(upperOrigin, dir, rayLength, groundLayer);

        if (lower.collider != null && upper.collider == null)
        {
            if (stepSmooth <= 0f)
                rb.position += new Vector2(0f, stepHeight + skin);
            else
                rb.position += new Vector2(0f, (stepHeight + skin) * stepSmooth * Time.fixedDeltaTime);
        }
    }

    private bool IsBlocked(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.01f) return false;

        Vector2 dir = new Vector2(Mathf.Sign(dirX), 0f);
        Bounds b = col.bounds;

        // Tynn boks langs siden av spilleren, litt lavere enn collideren
        // så vi ikke fanger opp bakken eller lave kanter vi kan gå over
        Vector2 origin = new Vector2(b.center.x, b.center.y + stepHeight * 0.5f);
        Vector2 size = new Vector2(b.size.x * 0.9f, b.size.y - stepHeight);

        return Physics2D.BoxCast(origin, size, 0f, dir, blockCheckDistance, blockingLayer);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, ladderCheckRadius);
    }

    public bool IsClimbing => isClimbing;
    public float HorizontalInput => moveInput;
    /// <summary>1 = ser mot høyre, -1 = ser mot venstre (bruk denne i stedet for spriteRenderer.flipX).</summary>
    public int Facing => facing;
}
