using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerNum { Player1, Player2 }
    [Header("Player Settings")]
    public PlayerNum playerNumber;

    [Header("Combat Hitbox Visuals")]
    public GameObject boxingGloveSprite;
    public GameObject hammerSprite;
    public GameObject spikeHelmetSprite;
    public GameObject bootSprite;
    public GameObject upBoxingGloveSprite;
    public GameObject leftBoxingGloveSprite;
    public GameObject rightBoxingGloveSprite;
    public GameObject shieldBubble;

    // --- Stats Data Structure ---
    [System.Serializable]
    public struct FighterStats
    {
        public float weight;
        public float initialDash;
        public float runSpeed;
        public float walkSpeed;
        public float traction;
        public float airFriction;
        public float airSpeed;
        public float baseAirAccel;
        public float additionalAirAccel;
        public float gravity;
        public float fallSpeed;
        public float fastFallSpeed;
        public int jumpsquatFrames;
        public float jumpHeight;
        public float shortHopHeight;
        public float doubleJumpHeight;
    }

    [Header("Read-Only Stats (Auto-Assigned)")]
    public FighterStats stats;

    // --- Inputs ---
    private KeyCode upKey, downKey, leftKey, rightKey, attackKey, shieldKey, walkModifierKey;

    // --- State Machine ---
    public enum FighterState 
    { 
        Idle, Walking, InitialDash, Running, Jumpsquat, Airborne, 
        Shielding, Rolling, SpotDodging, AirDodging, Attacking, Hitstun 
    }
    public FighterState currentState = FighterState.Idle;

    // --- Physics & Tracking ---
    private Rigidbody2D rb;
    private bool isGrounded = false;
    private int framesInState = 0;
    private bool hasDoubleJump = true;
    private bool isFastFalling = false;
    private float facingDirection = 1f; // 1 for right, -1 for left
    private int currentJabCount = 0;
    private Coroutine currentActionCoroutine;

    // Constants to convert "generic units per frame" to Unity physics scale
    private const float SPEED_MULTIPLIER = 5f; 
    private const float JUMP_MULTIPLIER = 5f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 0f; // We will handle gravity manually for Smash physics
        
        AssignStatsAndInputs();
        DisableAllHitboxes();
    }

    private void AssignStatsAndInputs()
    {
        if (playerNumber == PlayerNum.Player1)
        {
            // Red Player Stats
            stats = new FighterStats {
                weight = 98f, initialDash = 1.936f, runSpeed = 1.76f, walkSpeed = 1.155f,
                traction = 0.102f, airFriction = 0.015f, airSpeed = 1.208f, baseAirAccel = 0.01f,
                additionalAirAccel = 0.07f, gravity = 0.087f, fallSpeed = 1.5f, fastFallSpeed = 2.4f,
                jumpsquatFrames = 3, jumpHeight = 3.633f, shortHopHeight = 1.754f, doubleJumpHeight = 3.633f
            };
            // Red Player Inputs
            upKey = KeyCode.W; downKey = KeyCode.S; leftKey = KeyCode.A; rightKey = KeyCode.D;
            attackKey = KeyCode.F; shieldKey = KeyCode.E; walkModifierKey = KeyCode.LeftShift;
        }
        else
        {
            // Blue Player Stats
            stats = new FighterStats {
                weight = 86f, initialDash = 2.31f, runSpeed = 3.85f, walkSpeed = 1.444f,
                traction = 0.138f, airFriction = 0.01f, airSpeed = 1.208f, baseAirAccel = 0.01f,
                additionalAirAccel = 0.04f, gravity = 0.09f, fallSpeed = 1.65f, fastFallSpeed = 2.64f,
                jumpsquatFrames = 3, jumpHeight = 3.5f, shortHopHeight = 1.689f, doubleJumpHeight = 3.5f
            };
            // Blue Player Inputs
            upKey = KeyCode.UpArrow; downKey = KeyCode.DownArrow; leftKey = KeyCode.LeftArrow; rightKey = KeyCode.RightArrow;
            attackKey = KeyCode.Space; shieldKey = KeyCode.RightControl; walkModifierKey = KeyCode.RightShift;
        }
    }

    void Update()
    {
        CheckGrounded();
        HandleInputs();
        UpdateFacingDirection();
    }

    void FixedUpdate()
    {
        framesInState++;
        ApplyGravity();
        HandleMovementPhysics();
        HandleFriction();
    }

    // --- Core Systems ---

    private void CheckGrounded()
    {
        // Simple raycast downwards to check for ground layer
        bool wasGrounded = isGrounded;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.1f);
        isGrounded = hit.collider != null && !hit.collider.isTrigger;

        if (isGrounded && !wasGrounded && currentState == FighterState.Airborne)
        {
            // Landing
            ChangeState(FighterState.Idle);
            hasDoubleJump = true;
            isFastFalling = false;
        }

        // Wavedash landing check
        if (isGrounded && currentState == FighterState.AirDodging && framesInState > 2)
        {
            ChangeState(FighterState.Idle); // Momentum carries over into traction
        }
    }

    private void ChangeState(FighterState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        framesInState = 0;

        shieldBubble?.SetActive(currentState == FighterState.Shielding);
    }

    private void HandleInputs()
    {
        if (currentState == FighterState.Attacking || currentState == FighterState.Rolling || 
            currentState == FighterState.SpotDodging || currentState == FighterState.Hitstun) 
            return; // Lock inputs during animations

        bool isWalkMod = Input.GetKey(walkModifierKey);
        bool leftHeld = Input.GetKey(leftKey);
        bool rightHeld = Input.GetKey(rightKey);
        bool upHeld = Input.GetKey(upKey);
        bool downHeld = Input.GetKey(downKey);
        
        bool leftTap = Input.GetKeyDown(leftKey);
        bool rightTap = Input.GetKeyDown(rightKey);
        bool upTap = Input.GetKeyDown(upKey);
        bool downTap = Input.GetKeyDown(downKey);
        
        bool attackTap = Input.GetKeyDown(attackKey);
        bool shieldHeld = Input.GetKey(shieldKey);
        bool shieldTap = Input.GetKeyDown(shieldKey);

        // --- Defensive Actions ---
        if (shieldHeld)
        {
            if (isGrounded)
            {
                if (leftTap || rightTap) { StartAction(RollDodgeRoutine(leftTap ? -1 : 1)); return; }
                if (downTap) { StartAction(SpotDodgeRoutine()); return; }
                ChangeState(FighterState.Shielding);
            }
            else if (shieldTap) // Air dodge
            {
                Vector2 dir = Vector2.zero;
                if (upHeld) dir.y = 1; if (downHeld) dir.y = -1;
                if (leftHeld) dir.x = -1; if (rightHeld) dir.x = 1;
                StartAction(AirDodgeRoutine(dir.normalized));
                return;
            }
        }
        else if (currentState == FighterState.Shielding)
        {
            ChangeState(FighterState.Idle);
        }

        // --- Attacks ---
        if (attackTap)
        {
            if (isGrounded)
            {
                if (upTap) StartAction(UpSmashRoutine());
                else if (downTap) StartAction(DownSmashRoutine());
                else if (leftTap || rightTap) StartAction(ForwardSmashRoutine());
                else if (upHeld && isWalkMod) StartAction(UpTiltRoutine());
                else if (downHeld) StartAction(DownTiltRoutine());
                else if ((leftHeld || rightHeld) && isWalkMod) StartAction(ForwardTiltRoutine());
                else if (currentState == FighterState.Running) StartAction(DashAttackRoutine());
                else StartAction(JabRoutine());
                return;
            }
        }

        // --- Movement Actions ---
        if (isGrounded && currentState != FighterState.Jumpsquat && !shieldHeld)
        {
            if (upTap)
            {
                ChangeState(FighterState.Jumpsquat);
            }
            else if (leftHeld || rightHeld)
            {
                float dir = leftHeld ? -1f : 1f;
                if (isWalkMod)
                {
                    ChangeState(FighterState.Walking);
                }
                else
                {
                    // Dashdancing logic (rapid turnaround in initial dash)
                    if (currentState == FighterState.InitialDash && facingDirection != dir && framesInState < 15)
                    {
                        facingDirection = dir;
                        framesInState = 0; // reset initial dash
                        Vector2 vel = rb.linearVelocity;
                        vel.x = stats.initialDash * SPEED_MULTIPLIER * dir;
                        rb.linearVelocity = vel;
                    }
                    else if (currentState != FighterState.InitialDash && currentState != FighterState.Running)
                    {
                        ChangeState(FighterState.InitialDash);
                    }
                    else if (currentState == FighterState.InitialDash && framesInState >= 15)
                    {
                        ChangeState(FighterState.Running);
                    }
                }
            }
            else
            {
                ChangeState(FighterState.Idle);
            }
        }
        else if (!isGrounded && currentState != FighterState.AirDodging)
        {
            ChangeState(FighterState.Airborne);
            
            // Double Jump
            if (upTap && hasDoubleJump)
            {
                hasDoubleJump = false;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.doubleJumpHeight * JUMP_MULTIPLIER);
            }
            // Fast fall
            if (downTap && rb.linearVelocity.y <= 0 && !isFastFalling)
            {
                isFastFalling = true;
            }
        }

        // Jumpsquat execution
        if (currentState == FighterState.Jumpsquat && framesInState >= stats.jumpsquatFrames)
        {
            float jumpForce = Input.GetKey(upKey) ? stats.jumpHeight : stats.shortHopHeight;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * JUMP_MULTIPLIER);
            ChangeState(FighterState.Airborne);
        }
    }

    private void ApplyGravity()
    {
        if (isGrounded || currentState == FighterState.AirDodging) return;

        Vector2 vel = rb.linearVelocity;
        vel.y -= stats.gravity * JUMP_MULTIPLIER;

        float maxFall = isFastFalling ? stats.fastFallSpeed : stats.fallSpeed;
        if (vel.y < -maxFall * JUMP_MULTIPLIER)
        {
            vel.y = -maxFall * JUMP_MULTIPLIER;
        }
        
        rb.linearVelocity = vel;
    }

    private void HandleMovementPhysics()
    {
        if (currentState == FighterState.Attacking || currentState == FighterState.AirDodging || currentState == FighterState.Rolling) return;

        Vector2 vel = rb.linearVelocity;
        bool leftHeld = Input.GetKey(leftKey);
        bool rightHeld = Input.GetKey(rightKey);
        float inputDir = 0;
        if (leftHeld) inputDir = -1;
        if (rightHeld) inputDir = 1;

        if (isGrounded)
        {
            if (currentState == FighterState.InitialDash)
            {
                vel.x = stats.initialDash * SPEED_MULTIPLIER * facingDirection;
            }
            else if (currentState == FighterState.Running)
            {
                vel.x = stats.runSpeed * SPEED_MULTIPLIER * facingDirection;
            }
            else if (currentState == FighterState.Walking)
            {
                vel.x = stats.walkSpeed * SPEED_MULTIPLIER * facingDirection;
            }
        }
        else // Airborne
        {
            if (inputDir != 0)
            {
                // Applying air acceleration
                float accel = stats.baseAirAccel + stats.additionalAirAccel;
                vel.x += accel * inputDir * SPEED_MULTIPLIER;

                // Cap at air speed
                if (Mathf.Abs(vel.x) > stats.airSpeed * SPEED_MULTIPLIER)
                {
                    vel.x = stats.airSpeed * SPEED_MULTIPLIER * Mathf.Sign(vel.x);
                }
            }
        }

        rb.linearVelocity = vel;
    }

    private void HandleFriction()
    {
        if (currentState == FighterState.Attacking || currentState == FighterState.AirDodging) return;

        bool leftHeld = Input.GetKey(leftKey);
        bool rightHeld = Input.GetKey(rightKey);

        Vector2 vel = rb.linearVelocity;
        if (isGrounded)
        {
            if (!leftHeld && !rightHeld && currentState != FighterState.InitialDash)
            {
                vel.x = Mathf.MoveTowards(vel.x, 0, stats.traction * SPEED_MULTIPLIER);
            }
        }
        else // Air friction
        {
            if (!leftHeld && !rightHeld)
            {
                vel.x = Mathf.MoveTowards(vel.x, 0, stats.airFriction * SPEED_MULTIPLIER);
            }
        }
        rb.linearVelocity = vel;
    }

    private void UpdateFacingDirection()
    {
        if (currentState == FighterState.Attacking) return;
        
        bool leftHeld = Input.GetKey(leftKey);
        bool rightHeld = Input.GetKey(rightKey);

        if (leftHeld && !rightHeld) facingDirection = -1f;
        else if (rightHeld && !leftHeld) facingDirection = 1f;

        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * facingDirection, transform.localScale.y, transform.localScale.z);
    }

    // --- Action Coroutines (Dodges & Attacks) ---

    private void StartAction(IEnumerator routine)
    {
        if (currentActionCoroutine != null) StopCoroutine(currentActionCoroutine);
        ChangeState(FighterState.Attacking); // Use generic lock state
        currentActionCoroutine = StartCoroutine(routine);
    }

    private IEnumerator RollDodgeRoutine(float dir)
    {
        ChangeState(FighterState.Rolling);
        facingDirection = dir; // Face roll dir temporarily
        rb.linearVelocity = new Vector2(dir * stats.runSpeed * SPEED_MULTIPLIER * 1.5f, 0); // Burst of speed
        
        // Simulating invincibility frames visually
        GetComponent<SpriteRenderer>().color = new Color(1,1,1,0.5f);
        yield return new WaitForSeconds(0.4f);
        GetComponent<SpriteRenderer>().color = Color.white;
        
        rb.linearVelocity = Vector2.zero;
        ChangeState(FighterState.Idle);
    }

    private IEnumerator SpotDodgeRoutine()
    {
        ChangeState(FighterState.SpotDodging);
        rb.linearVelocity = Vector2.zero;
        
        GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.5f);
        yield return new WaitForSeconds(0.4f); // Spin / intangible duration
        GetComponent<SpriteRenderer>().color = Color.white;
        
        ChangeState(FighterState.Idle);
    }

    private IEnumerator AirDodgeRoutine(Vector2 direction)
    {
        ChangeState(FighterState.AirDodging);
        
        if (direction != Vector2.zero)
        {
            // Directional Air Dodge (can lead to Wavedash)
            rb.linearVelocity = direction * stats.airSpeed * SPEED_MULTIPLIER * 2f; 
        }
        else
        {
            // Neutral Air Dodge
            rb.linearVelocity = Vector2.zero; 
        }

        GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.5f);
        yield return new WaitForSeconds(0.3f);
        GetComponent<SpriteRenderer>().color = Color.white;

        if (!isGrounded) ChangeState(FighterState.Airborne);
    }

    // --- Attack Coroutines ---

    private IEnumerator JabRoutine()
    {
        ChangeState(FighterState.Attacking);
        rb.linearVelocity = Vector2.zero;

        if (currentJabCount == 0)
        {
            // Jab 1: 2% damage
            boxingGloveSprite.SetActive(true);
            LogAttack("Jab 1", 2f);
            yield return new WaitForSeconds(0.15f);
            boxingGloveSprite.SetActive(false);
            currentJabCount++;
        }
        else if (currentJabCount == 1)
        {
            // Jab 2: 2% damage
            boxingGloveSprite.SetActive(true);
            LogAttack("Jab 2", 2f);
            yield return new WaitForSeconds(0.15f);
            boxingGloveSprite.SetActive(false);
            currentJabCount++;
        }
        else if (currentJabCount >= 2)
        {
            // Jab 3: 4% damage
            boxingGloveSprite.SetActive(true);
            LogAttack("Jab 3", 4f);
            yield return new WaitForSeconds(0.3f); // Longer recovery
            boxingGloveSprite.SetActive(false);
            currentJabCount = 0;
        }

        // Window to continue combo or end
        float windowTimer = 0.2f;
        while(windowTimer > 0)
        {
            if (Input.GetKeyDown(attackKey) && currentJabCount < 3 && currentJabCount > 0)
            {
                StartAction(JabRoutine());
                yield break;
            }
            windowTimer -= Time.deltaTime;
            yield return null;
        }

        currentJabCount = 0;
        ChangeState(FighterState.Idle);
    }

    private IEnumerator ForwardTiltRoutine()
    {
        rb.linearVelocity = Vector2.zero;
        hammerSprite.SetActive(true);
        LogAttack("Forward Tilt", 8f);
        
        yield return new WaitForSeconds(0.3f); // Startup + Active
        
        hammerSprite.SetActive(false);
        yield return new WaitForSeconds(0.2f); // Recovery
        ChangeState(FighterState.Idle);
    }

    private IEnumerator UpTiltRoutine()
    {
        rb.linearVelocity = Vector2.zero;
        spikeHelmetSprite.SetActive(true);
        LogAttack("Up Tilt", 6f);
        
        yield return new WaitForSeconds(0.25f);
        
        spikeHelmetSprite.SetActive(false);
        yield return new WaitForSeconds(0.15f);
        ChangeState(FighterState.Idle);
    }

    private IEnumerator DownTiltRoutine()
    {
        rb.linearVelocity = Vector2.zero;
        bootSprite.SetActive(true);
        LogAttack("Down Tilt", 7f);
        
        yield return new WaitForSeconds(0.2f);
        
        bootSprite.SetActive(false);
        yield return new WaitForSeconds(0.15f);
        ChangeState(FighterState.Idle);
    }

    private IEnumerator DashAttackRoutine()
    {
        // Maintains some forward momentum
        rb.linearVelocity = new Vector2(stats.runSpeed * SPEED_MULTIPLIER * facingDirection * 0.8f, 0);
        boxingGloveSprite.SetActive(true);
        LogAttack("Dash Attack", 9f);

        yield return new WaitForSeconds(0.4f);
        
        boxingGloveSprite.SetActive(false);
        ChangeState(FighterState.Idle);
    }

    private IEnumerator ForwardSmashRoutine()
    {
        rb.linearVelocity = Vector2.zero;
        // In a real game, you would add a charge frame here if holding attack
        yield return new WaitForSeconds(0.2f); // Startup

        hammerSprite.SetActive(true);
        LogAttack("Forward Smash", 30f);
        
        yield return new WaitForSeconds(0.3f); // Active
        
        hammerSprite.SetActive(false);
        yield return new WaitForSeconds(0.4f); // Heavy recovery
        ChangeState(FighterState.Idle);
    }

    private IEnumerator UpSmashRoutine()
    {
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.15f);

        upBoxingGloveSprite.SetActive(true);
        LogAttack("Up Smash", 27.5f);

        yield return new WaitForSeconds(0.3f);

        upBoxingGloveSprite.SetActive(false);
        yield return new WaitForSeconds(0.35f);
        ChangeState(FighterState.Idle);
    }

    private IEnumerator DownSmashRoutine()
    {
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.15f);

        leftBoxingGloveSprite.SetActive(true);
        rightBoxingGloveSprite.SetActive(true);
        LogAttack("Down Smash", 25f); // 25% on both sides

        yield return new WaitForSeconds(0.3f);

        leftBoxingGloveSprite.SetActive(false);
        rightBoxingGloveSprite.SetActive(false);
        yield return new WaitForSeconds(0.35f);
        ChangeState(FighterState.Idle);
    }

    private void DisableAllHitboxes()
    {
        boxingGloveSprite?.SetActive(false);
        hammerSprite?.SetActive(false);
        spikeHelmetSprite?.SetActive(false);
        bootSprite?.SetActive(false);
        upBoxingGloveSprite?.SetActive(false);
        leftBoxingGloveSprite?.SetActive(false);
        rightBoxingGloveSprite?.SetActive(false);
        shieldBubble?.SetActive(false);
    }

    // Helper to visualize that attacks have triggered with correct damage
    private void LogAttack(string attackName, float damageAmount)
    {
        Debug.Log($"{playerNumber} performed {attackName}! Deals {damageAmount}% damage.");
        // Normally, you would assign the damage value to a Hitbox component on the active sprite here.
    }
}