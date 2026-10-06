using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("Referanser")]
    [Tooltip("Tomt barn av spiller-ROTA. Må ikke ligge under noe som speilvendes (Visual-barnet speilvendes, så ikke under det).")]
    [SerializeField] private Transform attackPivot;
    [Tooltip("Sverd-objektet, barn av pivoten.")]
    [SerializeField] private Transform sword;
    [SerializeField] private SpriteRenderer swordRenderer;
    [Tooltip("Sverdet sitter i spillerens hånd (HandSocket) og følger animasjonen. Da roteres/vises det ikke av denne scriptet.")]
    [SerializeField] private bool swordFollowsHand = true;
    [Tooltip("Hvor lenge sverdet blir synlig etter at svingen (hitboxen) er ferdig, så animasjonen får gjøre seg ferdig.")]
    [SerializeField] private float swordLinger = 0.15f;
    private Coroutine hideRoutine;

    [Header("Sving")]
    [Tooltip("Hvor stor buen er i grader. 120 = fra godt over til godt under.")]
    [SerializeField] private float swingArc = 120f;
    [Tooltip("Hvor lenge svingen varer.")]
    [SerializeField] private float swingDuration = 0.22f;
    [Tooltip("Hvile før neste slag.")]
    [SerializeField] private float attackCooldown = 0.45f;
    [Tooltip("Kurve for svingen. Bratt start = rask pisk.")]
    [SerializeField] private AnimationCurve swingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Hitbox")]
    [Tooltip("Avstand fra spilleren til midten av treffsonen.")]
    [SerializeField] private float hitboxDistance = 0.8f;
    [SerializeField] private float hitboxRadius = 0.45f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private int damage = 1;

    private PlayerControls controls;
    private Camera cam;
    private PlayerAnimator anim;
    private PlayerMovement movement;
    private PlayerBlock block;
    private bool isAttacking;
    private float lastAttackTime = -999f;
    private float currentAngle;

    private readonly HashSet<Collider2D> alreadyHit = new HashSet<Collider2D>();

    /// <summary>True mens sverdet svinger (brukes av PlayerMovement for å snu spilleren mot musa).</summary>
    public bool IsAttacking => isAttacking;

    void Awake()
    {
        cam = Camera.main;
        anim = GetComponent<PlayerAnimator>();
        movement = GetComponent<PlayerMovement>();
        block = GetComponent<PlayerBlock>();

        if (swordRenderer == null && sword != null)
            swordRenderer = sword.GetComponent<SpriteRenderer>();

        controls = new PlayerControls();
        controls.Player.Attack.performed += ctx => TryAttack();

        if (sword != null)
            ShowSword(false);
    }

    void OnEnable() => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();

    void Update()
    {
        // Mens vi ikke slår, peker pivoten mot musa så sverdet er klart
        if (!isAttacking)
            currentAngle = GetAimAngle();

        ApplyAngle(currentAngle);

        if (isAttacking)
            CheckHits();
    }

    private void TryAttack()
    {
        if (isAttacking) return;
        if (Time.time < lastAttackTime + attackCooldown) return;
        if (block != null && block.IsBlocking) return;   // ikke slå mens skjoldet er oppe

        // Mens du spurter kan du slå fremover, men ikke bakover - da må du stoppe opp først
        if (movement != null && movement.IsSprinting)
        {
            bool aimRight = Mathf.Cos(GetAimAngle() * Mathf.Deg2Rad) > 0f;
            bool moveRight = movement.HorizontalInput > 0f;
            if (aimRight != moveRight) return;
        }

        StartCoroutine(SwingRoutine());
    }

    private IEnumerator SwingRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        alreadyHit.Clear();

        if (anim != null) anim.Attack();           // spillerens egen slag-animasjon

        // Siktevinkelen låses når slaget starter
        float aim = GetAimAngle();
        float start = aim + swingArc * 0.5f;    // over siktelinjen
        float end = aim - swingArc * 0.5f;      // under siktelinjen

        if (hideRoutine != null) { StopCoroutine(hideRoutine); hideRoutine = null; }
        if (sword != null)
            ShowSword(true);

        float t = 0f;
        while (t < swingDuration)
        {
            t += Time.deltaTime;
            float p = swingCurve.Evaluate(Mathf.Clamp01(t / swingDuration));

            currentAngle = Mathf.Lerp(start, end, p);
            ApplyAngle(currentAngle);

            yield return null;
        }

        isAttacking = false;
        if (swordFollowsHand) hideRoutine = StartCoroutine(HideSwordAfter(swordLinger));
        else ShowSword(false);
    }

    private void ShowSword(bool on)
    {
        if (sword == null) return;
        if (swordFollowsHand)
        {
            // sverdet er barn av HandSocket: skru bare tegningen av og på
            if (swordRenderer != null) swordRenderer.enabled = on;
            return;
        }
        sword.gameObject.SetActive(on);
    }

    private IEnumerator HideSwordAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        ShowSword(false);
        hideRoutine = null;
    }

    private float GetAimAngle()
    {
        if (cam == null || attackPivot == null) return currentAngle;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = cam.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));

        Vector2 dir = (Vector2)mouseWorld - (Vector2)attackPivot.position;
        if (dir.sqrMagnitude < 0.0001f) return currentAngle;

        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }

    private void ApplyAngle(float angle)
    {
        if (attackPivot == null) return;

        attackPivot.rotation = Quaternion.Euler(0f, 0f, angle);

        if (swordRenderer != null && !swordFollowsHand)
        {
            // Peker vi mot venstre halvdel, snu spriten vertikalt
            float normalized = Mathf.Repeat(angle, 360f);
            swordRenderer.flipY = normalized > 90f && normalized < 270f;
        }
    }

    private void CheckHits()
    {
        Vector2 center = GetHitboxCenter();
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, hitboxRadius, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            if (alreadyHit.Contains(hit)) continue;
            alreadyHit.Add(hit);

            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null) continue;

            Vector2 knockbackDir = ((Vector2)hit.transform.position - (Vector2)attackPivot.position).normalized;
            target.TakeDamage(damage, knockbackDir);
        }
    }

    private Vector2 GetHitboxCenter()
    {
        if (attackPivot == null) return transform.position;
        return (Vector2)attackPivot.position + (Vector2)attackPivot.right * hitboxDistance;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPivot == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetHitboxCenter(), hitboxRadius);

        // Tegn buen slaget går gjennom
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        float aim = attackPivot.eulerAngles.z;
        for (float a = -swingArc * 0.5f; a <= swingArc * 0.5f; a += 10f)
        {
            Vector3 d = Quaternion.Euler(0f, 0f, aim + a) * Vector3.right;
            Gizmos.DrawLine(attackPivot.position, attackPivot.position + d * hitboxDistance);
        }
    }
}