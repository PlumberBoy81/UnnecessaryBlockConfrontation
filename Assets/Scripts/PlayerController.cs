using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PlayerController : MonoBehaviour
{
    public enum PlayerID { Player1_Red, Player2_Blue }
    public PlayerID playerType;

    public enum State { Grounded, Airborne, Shielding, Dodging, Hitstun, ChargingSmash, Helpless};

    [Header("Current State")]
    public State currentState = State.Grounded;

    [Header("Sprites/Objects (Assign in Inspector)")]
    public GameObject boxingGloveSprite;
    public GameObject backBoxingGloveSprite;
    public GameObject hammerSprite;
    public GameObject spikeHelmetSprite;
    public GameObject bootSprite;
    public GameObject upBoxingGloveSprite;
    public GameObject downBoxingGloveSprite;
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

    [Header("Facing Direction")]
    public bool isFacingRight = true;

    [Header("Current Status")]
    public float currentDamage = 0f; 
    public Vector3 respawnPoint = new Vector3(0, 5, 0); 
    public TextMeshProUGUI damageUI; 

    public bool isAttacking = false;
    
    [Header("Neutral Special Stats")]
    public KeyCode specialKey;
    public GameObject fireballPrefab; 
    public GameObject spinSprite;    
    public Transform shootPoint;      

    [Header("Side Special Stats")]
    public GameObject redSideSpecialSprite;  
    public GameObject blueSideSpecialSprite; 
    public float blueDashSpeed = 30f;         
    public float redLungeSpeed = 12f;         

    [Header("Up Special Stats")]
    public float redUpSpecialPower = 18f; 
    public float blueUpSpecialPower = 25f; 
    public GameObject redUpSpecialSprite; 

    [Header("Down Special Stats")]
    public GameObject redReflectorSprite; 
    public float blueBaseSpinSpeed = 15f; // The minimum speed of the dash
    public int maxSpinCharge = 6;         // How many mashes until it auto-fires    
    private int currentSpinCharge = 0;
    private bool isChargingSpin = false;

    [Header("Spin Settings")]
    public float spinRotationSpeed = 1500f;
    private bool isSpinning = false;
    
    [HideInInspector]
    public bool isReflecting = false; // Tells projectiles if they should bounce back
    
    [Header("Shield Stats")]
    public float maxShieldHealth = 50f;
    public float currentShieldHealth = 50f;
    public float shieldDepletionRate = 7f; // How fast it drains while holding
    public float shieldRegenRate = 5f;     // How fast it heals when not in use
    private Vector3 originalShieldScale;   // To shrink the bubble visually
    private float parryWindowEnd = -10f;   // Tracks the 5-frame window

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
    public float maxChargeMultiplier = 1.4f; 

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

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;                  
        rb.freezeRotation = true;              

        if (shieldBubble != null) originalShieldScale = shieldBubble.transform.localScale;

        AssignInputsAndStats();
        HideAllSprites();
    }


    void Update()
    {
        if (isSpinning)
        {
            transform.Rotate(0f, 0f, spinRotationSpeed * Time.deltaTime);
        }

        HandleInputs();
    }

    void FixedUpdate()
    {
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
        int xInput = (Input.GetKey(rightKey) ? 1 : 0) - (Input.GetKey(leftKey) ? 1 : 0);

        // --- HELPLESS STATE CHECK ---
        if (currentState == State.Helpless)
        {
            return; 
        }

        if (isChargingSpin)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (Input.GetKeyDown(specialKey))
            {
                currentSpinCharge++;
            }

            if (currentSpinCharge >= maxSpinCharge || !Input.GetKey(downKey))
            {
                isChargingSpin = false;
                isSpinning = true; 
                
                ExecuteAttack("DownSpecialBlue", boxingGloveSprite, 12f);
                
                float moveDir = isFacingRight ? 1f : -1f;
                float finalDashSpeed = blueBaseSpinSpeed + (currentSpinCharge * 4f); 
                
                rb.linearVelocity = new Vector2(moveDir * finalDashSpeed, 0f);
                currentSpinCharge = 0; 
                
                Invoke("ResetReflect", 0.4f); 
            }
            return; 
        }

        xInput = (Input.GetKey(rightKey) ? 1 : 0) - (Input.GetKey(leftKey) ? 1 : 0); 

        if (currentState == State.Grounded || currentState == State.Airborne)
        {
            if (xInput > 0) isFacingRight = true;
            else if (xInput < 0) isFacingRight = false;
        }

        if (isAttacking) return; 
        if (currentState == State.Hitstun) return;

        // --- CHARGE SMASH INTERCEPT ---
        if (currentState == State.ChargingSmash)
        {
            chargeTimer += Time.deltaTime;
            
            if (chargeTimer < maxChargeTime)
            {
                float shakeAmt = 0.05f;
                transform.position = new Vector3(rb.position.x + UnityEngine.Random.Range(-shakeAmt, shakeAmt), rb.position.y, transform.position.z);
            }
            else
            {
                transform.position = new Vector3(rb.position.x, rb.position.y, transform.position.z);
            }

            if (Input.GetKeyUp(attackKey))
            {
                ReleaseSmash();
            }
            return; 
        }

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

            return; 
        }
        
        xInput = (Input.GetKey(rightKey) ? 1 : 0) - (Input.GetKey(leftKey) ? 1 : 0);
        bool isWalkModifier = Input.GetKey(walkModKey);

        // --- DEFENSE ---
        if (Input.GetKey(shieldKey))
        {
            if (currentState == State.Grounded || currentState == State.Shielding) 
            {
                if (Input.GetKeyDown(downKey)) { ExecuteSpotDodge(); return; }
                if (Input.GetKeyDown(leftKey) || Input.GetKeyDown(rightKey)) { ExecuteRoll(); return; }
                
                currentState = State.Shielding;
                shieldBubble.SetActive(true);
                velocity.x = 0; 

                currentShieldHealth -= shieldDepletionRate * Time.deltaTime;
                UpdateShieldVisual();

                if (currentShieldHealth <= 0)
                {
                    TriggerShieldBreak();
                    return;
                }
            }
            else if (currentState == State.Airborne && Input.GetKeyDown(shieldKey))
            {
                ExecuteAirDodge(xInput, (Input.GetKey(upKey) ? 1 : 0) - (Input.GetKey(downKey) ? 1 : 0));
                return;
            }
        }
        else
        {
            if (currentState == State.Shielding) 
            {
                currentState = State.Grounded;
                shieldBubble.SetActive(false);
                
                parryWindowEnd = Time.time + (5f / 60f); 
            }
            else if (currentShieldHealth < maxShieldHealth)
            {
                currentShieldHealth += shieldRegenRate * Time.deltaTime;
                if (currentShieldHealth > maxShieldHealth) currentShieldHealth = maxShieldHealth;
            }
            shieldBubble.SetActive(false);
        }

        // --- ATTACKS ---
        if (Input.GetKeyDown(attackKey))
        {
            if (currentState == State.Grounded)
            {
                DetermineGroundAttack(xInput, isWalkModifier);
                return;
            }
            else if (currentState == State.Airborne)
            {
                DetermineAerialAttack(xInput);
                return;
            }
        }
        // --- SPECIAL ATTACKS ---
        if (Input.GetKeyDown(specialKey))
        {
            if (currentState == State.Grounded || currentState == State.Airborne)
            {
                DetermineSpecialAttack(xInput);
                return;
            }
        }

        // --- MOVEMENT & JUMPING ---
        if (Input.GetKeyDown(upKey))
        {
            if (currentState == State.Grounded || currentState == State.Shielding) StartJumpsquat();
            else if (currentState == State.Airborne && jumpsRemaining > 0) ExecuteJump(stats.doubleJumpHeight, true); 
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
                float targetSpeed = isWalkModifier ? stats.walkSpeed : stats.runSpeed;
                
                velocity.x = Mathf.MoveTowards(velocity.x, xInput * targetSpeed, stats.initialDash);
            }
            else
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0, stats.traction);
            }
        }
        else if (currentState == State.Airborne)
        {
            float maxFall = isFastFalling ? stats.fastFallSpeed : stats.fallSpeed;
            velocity.y -= stats.gravity;
            if (velocity.y < -maxFall) velocity.y = -maxFall;

            if (xInput != 0)
            {
                float accel = stats.baseAirAccel + stats.addAirAccel; 
                velocity.x = Mathf.MoveTowards(velocity.x, xInput * stats.airSpeed, accel);
            }
            else
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0, stats.airFriction);
            }
        }
    }

    // --- JUMP LOGIC ---
    private void StartJumpsquat()
    {
        float height = Input.GetKey(upKey) ? stats.jumpHeight : stats.shortHopHeight; 
        velocity.x = Mathf.Clamp(velocity.x, -stats.airSpeed, stats.airSpeed);         
        ExecuteJump(height, false);
    }

    private void ExecuteJump(float height, bool isDoubleJump)
    {
        currentState = State.Airborne;
        velocity.y = Mathf.Sqrt(2f * stats.gravity * height); 
        if (isDoubleJump) jumpsRemaining--; 
    }

    // --- COMBAT LOGIC ---
    private void DetermineGroundAttack(int xInput, bool isWalkMod)
    {
        // DOWN ATTACKS
        if (Input.GetKey(downKey))
        {
            if (Time.time - lastDownPress <= smashWindow)
            {
                StartChargeSmash("DownSmash", boxingGloveSprite, 25f, backBoxingGloveSprite);
            }
            else 
            {
                ExecuteAttack("DownTilt", bootSprite, 7f);
            }
        }        
        // UP ATTACKS
        else if (Input.GetKey(upKey))
        {
            if (Time.time - lastUpPress <= smashWindow)
            {
                StartChargeSmash("UpSmash", upBoxingGloveSprite, 20f);
            }
            else 
            {
                ExecuteAttack("UpTilt", spikeHelmetSprite, 8f);
            }
        }
        // FORWARD/SIDE ATTACKS
        else if (xInput != 0)
        {
            if (Time.time - lastSidePress <= smashWindow) 
            {
                StartChargeSmash("ForwardSmash", hammerSprite, 30f);
            }
            else if (Mathf.Abs(velocity.x) > stats.walkSpeed && !isWalkMod) 
            {
                ExecuteAttack("DashAttack", boxingGloveSprite, 9f);
            }
            else 
            {
                ExecuteAttack("ForwardTilt", hammerSprite, 8f);
            }
        }
        // NEUTRAL ATTACKS
        else
        {
            ExecuteJab();
        }
    }

    private void DetermineAerialAttack(int xInput)
    {
        // --- DOWN AIR ---
        if (Input.GetKey(downKey))
        {
            StartChargeSmash("DownAir", downBoxingGloveSprite, 13f);
            return;
        }
        // --- UP AIR ---
        else if (Input.GetKey(upKey))
        {
            ExecuteAttack("UpAir", upBoxingGloveSprite, 7f);
            return;
        }
        // --- NEUTRAL AIR ---
        else if (xInput == 0)
        {
            ExecuteAttack("NeutralAir", boxingGloveSprite, 8f);
            return;
        }
        // --- FORWARD AIR / BACK AIR ---
        else if (xInput != 0)
        {
            bool isHoldingForward = (xInput > 0 && isFacingRight) || (xInput < 0 && !isFacingRight);

            if (isHoldingForward)
            {
                StartChargeSmash("ForwardAir", hammerSprite, 13f);
            }
            else
            {
                StartChargeSmash("BackAir", hammerSprite, 13f);
            }
            return;
        }
    }

