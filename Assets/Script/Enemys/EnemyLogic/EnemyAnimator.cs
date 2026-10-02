using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private EnemyAI ai;
    private static readonly int StateHash = Animator.StringToHash("State");

    void Awake()
    {
        ai = GetComponent<EnemyAI>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void LateUpdate()
    {
        // Eneste som fortsatt pollers: hvilken vei den ser
        if (ai != null && ai.enabled && spriteRenderer != null)
            spriteRenderer.flipX = ai.FacingDirection < 0;
    }

    /// <summary>Kalles av statemaskinen i det staten endres.</summary>
    public void PlayState(EnemyState state)
    {
        if (animator == null) return;
        animator.SetInteger(StateHash, (int)state);
    }
}