using UnityEngine;

public class FireballScript : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 8f;
    public float bounceForce = 5f;

    [Header("Damage")]
    public float damage = 7f;

    [Header("Lifetime")]
    public float lifetime = 2.3f;

    [Header("Reflection")]
    public float reflectionSpeedMultiplier = 1.5f;
    public float reflectionDamageMultiplier = 1.5f;

    [Header("Collision")]
    public float reflectionCooldown = 0.05f;

    private Rigidbody2D rb;

    private PlayerController owner;

    private bool movingRight;

    private float lastReflectionTime = -10f;

    private bool initialized = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogError(
                $"{name}: FireballScript requires a Rigidbody2D."
            );

            enabled = false;
            return;
        }

        rb.gravityScale = 1f;

        Destroy(gameObject, lifetime);
    }

    public void Initialize(
        bool isFacingRight,
        PlayerController playerOwner)
    {
        owner = playerOwner;
        movingRight = isFacingRight;

        initialized = true;

        float horizontalVelocity =
            movingRight
                ? speed
                : -speed;

        // The fireball starts slightly downward,
        // allowing it to bounce off the ground.
        rb.linearVelocity =
            new Vector2(
                horizontalVelocity,
                -bounceForce
            );
    }

    private void FixedUpdate()
    {
        if (!initialized)
            return;

        // Keep horizontal speed consistent after bouncing
        // or reflection while allowing vertical physics.
        float desiredHorizontalSpeed =
            movingRight
                ? speed
                : -speed;

        rb.linearVelocity = new Vector2(
            desiredHorizontalSpeed,
            rb.linearVelocity.y
        );
    }

    private void OnCollisionEnter2D(
        Collision2D collision)
    {
        if (!initialized)
            return;

        // --------------------------------------------------------
        // GROUND BOUNCE
        // --------------------------------------------------------

        if (collision.gameObject.CompareTag("Ground"))
        {
            float horizontalVelocity =
                movingRight
                    ? speed
                    : -speed;

            rb.linearVelocity =
                new Vector2(
                    horizontalVelocity,
                    bounceForce
                );
        }
    }

    private void OnTriggerEnter2D(
        Collider2D collision)
    {
        if (!initialized)
            return;

        PlayerController hitPlayer =
            collision.GetComponent<PlayerController>();

        if (hitPlayer == null)
            return;

        // Never hit the current owner.
        if (hitPlayer == owner)
            return;

        // --------------------------------------------------------
        // REFLECTION
        // --------------------------------------------------------

        if (hitPlayer.isReflecting)
        {
            // Prevent the same fireball from being
            // reflected multiple times in the same instant.
            if (Time.time <
                lastReflectionTime +
                reflectionCooldown)
            {
                return;
            }

            lastReflectionTime = Time.time;

            Debug.Log(
                "PROJECTILE REFLECTED!"
            );

            // The reflector becomes the new owner.
            owner = hitPlayer;

            // Reverse direction.
            movingRight = !movingRight;

            // Increase speed and damage.
            speed *= reflectionSpeedMultiplier;
            damage *= reflectionDamageMultiplier;

            float horizontalVelocity =
                movingRight
                    ? speed
                    : -speed;

            float verticalVelocity =
                rb.linearVelocity.y;

            rb.linearVelocity =
                new Vector2(
                    horizontalVelocity,
                    verticalVelocity
                );

            return;
        }

        // --------------------------------------------------------
        // NORMAL HIT
        // --------------------------------------------------------

        Vector2 knockbackDirection =
            movingRight
                ? new Vector2(1f, 0.2f)
                : new Vector2(-1f, 0.2f);

        knockbackDirection.Normalize();

        hitPlayer.TakeHit(
            damage,
            knockbackDirection
        );

        Destroy(gameObject);
    }
}
