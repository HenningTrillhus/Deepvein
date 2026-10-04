using System.Collections;
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

    [Header("Ledge climb (1 tile)")]
    public bool enableLedgeClimb = true;
    [Tooltip("Høyden på kanten som kan klatres. 1 tile.")]
    public float climbLedgeHeight = 1f;
    [Tooltip("Hvor langt foran seg den ser etter kanten.")]
    public float climbCheckDistance = 0.1f;
    [Tooltip("Lengden på klatre-animasjonen. Match klippet.")]
    public float climbDuration = 0.5f;
    [Tooltip("Hvor langt inn på kanten spilleren havner.")]
    public float climbForwardOffset = 0.4f;
    [Tooltip("Collidere på dette laget hindrer klatring. La stå tomt for å tillate overalt.")]
    public LayerMask noClimbLayer;
    [Tooltip("Av: koden flytter spilleren. På: animasjonen gjør det selv (root motion).")]
    public bool animationDrivesClimb = false;
    [Tooltip("Når spilleren løftes oppover gjennom klatringen.")]
    public AnimationCurve climbCurveY = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Når spilleren flyttes forover. Hold den sen, så han går opp FØR han går inn.")]
    public AnimationCurve climbCurveX = AnimationCurve.EaseInOut(0.4f, 0f, 1f, 1f);

    [Header("Ladder")]
    public LayerMask ladderLayer;
    public float climbSpeed = 3.5f;
    public float slideSpeed = 1f;
    public float ladderCheckRadius = 0.2f;
    [Range(0f, 1f)] public float climbSideControl = 0.5f;

    [Header("Blocking")]
    public LayerMask blockingLayer;
    public float blockCheckDistance = 0.05f;

    [Header("Visual (the layered player prefab, child of this object)")]
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
    private bool isLedgeClimbing;
    private Coroutine ledgeRoutine;
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
        if (visual != null)
            visualBaseScale = new Vector3(Mathf.Abs(visual.localScale.x), visual.localScale.y, visual.localScale.z);

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
        // Under en kantklatring er retningen låst - ikke snu midt i animasjonen
        if (!isLedgeClimbing)
        {
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
        }

        if (visual != null)
            visual.localScale = new Vector3(visualBaseScale.x * facing, visualBaseScale.y, visualBaseScale.z);

        if (anim != null)
            anim.SetMotion(Mathf.Abs(rb.linearVelocity.x), rb.linearVelocity.y, isGrounded, isClimbing, climbInput);
    }

    void FixedUpdate()
    {
        if (playerHealth != null && (playerHealth.IsStunned || playerHealth.IsDead))
        {
            CancelLedgeClimb();
            return;
        }

        // Kantklatringen styrer posisjonen selv
        if (isLedgeClimbing) return;

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        onLadder = Physics2D.OverlapCircle(transform.position, ladderCheckRadius, ladderLayer);

        UpdateClimbState();

        if (isClimbing)
        {
            HandleClimbing();
            return;
        }

        // Prøv kantklatring før vanlig bevegelse
        if (TryStartLedgeClimb()) return;

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

    // ---------- Kantklatring ----------

    private bool TryStartLedgeClimb()
    {
        if (!enableLedgeClimb || isLedgeClimbing) return false;
        if (!isGrounded) return false;
        if (Mathf.Abs(moveInput) < 0.01f) return false;

        Vector2 dir = new Vector2(Mathf.Sign(moveInput), 0f);
        Bounds b = col.bounds;
        float rayLength = b.extents.x + climbCheckDistance;
        const float skin = 0.02f;

        // Noe i veien over steghøyde? (lavere kanter tar HandleStep)
        Vector2 lowOrigin = new Vector2(b.center.x, b.min.y + stepHeight + skin * 2f);
        if (Physics2D.Raycast(lowOrigin, dir, rayLength, groundLayer).collider == null)
            return false;

        // ... men fritt rett over én tile?
        Vector2 highOrigin = new Vector2(b.center.x, b.min.y + climbLedgeHeight + skin);
        if (Physics2D.Raycast(highOrigin, dir, rayLength, groundLayer).collider != null)
            return false;

        // Hvor spilleren skal ende opp, målt i collider-senter
        Vector2 standCenter = new Vector2(
            b.center.x + dir.x * (b.extents.x + climbForwardOffset),
            b.min.y + climbLedgeHeight + b.extents.y + skin);

        // Forbudt sone?
        if (Physics2D.OverlapCircle(standCenter, b.extents.x, noClimbLayer))
            return false;

        // Er det faktisk plass å stå der oppe?
        if (Physics2D.OverlapBox(standCenter, b.size * 0.85f, 0f, groundLayer))
            return false;

        // rb.position er ikke nødvendigvis collider-senteret
        Vector2 colOffset = (Vector2)b.center - rb.position;
        ledgeRoutine = StartCoroutine(LedgeClimbRoutine(standCenter - colOffset));
        return true;
    }

    private IEnumerator LedgeClimbRoutine(Vector2 target)
    {
        isLedgeClimbing = true;
        facing = (target.x > rb.position.x) ? 1 : -1;

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;

        if (anim != null) anim.LedgeClimb(true);

        Vector2 start = rb.position;
        float t = 0f;

        while (t < climbDuration)
        {
            t += Time.fixedDeltaTime;
            float p = Mathf.Clamp01(t / climbDuration);

            if (!animationDrivesClimb)
            {
                // Opp først, så inn - ellers ser det ut som han går gjennom veggen
                float x = Mathf.Lerp(start.x, target.x, Mathf.Clamp01(climbCurveX.Evaluate(p)));
                float y = Mathf.Lerp(start.y, target.y, Mathf.Clamp01(climbCurveY.Evaluate(p)));
                rb.MovePosition(new Vector2(x, y));
            }

            yield return new WaitForFixedUpdate();
        }

        if (!animationDrivesClimb)
            rb.position = target;

        FinishLedgeClimb();
    }

    private void FinishLedgeClimb()
    {
        isLedgeClimbing = false;
        ledgeRoutine = null;
        rb.gravityScale = defaultGravity;
        rb.linearVelocity = Vector2.zero;

        if (anim != null) anim.LedgeClimb(false);
    }

    /// <summary>Avbryter klatringen, f.eks. når spilleren blir truffet.</summary>
    public void CancelLedgeClimb()
    {
        if (!isLedgeClimbing) return;

        if (ledgeRoutine != null) StopCoroutine(ledgeRoutine);
        FinishLedgeClimb();
    }

    // ---------- Stige ----------

    private void UpdateClimbState()
    {
        if (!onLadder)
        {
            StopClimbing();
            return;
        }

        if (!isClimbing && Mathf.Abs(climbInput) > 0.01f)
        {
            if (climbInput < 0f && isGrounded) return;

            isClimbing = true;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void HandleClimbing()
    {
        if (jumpPressed)
        {
            jumpPressed = false;
            StopClimbing();
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, jumpForce);
            if (anim != null) anim.Jump();
            return;
        }

        float vertical;
        if (climbInput > 0.01f)       vertical = climbSpeed;
        else if (climbInput < -0.01f) vertical = -climbSpeed;
        else                          vertical = -slideSpeed;

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

        // Klatresonen
        if (col != null && Application.isPlaying)
        {
            Bounds b = col.bounds;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(
                new Vector3(b.center.x, b.min.y + climbLedgeHeight, 0f),
                new Vector3(b.center.x + facing * (b.extents.x + climbCheckDistance), b.min.y + climbLedgeHeight, 0f));
        }
    }

    public bool IsClimbing => isClimbing;
    public bool IsLedgeClimbing => isLedgeClimbing;
    public float HorizontalInput => moveInput;
    public int Facing => facing;
}