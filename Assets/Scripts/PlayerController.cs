using UnityEngine;
using System;

public class PlayerController : MonoBehaviour
{
    public enum PlayerID { Player1_Red, Player2_Blue }
    public PlayerID playerType;

    public enum State { Grounded, Airborne, Shielding, Dodging, Attacking, Hitstun }
    [Header("Current State")]
    public State currentState = State.Grounded;

    [Header("Sprites/Objects (Assign in Inspector)")]
    public GameObject boxingGloveSprite;
    public GameObject backBoxingGloveSprite;
    public GameObject hammerSprite;
    public GameObject spikeHelmetSprite;
    public GameObject bootSprite;
    public GameObject upBoxingGloveSprite;
    public GameObject shieldBubble;

    // --- Physics Stats ---
    [Serializable]
    public struct CharacterStats
    {
        public float weight, initialDash, runSpeed, walkSpeed, traction, airFriction, airSpeed;
        public float baseAirAccel, addAirAccel, gravity, fallSpeed, fastFallSpeed;
        public int jumpsquatFrames;
        public float jumpHeight, shortHopHeight, doubleJumpHeight;
    }
    
    [Header("Assigned Stats")]
    public CharacterStats stats;

    [Header("Engine Settings")]
    public float unitScale = 0.1f;


    // Internal physics variables
    private Rigidbody2D rb;
    private Vector2 velocity;
    private int jumpsquatCounter = 0;
    private bool isFastFalling = false;
    private int jumpsRemaining = 1;
    private int currentJabCombo = 0;
    private float lastJabTime = 0f;

    // Inputs
    private KeyCode upKey, downKey, leftKey, rightKey, walkModKey, attackKey, shieldKey;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // --- NEW PHYSICS SETUP ---
        rb.bodyType = RigidbodyType2D.Dynamic; // Allows collision with the ground
        rb.gravityScale = 0f;                  // Turns off Unity's gravity (we use our own!)
        rb.freezeRotation = true;              // Prevents the squares from tumbling over