private void DetermineSpecialAttack(int xInput)
    {
        // --- UP SPECIAL ---
        if (Input.GetKey(upKey))
        {
            if (playerType == PlayerID.Player1_Red)
            {
                ExecuteAttack("UpSpecialRed", redUpSpecialSprite, 25f);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.5f, redUpSpecialPower);
            }
            else
            {
                ExecuteAttack("UpSpecialBlue", blueSideSpecialSprite, 5f);
                rb.linearVelocity = new Vector2(0f, blueUpSpecialPower);
            }

            currentState = State.Helpless;
            return;
        }

        // --- DOWN SPECIAL ---
        else if (Input.GetKey(downKey))
        {
            if (playerType == PlayerID.Player1_Red)
            {
                isReflecting = true;
                ExecuteAttack("DownSpecialRed", redReflectorSprite, 5f);
                rb.linearVelocity = Vector2.zero; 
                Invoke("ResetReflect", 0.5f); 
            }
            else
            {
                isChargingSpin = true;
                currentSpinCharge = 0;
                isAttacking = true; 
            }
            return;
        }

        // --- NEUTRAL SPECIAL ---
        if (xInput == 0 && !Input.GetKey(upKey) && !Input.GetKey(downKey))
        {
            if (playerType == PlayerID.Player1_Red)
            {
                ExecuteAttack("FireballThrow", boxingGloveSprite, 0f); 
                SpawnFireball();
            }
            else 
            {
                isReflecting = true; 
                isSpinning = true;
                
                ExecuteAttack("SpinAttack", boxingGloveSprite, 15f);
                
                Invoke("ResetReflect", 0.3f); 
            }
            return;
        }
        // --- SIDE SPECIAL ---
        else if (xInput != 0)
        {
            float moveDir = isFacingRight ? 1f : -1f;

            if (playerType == PlayerID.Player1_Red)
            {
                isReflecting = true;
                ExecuteAttack("SideSpecialRed", redSideSpecialSprite, 25f);
                
                rb.linearVelocity = new Vector2(moveDir * redLungeSpeed, rb.linearVelocity.y);
                
                Invoke("ResetReflect", 0.4f);
            }
            else
            {
                ExecuteAttack("SideSpecialBlue", blueSideSpecialSprite, 5f);
                rb.linearVelocity = new Vector2(moveDir * blueDashSpeed, 0f);
            }
            return;
        }
    }

    private void SpawnFireball()
    {
        if (fireballPrefab != null && shootPoint != null)
        {
            GameObject fireball = Instantiate(fireballPrefab, shootPoint.position, Quaternion.identity);
            FireballScript fbScript = fireball.GetComponent<FireballScript>();
            
            if (fbScript != null) fbScript.Initialize(isFacingRight, this);
        }
    }

    private void ResetReflect()
    {
        isReflecting = false;
        isSpinning = false;
        
        transform.rotation = Quaternion.identity; 
    }

    private void ExecuteAttack(string attackName, GameObject spriteToShow, float damage, GameObject secondarySprite = null, bool isMeteor = false, bool isInstaKill = false)
    {
        Debug.Log($"{playerType} performed {attackName} dealing {damage}% damage!");
        
        isAttacking = true;
        
        HideAllSprites();
        
        if (spriteToShow != null) spriteToShow.SetActive(true);
        if (secondarySprite != null) secondarySprite.SetActive(true); 
        
        // --- HITBOX & KNOCKBACK LOGIC ---
        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(transform.position, 1.5f); 
        foreach (Collider2D hit in hitPlayers)
        {
            PlayerController enemyPlayer = hit.GetComponent<PlayerController>();
            
            if (enemyPlayer != null && enemyPlayer != this)
            {
                Vector2 knockbackDir = (enemyPlayer.transform.position - transform.position).normalized;
                
                // --- METEOR SMASH OVERRIDE ---
                if (isMeteor && enemyPlayer.currentState == State.Airborne)
                {
                    knockbackDir = Vector2.down; 
                }
                // --- VERTICAL LAUNCH OVERRIDES ---
                else if (attackName == "UpAir" || attackName == "UpSmash" || attackName == "UpTilt")
                {
                    knockbackDir = Vector2.up; 
                }
                // --- RED SIDE SPECIAL (Electroshock Angle) ---
                else if (attackName == "SideSpecialRed")
                {
                    float hitDir = (enemyPlayer.transform.position.x > transform.position.x) ? 1f : -1f;
                    knockbackDir = new Vector2(hitDir, 1f).normalized; 
                }
                // --- BLUE SIDE SPECIAL (Weak Horizontal Angle) ---
                else if (attackName == "SideSpecialBlue")
                {
                    float hitDir = (enemyPlayer.transform.position.x > transform.position.x) ? 1f : -1f;
                    knockbackDir = new Vector2(hitDir, 0.2f).normalized;
                }
                else if (attackName == "UpSpecialRed")
                {
                    float hitDir = (enemyPlayer.transform.position.x > transform.position.x) ? 0.3f : -0.3f;
                    knockbackDir = new Vector2(hitDir, 1f).normalized;
                }
                // --- STANDARD KNOCKBACK ---
                else
                {
                    knockbackDir.y += 0.5f; 
                }
                
                enemyPlayer.TakeHit(damage, knockbackDir, isInstaKill);
            }
        }

        Invoke("EndAttack", 0.3f); 
    }

    public void TakeHit(float incomingDamage, Vector2 knockbackDir, bool isInstaKill = false)
    {
        isChargingSpin = false;
        currentSpinCharge = 0;

        if (currentState == State.Dodging) return; 

        if (Time.time <= parryWindowEnd)
        {
            Debug.Log($"** PARRY! ** {playerType} perfectly deflected the attack!");
            return; 
        }

        if (currentState == State.Shielding)
        {
            currentShieldHealth -= incomingDamage;
            UpdateShieldVisual();
            
            if (currentShieldHealth <= 0) TriggerShieldBreak();
            
            return; 
        }

        currentState = State.Hitstun;
        HideAllSprites();
        
        currentDamage += incomingDamage;
        if (damageUI != null) damageUI.text = Mathf.FloorToInt(currentDamage).ToString() + "%";
        
        float knockbackForce = (currentDamage * incomingDamage) / stats.weight; 
        knockbackForce = Mathf.Clamp(knockbackForce, 5f, 100f); 

        if (isInstaKill)
        {
            Debug.Log("INSTAKILL METEOR SMASH!");
            knockbackForce = 100f; // Max out the speed
            knockbackDir = Vector2.down; // Guarantee downward trajectory
        }
        
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
        velocity = Vector2.zero; 
        rb.linearVelocity = Vector2.zero;
        chargeTimer = 0f;
        
        pendingAttackName = attackName;
        pendingDamage = baseDamage;
        pendingSprite = sprite;
        pendingSecondarySprite = secondary;
        
        HideAllSprites();
        if (pendingSprite != null) pendingSprite.SetActive(true);
        if (pendingSecondarySprite != null) pendingSecondarySprite.SetActive(true);
    }

    private void ReleaseSmash()
    {
        transform.position = new Vector3(rb.position.x, rb.position.y, transform.position.z);
        if (pendingAttackName == "ForwardAir" || pendingAttackName == "BackAir" || pendingAttackName == "DownAir")
        {
            currentState = State.Airborne;
        }
        else
        {
            currentState = State.Grounded; 
        } 
        
        float chargePercent = Mathf.Clamp01(chargeTimer / maxChargeTime);
        
        // Default Smash Math
        float finalDamage = pendingDamage * Mathf.Lerp(1f, maxChargeMultiplier, chargePercent);
        bool isMeteor = false;
        bool isInstaKill = false;

        if (pendingAttackName == "ForwardAir" || pendingAttackName == "BackAir" || pendingAttackName == "DownAir")
        {
            // Scale from 1.0x to 3.0x damage (13 -> 39)
            finalDamage = pendingDamage * Mathf.Lerp(1f, 3f, chargePercent);
            isMeteor = true; // Always spikes!
            
            // If held for the full 1 second, flag the instakill
            if (chargePercent >= 1f) isInstaKill = true; 
        }
        
        // Pass our new flags into ExecuteAttack
        ExecuteAttack(pendingAttackName, pendingSprite, finalDamage, pendingSecondarySprite, isMeteor, isInstaKill);
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
        if (downBoxingGloveSprite != null) downBoxingGloveSprite.SetActive(false);
        if (upBoxingGloveSprite != null) upBoxingGloveSprite.SetActive(false);
        if (hammerSprite != null) hammerSprite.SetActive(false);
        if (spikeHelmetSprite != null) spikeHelmetSprite.SetActive(false);
        if (backBoxingGloveSprite != null) backBoxingGloveSprite.SetActive(false);
        if (redReflectorSprite != null) redReflectorSprite.SetActive(false);
    }

    // --- GROUND COLLISIONS & WAVEDASHING ---
    private void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Ground")) 
        {
            if (currentState == State.Helpless || currentState == State.Airborne)
            {
                currentState = State.Grounded;
                jumpsRemaining = 1; 
            }

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

    private void UpdateShieldVisual()
    {
        if (shieldBubble != null && maxShieldHealth > 0)
        {
            float scalePercent = Mathf.Clamp(currentShieldHealth / maxShieldHealth, 0.2f, 1f);
            shieldBubble.transform.localScale = originalShieldScale * scalePercent;
        }
    }

    private void TriggerShieldBreak()
    {
        Debug.Log($"SHIELD BREAK! {playerType} is blasting off again!");
        
        currentState = State.Hitstun;
        shieldBubble.SetActive(false);
        
        velocity = new Vector2(0, stats.jumpHeight * 1f); 
        rb.linearVelocity = velocity; 

        currentShieldHealth = maxShieldHealth; 
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
        currentShieldHealth = maxShieldHealth;
    }

    private void EndDodge()
    {
        HideAllSprites();

        if (Mathf.Abs(rb.linearVelocity.y) > 0.1f) currentState = State.Airborne;
        else currentState = State.Grounded;
    }
}