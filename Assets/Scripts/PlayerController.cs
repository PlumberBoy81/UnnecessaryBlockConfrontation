using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerNumber { Player1_Red, Player2_Blue }
    public enum PlayerState 
    { 
        Idle, Walk, InitialDash, Run, 
        JumpSquat, InAir, Freefall, 
        Shield, Roll, SpotDodge, AirDodge, 
        Jab1, Jab2, Jab3, ForwardTilt, UpTilt, DownTilt, DashAttack, ForwardSmash, UpSmash, DownSmash, Hitstun 
    }

    [Header("Player Settings")]
    public PlayerNumber playerNumber;
    
    // Internal State
    public PlayerState currentState = PlayerState.InAir;
    public float currentDamage = 0f; // Smash % damage
    public int facingDirection = 1; // 1 for right, -1 for left
    
    [Header("Visuals (Auto-generated if left empty)")]
    public SpriteRenderer spriteRenderer;
    public GameObject shieldBubble;
    public GameObject boxingGloveSprite; // Used for Jab
    public GameObject hammerSprite; // Used for F-Tilt
    public GameObject spikeHelmetSprite; // Used for U-Tilt
    public GameObject bootSprite; // Used for D-Tilt
    public GameObject upBoxingGloveSprite; // Used for U-Smash
    public GameObject backBoxingGloveSprite; // Used for D-Smash

    [Header("Fighter Stats")]
    public FighterStats stats;

    [System.Serializable]
    public struct FighterStats
    {
        public float weight;
        public float initialDashSpeed;
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
    }

    // Physics & Component References
    private Rigidbody2D rb;
    private BoxCollider2D boxCol;

    // Trackers
    private int stateFrameTimer = 0;
    private int attackWindowTimer = 0;
    private bool isGrounded = false;
    private bool hasDoubleJump = true;
    private bool hasAirDodged = false;
    private bool isFastFalling = false;
    
    // Inputs
    private float inputX;
    private float inputY;
    private bool jumpPressed;
    private bool jumpHeld;
    private bool walkModHeld;
    private bool shieldHeld;
    private bool attackPressed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCol = GetComponent<BoxCollider2D>();
        
        // Prevent Unity's physics from interfering with our custom Smash physics
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        SetupPlayer();
        SetupVisuals();
    }

    private void SetupPlayer()
    {
        if (playerNumber == PlayerNumber.Player1_Red)
        {
            gameObject.name = "Player 1 (Red)";
            stats = new FighterStats {
                weight = 98f, initialDashSpeed = 1.936f, runSpeed = 1.76f, walkSpeed = 1.155f,
                traction = 0.102f, airFriction = 0.015f, airSpeed = 1.208f, baseAirAcceleration = 0.01f,
                additionalAirAcceleration = 0.07f, gravity = 0.087f, fallSpeed = 1.5f, fastFallSpeed = 2.4f,
                jumpsquatFrames = 3, jumpHeight = 2.51f, shortHopHeight = 1.75f, doubleJumpHeight = 2.51f
            };
        }
        else
        {
            gameObject.name = "Player 2 (Blue)";
            stats = new FighterStats {
                weight = 86f, initialDashSpeed = 2.31f, runSpeed = 3.85f, walkSpeed = 1.444f,
                traction = 0.138f, airFriction = 0.01f, airSpeed = 1.208f, baseAirAcceleration = 0.01f,
                additionalAirAcceleration = 0.04f, gravity = 0.09f, fallSpeed = 1.65f, fastFallSpeed = 2.64f,
                jumpsquatFrames = 3, jumpHeight = 2.51f, shortHopHeight = 1.74f, doubleJumpHeight = 2.51f
            };
        }
    }

    private void SetupVisuals()
    {
        // Auto-generate sprites if not assigned
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            Texture2D tex = new Texture2D(1, 1);
            
            Color playerColor = Color.white;
            if (playerNumber == PlayerNumber.Player1_Red)
                ColorUtility.TryParseHtmlString("#800000", out playerColor);
            else
                ColorUtility.TryParseHtmlString("#000080", out playerColor);
                
            tex.SetPixel(0, 0, playerColor);
            tex.Apply();
            spriteRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            transform.localScale = new Vector3(2, 2, 1);
        }

        if (shieldBubble == null)
        {
            shieldBubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shieldBubble.transform.SetParent(transform);
            shieldBubble.transform.localPosition = Vector3.zero;
            shieldBubble.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            Destroy(shieldBubble.GetComponent<Collider>());
            shieldBubble.GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 1f, 0.4f); // Transparent pink
            shieldBubble.SetActive(false);
        }

        if (boxingGloveSprite == null) CreateHitboxVisual(ref boxingGloveSprite, "Boxing Glove", Color.red, new Vector3(0.8f, 0.4f, 1f), new Vector3(1f, 0, 0));
        if (hammerSprite == null) CreateHitboxVisual(ref hammerSprite, "Hammer", new Color(0.6f, 0.3f, 0f), new Vector3(1.5f, 0.5f, 1f), new Vector3(1.2f, 0, 0));
        if (spikeHelmetSprite == null) CreateHitboxVisual(ref spikeHelmetSprite, "Spike Helmet", Color.gray, new Vector3(1.2f, 0.8f, 1f), new Vector3(0, 0.8f, 0)); // Positioned above
        if (bootSprite == null) CreateHitboxVisual(ref bootSprite, "Boot", Color.black, new Vector3(1f, 0.4f, 1f), new Vector3(1f, -0.6f, 0)); // Positioned low
        if (upBoxingGloveSprite == null) CreateHitboxVisual(ref upBoxingGloveSprite, "Up Boxing Glove", Color.red, new Vector3(1.2f, 2.4f, 1f), new Vector3(0, 1.5f, 0)); // Positioned high and large
        if (backBoxingGloveSprite == null) CreateHitboxVisual(ref backBoxingGloveSprite, "Back Boxing Glove", Color.red, new Vector3(0.8f, 0.4f, 1f), new Vector3(-1f, 0, 0)); // Positioned behind
    }

    private void CreateHitboxVisual(ref GameObject obj, string name, Color color, Vector3 scale, Vector3 localPosition)
    {
        obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(transform);
        obj.transform.localPosition = localPosition;
        obj.transform.localScale = scale;
        Destroy(obj.GetComponent<Collider>());
        obj.GetComponent<Renderer>().material.color = color;
        obj.SetActive(false);
    }

    void Update()
    {
        GatherInputs();
        CheckGrounded();
        FlipSprite();
    }

    void FixedUpdate()
    {
        stateFrameTimer++;
        ProcessState();
    }

    private void GatherInputs()
    {
        if (playerNumber == PlayerNumber.Player1_Red)
        {
            inputX = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            inputY = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            jumpPressed = Input.GetKeyDown(KeyCode.W);
            jumpHeld = Input.GetKey(KeyCode.W);
            walkModHeld = Input.GetKey(KeyCode.LeftShift);
            shieldHeld = Input.GetKey(KeyCode.E);
            attackPressed = Input.GetKeyDown(KeyCode.F);
        }
        else
        {
            inputX = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            inputY = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            jumpPressed = Input.GetKeyDown(KeyCode.UpArrow);
            jumpHeld = Input.GetKey(KeyCode.UpArrow);
            walkModHeld = Input.GetKey(KeyCode.RightShift);
            shieldHeld = Input.GetKey(KeyCode.RightControl);
            attackPressed = Input.GetKeyDown(KeyCode.Space);
        }
    }

    private void CheckGrounded()
    {
        // Use BoxCastAll to check below the player, then filter out the player's own collider
        RaycastHit2D[] hits = Physics2D.BoxCastAll(boxCol.bounds.center, boxCol.bounds.size * 0.9f, 0f, Vector2.down, 0.1f);
        
        bool hitGround = false;
        foreach (var h in hits)
        {
            if (h.collider != null && h.collider.gameObject != gameObject && !h.collider.isTrigger)
            {
                hitGround = true;
                break;
            }
        }

        bool wasGrounded = isGrounded;
        isGrounded = hitGround;

        if (isGrounded && !wasGrounded)
        {
            hasDoubleJump = true;
            hasAirDodged = false;
            isFastFalling = false;
            
            // Wavedash Slide Logic
            // If we land while air dodging, we maintain horizontal momentum and slide (Wavedash)
            if (currentState == PlayerState.AirDodge) 
            {
                ChangeState(PlayerState.Idle);
                // Keep the massive velocity from the air dodge to slide on the ground
            }
            else if (currentState == PlayerState.InAir || currentState == PlayerState.Freefall)
            {
                ChangeState(PlayerState.Idle);
            }
        }
        else if (!isGrounded && wasGrounded && currentState != PlayerState.JumpSquat && currentState != PlayerState.InAir && currentState != PlayerState.Freefall)
        {
            // Walked off a ledge!
            ChangeState(PlayerState.InAir);
        }
    }

    private void FlipSprite()
    {
        if (currentState == PlayerState.Hitstun || currentState == PlayerState.Jab1 || currentState == PlayerState.Jab2 || currentState == PlayerState.Jab3 || currentState == PlayerState.ForwardTilt || currentState == PlayerState.UpTilt || currentState == PlayerState.DownTilt || currentState == PlayerState.DashAttack || currentState == PlayerState.ForwardSmash || currentState == PlayerState.UpSmash || currentState == PlayerState.DownSmash) return;
        
        if (inputX > 0) facingDirection = 1;
        else if (inputX < 0) facingDirection = -1;

        spriteRenderer.flipX = (facingDirection == -1);
        
        // Flip attack hitboxes visually
        boxingGloveSprite.transform.localPosition = new Vector3(1f * facingDirection, 0, 0);
        hammerSprite.transform.localPosition = new Vector3(1.2f * facingDirection, 0, 0);
        bootSprite.transform.localPosition = new Vector3(1f * facingDirection, -0.6f, 0);
        backBoxingGloveSprite.transform.localPosition = new Vector3(-1f * facingDirection, 0, 0);
        // Spike helmet stays on top, no need to flip its X position
    }

    private void ChangeState(PlayerState newState)
    {
        currentState = newState;
        stateFrameTimer = 0;
        shieldBubble.SetActive(newState == PlayerState.Shield);
        
        boxingGloveSprite.SetActive(newState == PlayerState.Jab1 || newState == PlayerState.Jab2 || newState == PlayerState.Jab3 || newState == PlayerState.DashAttack || newState == PlayerState.DownSmash);
        hammerSprite.SetActive(newState == PlayerState.ForwardTilt || newState == PlayerState.ForwardSmash);
        spikeHelmetSprite.SetActive(newState == PlayerState.UpTilt);
        bootSprite.SetActive(newState == PlayerState.DownTilt);
        upBoxingGloveSprite.SetActive(newState == PlayerState.UpSmash);
        backBoxingGloveSprite.SetActive(newState == PlayerState.DownSmash);
    }

    private void ProcessState()
    {
        Vector2 vel = rb.velocity;

        switch (currentState)
        {
            case PlayerState.Idle:
                ApplyTraction(ref vel);
                
                if (attackPressed) 
                {
                    if (walkModHeld && inputY > 0) ChangeState(PlayerState.UpTilt);
                    else if (inputY > 0) ChangeState(PlayerState.UpSmash);
                    else if (walkModHeld && inputY < 0) ChangeState(PlayerState.DownTilt);
                    else if (inputY < 0) ChangeState(PlayerState.DownSmash);
                    else if (walkModHeld && Mathf.Abs(inputX) > 0) ChangeState(PlayerState.ForwardTilt);
                    else if (Mathf.Abs(inputX) > 0) ChangeState(PlayerState.ForwardSmash);
                    else ChangeState(PlayerState.Jab1);
                }
                else if (shieldHeld) ChangeState(PlayerState.Shield);
                else if (jumpPressed) ChangeState(PlayerState.JumpSquat);
                else if (Mathf.Abs(inputX) > 0)
                {
                    if (walkModHeld) ChangeState(PlayerState.Walk);
                    else ChangeState(PlayerState.InitialDash);
                }
                break;

            case PlayerState.Walk:
                vel.x = inputX * stats.walkSpeed;
                
                if (attackPressed)
                {
                    if (walkModHeld && inputY > 0) ChangeState(PlayerState.UpTilt);
                    else if (inputY > 0) ChangeState(PlayerState.UpSmash);
                    else if (walkModHeld && inputY < 0) ChangeState(PlayerState.DownTilt);
                    else if (inputY < 0) ChangeState(PlayerState.DownSmash);
                    else if (walkModHeld && Mathf.Abs(inputX) > 0) ChangeState(PlayerState.ForwardTilt);
                    else if (Mathf.Abs(inputX) > 0) ChangeState(PlayerState.ForwardSmash);
                    else ChangeState(PlayerState.Jab1);
                }
                else if (jumpPressed) ChangeState(PlayerState.JumpSquat);
                else if (shieldHeld) ChangeState(PlayerState.Shield);
                else if (inputX == 0) ChangeState(PlayerState.Idle);
                else if (!walkModHeld) ChangeState(PlayerState.InitialDash);
                break;

            case PlayerState.InitialDash:
                vel.x = facingDirection * stats.initialDashSpeed;
                
                if (attackPressed) ChangeState(PlayerState.DashAttack);
                // Dashdance: Ficking the opposite direction during the initial 15 frame burst
                else if (inputX != 0 && inputX != facingDirection)
                {
                    facingDirection = (int)Mathf.Sign(inputX);
                    ChangeState(PlayerState.InitialDash); 
                }
                else if (jumpPressed) ChangeState(PlayerState.JumpSquat);
                else if (shieldHeld) ChangeState(PlayerState.Shield); // Shield stop
                else if (stateFrameTimer >= 15) // Initial dash frames
                {
                    if (inputX != 0) ChangeState(PlayerState.Run);
                    else ChangeState(PlayerState.Idle);
                }
                break;

            case PlayerState.Run:
                vel.x = inputX * stats.runSpeed;
                
                if (attackPressed) ChangeState(PlayerState.DashAttack);
                else if (jumpPressed) ChangeState(PlayerState.JumpSquat);
                else if (shieldHeld) ChangeState(PlayerState.Shield);
                else if (inputX == 0) ChangeState(PlayerState.Idle);
                break;

            case PlayerState.JumpSquat:
                ApplyTraction(ref vel);
                
                if (stateFrameTimer >= stats.jumpsquatFrames)
                {
                    ChangeState(PlayerState.InAir);
                    // Determine if short hop or full hop
                    vel.y = jumpHeld ? stats.jumpHeight : stats.shortHopHeight; 
                }
                break;

            case PlayerState.InAir:
                ProcessAirPhysics(ref vel);
                ProcessAirActions(ref vel);
                break;

            case PlayerState.Freefall:
                ProcessAirPhysics(ref vel);
                // Helpless, no actions allowed until landing
                break;

            case PlayerState.Shield:
                ApplyTraction(ref vel); // Slide slightly if carrying momentum into shield
                
                if (!shieldHeld) ChangeState(PlayerState.Idle);
                else if (jumpPressed) ChangeState(PlayerState.JumpSquat); // Jump out of shield
                else if (inputY < 0) ChangeState(PlayerState.SpotDodge);
                else if (Mathf.Abs(inputX) > 0) ChangeState(PlayerState.Roll);
                break;

            case PlayerState.SpotDodge:
                vel = Vector2.zero; // Intangible spin
                if (stateFrameTimer >= 25) ChangeState(PlayerState.Idle); // 25 frames duration
                break;

            case PlayerState.Roll:
                vel.x = facingDirection * (stats.runSpeed * 1.5f); // Fast roll speed
                vel.y = 0;
                if (stateFrameTimer >= 30) ChangeState(PlayerState.Idle); // 30 frames duration
                break;

            case PlayerState.AirDodge:
                // Directional or Neutral
                if (stateFrameTimer == 1)
                {
                    if (inputX != 0 || inputY != 0)
                    {
                        // Directional air dodge burst
                        Vector2 dodgeDir = new Vector2(inputX, inputY).normalized;
                        vel = dodgeDir * (stats.airSpeed * 3.5f); 
                    }
                    else
                    {
                        // Neutral air dodge stalls momentum briefly
                        vel = Vector2.zero; 
                    }
                }
                
                // Momentum tapers off, enter freefall
                if (stateFrameTimer > 10) 
                {
                    vel = Vector2.Lerp(vel, Vector2.zero, 0.1f);
                }
                if (stateFrameTimer >= 35) ChangeState(PlayerState.Freefall);
                break;

            case PlayerState.Jab1:
                ProcessAttack(ref vel, 2f, 15, PlayerState.Jab2);
                break;
            case PlayerState.Jab2:
                ProcessAttack(ref vel, 2f, 15, PlayerState.Jab3);
                break;
            case PlayerState.Jab3:
                ProcessAttack(ref vel, 4f, 25, PlayerState.Idle);
                break;

            case PlayerState.ForwardTilt:
                ProcessAttack(ref vel, 8f, 30, PlayerState.Idle);
                break;

            case PlayerState.UpTilt:
                // Fast anti-air, 20 frames total
                ProcessAttack(ref vel, 6f, 20, PlayerState.Idle);
                break;

            case PlayerState.DownTilt:
                // Fast low kick combo starter, 20 frames total
                ProcessAttack(ref vel, 7f, 20, PlayerState.Idle);
                break;

            case PlayerState.DashAttack:
                // Running punch, 35 frames total, momentum carries over automatically
                ProcessAttack(ref vel, 9f, 35, PlayerState.Idle);
                break;

            case PlayerState.ForwardSmash:
                // Powerful, slow hammer strike
                ProcessAttack(ref vel, 30f, 50, PlayerState.Idle);
                break;

            case PlayerState.UpSmash:
                // Powerful, slow upward punch
                ProcessAttack(ref vel, 27.5f, 48, PlayerState.Idle);
                break;

            case PlayerState.DownSmash:
                // Strikes both sides simultaneously
                ProcessAttack(ref vel, 25f, 45, PlayerState.Idle);
                break;

            case PlayerState.Hitstun:
                // Knockback decay
                vel.x = Mathf.Lerp(vel.x, 0, stats.airFriction);
                vel.y -= stats.gravity;
                if (stateFrameTimer > currentDamage * 1.5f) // Hitstun scales with damage
                {
                    ChangeState(isGrounded ? PlayerState.Idle : PlayerState.InAir);
                }
                break;
        }

        rb.velocity = vel;
    }

    private void ProcessAirPhysics(ref Vector2 vel)
    {
        // Gravity & Fast Falling
        if (inputY < 0 && vel.y <= 0 && !isFastFalling && currentState != PlayerState.Freefall)
        {
            isFastFalling = true;
        }

        float maxFall = isFastFalling ? stats.fastFallSpeed : stats.fallSpeed;
        vel.y -= stats.gravity;
        if (vel.y < -maxFall) vel.y = -maxFall;

        // Horizontal Air Mobility
        if (inputX != 0)
        {
            float targetSpeed = inputX * stats.airSpeed;
            float accel = stats.baseAirAcceleration + (Mathf.Abs(inputX) * stats.additionalAirAcceleration);
            vel.x = Mathf.MoveTowards(vel.x, targetSpeed, accel);
        }
        else
        {
            vel.x = Mathf.MoveTowards(vel.x, 0, stats.airFriction);
        }
    }

    private void ProcessAirActions(ref Vector2 vel)
    {
        // Double Jump
        if (jumpPressed && hasDoubleJump)
        {
            hasDoubleJump = false;
            isFastFalling = false;
            vel.y = stats.doubleJumpHeight;
            // Preserves horizontal momentum
        }

        // Air Dodge
        if (shieldHeld && !hasAirDodged)
        {
            hasAirDodged = true;
            ChangeState(PlayerState.AirDodge);
        }
    }

    private void ApplyTraction(ref Vector2 vel)
    {
        // Smoothly stop the character (Critical for wavedash sliding!)
        vel.x = Mathf.MoveTowards(vel.x, 0, stats.traction);
    }

    private void ProcessAttack(ref Vector2 vel, float damageDone, int totalFrames, PlayerState nextComboState)
    {
        ApplyTraction(ref vel); // Halt momentum during attacks
        
        // Setup timing: Smash attacks are slower to come out
        int hitStart = (currentState == PlayerState.ForwardSmash || currentState == PlayerState.UpSmash || currentState == PlayerState.DownSmash) ? 14 : 3;
        int hitEnd = (currentState == PlayerState.ForwardSmash || currentState == PlayerState.UpSmash || currentState == PlayerState.DownSmash) ? 22 : 8;
        int hitCheck = hitStart + 1;

        // Attack hitboxes are active based on the move's frame data
        bool hitboxActive = stateFrameTimer >= hitStart && stateFrameTimer <= hitEnd;
        
        GameObject[] activeHitboxes = new GameObject[] { boxingGloveSprite }; // Default

        if (currentState == PlayerState.ForwardTilt || currentState == PlayerState.ForwardSmash) activeHitboxes = new GameObject[] { hammerSprite };
        else if (currentState == PlayerState.UpTilt) activeHitboxes = new GameObject[] { spikeHelmetSprite };
        else if (currentState == PlayerState.DownTilt) activeHitboxes = new GameObject[] { bootSprite };
        else if (currentState == PlayerState.UpSmash) activeHitboxes = new GameObject[] { upBoxingGloveSprite };
        else if (currentState == PlayerState.DownSmash) activeHitboxes = new GameObject[] { boxingGloveSprite, backBoxingGloveSprite };

        if (currentState == PlayerState.ForwardTilt || currentState == PlayerState.ForwardSmash) hammerSprite.SetActive(hitboxActive);
        else if (currentState == PlayerState.UpTilt) spikeHelmetSprite.SetActive(hitboxActive);
        else if (currentState == PlayerState.DownTilt) bootSprite.SetActive(hitboxActive);
        else if (currentState == PlayerState.UpSmash) upBoxingGloveSprite.SetActive(hitboxActive);
        else if (currentState == PlayerState.DownSmash) 
        {
            boxingGloveSprite.SetActive(hitboxActive);
            backBoxingGloveSprite.SetActive(hitboxActive);
        }
        else boxingGloveSprite.SetActive(hitboxActive);

        // Perform OverlapBox check to hit opponent
        if (hitboxActive && stateFrameTimer == hitCheck) 
        {
            System.Collections.Generic.HashSet<PlayerController> alreadyHit = new System.Collections.Generic.HashSet<PlayerController>();

            foreach (GameObject hitbox in activeHitboxes)
            {
                Collider2D[] hits = Physics2D.OverlapBoxAll(
                    hitbox.transform.position,
                    hitbox.transform.localScale,
                    0f
                );

                foreach (Collider2D hit in hits)
                {
                    PlayerController opponent = hit.GetComponent<PlayerController>();
                    if (opponent != null && opponent != this && !alreadyHit.Contains(opponent) && opponent.currentState != PlayerState.SpotDodge && opponent.currentState != PlayerState.Roll && opponent.currentState != PlayerState.AirDodge)
                    {
                        alreadyHit.Add(opponent);
                        bool isVerticalKnockback = currentState == PlayerState.UpTilt || currentState == PlayerState.DownTilt || currentState == PlayerState.UpSmash;
                        
                        // For Down Smash, knock them away based on which glove hit them
                        int dir = facingDirection;
                        if (currentState == PlayerState.DownSmash && hitbox == backBoxingGloveSprite) dir = -facingDirection;

                        opponent.TakeDamage(damageDone, dir, isVerticalKnockback);
                    }
                }
            }
        }

        // Jab Combo linking logic
        if (nextComboState != PlayerState.Idle && attackPressed && stateFrameTimer > 8)
        {
            attackWindowTimer = 1; // Flag that input was pressed early
        }

        if (stateFrameTimer >= totalFrames)
        {
            if (attackWindowTimer == 1)
            {
                attackWindowTimer = 0;
                ChangeState(nextComboState);
            }
            else
            {
                ChangeState(PlayerState.Idle);
            }
        }
    }

    public void TakeDamage(float damageAmount, int attackDirection, bool isVertical = false)
    {
        currentDamage += damageAmount;
        
        // Calculate knockback heavily inspired by Smash weight formula
        float knockbackForce = (currentDamage / 10f) * (200f / stats.weight) + (damageAmount * 2f);
        
        if (isVertical)
        {
            // Up tilts send opponents mostly upwards for combo potential
            rb.velocity = new Vector2(attackDirection * knockbackForce * 0.15f, knockbackForce * 1.1f);
        }
        else
        {
            // Standard attacks send up and away
            rb.velocity = new Vector2(attackDirection * knockbackForce, knockbackForce * 0.5f); 
        }
        
        ChangeState(PlayerState.Hitstun);
        
        Debug.Log($"{gameObject.name} took {damageAmount}% damage! Total: {currentDamage}%");
    }
}