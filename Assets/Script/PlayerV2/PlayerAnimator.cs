using UnityEngine;

/// <summary>
/// Small wrapper around the Animator of the layered player. Other scripts (movement, health, combat)
/// call these methods instead of touching Animator parameters by name.
/// Put it on the same object as PlayerMovement; it finds the Animator on the visual child.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;

    static readonly int pSpeed = Animator.StringToHash("Speed");
    static readonly int pVelY = Animator.StringToHash("VelY");
    static readonly int pGrounded = Animator.StringToHash("Grounded");
    static readonly int pClimbing = Animator.StringToHash("Climbing");
    static readonly int pClimbSpeed = Animator.StringToHash("ClimbSpeed");
    static readonly int pDead = Animator.StringToHash("Dead");
    static readonly int pBlocking = Animator.StringToHash("Blocking");
    static readonly int pStunned = Animator.StringToHash("Stunned");
    static readonly int pChopping = Animator.StringToHash("Chopping");
    static readonly int pMining = Animator.StringToHash("Mining");
    static readonly int pJump = Animator.StringToHash("Jump");
    static readonly int pAttack = Animator.StringToHash("Attack");
    static readonly int pHit = Animator.StringToHash("Hit");
    static readonly int pStun = Animator.StringToHash("Stun");
    static readonly int pShieldHit = Animator.StringToHash("ShieldHit");
    static readonly int pPickUp = Animator.StringToHash("PickUp");
    static readonly int pLedgeClimbing = Animator.StringToHash("LedgeHang");
    static readonly int pLedgeClimb = Animator.StringToHash("LedgeClimb");

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    /// <summary>Called every frame by PlayerMovement.</summary>
    public void SetMotion(float speed, float velY, bool grounded, bool climbing, float climbInput)
    {
        if (animator == null) return;
        animator.SetFloat(pSpeed, speed);
        animator.SetFloat(pVelY, velY);
        animator.SetBool(pGrounded, grounded);
        animator.SetBool(pClimbing, climbing);
        animator.SetFloat(pClimbSpeed, Mathf.Abs(climbInput));   // 0 = hold still on the ladder
    }

    public void Jump()      { if (animator != null) animator.SetTrigger(pJump); }
    public void Attack()    { if (animator != null) animator.SetTrigger(pAttack); }
    public void Hit()       { if (animator != null) animator.SetTrigger(pHit); }
    public void ShieldHit() { if (animator != null) animator.SetTrigger(pShieldHit); }
    public void PickUp()    { if (animator != null) animator.SetTrigger(pPickUp); }

    public void SetBlocking(bool on) { if (animator != null) animator.SetBool(pBlocking, on); }
    public void SetChopping(bool on) { if (animator != null) animator.SetBool(pChopping, on); }
    public void SetMining(bool on)   { if (animator != null) animator.SetBool(pMining, on); }
    public void LedgeClimb(bool on)
    {
        if (animator == null) return;
        animator.SetBool(pLedgeClimbing, on);
        if (on) animator.SetTrigger(pLedgeClimb);
    }

    /// <summary>Start the stun (intro + loop). Call EndStun() when it is over.</summary>
    public void StartStun()
    {
        if (animator == null) return;
        animator.SetBool(pStunned, true);
        animator.SetTrigger(pStun);
    }
    public void EndStun() { if (animator != null) animator.SetBool(pStunned, false); }

    /// <summary>Death animation followed by the fade. The player ends on an empty frame.</summary>
    public void Die() { if (animator != null) animator.SetBool(pDead, true); }
    public void Revive() { if (animator != null) animator.SetBool(pDead, false); }
}
