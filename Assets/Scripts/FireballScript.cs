using UnityEngine;

public class FireballScript : MonoBehaviour
{
    public float speed = 8f;
    public float bounceForce = 5f;
    public float damage = 7f;
    
    private Rigidbody2D rb;
    private PlayerController owner; // Tracks who threw it
    private bool movingRight;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // 139 frames at 60fps is roughly 2.3 seconds. Destroy automatically.
        Destroy(gameObject, 2.3f); 
    }

    public void Initialize(bool isFacingRight, PlayerController playerOwner)
    {
        movingRight = isFacingRight;
        owner = playerOwner;
        
        // Give it initial forward and downward momentum to start the bounce
        float xVel = movingRight ? speed : -speed;
        rb.linearVelocity = new Vector2(xVel, -bounceForce);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        // --- BOUNCE LOGIC ---
        if (col.gameObject.CompareTag("Ground"))
        {
            // Maintain forward speed, bump it upwards slightly
            float xVel = movingRight ? speed : -speed;
            rb.linearVelocity = new Vector2(xVel, bounceForce);
            return;
        }
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        PlayerController hitPlayer = col.GetComponent<PlayerController>();

        if (hitPlayer != null && hitPlayer != owner)
        {
            // --- REFLECT LOGIC ---
            if (hitPlayer.isReflecting)
            {
                Debug.Log("PROJECTILE REFLECTED!");
                owner = hitPlayer; // Blue now owns the fireball!
                movingRight = !movingRight; // Flip direction
                
                // Boost speed and damage for the reflected projectile
                speed *= 1.5f; 
                damage *= 1.5f; 
                
                float xVel = movingRight ? speed : -speed;
                rb.linearVelocity = new Vector2(xVel, rb.linearVelocity.y);
                return;
            }

            // --- HIT LOGIC ---
            // Small flinch knockback, mostly horizontal
            Vector2 knockback = movingRight ? new Vector2(1, 0.2f) : new Vector2(-1, 0.2f);
            hitPlayer.TakeHit(damage, knockback);
            Destroy(gameObject);
        }
    }
}
