using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PlayerController : MonoBehaviour
{
    public enum PlayerID { Player1_Red, Player2_Blue }
    public PlayerID playerType;

   public enum State { Grounded, Airborne, Shielding, Dodging, Hitstun, ChargingSmash };
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

    [Header("Current Status")]
    public float currentDamage = 0f; 
    public Vector3 respawnPoint = new Vector3(0, 5, 0); 
    public TextMeshProUGUI damageUI; // NEW: The text element on the screen

    public bool isAttacking = false;
    
    [Header("Assigned Stats")]
    public CharacterStats stats;

    [Header("Engine Settings")]
    public float unitScale = 0.1f;

    [Header("Input Buffer")]
    public float smashWindow = 0.2f; // Gives you 0.2 seconds to hit attack after pressing a direction
    private float lastDownPress = -10f;
    private float lastUpPress = -10f;
    private float lastSidePress = -10f;

    [Header("Smash Charge Settings")]
    public float maxChargeTime = 1f; // 60 frames (1 second)
    public float maxChargeMultiplier = 1.4f; // Smash standard: 1.4x damage at max charge

    // Internal physics variables
    private Rigidbody2D rb;
    private Vector2 velocity;
    private bool isFastFalling = false;
    private int jumpsRemaining = 1;
    private int currentJabCombo = 0;
    private float lastJabTime = 0f;
    private float chargeTimer = 0f;
    private string pendingAttackName;
    private float pendingDamage;
    private GameObject pendingSprite;
    private GameObject pendingSecondarySprite;

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
        // ADDED Hitstun so players fall in an arc when knocked back!
        if (currentState == State.Grounded || currentState == State.Airborne || currentState == State.Hitstun)
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
        }
    }

    private void HandleInputs()
    {
        // --- CHARGE SMASH INTERCEPT ---
        if (currentState == State.ChargingSmash)
        {
            chargeTimer += Time.deltaTime;
            
            // Visual Shake Indicator
            if (chargeTimer < maxChargeTime)
            {
                // Rapidly jitter the sprite left and right by 0.05 units
                float shakeAmt = 0.05f;
                transform.position = new Vector3(rb.position.x + UnityEngine.Random.Range(-shakeAmt, shakeAmt), rb.position.y, transform.position.z);
            }
            else
            {
                // Max power reached! Stop shaking and lock into place.
                transform.position = new Vector3(rb.position.x, rb.position.y, transform.position.z);
            }

            // When the player lets go of the attack key, release the smash!
            if (Input.GetKeyUp(attackKey))
            {
                ReleaseSmash();
            }
            return; // CRITICAL: Stop reading other inputs so they can't jump/run while charging
        }

        // Track the timestamp of directional presses for Smash Attacks
        if (Input.GetKeyDown(downKey)) lastDownPress = Time.time;
        if (Input.GetKeyDown(upKey)) lastUpPress = Time.time;
        if (Input.GetKeyDown(leftKey) || Input.GetKeyDown(rightKey)) lastSidePress = Time.time;

        if (currentState == State.Dodging || currentState == State.Hitstun) return;

        if (isAttacking)
        {
            if (currentState == State.Grounded)
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0, 40f * Time.deltaTime); 
                rb.linearVelocity = new Vector2(velocity.x, rb.linearVelocity.y); 
            }
            
            // 3. Return early so they can't input new jumps or direction changes
            return; 
        }
        
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
        bool smashInput = (Input.GetKeyDown(leftKey) || Input.GetKeyDown(rightKey)) && Input.GetKeyDown(attackKey);
        
        if (Input.GetKey(downKey))
        {
            if (Input.GetKeyDown(attackKey)) 
            {
                // If the time between tapping Down and pressing Attack is less than 0.2s, it's a Smash!
                if (Time.time - lastDownPress <= smashWindow)
                    StartChargeSmash("DownSmash", boxingGloveSprite, 25f, backBoxingGloveSprite);
                else 
                    ExecuteAttack("DownTilt", bootSprite, 7f);
            }
        }        
        else if (Input.GetKey(upKey))
        {
            if (Input.GetKeyDown(attackKey)) 
            {
                if (Time.time - lastUpPress <= smashWindow)
                    StartChargeSmash("UpSmash", upBoxingGloveSprite, 20f);
                else 
                    ExecuteAttack("UpTilt", spikeHelmetSprite, 8f);
            }
        }
        else if (xInput != 0)
        {
            if (smashInput) if (Time.time - lastSidePress <= smashWindow) StartChargeSmash("ForwardSmash", hammerSprite, 30f);
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
        
        isAttacking = true; // CHANGED: We are now attacking, but we remain Grounded or Airborne!
        
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

        Invoke("EndAttack", 0.3f); 
    }

    // NEW: Handles taking damage and flying backward
    public void TakeHit(float incomingDamage, Vector2 knockbackDir)
    {
        if (currentState == State.Dodging) return; 
        
        currentState = State.Hitstun;
        HideAllSprites();
        
        // Accumulate damage!
        currentDamage += incomingDamage;
        
        // --- NEW: UPDATE THE UI ---
        if (damageUI != null) 
        {
            damageUI.text = Mathf.FloorToInt(currentDamage).ToString() + "%";
        }
        
        // Knockback Math
        float knockbackForce = (currentDamage * incomingDamage) / stats.weight; 
        knockbackForce = Mathf.Clamp(knockbackForce, 5f, 100f); 
        
        velocity = knockbackDir.normalized * knockbackForce * 0.2f; 
        
        Invoke("ResetAirborne", 0.5f); 
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

    private void StartChargeSmash(string attackName, GameObject sprite, float baseDamage, GameObject secondary = null)
    {
        currentState = State.ChargingSmash;
        velocity = Vector2.zero; // Stop sliding
        rb.linearVelocity = Vector2.zero;
        chargeTimer = 0f;
        
        // Store the attack details for when we release the button
        pendingAttackName = attackName;
        pendingDamage = baseDamage;
        pendingSprite = sprite;
        pendingSecondarySprite = secondary;
        
        // Show the wind-up frame (using the attack sprite for now)
        HideAllSprites();
        if (pendingSprite != null) pendingSprite.SetActive(true);
        if (pendingSecondarySprite != null) pendingSecondarySprite.SetActive(true);
    }

    private void ReleaseSmash()
    {
        // Snap position back to perfectly center in case they were shaking
        transform.position = new Vector3(rb.position.x, rb.position.y, transform.position.z);
        
        currentState = State.Grounded;
        
        // Calculate the damage multiplier (Mathf.Lerp smoothly scales from 1.0 to 1.4 over 1 second)
        float chargePercent = Mathf.Clamp01(chargeTimer / maxChargeTime);
        float finalDamage = pendingDamage * Mathf.Lerp(1f, maxChargeMultiplier, chargePercent);
        
        // Execute the attack with the newly scaled damage!
        ExecuteAttack(pendingAttackName, pendingSprite, finalDamage, pendingSecondarySprite);
    }

    // --- DEFENSIVE MOVES ---
    private void ExecuteSpotDodge() 
    { 
        Debug.Log($"{playerType} Spot Dodged!"); 
        currentState = State.Dodging; 
        shieldBubble.SetActive(false); 
        velocity = Vector2.zero;
        Invoke("EndDodge", 0.4f);
    }

    private void ExecuteRoll() 
    { 
        Debug.Log($"{playerType} Rolled!"); 
        currentState = State.Dodging; 
        shieldBubble.SetActive(false);
        
        int dirX = Input.GetKey(leftKey) ? -1 : 1;
        
        velocity = new Vector2(dirX * stats.runSpeed * 1.5f, 0); 
        
        Invoke("EndDodge", 0.5f);
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
        if (boxingGloveSprite != null) boxingGloveSprite.SetActive(false);
        if (bootSprite != null) bootSprite.SetActive(false);
        if (upBoxingGloveSprite != null) upBoxingGloveSprite.SetActive(false);
        if (hammerSprite != null) hammerSprite.SetActive(false);
        if (spikeHelmetSprite != null) spikeHelmetSprite.SetActive(false);
        if (backBoxingGloveSprite != null) backBoxingGloveSprite.SetActive(false); 
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

    private void EndAttack()
    {
        HideAllSprites();
        isAttacking = false; // Turn off the attack lock, returning input control to the player
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Ground")) 
        {
            currentState = State.Airborne;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("BlastZone"))
        {
            ExecuteKO();
        }
    }

    private void ExecuteKO()
    {
        Debug.Log($"GAME! {playerType} was blasted off the screen!");
        
        // Reset stats
        currentDamage = 0f;
        
        // --- NEW: RESET THE UI ---
        if (damageUI != null) 
        {
            damageUI.text = "0%";
        }

        velocity = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        
        rb.position = respawnPoint; 
        transform.position = respawnPoint; 
        
        currentState = State.Airborne; 
        CancelInvoke(); 
        HideAllSprites();
    }

    private void EndDodge()
    {
        HideAllSprites();
        // If we are falling, we must be in the air. Otherwise, we are on the ground.
        if (Mathf.Abs(rb.linearVelocity.y) > 0.1f) currentState = State.Airborne;
        else currentState = State.Grounded;
    }
}