        AssignInputsAndStats();
        HideAllSprites();
    }


    void Update()
    {
        // Inputs are read in Update, Physics in FixedUpdate
        HandleInputs();
    }

    void FixedUpdate()
    {
        if (currentState == State.Grounded || currentState == State.Airborne)
        {
            ApplyPhysics();
        }
       
        rb.linearVelocity = velocity * (1f / Time.fixedDeltaTime) * unitScale; 
    }

    private void AssignInputsAndStats()
    {
        if (playerType == PlayerID.Player1_Red)
        {
            // Player 1 Inputs
            upKey = KeyCode.W; downKey = KeyCode.S; leftKey = KeyCode.A; rightKey = KeyCode.D;
            walkModKey = KeyCode.LeftShift; attackKey = KeyCode.F; shieldKey = KeyCode.E;

            // Player 1 Stats
            stats = new CharacterStats {
                weight = 98, initialDash = 1.936f, runSpeed = 1.76f, walkSpeed = 1.155f, 
                traction = 0.102f, airFriction = 0.015f, airSpeed = 1.208f, baseAirAccel = 0.01f, 
                addAirAccel = 0.07f, gravity = 0.087f, fallSpeed = 1.5f, fastFallSpeed = 2.4f, 
                jumpsquatFrames = 3, jumpHeight = 36.33f, shortHopHeight = 17.54f, doubleJumpHeight = 36.33f
            };
            GetComponent<SpriteRenderer>().color = Color.red;
        }
        else
        {
            // Player 2 Inputs
            upKey = KeyCode.UpArrow; downKey = KeyCode.DownArrow; leftKey = KeyCode.LeftArrow; rightKey = KeyCode.RightArrow;
            walkModKey = KeyCode.RightShift; attackKey = KeyCode.Space; shieldKey = KeyCode.RightControl;

            // Player 2 Stats
            stats = new CharacterStats {
                weight = 86, initialDash = 2.31f, runSpeed = 3.85f, walkSpeed = 1.444f, 
                traction = 0.138f, airFriction = 0.01f, airSpeed = 1.208f, baseAirAccel = 0.01f, 
                addAirAccel = 0.04f, gravity = 0.09f, fallSpeed = 1.65f, fastFallSpeed = 2.64f, 
                jumpsquatFrames = 3, jumpHeight = 35f, shortHopHeight = 16.89f, doubleJumpHeight = 35f
            };
            GetComponent<SpriteRenderer>().color = Color.blue;
        }
    }

    private void HandleInputs()
    {
        if (currentState == State.Attacking || currentState == State.Dodging || currentState == State.Hitstun) return;

        int xInput = (Input.GetKey(rightKey) ? 1 : 0) - (Input.GetKey(leftKey) ? 1 : 0);
        bool isWalkModifier = Input.GetKey(walkModKey);

        // --- DEFENSE ---
        if (Input.GetKey(shieldKey))
        {
            if (currentState == State.Grounded || currentState == State.Shielding) // FIX: Allow dodging while shielding
            {
                if (Input.GetKeyDown(downKey)) { ExecuteSpotDodge(); return; }
                if (Input.GetKeyDown(leftKey) || Input.GetKeyDown(rightKey)) { ExecuteRoll(); return; }
                
                currentState = State.Shielding;
                shieldBubble.SetActive(true);
                velocity.x = 0; // FIX: Stop sliding when shield is pulled up
            }
            else if (currentState == State.Airborne && Input.GetKeyDown(shieldKey))
            {
                ExecuteAirDodge(xInput, (Input.GetKey(upKey) ? 1 : 0) - (Input.GetKey(downKey) ? 1 : 0));
                return;
            }
        }
        else
        {
            if (currentState == State.Shielding) currentState = State.Grounded;
            shieldBubble.SetActive(false);
        }

        // --- ATTACKS ---
        if (Input.GetKeyDown(attackKey) && currentState == State.Grounded)
        {
            DetermineGroundAttack(xInput, isWalkModifier);
            return;
        }

        // --- MOVEMENT & JUMPING ---
        if (Input.GetKeyDown(upKey))
        {
            if (currentState == State.Grounded || currentState == State.Shielding) StartJumpsquat();
            else if (currentState == State.Airborne && jumpsRemaining > 0) ExecuteJump(stats.doubleJumpHeight, true); // FIX: Flag as double jump
        }

        if (currentState == State.Airborne && Input.GetKeyDown(downKey) && velocity.y < 0)
        {
            isFastFalling = true;
        }
    }

    private void ApplyPhysics()
    {
        int xInput = (Input.GetKey(rightKey) ? 1 : 0) - (Input.GetKey(leftKey) ? 1 : 0);
        bool isWalkModifier = Input.GetKey(walkModKey);

        if (currentState == State.Grounded)
        {
            isFastFalling = false;
            jumpsRemaining = 1;
            velocity.y = 0;

            if (xInput != 0)
            {
                // Dashdance / Initial Dash logic would be expanded here. For now, basic limits:
                float targetSpeed = isWalkModifier ? stats.walkSpeed : stats.runSpeed;
                
                // Accelerate horizontally
                velocity.x = Mathf.MoveTowards(velocity.x, xInput * targetSpeed, stats.initialDash);
            }
            else
            {
                // Apply Traction
                velocity.x = Mathf.MoveTowards(velocity.x, 0, stats.traction);
            }
        }
        else if (currentState == State.Airborne)
        {
            // Vertical: Gravity
            float maxFall = isFastFalling ? stats.fastFallSpeed : stats.fallSpeed;
            velocity.y -= stats.gravity;
            if (velocity.y < -maxFall) velocity.y = -maxFall;

            // Horizontal: Aerial drift
            if (xInput != 0)
            {
                float accel = stats.baseAirAccel + stats.addAirAccel; 
                velocity.x = Mathf.MoveTowards(velocity.x, xInput * stats.airSpeed, accel);
            }
            else
            {
                // Apply Air Friction
                velocity.x = Mathf.MoveTowards(velocity.x, 0, stats.airFriction);
            }
        }
    }

    // --- JUMP LOGIC ---
    private void StartJumpsquat()
    {
        float height = Input.GetKey(upKey) ? stats.jumpHeight : stats.shortHopHeight;
        
        // FIX: Cap horizontal momentum to air speed when leaving the ground
        velocity.x = Mathf.Clamp(velocity.x, -stats.airSpeed, stats.airSpeed); 
        
        ExecuteJump(height, false);
    }

    private void ExecuteJump(float height, bool isDoubleJump)
    {
        currentState = State.Airborne;
        velocity.y = Mathf.Sqrt(2f * stats.gravity * height); 
        
        // FIX: Only consume a jump if we are actually double jumping
        if (isDoubleJump) jumpsRemaining--; 
    }

    // --- COMBAT LOGIC ---
    private void DetermineGroundAttack(int xInput, bool isWalkMod)
    {
        currentState = State.Attacking;

        bool smashInput = (Input.GetKeyDown(leftKey) || Input.GetKeyDown(rightKey)) && Input.GetKeyDown(attackKey);
        
        if (Input.GetKey(upKey))
        {
            if (Input.GetKeyDown(upKey) && Input.GetKeyDown(attackKey)) ExecuteAttack("UpSmash", upBoxingGloveSprite, 27.5f);
            else ExecuteAttack("UpTilt", spikeHelmetSprite, 6f);
        }
        else if (Input.GetKey(downKey))
        {
            if (Input.GetKeyDown(downKey) && Input.GetKeyDown(attackKey)) 
                ExecuteAttack("DownSmash", boxingGloveSprite, 25f, backBoxingGloveSprite);
            else 
                ExecuteAttack("DownTilt", bootSprite, 7f);
        }
        else if (xInput != 0)
        {
            if (smashInput) ExecuteAttack("ForwardSmash", hammerSprite, 30f);
            else if (Mathf.Abs(velocity.x) > stats.walkSpeed && !isWalkMod) ExecuteAttack("DashAttack", boxingGloveSprite, 9f);
            else ExecuteAttack("ForwardTilt", hammerSprite, 8f);
        }
        else
        {
            ExecuteJab();
        }
    }

    private void ExecuteAttack(string attackName, GameObject spriteToShow, float damage, GameObject secondarySprite = null)
    {
        Debug.Log($"{playerType} performed {attackName} dealing {damage}% damage!");
        HideAllSprites();
        
        if (spriteToShow != null) spriteToShow.SetActive(true);
        if (secondarySprite != null) secondarySprite.SetActive(true); // Turns on the back glove for D-Smash
        
        // --- HITBOX & KNOCKBACK LOGIC ---
        // Check for colliders within a 1.5 unit radius
        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(transform.position, 1.5f); 
        foreach (Collider2D hit in hitPlayers)
        {
            PlayerController enemyPlayer = hit.GetComponent<PlayerController>();
            
            // If we found a player, and it's NOT us...
            if (enemyPlayer != null && enemyPlayer != this)
            {
                // Calculate knockback direction (away from attacker)
                Vector2 knockbackDir = (enemyPlayer.transform.position - transform.position).normalized;
                knockbackDir.y += 0.5f; // Add an upward angle to the hit
                
                enemyPlayer.TakeHit(damage, knockbackDir);
            }
        }

        Invoke("ResetToGrounded", 0.3f); 
    }

    // NEW: Handles taking damage and flying backward
    public void TakeHit(float damage, Vector2 knockbackDir)
    {
        if (currentState == State.Dodging) return; // Invincibility frames!
        
        currentState = State.Hitstun;
        HideAllSprites();
        
        // Basic Smash Knockback Math: heavier characters fly less
        float knockbackForce = damage * (100f / stats.weight); 
        velocity = knockbackDir.normalized * knockbackForce * 0.2f; 
        
        Invoke("ResetAirborne", 0.5f); // Recover from hitstun after 0.5 seconds
    }

    private void ExecuteJab()
    {
        // Basic 3-Hit Jab Combo Logic
        if (Time.time - lastJabTime > 0.5f) currentJabCombo = 0;

        currentJabCombo++;
        lastJabTime = Time.time;

        float damage = (currentJabCombo == 3) ? 4f : 2f;
        ExecuteAttack($"Jab hit {currentJabCombo}", boxingGloveSprite, damage);

        if (currentJabCombo >= 3) currentJabCombo = 0;
    }

    // --- DEFENSIVE MOVES ---
    private void ExecuteSpotDodge() 
    { 
        Debug.Log($"{playerType} Spot Dodged!"); 
        currentState = State.Dodging; 
        shieldBubble.SetActive(false); 
        velocity = Vector2.zero;
        Invoke("ResetToGrounded", 0.4f); 
    }

    private void ExecuteRoll() 
    { 
        Debug.Log($"{playerType} Rolled!"); 
        currentState = State.Dodging; 
        shieldBubble.SetActive(false);
        
        int dirX = Input.GetKey(leftKey) ? -1 : 1;
        
        velocity = new Vector2(dirX * stats.runSpeed * 1.5f, 0); 
        
        Invoke("ResetToGrounded", 0.5f); 
    }

    private void ExecuteAirDodge(int dirX, int dirY)
    {
        currentState = State.Dodging;
        shieldBubble.SetActive(false); // Make sure shield bubble isn't showing in air

        // FIX: Halt current momentum so they don't float into the stratosphere
        velocity = Vector2.zero; 

        // Apply a burst of momentum for directional air dodges
        if (dirX != 0 || dirY != 0) 
        {
            velocity = new Vector2(dirX, dirY).normalized * stats.airSpeed * 2f;
            Debug.Log($"{playerType} Directional Air Dodged: {dirX}, {dirY}");
        }
        else 
        {
            Debug.Log($"{playerType} Neutral Air Dodged!");
        }

        Invoke("ResetAirborne", 0.4f);
    }

    // --- UTILITY ---
    private void ResetToGrounded() { HideAllSprites(); currentState = State.Grounded; }
    private void ResetAirborne() { HideAllSprites(); currentState = State.Airborne; }
    
    private void HideAllSprites()
    {
        if(boxingGloveSprite) boxingGloveSprite.SetActive(false);
        if(hammerSprite) hammerSprite.SetActive(false);
        if(spikeHelmetSprite) spikeHelmetSprite.SetActive(false);
        if(bootSprite) bootSprite.SetActive(false);
        if(upBoxingGloveSprite) upBoxingGloveSprite.SetActive(false);
        if(shieldBubble) shieldBubble.SetActive(false);
    }

    // --- GROUND COLLISIONS & WAVEDASHING ---
    private void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Ground")) 
        {
            if (currentState == State.Dodging && velocity.y < 0)
            {
                if (Mathf.Abs(velocity.x) > 0.1f)
                {
                    Debug.Log($"{playerType} perfectly WAVEDASHED!");
                    velocity.x *= 1.5f; 
                }
            }

            currentState = State.Grounded;
            velocity.y = 0;
            jumpsRemaining = 1;
            isFastFalling = false;

            CancelInvoke("ResetAirborne");
            CancelInvoke("ResetToGrounded");
        }
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Ground") && currentState == State.Grounded) 
        {
            currentState = State.Airborne;
        }
    }
}