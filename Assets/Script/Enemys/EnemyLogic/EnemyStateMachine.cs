using System.Collections;
using UnityEngine;

public class EnemyStateMachine : MonoBehaviour
{
    [Header("Låsetider")]
    [Tooltip("Hvor lenge hurt-reaksjonen varer. Match klippets lengde.")]
    [SerializeField] private float hurtLockTime = 0.18f;
    [Tooltip("Hvor lenge parry-reaksjonen varer. Match klippets lengde.")]
    [SerializeField] private float parryLockTime = 0.15f;

    [Header("Stun")]
    [Tooltip("Lengden på stun-start-klippet.")]
    [SerializeField] private float stunStartTime = 0.25f;
    [Tooltip("Hvor lenge loopen varer før den går ut av stun.")]
    [SerializeField] private float stunLoopTime = 1f;
    [Tooltip("Lengden på stun-slutt-klippet.")]
    [SerializeField] private float stunEndTime = 0.3f;

    private EnemyAnimator animatorControl;
    private Rigidbody2D rb;

    private EnemyState state = EnemyState.Idle;
    private float lockUntil;
    private Coroutine stunRoutine;

    public EnemyState Current => state;
    public bool IsDead => state == EnemyState.Dead || state == EnemyState.Fade;

    public bool IsStunned => state == EnemyState.StunStart
                          || state == EnemyState.StunLoop
                          || state == EnemyState.StunEnd;

    public bool CanAct => Time.time >= lockUntil && !IsDead && !IsStunned;
    public bool CanMove => CanAct && state != EnemyState.Block;

    void Awake()
    {
        animatorControl = GetComponent<EnemyAnimator>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (IsDead || IsStunned) return;
        if (Time.time < lockUntil) return;

        if (state == EnemyState.Hurt || state == EnemyState.Parry || state == EnemyState.Attack)
        {
            Enter(IsMoving() ? EnemyState.Walk : EnemyState.Idle);
            return;
        }

        if (state == EnemyState.Idle && IsMoving())
            Enter(EnemyState.Walk);
        else if (state == EnemyState.Walk && !IsMoving())
            Enter(EnemyState.Idle);
    }

    private bool IsMoving() => rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f;

    // ---------- Inngangene andre scripts bruker ----------

    public void EnterAttack(float duration)  => Enter(EnemyState.Attack, duration);
    public void EnterBlock()                 => Enter(EnemyState.Block);
    public void ExitBlock()                  { if (state == EnemyState.Block) Enter(EnemyState.Idle); }
    public void EnterParry()                 => Enter(EnemyState.Parry, parryLockTime);
    public void EnterDead()
    {
        StopStun();
        Enter(EnemyState.Dead, float.MaxValue);
    }

    public void EnterHurt()
    {
        // Et treff bryter stunet
        StopStun();
        Enter(EnemyState.Hurt, hurtLockTime);
    }

    /// <summary>Spilleren parerte. Kjører start -> loop -> slutt.</summary>
    public void EnterStun()
    {
        if (IsDead) return;

        StopStun();
        stunRoutine = StartCoroutine(StunRoutine());
    }

    private IEnumerator StunRoutine()
    {
        SetState(EnemyState.StunStart);
        yield return new WaitForSeconds(stunStartTime);

        SetState(EnemyState.StunLoop);
        yield return new WaitForSeconds(stunLoopTime);

        SetState(EnemyState.StunEnd);
        yield return new WaitForSeconds(stunEndTime);

        stunRoutine = null;
        SetState(EnemyState.Idle);
    }

    private void StopStun()
    {
        if (stunRoutine == null) return;
        StopCoroutine(stunRoutine);
        stunRoutine = null;
    }

    public void EnterFade()
    {
        StopStun();
        SetState(EnemyState.Fade);
        lockUntil = float.MaxValue;
    }

    // ---------- Internt ----------

    private void Enter(EnemyState newState, float lockTime = 0f)
    {
        if (IsDead) return;

        bool isReaction = newState == EnemyState.Hurt
                       || newState == EnemyState.Parry
                       || newState == EnemyState.Dead;

        // Under stun slipper bare reaksjoner gjennom
        if (IsStunned && !isReaction) return;

        if (!isReaction && Time.time < lockUntil) return;
        if (newState == state && lockTime <= 0f) return;

        lockUntil = Time.time + lockTime;
        SetState(newState);
    }

    private void SetState(EnemyState newState)
    {
        // Ingenting overstyrer død eller fade
        if (IsDead && newState != EnemyState.Dead && newState != EnemyState.Fade)
            return;

        state = newState;

        if (animatorControl != null)
            animatorControl.PlayState(newState);
    }
}