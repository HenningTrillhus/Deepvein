using UnityEngine;

public class EnemyBlock : MonoBehaviour
{
    [Header("Referanser")]
    [Tooltip("Skjold-sprite som vises mens fienden blokkerer.")]
    [SerializeField] private GameObject shieldVisual;

    [Header("Oppførsel")]
    [Tooltip("Sjanse for å blokkere når spilleren slår. 0.4 = 40%.")]
    [Range(0f, 1f)]
    [SerializeField] private float blockChance = 0.4f;
    [Tooltip("Hvor nær spilleren må være før fienden vurderer å blokkere.")]
    [SerializeField] private float blockRange = 2f;
    [Tooltip("Hvor lenge skjoldet holdes oppe om gangen.")]
    [SerializeField] private float blockDuration = 1.2f;
    [Tooltip("Hvile før den kan blokkere igjen.")]
    [SerializeField] private float blockCooldown = 2f;
    [Tooltip("Hvor ofte den vurderer å begynne å blokkere.")]
    [SerializeField] private float decisionInterval = 0.5f;

    [Header("Effekt")]
    [Tooltip("Skade som slipper gjennom. 0 = full blokk.")]
    [Range(0f, 1f)]
    [SerializeField] private float damageThrough = 0f;
    [Tooltip("Hvor hardt spilleren dyttes tilbake ved blokk.")]
    [SerializeField] private float parryKnockback = 5f;
    [Tooltip("Blokk-vinkelen foran fienden.")]
    [Range(30f, 360f)]
    [SerializeField] private float blockAngle = 140f;

    private EnemyAI ai;
    private EnemyAttack attack;
    private Transform player;
    private EnemyAnimator enemyAnimator;
    private EnemyStateMachine states;

    private bool isBlocking;
    private float blockEndTime;
    private float cooldownUntil;
    private float nextDecision;

    public bool IsBlocking => isBlocking;
    public float DamageMultiplier => damageThrough;
    public float ParryKnockback => parryKnockback;

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        attack = GetComponent<EnemyAttack>();
        enemyAnimator = GetComponent<EnemyAnimator>();
        states = GetComponent<EnemyStateMachine>();

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null) player = found.transform;

        if (shieldVisual != null)
            shieldVisual.SetActive(false);
    }

    void Update()
    {
        if (isBlocking)
        {
            if (Time.time >= blockEndTime)
                StopBlocking();

            return;
        }

        TryStartBlocking();
    }

    private void TryStartBlocking()
    {
        if (states != null && !states.CanAct) return;
        if (player == null) return;
        if (Time.time < cooldownUntil) return;
        if (Time.time < nextDecision) return;

        nextDecision = Time.time + decisionInterval;

        // Blokkerer ikke midt i sitt eget angrep
        if (attack != null && attack.IsAttacking) return;

        if (Vector2.Distance(transform.position, player.position) > blockRange) return;

        if (Random.value <= blockChance)
            StartBlocking();
    }

    private void StartBlocking()
    {
        isBlocking = true;
        blockEndTime = Time.time + blockDuration;
        if (states != null) states.EnterBlock();
    }

    private void StopBlocking()
    {
        isBlocking = false;
        cooldownUntil = Time.time + blockCooldown;
        if (states != null) states.ExitBlock();
    }

    /// <summary>Kan vi blokkere et slag som kommer fra denne retningen?</summary>
    public bool CanBlockFrom(Vector2 attackOrigin)
    {
        if (!isBlocking) return false;

        Vector2 toAttacker = (attackOrigin - (Vector2)transform.position).normalized;
        Vector2 facing = Vector2.right * (ai != null ? ai.FacingDirection : 1);

        return Vector2.Angle(facing, toAttacker) <= blockAngle * 0.5f;
    }

    /// <summary>Kalles når et slag faktisk ble blokkert.</summary>
    public void RegisterBlockedHit()
    {
        blockEndTime = Mathf.Min(blockEndTime, Time.time + 0.2f);
        if (states != null) states.EnterParry();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, blockRange);
    }
}