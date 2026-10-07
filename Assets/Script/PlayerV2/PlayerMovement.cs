using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Fart når du bare går.")]
    public float walkSpeed = 3f;
    [Tooltip("Fart når du holder Shift. Må være over 4, ellers blir det ikke løpe-animasjon.")]
    public float sprintSpeed = 5.5f;
    public float jumpForce = 7f;

    [Header("Crouch (hold Ctrl)")]
    [Tooltip("Fart når du går bøyd.")]
    public float crouchSpeed = 1.6f;
    [Tooltip("Hvor høy collideren er når du er bøyd (1 = vanlig). Bunnen blir stående på bakken.")]
    [Range(0.3f, 1f)] public float crouchColliderHeight = 0.65f;

    [Header("Roll (Alt)")]
    public bool enableRoll = true;
    [Tooltip("Hvor langt rullen går, i units (tiles).")]
    public float rollDistance = 2.6f;
    [Tooltip("Lengden på rulle-animasjonen. Match klippet (ca. 0.555 s).")]
    public float rollDuration = 0.555f;
    [Tooltip("Pause etter en rull før du kan rulle igjen.")]
    public float rollCooldown = 0.6f;
    [Range(0.3f, 1f)] public float rollColliderHeight = 0.5f;
    [Tooltip("Uskadelig fra og til dette tidspunktet i rullen (sekunder). Passer bildene 3-9.")]
    public float invulnerableFrom = 0.105f;
    public float invulnerableTo = 0.425f;

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
    private PlayerStamina stamina;
    private Camera cam;

    private Rigidbody2D rb;
    private Collider2D col;
    private LedgeClimbDriver ledgeDriver;
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

    // crouch / roll
    private PlayerHealth health_;
    private bool isCrouching;
    private bool isRolling;
    private Coroutine rollRoutine;
    private float rollCooldownUntil;
    private float rollPressTime = -10f;
    private float colFactor = 1f;
    private Vector2 colSize0, colOffset0;
    private bool colSized;

    /// <summary>Called when a roll starts (direction -1 / +1) and when it ends. Hook the wind and dust effects here.</summary>
    public event System.Action<int> RollStarted;
    public event System.Action RollEnded;

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        anim = GetComponent<PlayerAnimator>();
        attack = GetComponent<PlayerAttack>();
        block = GetComponent<PlayerBlock>();
        stamina = GetComponent<PlayerStamina>();
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        ledgeDriver = GetComponent<LedgeClimbDriver>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        defaultGravity = rb.gravityScale;
        health_ = playerHealth;
        CacheCollider();

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
    void OnDisable()
    {
        controls.Player.Disable();
        CancelRoll();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.leftAltKey.wasPressedThisFrame) rollPressTime = Time.time;        // Alt = rull (lagres et øyeblikk så det ikke mistes)

        // Under en kantklatring / rull er retningen låst - ikke snu midt i animasjonen
        if (!isLedgeClimbing && !isRolling)
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
            CancelRoll();
            return;
        }

        // Kantklatringen styrer posisjonen selv
        if (isLedgeClimbing) return;

        // Rullen styrer farten selv (coroutine)
        if (isRolling) return;

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        onLadder = Physics2D.OverlapCircle(transform.position, ladderCheckRadius, ladderLayer);

        UpdateClimbState();

        if (isClimbing)
        {
            HandleClimbing();
            return;
        }

        // Skjoldet oppe: du står stille (og kan ikke hoppe), men kan fortsatt snu deg mot musa
        bool shieldUp = block != null && block.IsBlocking;

        UpdateCrouch(shieldUp);

        // Alt: rull (på bakken, ikke med skjoldet oppe, ikke midt i et slag)
        if (enableRoll && Time.time - rollPressTime <= 0.12f && Time.time >= rollCooldownUntil && isGrounded && !shieldUp
            && !(attack != null && attack.IsAttacking) && (stamina == null || stamina.CanRoll))
        {
            rollPressTime = -10f;
            if (stamina != null) stamina.SpendRoll();
            int dir = Mathf.Abs(moveInput) > 0.01f ? (int)Mathf.Sign(moveInput) : facing;
            rollRoutine = StartCoroutine(RollRoutine(dir));
            return;
        }

        // Prøv kantklatring før vanlig bevegelse
        if (TryStartLedgeClimb()) return;

        float h = shieldUp ? 0f : moveInput;
        if (IsBlocked(h)) h = 0f;

        rb.linearVelocity = new Vector2(h * CurrentSpeed(), rb.linearVelocity.y);

        if (jumpPressed)
        {
            if (isGrounded && !shieldUp && !isCrouching)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                if (anim != null) anim.Jump();
                if (stamina != null) stamina.SpendJump();     // et hopp koster stamina, men blir aldri stoppet
            }

            jumpPressed = false;
        }

        if (!shieldUp) HandleStep();
    }

    /// <summary>Gå = walkSpeed. Hold Shift = sprintSpeed (ikke mens du blokkerer eller slår).</summary>
    private float CurrentSpeed()
    {
        if (isCrouching) return crouchSpeed;
        return IsSprinting ? sprintSpeed : walkSpeed;
    }

    // ---------- Bøy deg (Ctrl) ----------

    private void CacheCollider()
    {
        if (col is BoxCollider2D b) { colSize0 = b.size; colOffset0 = b.offset; colSized = true; }
        else if (col is CapsuleCollider2D c) { colSize0 = c.size; colOffset0 = c.offset; colSized = true; }
        else Debug.LogWarning("[PlayerMovement] Bøy deg / rull kan bare gjøre collideren lavere hvis den er en Box eller Capsule Collider 2D.");
    }

    /// <summary>1 = vanlig høyde. Bunnen av collideren blir stående på samme sted.</summary>
    private void SetColliderHeight(float f)
    {
        colFactor = f;
        if (!colSized) return;
        Vector2 size = new Vector2(colSize0.x, colSize0.y * f);
        Vector2 offset = new Vector2(colOffset0.x, colOffset0.y - colSize0.y * (1f - f) * 0.5f);
        if (col is BoxCollider2D b) { b.size = size; b.offset = offset; }
        else if (col is CapsuleCollider2D c) { c.size = size; c.offset = offset; }
    }

    /// <summary>Er det plass over hodet til å reise seg?</summary>
    private bool CanStandUp()
    {
        if (!colSized || colFactor >= 0.999f) return true;
        Bounds b = col.bounds;
        float fullH = colSize0.y * Mathf.Abs(col.transform.lossyScale.y);
        Vector2 centre = new Vector2(b.center.x, b.min.y + fullH * 0.5f + 0.02f);
        Vector2 size = new Vector2(b.size.x * 0.9f, Mathf.Max(0.1f, fullH - 0.06f));
        return Physics2D.OverlapBox(centre, size, 0f, groundLayer) == null;
    }

    private void UpdateCrouch(bool shieldUp)
    {
        var kb = Keyboard.current;
        bool ctrl = kb != null && kb.leftCtrlKey.isPressed;
        bool want = ctrl && isGrounded && !isClimbing && !shieldUp;
        bool stuckLow = colFactor < 0.999f && !CanStandUp();          // under et lavt tak: blir bøyd til det er plass
        bool crouch = want || stuckLow;

        if (crouch != isCrouching || (!crouch && colFactor < 0.999f) || (crouch && colFactor > crouchColliderHeight + 0.001f))
        {
            isCrouching = crouch;
            SetColliderHeight(crouch ? crouchColliderHeight : 1f);
            if (anim != null) anim.SetCrouching(crouch);
        }
    }

    // ---------- Rull (Alt) ----------

    private IEnumerator RollRoutine(int dir)
    {
        isRolling = true;
        facing = dir;
        rollCooldownUntil = Time.time + rollDuration + rollCooldown;
        if (anim != null) anim.Roll();
        SetColliderHeight(rollColliderHeight);
        if (RollStarted != null) RollStarted(dir);

        // farten starter høyt og bremser (integralet = rollDistance)
        const float k = 0.7f;
        float v0 = rollDistance / Mathf.Max(0.05f, rollDuration) / (1f - k * 0.5f);
        float t = 0f;
        while (t < rollDuration)
        {
            if (playerHealth != null && (playerHealth.IsStunned || playerHealth.IsDead)) break;
            float u = t / rollDuration;
            float vx = IsBlocked(dir) ? 0f : dir * v0 * (1f - k * u);
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            if (health_ != null) health_.SetDodging(t >= invulnerableFrom && t <= invulnerableTo);
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        EndRoll();
    }

    private void EndRoll()
    {
        if (health_ != null) health_.SetDodging(false);
        bool wasRolling = isRolling;
        isRolling = false;
        rollRoutine = null;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        // reis deg hvis det er plass, ellers bli bøyd (UpdateCrouch tar seg av resten)
        SetColliderHeight(isCrouching ? crouchColliderHeight : colFactor < 0.999f ? rollColliderHeight : 1f);
        if (wasRolling && RollEnded != null) RollEnded();
    }

    /// <summary>Avbryter rullen, f.eks. når spilleren blir truffet eller dør.</summary>
    public void CancelRoll()
    {
        if (!isRolling) return;
        if (rollRoutine != null) StopCoroutine(rollRoutine);
        EndRoll();
    }

    // ---------- Kantklatring ----------

    private bool TryStartLedgeClimb()
    {
        if (!enableLedgeClimb || isLedgeClimbing || isCrouching) return false;
        if (block != null && block.IsBlocking) return false;   // ikke klatre med skjoldet oppe
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

        // Finn selve toppflaten på kanten i stedet for å anta at den er nøyaktig én tile opp
        float standX = b.center.x + dir.x * (b.extents.x + climbForwardOffset);
        float ledgeTop = b.min.y + climbLedgeHeight;                 // reserve hvis strålen ikke treffer noe
        RaycastHit2D topHit = Physics2D.Raycast(new Vector2(standX, b.min.y + climbLedgeHeight + 0.5f), Vector2.down, climbLedgeHeight + 0.5f, groundLayer);
        if (topHit.collider != null) ledgeTop = topHit.point.y;

        // Hvor spilleren skal ende opp, målt i collider-senter
        Vector2 standCenter = new Vector2(standX, ledgeTop + b.extents.y + skin);

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
        col.enabled = false;                 // ingen kollisjon mens han klatrer

        Vector2 start = rb.position;

        if (ledgeDriver != null && ledgeDriver.Ready)
        {
            // LedgeClimbDriver velger bildene og flytter spilleren jevnt
            var steps = ledgeDriver.Play(rb, start, target, climbLedgeHeight);
            while (steps.MoveNext()) yield return steps.Current;
        }
        else
        {
            // Reserve: den gamle måten
            if (anim != null) anim.LedgeClimb(true);
            float t = 0f;
            while (t < climbDuration)
            {
                t += Time.fixedDeltaTime;
                float p = Mathf.Clamp01(t / climbDuration);
                float x = Mathf.Lerp(start.x, target.x, Mathf.Clamp01(climbCurveX.Evaluate(p)));
                float y = Mathf.Lerp(start.y, target.y, Mathf.Clamp01(climbCurveY.Evaluate(p)));
                rb.MovePosition(new Vector2(x, y));
                yield return new WaitForFixedUpdate();
            }
        }

        rb.position = target;
        FinishLedgeClimb();
    }

    private void FinishLedgeClimb()
    {
        col.enabled = true;
        if (ledgeDriver != null) ledgeDriver.Stop();

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
            rb.linearVelocity = new Vector2(moveInput * walkSpeed, jumpForce);
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

        rb.linearVelocity = new Vector2(moveInput * walkSpeed * climbSideControl, vertical);
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
    public bool IsCrouching => isCrouching;
    public bool IsRolling => isRolling;
    public float HorizontalInput => moveInput;
    public int Facing => facing;

    /// <summary>True når du holder Shift og går (ikke mens du blokkerer, slår, er bøyd eller ruller).</summary>
    public bool IsSprinting =>
        Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && Mathf.Abs(moveInput) > 0.01f
        && !(block != null && block.IsBlocking) && !(attack != null && attack.IsAttacking)
        && !isCrouching && !isRolling      // ingen spurt mens du er bøyd eller ruller
        && (stamina == null || stamina.CanSprint);   // og ikke når du er tom for stamina
}