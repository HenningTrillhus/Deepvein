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
    [Tooltip("Et klikk rett FØR neste slag er klart huskes så lenge (sekunder) og slår så snart det er klart. Gjør slagene lettere å time.")]
    [SerializeField] private float inputBuffer = 0.18f;
    [Tooltip("Kurve for svingen. Bratt start = rask pisk.")]
    [SerializeField] private AnimationCurve swingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Sveipe-effekt")]
    [Tooltip("En lys sveipe bak sverdet mens du svinger.")]
    [SerializeField] private bool slashEffect = true;
    [Tooltip("Avstand fra spilleren til sveipen (andel av hitbox-avstanden).")]
    [SerializeField] private float slashRadiusScale = 1.15f;

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
    private TreeChopper chopper;
    private RockMiner rockMiner;
    private bool isAttacking;
    private Coroutine swingRoutine;
    private PlayerHealth health;
    private float bufferedUntil;
    private AirSlam airSlam;
    private AirSlam Slam { get { if (airSlam == null) { airSlam = GetComponent<AirSlam>(); if (airSlam == null) airSlam = gameObject.AddComponent<AirSlam>(); } return airSlam; } }
    private float lastAttackTime = -999f;
    private float currentAngle;

    private readonly HashSet<Collider2D> alreadyHit = new HashSet<Collider2D>();

    /// <summary>True mens sverdet svinger (brukes av PlayerMovement for å snu spilleren mot musa).</summary>
    public bool IsAttacking => isAttacking;

    /// <summary>Kommer for hvert treff på en fiende (LevelXp gir Combat-XP).</summary>
    public event System.Action<GameObject> EnemyHit;

    void Awake()
    {
        cam = Camera.main;
        anim = GetComponent<PlayerAnimator>();
        movement = GetComponent<PlayerMovement>();
        block = GetComponent<PlayerBlock>();
        health = GetComponent<PlayerHealth>();
        airSlam = GetComponent<AirSlam>();
        chopper = GetComponent<TreeChopper>();
        rockMiner = GetComponent<RockMiner>();

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

        // a click that came just before the swing was ready is fired as soon as it is
        if (bufferedUntil > 0f)
        {
            if (Time.time > bufferedUntil || (health != null && (health.IsDead || health.IsStunned))) bufferedUntil = 0f;
            else if (!isAttacking && Time.time >= lastAttackTime + attackCooldown)
            {
                bufferedUntil = 0f;
                if (CanSwingNow()) StartSwing();
            }
        }
    }

    /// <summary>The damage of a normal swing (the air slam multiplies it).</summary>
    public int Damage { get { return damage; } }

    /// <summary>Shows or hides the sword from outside (the air slam holds it up in its own animation).</summary>
    public void ForceSword(bool on)
    {
        if (hideRoutine != null) { StopCoroutine(hideRoutine); hideRoutine = null; }
        ShowSword(on);
    }

    /// <summary>Lets LevelXp etc. know that an enemy was hit by something other than the normal swing.</summary>
    public void NotifyEnemyHit(GameObject enemy) { if (EnemyHit != null) EnemyHit(enemy); }

    /// <summary>Hvilke lag fiendene ligger på (TreeChopper bruker den til å se om det er fiender i nærheten).</summary>
    public LayerMask EnemyLayer => enemyLayer;

    private void TryAttack()
    {
        // klikk på et tre med øks i hånden = hugge, ikke slå (TreeChopper bestemmer, og slår aldri til når en fiende er nær)
        if (chopper != null && chopper.OnAttackClick()) return;
        // klikk på en stein med hakke i hånden = hakke (RockMiner, samme regler: fiende nær = slå)
        if (rockMiner != null && rockMiner.OnAttackClick()) return;
        if (health != null && (health.IsStunned || health.IsDead)) return;      // stunned or dead: no swing
        if (Slam.IsActive) return;                        // the air slam is going: no other swing
        // an attack high in the air = the air slam (the sword goes up and he comes down hard)
        if (!isAttacking && Time.time >= lastAttackTime + attackCooldown && Slam.TryStart()) return;
        if (isAttacking || Time.time < lastAttackTime + attackCooldown)
        {
            bufferedUntil = Time.time + inputBuffer;                            // not ready yet: remember the click
            return;
        }
        if (!CanSwingNow()) return;
        StartSwing();
    }

    private bool CanSwingNow()
    {
        if (health != null && (health.IsStunned || health.IsDead)) return false;
        if (block != null && block.IsBlocking) return false;   // ikke slå mens skjoldet er oppe
        if (movement != null && (movement.IsCrouching || movement.IsRolling)) return false;

        // Mens du spurter kan du slå fremover, men ikke bakover - da må du stoppe opp først
        if (movement != null && movement.IsSprinting)
        {
            bool aimRight = Mathf.Cos(GetAimAngle() * Mathf.Deg2Rad) > 0f;
            bool moveRight = movement.HorizontalInput > 0f;
            if (aimRight != moveRight) return false;
        }
        return true;
    }

    private void StartSwing()
    {
        swingRoutine = StartCoroutine(SwingRoutine());
    }

    /// <summary>The swing is knocked out of the player's hand (hit, stun): no more hits from it, the slash effect goes away.</summary>
    public void CancelSwing()
    {
        if (!isAttacking) return;
        if (swingRoutine != null) { StopCoroutine(swingRoutine); swingRoutine = null; }
        isAttacking = false;
        bufferedUntil = 0f;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c.name == "FxFlip") Destroy(c.gameObject);
        }
        if (hideRoutine != null) { StopCoroutine(hideRoutine); hideRoutine = null; }
        ShowSword(false);
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

        if (slashEffect && attackPivot != null)
        {
            int order = swordRenderer != null ? swordRenderer.sortingOrder - 1 : CombatFx.SortingOrder - 10;
            CombatFx.Slash(transform, (Vector2)(attackPivot.position - transform.position), start, end, swingDuration,
                           hitboxDistance * slashRadiusScale, swingCurve, CombatFx.PlayerSlash, order);
        }

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
        swingRoutine = null;
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

            // a dead enemy is not hit again (and gives no XP)
            var enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null && enemy.IsDead) continue;

            // sideways from the player (a little up), not from the pivot to the enemy's feet: the push is steady and the shield check is true
            float dx = hit.bounds.center.x - transform.position.x;
            float side = Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : (Mathf.Cos(attackPivot.eulerAngles.z * Mathf.Deg2Rad) >= 0f ? 1f : -1f);
            Vector2 knockbackDir = new Vector2(side, 0.25f).normalized;
            CombatTrace.Hit(target, gameObject, damage, knockbackDir);
            if (EnemyHit != null) EnemyHit(hit.gameObject);
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