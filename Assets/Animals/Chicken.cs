using System.Collections;
using UnityEngine;

/// <summary>
/// A chicken that wanders around inside an area: it stands and looks around, pecks at the ground, walks a bit, and runs when the player comes close.
/// It does not attack and cannot be hurt. No collider is needed: it follows the ground with rays (so it walks on slopes and does not walk off edges
/// or through walls) and goes right through the player, the enemies and the other chickens.
/// Made by DeepVain > Camp > Chickens > 1 - Build chicken prefabs. Placed by CampChickens (or drop a prefab in by hand).
/// </summary>
[DisallowMultipleComponent]
public class Chicken : MonoBehaviour
{
    public enum State { Idle, Peck, Walk, Run }

    [Header("Bilder (settes av menyen)")]
    public SpriteRenderer sr;
    public Sprite[] idle, peck, walk, run;
    public float idleFps = 4.5f, peckFps = 9f, walkFps = 9f, runFps = 14f;

    [Header("Fart (units per sekund)")]
    public float walkSpeed = 0.8f;
    public float runSpeed = 2.8f;

    [Header("Hvor den holder seg")]
    [Tooltip("På: holder seg innen Roam Half Width fra Home X. Settes av CampChickens. Av: går fritt (men snur ved vegger og kanter).")]
    public bool useHome = false;
    public float homeX;
    public float roamHalfWidth = 4f;

    [Header("Redd for spilleren")]
    public float scareDistance = 2.4f;
    public float scareRunTime = 1.7f;
    [Tooltip("Hvor mye høyere/lavere enn kyllingen spilleren kan være og likevel skremme den (units).")]
    public float scareHeight = 2f;

    [Header("Bakken")]
    [Tooltip("Hva kyllingen går på. Tomt = det spilleren går på (PlayerMovement.groundLayer), ellers laget Ground.")]
    public LayerMask groundLayers;
    [Tooltip("Hvor høyt et trinn den kan gå opp (units).")]
    public float stepHeight = 0.35f;
    public float gravity = 22f;

    public State Current { get; private set; } = State.Idle;
    public int Facing { get; private set; } = 1;

    Transform player;
    float stateTime, stateLen, animT, vy;
    int peckLeft;
    bool scared;

