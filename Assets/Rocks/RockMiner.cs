using UnityEngine;
using DeepVain.Items;
using DeepVain.Levels;

/// <summary>
/// Klikk på en stein med hakke i hånden = spilleren stiller seg riktig og hakker (Mine-animasjonen). Fungerer akkurat som TreeChopper:
///   - Steinen må ha musa over seg og stå innen rekkevidde (TreeHover > Mine Range). Utenfor rekkevidde skjer ingenting, og det er ingen kontur.
///   - Spilleren flyttes bare et lite stykke, til Stand Distance fra steinen, og snur seg mot den.
///   - Er det en fiende innen Enemy Range, hakkes det ikke: da er klikket et slag med sverdet.
///   - Hakkingen avbrytes hvis spilleren selv går/hopper, ruller, klatrer, blir stunnet, slår, plukker, eller en fiende kommer nær.
/// Hvert slag tar hakkas Tool Power fra steinens Health (Mineable). Når den er tom knuses den (RockFx) og gir stein/kull (RockLoot).
/// Legges på spilleren (samme objekt som TreeChopper). PlayerAttack spør denne etter TreeChopper.
/// </summary>
[DisallowMultipleComponent]
public class RockMiner : MonoBehaviour
{
    enum State { Idle, Approach, Mine }

    [Header("Avstand")]
    [Tooltip("Hvor langt fra steinen spilleren står når han hakker (sidelengs, units).")]
    public float standDistance = 0.9f;
    public float arriveTolerance = 0.08f;
    public float approachTimeout = 1.5f;

    [Header("Fiender")]
    [Tooltip("Er det en fiende nærmere enn dette, er klikket et slag, ikke hakking.")]
    public float enemyRange = 5f;
    [Tooltip("Tomt = samme som PlayerAttack bruker.")]
    public LayerMask enemyLayer;

    [Header("Hakking")]
    [Tooltip("Sekunder mellom hvert slag. Mine-animasjonen er 0.53 s.")]
    public float mineInterval = 0.6f;
    [Tooltip("Sekunder fra han begynner til første slag.")]
    public float firstHitDelay = 0.3f;

    /// <summary>Hvert slag på en stein (steinen, gjenværende Health).</summary>
    public event System.Action<GameObject, float> RockHit;
    /// <summary>Steinen er knust (Health tom).</summary>
    public event System.Action<GameObject> RockBroken;

    PlayerMovement movement;
    PlayerAttack attack;
    PlayerBlock block;
    PlayerHealth health;
    PlayerAnimator anim;
    PlayerEquipment equipment;
    ForagePicker picker;
    Rigidbody2D rb;

    State state = State.Idle;
    GameObject target;
    float stateStart, nextHit;
    readonly Collider2D[] enemyBuf = new Collider2D[24];
    ContactFilter2D enemyFilter;

    public bool IsMining { get { return state == State.Mine; } }
    public bool IsBusyWithRock { get { return state != State.Idle; } }

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        attack = GetComponent<PlayerAttack>();
        block = GetComponent<PlayerBlock>();
        health = GetComponent<PlayerHealth>();
        anim = GetComponent<PlayerAnimator>();
        picker = GetComponent<ForagePicker>();
        rb = GetComponent<Rigidbody2D>();
        equipment = GetComponentInChildren<PlayerEquipment>();
        if (equipment == null) equipment = FindFirstObjectByType<PlayerEquipment>();
    }

    void OnEnable() { if (movement != null) movement.PlayerInput += OnPlayerInput; }
    void OnDisable() { if (movement != null) movement.PlayerInput -= OnPlayerInput; Cancel(); }

    void OnPlayerInput() { if (state != State.Idle) Cancel(); }

    /// <summary>PlayerAttack kaller denne når musetasten trykkes. true = klikket er brukt til hakking (ikke slå).</summary>
    public bool OnAttackClick()
    {
        if (EnemyNear())
        {
            if (state != State.Idle) Cancel();
            return false;
        }

        var rock = TreeHover.HoveredRock;

        if (state != State.Idle)
        {
            if (rock != null && rock == target) return true;
            Cancel();
            return false;
        }

        if (rock == null || !TreeHover.HoveredRockInRange) return false;
        if (equipment == null || !equipment.HasPickaxe) return false;
        var m = rock.GetComponent<Mineable>();
        if (m == null || m.broken) return false;
        if (Busy()) return false;

        Begin(rock);
        return true;
    }

    bool Busy()
    {
        if (movement != null && (movement.IsClimbing || movement.IsLedgeClimbing || movement.IsRolling || movement.IsCrouching || !movement.IsGrounded)) return true;
        if (attack != null && attack.IsAttacking) return true;
        if (block != null && block.IsBlocking) return true;
        if (health != null && (health.IsStunned || health.IsDead)) return true;
        if (picker != null && picker.IsPicking) return true;
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

    void Begin(GameObject rock)
    {
        target = rock;
        stateStart = Time.time;
        float dx = rock.transform.position.x - transform.position.x;
        int dir = dx < 0f ? -1 : 1;
        movement.SetForcedFacing(dir);
        if (Mathf.Abs(dx) > standDistance + arriveTolerance)
        {
            state = State.Approach;
            movement.SetForcedMove(dir);
        }
        else StartMining();
    }

    void StartMining()
    {
        state = State.Mine;
        stateStart = Time.time;
        nextHit = Time.time + firstHitDelay;
        movement.SetForcedMove(0f);
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (anim != null) anim.SetMining(true);
    }

    /// <summary>Stopp hakking / gåing inntil steinen.</summary>
    public void Cancel()
    {
        bool was = state != State.Idle;
        if (was && movement != null) { movement.SetForcedMove(0f); movement.SetForcedFacing(0); }
        if (anim != null) anim.SetMining(false);
        state = State.Idle;
        target = null;
    }

    void Update()
    {
        if (state == State.Idle) return;

        if (target == null || !target.activeInHierarchy) { Cancel(); return; }
        var m = target.GetComponent<Mineable>();
        if (m != null && m.broken) { Cancel(); return; }
        if (equipment == null || !equipment.HasPickaxe) { Cancel(); return; }
        if (movement.IsClimbing || movement.IsLedgeClimbing || movement.IsRolling || (attack != null && attack.IsAttacking)
            || (block != null && block.IsBlocking) || (health != null && (health.IsStunned || health.IsDead))
            || (picker != null && picker.IsPicking)) { Cancel(); return; }
        if (EnemyNear()) { Cancel(); return; }

        if (state == State.Approach)
        {
            float dx = target.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) <= standDistance + arriveTolerance) StartMining();
            else if (Time.time - stateStart > approachTimeout) Cancel();
            else
            {
                int dir = dx < 0f ? -1 : 1;
                movement.SetForcedMove(dir);
                movement.SetForcedFacing(dir);
            }
            return;
        }

        if (Time.time >= nextHit)
        {
            nextHit += mineInterval * Mathf.Clamp(1f - PlayerLevels.Bonus("mine_speed"), 0.4f, 1f);       // skill tree: Swift Swing ...
            Hit(target);
        }
    }

    void Hit(GameObject rock)
    {
        var m = rock.GetComponent<Mineable>();
        if (m == null) return;
        m.health -= Mathf.Max(1, equipment != null ? equipment.PickaxePower : 1);
        if (RockHit != null) RockHit(rock, Mathf.Max(0f, m.health));
        if (m.health <= 0f)
        {
            m.broken = true;
            if (RockBroken != null) RockBroken(rock);
            Cancel();
        }
    }
}
