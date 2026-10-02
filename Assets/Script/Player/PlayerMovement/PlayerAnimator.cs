using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private PlayerMovement movement;
    private Rigidbody2D rb;

    // Hash-verdier er raskere enn strenger, og fanger skrivefeil ved oppstart
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (animator == null) return;

        float speed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat(SpeedHash, speed);
    }
}