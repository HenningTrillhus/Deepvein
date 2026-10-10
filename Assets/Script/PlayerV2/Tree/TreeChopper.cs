using UnityEngine;
using DeepVain.Items;

/// <summary>
/// Klikk på et tre med øks i hånden = spilleren stiller seg riktig og hugger.
///
///   - Treet må ha musa over seg (TreeHover) og stå innen rekkevidde (TreeHover > Chop Range).
///   - Spilleren flyttes bare et lite stykke, til Stand Distance fra stammen, og snur seg mot treet.
///   - Er det en fiende innen Enemy Range, hugges det ikke: da er klikket et slag med sverdet.
///   - Hogging avbrytes hvis spilleren selv går/hopper, ruller, klatrer, blir stunnet, slår, eller en fiende kommer nær.
///
/// Legges på spilleren (samme objekt som PlayerMovement og PlayerAttack). PlayerAttack spør denne før den slår.
/// Treet får HP (Tree Health). Hvert treff tar AxePower. Hva som skjer når treet er hugget ned bygger vi neste steg:
/// lytt på TreeFelled (eller bruk ChoppableTree.felled).
/// </summary>
[DisallowMultipleComponent]
public class TreeChopper : MonoBehaviour
{
    enum State { Idle, Approach, Chop }

    [Header("Avstand")]
    [Tooltip("Hvor langt fra stammen spilleren står når han hugger (sidelengs, units).")]
    public float standDistance = 0.9f;
    [Tooltip("Hvor nøyaktig han må stå.")]
    public float arriveTolerance = 0.08f;
    [Tooltip("Gir opp å gå inntil treet etter så mange sekunder (hvis noe står i veien).")]
    public float approachTimeout = 1.5f;

    [Header("Fiender")]
    [Tooltip("Er det en fiende nærmere enn dette, er klikket et slag, ikke hugging.")]
    public float enemyRange = 5f;
    [Tooltip("Hvilke lag fiendene ligger på. Tomt = samme som PlayerAttack bruker.")]
    public LayerMask enemyLayer;

    [Header("Hogging")]
    [Tooltip("Treets styrke. Hvert treff tar øksas Tool Power (Starter Axe = 5), så 15 = 3 treff.")]
    public float treeHealth = 15f;
    [Tooltip("Sekunder mellom hvert treff mens han hugger. Tilpass til Chop-animasjonen.")]
    public float chopInterval = 0.7f;
    [Tooltip("Sekunder fra han begynner å hugge til første treff.")]
    public float firstHitDelay = 0.35f;

    /// <summary>Hvert treff på et tre (treet, gjenværende HP).</summary>
    public event System.Action<GameObject, float> TreeHit;
    /// <summary>Treet er hugget ned (HP tom). Her kan vi senere la det falle og slippe tre.</summary>
    public event System.Action<GameObject> TreeFelled;

    PlayerMovement movement;
    PlayerAttack attack;
    PlayerBlock block;
    PlayerHealth health;
    PlayerAnimator anim;
    PlayerEquipment equipment;
    Rigidbody2D rb;

    State state = State.Idle;
    GameObject target;
    float stateStart, nextHit;
    readonly Collider2D[] enemyBuf = new Collider2D[24];
    ContactFilter2D enemyFilter;

