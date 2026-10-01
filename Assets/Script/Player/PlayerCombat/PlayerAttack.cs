using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("Referanser")]
    [Tooltip("Tomt barn av spiller-ROTA. Må ikke ligge under noe som speilvendes.")]
    [SerializeField] private Transform attackPivot;
    [Tooltip("Sverd-objektet, barn av pivoten.")]
    [SerializeField] private Transform sword;
    [SerializeField] private SpriteRenderer swordRenderer;

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
    private bool isAttacking;
    private float lastAttackTime = -999f;
    private float currentAngle;

    private readonly HashSet<Collider2D> alreadyHit = new HashSet<Collider2D>();

    void Awake()
    {
        cam = Camera.main;

        if (swordRenderer == null && sword != null)
            swordRenderer = sword.GetComponent<SpriteRenderer>();

        controls = new PlayerControls();
        controls.Player.Attack.performed += ctx => TryAttack();

        if (sword != null)
            sword.gameObject.SetActive(false);
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

        StartCoroutine(SwingRoutine());
    }

    private IEnumerator SwingRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        alreadyHit.Clear();

        // Siktevinkelen låses når slaget starter
        float aim = GetAimAngle();
        float start = aim + swingArc * 0.5f;    // over siktelinjen
        float end = aim - swingArc * 0.5f;      // under siktelinjen

        if (sword != null)
            sword.gameObject.SetActive(true);

        float t = 0f;
        while (t < swingDuration)
        {
            t += Time.deltaTime;
            float p = swingCurve.Evaluate(Mathf.Clamp01(t / swingDuration));

            currentAngle = Mathf.Lerp(start, end, p);
            ApplyAngle(currentAngle);

            yield return null;
        }

        if (sword != null)
            sword.gameObject.SetActive(false);

        isAttacking = false;
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

        if (swordRenderer != null)
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