    // ------------------------------------------------------------------------------------------------ setup
    void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (groundLayers.value == 0)
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            groundLayers = pm != null && pm.groundLayer.value != 0 ? pm.groundLayer : (LayerMask)LayerMask.GetMask("Ground");
        }
        Facing = Random.value < 0.5f ? -1 : 1;
        animT = Random.value * 3f;
    }

    void Start()
    {
        EnterState(State.Idle);
    }

    /// <summary>Sets the area it stays in (CampChickens calls this).</summary>
    public void SetHome(float x, float halfWidth)
    {
        useHome = true; homeX = x; roamHalfWidth = halfWidth;
    }

    /// <summary>Scare it from a point (other scripts can call this, for instance when something loud happens).</summary>
    public void Scare(Vector2 from)
    {
        Facing = transform.position.x >= from.x ? 1 : -1;
        scared = true;
        EnterState(State.Run, scareRunTime);
    }

    // ------------------------------------------------------------------------------------------------ states
    void EnterState(State s, float length = -1f)
    {
        Current = s; stateTime = 0f; animT = 0f;
        switch (s)
        {
            case State.Idle: stateLen = length > 0f ? length : Random.Range(0.9f, 2.6f); break;
            case State.Peck: peckLeft = Random.Range(1, 4); stateLen = float.MaxValue; break;
            case State.Walk: stateLen = length > 0f ? length : Random.Range(1f, 3.2f); if (!scared && Random.value < 0.5f) Facing = -Facing; break;
            case State.Run: stateLen = length > 0f ? length : Random.Range(0.5f, 1.1f); break;
        }
    }

    void NextState()
    {
        scared = false;
        float r = Random.value;
        if (Current == State.Idle) { if (r < 0.5f) EnterState(State.Peck); else if (r < 0.88f) EnterState(State.Walk); else EnterState(State.Run); }
        else EnterState(State.Idle);
    }

    // ------------------------------------------------------------------------------------------------ update
    void Update()
    {
        float dt = Time.deltaTime;
        WatchPlayer();
        stateTime += dt; animT += dt;
        float speed = 0f;
        if (Current == State.Walk) speed = walkSpeed;
        else if (Current == State.Run) speed = runSpeed;

        if (useHome)                                                                                    // keep inside the area
        {
            float x = transform.position.x;
            if (x > homeX + roamHalfWidth && Facing > 0) Facing = -1;
            else if (x < homeX - roamHalfWidth && Facing < 0) Facing = 1;
        }
        if (speed > 0f) Move(speed * dt);
        FollowGround(dt);

        if (Current == State.Peck)
        {
            int n = peck != null ? peck.Length : 0;
            if (n > 0 && animT * peckFps >= n) { animT = 0f; peckLeft--; if (peckLeft <= 0) EnterState(State.Idle, Random.Range(0.4f, 1.2f)); }
        }
        else if (stateTime >= stateLen) NextState();

        Show();
    }

    void WatchPlayer()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
            else return;
        }
        Vector2 d = player.position - transform.position;
        bool near = Mathf.Abs(d.x) < scareDistance && Mathf.Abs(d.y) < scareHeight;
        if (near && Current != State.Run) Scare(player.position);
        else if (near && scared) Facing = d.x > 0f ? -1 : 1;                                             // keep running away
    }

    void Move(float step)
    {
        Vector2 p = transform.position;
        float aheadX = p.x + Facing * 0.32f;
        bool wall = Physics2D.Raycast(new Vector2(p.x, p.y + stepHeight + 0.05f), Vector2.right * Facing, 0.34f, groundLayers).collider != null;
        bool drop = Physics2D.Raycast(new Vector2(aheadX, p.y + 0.5f), Vector2.down, 1.3f, groundLayers).collider == null;
        if (wall || drop)
        {
            Facing = -Facing;                                                                             // turn round (a scared chicken runs the other way, past the player)
            if (!scared) EnterState(Random.value < 0.6f ? State.Idle : State.Walk, 0.8f);
            return;
        }
        transform.position = new Vector3(p.x + Facing * step, p.y, transform.position.z);
    }

    void FollowGround(float dt)
    {
        Vector2 p = transform.position;
        var hit = Physics2D.Raycast(new Vector2(p.x, p.y + 0.6f), Vector2.down, 2.5f, groundLayers);
        if (hit.collider != null && hit.point.y <= p.y + 0.6f && hit.point.y >= p.y - 0.25f)
        {
            vy = 0f;
            float y = Mathf.Lerp(p.y, hit.point.y, 0.5f);
            if (Mathf.Abs(y - hit.point.y) < 0.01f) y = hit.point.y;
            transform.position = new Vector3(p.x, y, transform.position.z);
        }
        else
        {
            vy -= gravity * dt;                                                                           // no ground just below: fall
            float ny = p.y + vy * dt;
            if (hit.collider != null && ny < hit.point.y) { ny = hit.point.y; vy = 0f; }
            transform.position = new Vector3(p.x, ny, transform.position.z);
        }
    }

    void Show()
    {
        if (sr == null) return;
        sr.flipX = Facing < 0;
        Sprite[] set = Current == State.Peck ? peck : Current == State.Walk ? walk : Current == State.Run ? run : idle;
        float fps = Current == State.Peck ? peckFps : Current == State.Walk ? walkFps : Current == State.Run ? runFps : idleFps;
        if (set == null || set.Length == 0) return;
        int i = Mathf.FloorToInt(animT * fps);
        sr.sprite = set[Current == State.Peck ? Mathf.Clamp(i, 0, set.Length - 1) : i % set.Length];
    }

    void OnDrawGizmosSelected()
    {
        if (!useHome) return;
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.8f);
        Vector3 p = transform.position;
        Gizmos.DrawLine(new Vector3(homeX - roamHalfWidth, p.y + 0.2f, 0), new Vector3(homeX + roamHalfWidth, p.y + 0.2f, 0));
        Gizmos.DrawWireSphere(new Vector3(homeX, p.y + 0.2f, 0), 0.15f);
    }
}