    public bool IsChopping { get { return state == State.Chop; } }
    public bool IsBusyWithTree { get { return state != State.Idle; } }

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        attack = GetComponent<PlayerAttack>();
        block = GetComponent<PlayerBlock>();
        health = GetComponent<PlayerHealth>();
        anim = GetComponent<PlayerAnimator>();
        rb = GetComponent<Rigidbody2D>();
        equipment = GetComponentInChildren<PlayerEquipment>();
        if (equipment == null) equipment = FindFirstObjectByType<PlayerEquipment>();
    }

    void OnEnable() { if (movement != null) movement.PlayerInput += OnPlayerInput; }
    void OnDisable() { if (movement != null) movement.PlayerInput -= OnPlayerInput; Cancel(); }

    void OnPlayerInput() { if (state != State.Idle) Cancel(); }

    /// <summary>PlayerAttack kaller denne når musetasten trykkes. true = klikket er brukt til hugging (ikke slå).</summary>
    public bool OnAttackClick()
    {
        // en fiende er nær: sverdet vinner alltid, og en pågående hogging stoppes
        if (EnemyNear())
        {
            if (state != State.Idle) Cancel();
            return false;
        }

        var tree = TreeHover.Hovered;

        if (state != State.Idle)
        {
            if (tree != null && tree == target) return true;      // trykker på samme tre igjen: fortsett
            Cancel();
            return false;                                          // trykket et annet sted: vanlig slag
        }

        if (tree == null || !TreeHover.HoveredInRange) return false;
        if (equipment == null || !equipment.HasAxe) return false;
        var ct = tree.GetComponent<ChoppableTree>();
        if (ct != null && ct.felled) return false;
        if (Busy()) return false;

        Begin(tree);
        return true;
    }

    bool Busy()
    {
        if (movement != null && (movement.IsClimbing || movement.IsLedgeClimbing || movement.IsRolling || movement.IsCrouching || !movement.IsGrounded)) return true;
        if (attack != null && attack.IsAttacking) return true;
        if (block != null && block.IsBlocking) return true;
        if (health != null && (health.IsStunned || health.IsDead)) return true;
        return false;
    }

    bool EnemyNear()
    {
        LayerMask mask = enemyLayer.value != 0 ? enemyLayer : (attack != null ? attack.EnemyLayer : (LayerMask)0);
        if (mask.value == 0) return false;
        enemyFilter.useLayerMask = true; enemyFilter.layerMask = mask; enemyFilter.useTriggers = true;
        int n = Physics2D.OverlapCircle(transform.position, enemyRange, enemyFilter, enemyBuf);
        for (int i = 0; i < n; i++)
        {
            var c = enemyBuf[i];
            if (c != null && c.GetComponentInParent<IDamageable>() != null && c.transform.root != transform.root) return true;
        }
        return false;
    }

    void Begin(GameObject tree)
    {
        target = tree;
        stateStart = Time.time;
        float dx = tree.transform.position.x - transform.position.x;
        int dir = dx < 0f ? -1 : 1;
        movement.SetForcedFacing(dir);
        if (Mathf.Abs(dx) > standDistance + arriveTolerance)
        {
            state = State.Approach;
            movement.SetForcedMove(dir);
        }
        else StartChop();
    }

    void StartChop()
    {
        state = State.Chop;
        stateStart = Time.time;
        nextHit = Time.time + firstHitDelay;
        movement.SetForcedMove(0f);
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (anim != null) anim.SetChopping(true);
    }

    /// <summary>Stopp hugging / gåing inntil treet.</summary>
    public void Cancel()
    {
        if (movement != null) { movement.SetForcedMove(0f); movement.SetForcedFacing(0); }
        if (anim != null) anim.SetChopping(false);
        state = State.Idle;
        target = null;
    }

    void Update()
    {
        if (state == State.Idle) return;

        if (target == null || !target.activeInHierarchy) { Cancel(); return; }
        var ct = target.GetComponent<ChoppableTree>();
        if (ct != null && ct.felled) { Cancel(); return; }
        if (equipment == null || !equipment.HasAxe) { Cancel(); return; }
        if (movement.IsClimbing || movement.IsLedgeClimbing || movement.IsRolling || (attack != null && attack.IsAttacking)
            || (block != null && block.IsBlocking) || (health != null && (health.IsStunned || health.IsDead))) { Cancel(); return; }
        if (EnemyNear()) { Cancel(); return; }

        if (state == State.Approach)
        {
            float dx = target.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) <= standDistance + arriveTolerance) StartChop();
            else if (Time.time - stateStart > approachTimeout) Cancel();
            else
            {
                int dir = dx < 0f ? -1 : 1;
                movement.SetForcedMove(dir);
                movement.SetForcedFacing(dir);
            }
            return;
        }

        // Chop
        if (Time.time >= nextHit)
        {
            nextHit += chopInterval;
            Hit(target);
        }
    }

    void Hit(GameObject tree)
    {
        var ct = tree.GetComponent<ChoppableTree>();
        if (ct == null) { ct = tree.AddComponent<ChoppableTree>(); ct.hp = treeHealth; }
        ct.hp -= Mathf.Max(1, equipment != null ? equipment.AxePower : 1);
        if (TreeHit != null) TreeHit(tree, Mathf.Max(0f, ct.hp));
        if (ct.hp <= 0f)
        {
            ct.felled = true;
            if (TreeFelled != null) TreeFelled(tree);
            Cancel();
        }
    }
}

/// <summary>Settes på et tre første gang det hugges: hvor mye som er igjen, og om det er hugget ned.</summary>
public class ChoppableTree : MonoBehaviour
{
    public float hp;
    public bool felled;
}
