using UnityEngine;
using System.Collections;


[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerID { Player1_Red, Player2_Blue }
   
    [Header("Player Settings")]
    public PlayerID playerID;


    // --- CHARACTER STATS ---
    [Header("Stats")]
    public float weight;
    public float initialDash;
    public float runSpeed;
    public float walkSpeed;
    public float traction;
    public float airFriction;
    public float airSpeed;
    public float baseAirAcceleration;
    public float additionalAirAcceleration;
    public float gravity;
    public float fallSpeed;
    public float fastFallSpeed;
    public int jumpsquatFrames;
    public float jumpHeight;
    public float shortHopHeight;
    public float doubleJumpHeight;


    // --- INPUT KEYS ---
    private KeyCode keyUp, keyDown, keyLeft, keyRight;
    private KeyCode keyWalkModifier;
    private KeyCode keyShieldDodge;
    private KeyCode keyAttack;


    // --- STATE MANAGEMENT ---
    public enum FighterState
    {
        Idle, Walking, InitialDash, Running, Jumpsquat, Airborne,
        Shielding, Rolling, SpotDodging, AirDodging, Attacking, Hitstun
    }
    public FighterState currentState = FighterState.Idle;


    // --- HITBOX VISUALS & OBJECTS ---
    [Header("Weapon GameObjects / Hitboxes")]
    public GameObject boxingGloveSprite;
    public GameObject hammerSprite;
    public GameObject spikeHelmetSprite;
    public GameObject bootSprite;
    public GameObject upBoxingGloveSprite;
    public GameObject leftBoxingGloveSprite; // For Down Smash
    public GameObject rightBoxingGloveSprite; // For Down Smash
    public GameObject shieldBubbleSprite;


    // --- INTERNAL TRACKING ---
    private Rigidbody2D rb;
    private int frameCounter = 0;
    private int stateFrameTimer = 0;
   
    // Jump tracking
    private bool isGrounded = false;
    private bool hasDoubleJump = true;
    private bool isFastFalling = false;
    private bool jumpButtonHeldDuringJumpsquat = false;


    // Attack / Combo tracking
    private int jabComboStep = 0;
    private float lastAttackTime = 0f;
    private float attackWindow = 0.4f; // Time allowed to press attack again for next jab


    // Input Timers (for Smash vs Tilt detection)
    private float leftPressedTime = -1f;
    private float rightPressedTime = -1f;
    private float upPressedTime = -1f;
    private float downPressedTime = -1f;
    private float smashTapWindow = 0.1f; // ~6 frames to tap and press attack for a smash


    // Facing direction (1 for right, -1 for left)
    private int facingDirection = 1;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
       
        // Disable Unity's default gravity, this script simulates Smash gravity manually in FixedUpdate
        rb.gravityScale = 0f;
       
        AssignStats();
        AssignInputs();
        DisableAllHitboxes();
    }


    void AssignStats()
    {
        if (playerID == PlayerID.Player1_Red)
        {
            weight = 98f;
            initialDash = 1.936f;
            runSpeed = 1.76f;
            walkSpeed = 1.155f;
            traction = 0.102f;
            airFriction = 0.015f;
            airSpeed = 1.208f;
            baseAirAcceleration = 0.01f;
            additionalAirAcceleration = 0.07f;
            gravity = 0.087f;
            fallSpeed = 1.5f;
            fastFallSpeed = 2.4f;
            jumpsquatFrames = 3;
            jumpHeight = 3.633f;
            shortHopHeight = 1.754f;
            doubleJumpHeight = 3.633f;
        }
        else if (playerID == PlayerID.Player2_Blue)
        {
            weight = 86f;
            initialDash = 2.31f;
            runSpeed = 3.85f;
            walkSpeed = 1.444f;
            traction = 0.138f;
            airFriction = 0.01f;
            airSpeed = 1.208f;
            baseAirAcceleration = 0.01f;
            additionalAirAcceleration = 0.04f;
            gravity = 0.09f;
            fallSpeed = 1.65f;
            fastFallSpeed = 2.64f;
            jumpsquatFrames = 3;
            jumpHeight = 3.5f;
            shortHopHeight = 1.689f;
            doubleJumpHeight = 3.5f;
        }
    }


    void AssignInputs()
    {
        if (playerID == PlayerID.Player1_Red)
        {
            keyUp = KeyCode.W;
            keyDown = KeyCode.S;
            keyLeft = KeyCode.A;
            keyRight = KeyCode.D;
            keyWalkModifier = KeyCode.LeftShift;
            keyShieldDodge = KeyCode.E;
            keyAttack = KeyCode.F;
        }
        else
        {
            keyUp = KeyCode.UpArrow;
            keyDown = KeyCode.DownArrow;
            keyLeft = KeyCode.LeftArrow;
            keyRight = KeyCode.RightArrow;
            keyWalkModifier = KeyCode.RightShift;
            keyShieldDodge = KeyCode.RightControl;
            keyAttack = KeyCode.Space;
        }
    }


    void Update()
    {
        // Track how recently direction keys were pressed for Smash Attack detection
        if (Input.GetKeyDown(keyLeft)) leftPressedTime = Time.time;
        if (Input.GetKeyDown(keyRight)) rightPressedTime = Time.time;
        if (Input.GetKeyDown(keyUp)) upPressedTime = Time.time;
        if (Input.GetKeyDown(keyDown)) downPressedTime = Time.time;


        HandleInput();
    }


    void HandleInput()
    {
        if (currentState == FighterState.Hitstun || currentState == FighterState.Attacking ||
            currentState == FighterState.Rolling || currentState == FighterState.SpotDodging ||
            currentState == FighterState.AirDodging)
        {
            // Lock out most inputs during these committed states
            return;
        }


        bool attackPressed = Input.GetKeyDown(keyAttack);
        bool shieldPressed = Input.GetKeyDown(keyShieldDodge);
        bool shieldHeld = Input.GetKey(keyShieldDodge);
        bool walkModHeld = Input.GetKey(keyWalkModifier);
       
        float xInput = 0f;
        if (Input.GetKey(keyRight)) xInput += 1f;
        if (Input.GetKey(keyLeft)) xInput -= 1f;
       
        float yInput = 0f;
        if (Input.GetKey(keyUp)) yInput += 1f;
        if (Input.GetKey(keyDown)) yInput -= 1f;


        // --- ATTACKS ---
        if (attackPressed && isGrounded)
        {
            DetermineGroundAttack(xInput, yInput, walkModHeld);
            return;
        }


        // --- DEFENSE (Shields & Dodges) ---
        if (isGrounded)
        {
            if (shieldHeld && !attackPressed)
            {
                if (Input.GetKeyDown(keyDown))
                {
                    StartCoroutine(SpotDodgeRoutine());
                    return;
                }
                else if (Input.GetKeyDown(keyLeft) || Input.GetKeyDown(keyRight))
                {
                    StartCoroutine(RollDodgeRoutine(Input.GetKeyDown(keyLeft) ? -1 : 1));
                    return;
                }
                else if (currentState != FighterState.Shielding)
                {
                    ChangeState(FighterState.Shielding);
                }
            }
            else if (currentState == FighterState.Shielding && !shieldHeld)
            {
                ChangeState(FighterState.Idle);
                if(shieldBubbleSprite) shieldBubbleSprite.SetActive(false);
            }
        }
        else if (!isGrounded && shieldPressed) // Air Dodge
        {
            StartCoroutine(AirDodgeRoutine(xInput, yInput));
            return;
        }


        // Lock out movement if shielding
        if (currentState == FighterState.Shielding)
        {
            if (shieldBubbleSprite) shieldBubbleSprite.SetActive(true);
            return;
        }


        // --- JUMPING ---
        if (Input.GetKeyDown(keyUp))
        {
            if (isGrounded)
            {
                ChangeState(FighterState.Jumpsquat);
            }
            else if (hasDoubleJump)
            {
                ExecuteDoubleJump();
            }
        }


        // --- FAST FALLING ---
        if (!isGrounded && Input.GetKeyDown(keyDown) && rb.linearVelocity.y <= 0)
        {
            isFastFalling = true;
        }
    }


    void FixedUpdate()
    {
        frameCounter++;
        CheckGrounded();
        Vector2 currentVelocity = rb.linearVelocity;


        float xInput = 0f;
        if (Input.GetKey(keyRight)) xInput += 1f;
        if (Input.GetKey(keyLeft)) xInput -= 1f;


        // State machine logic for movement
        switch (currentState)
        {
            case FighterState.Idle:
                // Apply Traction to slide to a halt
                currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, 0, traction);
               
                if (xInput != 0 && isGrounded)
                {
                    facingDirection = xInput > 0 ? 1 : -1;
                    if (Input.GetKey(keyWalkModifier))
                    {
                        ChangeState(FighterState.Walking);
                    }
                    else
                    {
                        // Start Dash
                        ChangeState(FighterState.InitialDash);
                        currentVelocity.x = initialDash * facingDirection;
                    }
                }
                break;


            case FighterState.Walking:
                if (xInput == 0) ChangeState(FighterState.Idle);
                else
                {
                    facingDirection = xInput > 0 ? 1 : -1;
                    currentVelocity.x = walkSpeed * facingDirection;
                    if (!Input.GetKey(keyWalkModifier)) ChangeState(FighterState.InitialDash);
                }
                break;


            case FighterState.InitialDash:
                stateFrameTimer++;
               
                // DASHDANCE: If you press the opposite direction during Initial Dash, instantly dash back
                if (xInput != 0 && xInput != facingDirection)
                {
                    facingDirection = xInput > 0 ? 1 : -1;
                    ChangeState(FighterState.InitialDash); // Reset dash
                    currentVelocity.x = initialDash * facingDirection;
                }
                else
                {
                    currentVelocity.x = initialDash * facingDirection;
                    if (stateFrameTimer >= 15) // 15 frames of initial dash
                    {
                        if (xInput != 0) ChangeState(FighterState.Running);
                        else ChangeState(FighterState.Idle);
                    }
                }
                break;


            case FighterState.Running:
                if (xInput == 0) ChangeState(FighterState.Idle);
                else if (xInput != facingDirection)
                {
                    // Turnaround skid (simplified to idle with traction)
                    ChangeState(FighterState.Idle);
                }
                else
                {
                    currentVelocity.x = runSpeed * facingDirection;
                }
                break;


            case FighterState.Jumpsquat:
                stateFrameTimer++;
                // Apply traction while in jumpsquat (you slide slightly if carrying momentum)
                currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, 0, traction);
               
                // Track if jump is held for full hop
                if (Input.GetKey(keyUp)) jumpButtonHeldDuringJumpsquat = true;


                if (stateFrameTimer >= jumpsquatFrames)
                {
                    ExecuteJump();
                }
                break;


            case FighterState.Airborne:
                // Horizontal Air Movement
                if (xInput != 0)
                {
                    // Apply air acceleration based on how hard input is (1.0 for keyboard)
                    float accel = baseAirAcceleration + (additionalAirAcceleration * Mathf.Abs(xInput));
                    currentVelocity.x += accel * xInput;
                   
                    // Cap at air speed
                    currentVelocity.x = Mathf.Clamp(currentVelocity.x, -airSpeed, airSpeed);
                }
                else
                {
                    // Air friction
                    currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, 0, airFriction);
                }


                // Vertical Air Movement (Gravity)
                if (isFastFalling)
                {
                    currentVelocity.y = -fastFallSpeed;
                }
                else
                {
                    currentVelocity.y -= gravity;
                    if (currentVelocity.y < -fallSpeed) currentVelocity.y = -fallSpeed;
                }
               
                // Landing check (if we fall into the ground)
                if (isGrounded && currentVelocity.y <= 0)
                {
                    ChangeState(FighterState.Idle);
                    isFastFalling = false;
                    hasDoubleJump = true;
                }
                break;
        }


        // Flip character visually
        if (facingDirection == 1) transform.localScale = new Vector3(1, 1, 1);
        else if (facingDirection == -1) transform.localScale = new Vector3(-1, 1, 1);


        rb.linearVelocity = currentVelocity;
    }


    void ExecuteJump()
    {
        ChangeState(FighterState.Airborne);
        isGrounded = false;
       
        float jumpForce = jumpButtonHeldDuringJumpsquat ? jumpHeight : shortHopHeight;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        jumpButtonHeldDuringJumpsquat = false;
    }


    void ExecuteDoubleJump()
    {
        hasDoubleJump = false;
        isFastFalling = false; // Reset fast fall
       
        // Double jump overrides current Y momentum
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, doubleJumpHeight);
        ChangeState(FighterState.Airborne);
    }


    void DetermineGroundAttack(float xInput, float yInput, bool walkModHeld)
    {
        bool isSmashForward = (Time.time - leftPressedTime <= smashTapWindow) || (Time.time - rightPressedTime <= smashTapWindow);
        bool isSmashUp = (Time.time - upPressedTime <= smashTapWindow);
        bool isSmashDown = (Time.time - downPressedTime <= smashTapWindow);


        if (currentState == FighterState.Running)
        {
            StartCoroutine(AttackRoutine("DashAttack", 9f, boxingGloveSprite));
        }
        else if (isSmashUp)
        {
            StartCoroutine(AttackRoutine("UpSmash", 27.5f, upBoxingGloveSprite));
        }
        else if (isSmashDown)
        {
            StartCoroutine(DownSmashRoutine());
        }
        else if (isSmashForward && xInput != 0)
        {
            StartCoroutine(AttackRoutine("ForwardSmash", 30f, hammerSprite));
        }
        else if (walkModHeld && yInput > 0)
        {
            StartCoroutine(AttackRoutine("UpTilt", 6f, spikeHelmetSprite));
        }
        else if (walkModHeld && xInput != 0)
        {
            StartCoroutine(AttackRoutine("ForwardTilt", 8f, hammerSprite));
        }
        else if (yInput < 0)
        {
            StartCoroutine(AttackRoutine("DownTilt", 7f, bootSprite));
        }
        else
        {
            // Jab Combo Logic
            ExecuteJabCombo();
        }
    }


    void ExecuteJabCombo()
    {
        if (Time.time - lastAttackTime > attackWindow)
        {
            jabComboStep = 0; // Reset if too slow
        }


        jabComboStep++;
        lastAttackTime = Time.time;


        if (jabComboStep == 1)
        {
            StartCoroutine(AttackRoutine("Jab1", 2f, boxingGloveSprite, 0.2f));
        }
        else if (jabComboStep == 2)
        {
            StartCoroutine(AttackRoutine("Jab2", 2f, boxingGloveSprite, 0.2f));
        }
        else if (jabComboStep >= 3)
        {
            StartCoroutine(AttackRoutine("Jab3", 4f, boxingGloveSprite, 0.4f));
            jabComboStep = 0; // Reset after finisher
        }
    }


    IEnumerator AttackRoutine(string attackName, float damage, GameObject weaponSprite, float duration = 0.3f)
    {
        ChangeState(FighterState.Attacking);
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); // Stop horizontal momentum on ground attacks
       
        Debug.Log($"{playerID} performed {attackName} for {damage}% damage!");


        if (weaponSprite != null) weaponSprite.SetActive(true);
       
        // Wait for attack duration
        yield return new WaitForSeconds(duration);
       
        if (weaponSprite != null) weaponSprite.SetActive(false);
        ChangeState(FighterState.Idle);
    }


    IEnumerator DownSmashRoutine()
    {
        ChangeState(FighterState.Attacking);
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
       
        Debug.Log($"{playerID} performed Down Smash for 25% damage on both sides!");


        if (leftBoxingGloveSprite != null) leftBoxingGloveSprite.SetActive(true);
        if (rightBoxingGloveSprite != null) rightBoxingGloveSprite.SetActive(true);
       
        yield return new WaitForSeconds(0.4f);
       
        if (leftBoxingGloveSprite != null) leftBoxingGloveSprite.SetActive(false);
        if (rightBoxingGloveSprite != null) rightBoxingGloveSprite.SetActive(false);
       
        ChangeState(FighterState.Idle);
    }


    IEnumerator SpotDodgeRoutine()
    {
        ChangeState(FighterState.SpotDodging);
        if(shieldBubbleSprite) shieldBubbleSprite.SetActive(false);
       
        // Visual indicator of intangibility
        GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.5f);
        rb.linearVelocity = Vector2.zero;
       
        yield return new WaitForSeconds(0.4f); // Invincibility duration
       
        GetComponent<SpriteRenderer>().color = Color.white;
        ChangeState(FighterState.Idle);
    }


    IEnumerator RollDodgeRoutine(int dir)
    {
        ChangeState(FighterState.Rolling);
        if(shieldBubbleSprite) shieldBubbleSprite.SetActive(false);
       
        GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.5f);
       
        float rollSpeed = 6f;
        rb.linearVelocity = new Vector2(rollSpeed * dir, 0);
       
        yield return new WaitForSeconds(0.4f);
       
        rb.linearVelocity = Vector2.zero;
        GetComponent<SpriteRenderer>().color = Color.white;
        ChangeState(FighterState.Idle);
    }


    IEnumerator AirDodgeRoutine(float dirX, float dirY)
    {
        ChangeState(FighterState.AirDodging);
        GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.5f);
       
        rb.linearVelocity = Vector2.zero; // Halt current momentum
       
        if (dirX != 0 || dirY != 0) // Directional Air Dodge
        {
            Vector2 dodgeVelocity = new Vector2(dirX, dirY).normalized * 8f;
            rb.linearVelocity = dodgeVelocity;
           
            // Wait for brief duration while velocity pushes them
            yield return new WaitForSeconds(0.2f);
           
        }
        else // Neutral Air Dodge
        {
            yield return new WaitForSeconds(0.3f);
        }


        GetComponent<SpriteRenderer>().color = Color.white;
       
        if (!isGrounded)
        {
            ChangeState(FighterState.Airborne);
            hasDoubleJump = false; // Usually lose jump after air dodge
        }
    }


    void CheckGrounded()
    {
        // Simple raycast down from the center of the collider to check for "Ground" layer
        // Adjust ray length based on your character sprite size.
        float castDistance = GetComponent<BoxCollider2D>().bounds.extents.y + 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, castDistance, LayerMask.GetMask("Default"));


        if (hit.collider != null)
        {
            if (!isGrounded)
            {
                // We just landed
                isGrounded = true;
                isFastFalling = false;
                hasDoubleJump = true;
               
                if (currentState == FighterState.Airborne || currentState == FighterState.AirDodging)
                {
                    ChangeState(FighterState.Idle);
                    GetComponent<SpriteRenderer>().color = Color.white; 
                }
            }
        }
        else
        {
            if (isGrounded && currentState != FighterState.Jumpsquat)
            {
                // Walked off an edge
                isGrounded = false;
                ChangeState(FighterState.Airborne);
            }
        }
    }


    void DisableAllHitboxes()
    {
        if (boxingGloveSprite) boxingGloveSprite.SetActive(false);
        if (hammerSprite) hammerSprite.SetActive(false);
        if (spikeHelmetSprite) spikeHelmetSprite.SetActive(false);
        if (bootSprite) bootSprite.SetActive(false);
        if (upBoxingGloveSprite) upBoxingGloveSprite.SetActive(false);
        if (leftBoxingGloveSprite) leftBoxingGloveSprite.SetActive(false);
        if (rightBoxingGloveSprite) rightBoxingGloveSprite.SetActive(false);
        if (shieldBubbleSprite) shieldBubbleSprite.SetActive(false);
    }


    void ChangeState(FighterState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        stateFrameTimer = 0;
    }
}