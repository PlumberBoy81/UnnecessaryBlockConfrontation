using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    public enum PlayerID { Player1_Red, Player2_Blue }
    public PlayerID playerType;

    public enum State { Grounded, Airborne, Shielding, Dodging, Hitstun, ChargingSmash, Helpless };

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
    public GameObject fireballPrefab; 
    public GameObject spinSprite;     
    public Transform shootPoint;      

    [Header("Side Special Stats")]
    public GameObject redSideSpecialSprite;   
    public GameObject blueSideSpecialSprite;  
    public float blueDashSpeed = 18f;         
    public float redLungeSpeed = 12f;         

    [Header("Up Special Stats")]
    public float redUpSpecialPower = 18f; 
    public float blueUpSpecialPower = 18f; 
    public GameObject redUpSpecialSprite; 

    [Header("Down Special Stats")]
    public GameObject redReflectorSprite; 
    public float blueBaseSpinSpeed = 15f; 
    public int maxSpinCharge = 6;         
    private int currentSpinCharge = 0;
    private bool isChargingSpin = false;

    [Header("Spin Settings")]
    public float spinRotationSpeed = 1500f; 
    private bool isSpinning = false;

    [HideInInspector]
    public bool isReflecting = false; 

    [Header("Shield Stats")]
    public float maxShieldHealth = 50f;
    public float currentShieldHealth = 50f;
    public float shieldDepletionRate = 7f; 
    public float shieldRegenRate = 5f;     
    private Vector3 originalShieldScale;   
    private float parryWindowEnd = -10f;   

    [Header("Assigned Stats")]
    public CharacterStats stats;

    [Header("Engine Settings")]
    public float unitScale = 0.1f;

    [Header("Input Buffer")]
    public float smashWindow = 0.2f; 
    private float lastDownPress = -10f;
    private float lastUpPress = -10f;
    private float lastSidePress = -10f;

    [Header("Smash Charge Settings")]
    public float maxChargeTime = 1f; 
    public float maxChargeMultiplier = 1.4f; 

    [Header("Input Settings")]
    public bool useGamepad = false;
    private string hAxis, vAxis;
    private KeyCode gpAttack, gpSpecial, gpJump, gpShield;
    
    // Keyboard fallbacks
    private KeyCode upKey, downKey, leftKey, rightKey, walkModKey, attackKey, shieldKey, specialKey;

    // Analog Stick State Tracking (to simulate "GetKeyDown" for sticks)
    private bool wasUp, wasDown, wasLeft, wasRight;
    private bool isUp, isDown, isLeft, isRight;

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

        UpdateGamepadAxes();
        HandleInputs();
    }

    void FixedUpdate()
    {
        if (currentState == State.Grounded || currentState == State.Airborne || currentState == State.Hitstun || currentState == State.Helpless)
        {
            ApplyPhysics();
        }

        rb.linearVelocity = velocity * (1f / Time.fixedDeltaTime) * unitScale;
    }

    private void AssignInputsAndStats()
    {
        if (playerType == PlayerID.Player1_Red)
        {
            // Keyboard Inputs
            upKey = KeyCode.W; downKey = KeyCode.S; leftKey = KeyCode.A; rightKey = KeyCode.D;
            walkModKey = KeyCode.LeftShift; attackKey = KeyCode.F; shieldKey = KeyCode.E; specialKey = KeyCode.R;

            // Gamepad Inputs (F310 in X mode: A=0, B=1, X=2, Y=3, LB=4, RB=5)
            hAxis = "P1_Horizontal"; vAxis = "P1_Vertical";
            gpAttack = KeyCode.Joystick1Button0;   // A Button
            gpSpecial = KeyCode.Joystick1Button1;  // B Button
            gpJump = KeyCode.Joystick1Button3;     // Y Button (and X Button)
            gpShield = KeyCode.Joystick1Button5;   // Right Bumper

            stats = new CharacterStats
            {
                weight = 98, initialDash = 1.936f, runSpeed = 1.76f, walkSpeed = 1.155f,
                traction = 0.102f, airFriction = 0.015f, airSpeed = 1.208f, baseAirAccel = 0.01f,
                addAirAccel = 0.07f, gravity = 0.087f, fallSpeed = 1.5f, fastFallSpeed = 2.4f,
                jumpsquatFrames = 3, jumpHeight = 36.33f, shortHopHeight = 17.54f, doubleJumpHeight = 36.33f
            };
        }
        else
        {
            // Keyboard Inputs
            upKey = KeyCode.UpArrow; downKey = KeyCode.DownArrow; leftKey = KeyCode.LeftArrow; rightKey = KeyCode.RightArrow;
            walkModKey = KeyCode.RightShift; attackKey = KeyCode.RightControl; shieldKey = KeyCode.Keypad0; specialKey = KeyCode.RightAlt;

            // Gamepad Inputs
            hAxis = "P2_Horizontal"; vAxis = "P2_Vertical";
            gpAttack = KeyCode.Joystick2Button0;   
            gpSpecial = KeyCode.Joystick2Button1;  
            gpJump = KeyCode.Joystick2Button3;     
            gpShield = KeyCode.Joystick2Button5;   

            stats = new CharacterStats
            {
                weight = 86, initialDash = 2.31f, runSpeed = 3.85f, walkSpeed = 1.444f,
                traction = 0.138f, airFriction = 0.01f, airSpeed = 1.208f, baseAirAccel = 0.01f,
                addAirAccel = 0.04f, gravity = 0.09f, fallSpeed = 1.65f, fastFallSpeed = 2.64f,
                jumpsquatFrames = 3, jumpHeight = 35f, shortHopHeight = 16.89f, doubleJumpHeight = 35f
            };
        }
    }

    // --- INPUT HELPER METHODS ---
    private void UpdateGamepadAxes()
    {
        if (!useGamepad) return;
        wasUp = isUp; wasDown = isDown; wasLeft = isLeft; wasRight = isRight;

        float x = Input.GetAxisRaw(hAxis);
        float y = Input.GetAxisRaw(vAxis);

        // Standard deadzones
        isRight = x > 0.4f;
        isLeft = x < -0.4f;
        isUp = y > 0.4f;
        isDown = y < -0.4f;
    }

    private bool GetUpDown() { return useGamepad ? ((isUp && !wasUp) || Input.GetKeyDown(gpJump)) : Input.GetKeyDown(upKey); }
    private bool GetUpDirectionDown() { return useGamepad ? (isUp && !wasUp) : Input.GetKeyDown(upKey); } // Pure directional up tracking
    private bool GetUp() { return useGamepad ? (isUp || Input.GetKey(gpJump)) : Input.GetKey(upKey); }
    private bool GetDownDown() { return useGamepad ? (isDown && !wasDown) : Input.GetKeyDown(downKey); }
    private bool GetDown() { return useGamepad ? isDown : Input.GetKey(downKey); }
    private bool GetLeftDown() { return useGamepad ? (isLeft && !wasLeft) : Input.GetKeyDown(leftKey); }
    private bool GetLeft() { return useGamepad ? isLeft : Input.GetKey(leftKey); }
    private bool GetRightDown() { return useGamepad ? (isRight && !wasRight) : Input.GetKeyDown(rightKey); }
    private bool GetRight() { return useGamepad ? isRight : Input.GetKey(rightKey); }
    private bool GetAttackDown() { return useGamepad ? Input.GetKeyDown(gpAttack) : Input.GetKeyDown(attackKey); }
    private bool GetAttackUp() { return useGamepad ? Input.GetKeyUp(gpAttack) : Input.GetKeyUp(attackKey); }
    private bool GetSpecialDown() { return useGamepad ? Input.GetKeyDown(gpSpecial) : Input.GetKeyDown(specialKey); }
    private bool GetShieldDown() { return useGamepad ? Input.GetKeyDown(gpShield) : Input.GetKeyDown(shieldKey); }
    private bool GetShield() { return useGamepad ? Input.GetKey(gpShield) : Input.GetKey(shieldKey); }

    private void HandleInputs()
    {
        if (currentState == State.Helpless) return; 

        if (isChargingSpin)
        {
            velocity.x = 0f;

            if (GetSpecialDown()) currentSpinCharge++;

            if (currentSpinCharge >= maxSpinCharge || !GetDown())
            {
                isChargingSpin = false;
                ExecuteAttack("DownSpecialBlue", spinSprite, 12f);

                float moveDir = isFacingRight ? 1f : -1f;
                float finalDashSpeed = blueBaseSpinSpeed + (currentSpinCharge * 4f);
                velocity = new Vector2(moveDir * finalDashSpeed, velocity.y);
                currentSpinCharge = 0; 
            }
            return; 
        }

        int xInput = (GetRight() ? 1 : 0) - (GetLeft() ? 1 : 0);

        if (currentState == State.Grounded || currentState == State.Airborne)
        {
            if (xInput > 0) isFacingRight = true;
            else if (xInput < 0) isFacingRight = false;
        }

        if (isAttacking) return;
        if (currentState == State.Hitstun) return;

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

            if (GetAttackUp()) ReleaseSmash();
            return;
        }

        if (GetDownDown()) lastDownPress = Time.time;
        if (GetUpDirectionDown()) lastUpPress = Time.time;
        if (GetLeftDown() || GetRightDown()) lastSidePress = Time.time;

        if (currentState == State.Dodging) return;

        // Automatically walk if analog stick is tilted lightly, or if Shift is held on keyboard
        float analogMag = useGamepad ? Mathf.Abs(Input.GetAxisRaw(hAxis)) : 1f;
        bool isWalkModifier = Input.GetKey(walkModKey) || (useGamepad && analogMag > 0.1f && analogMag < 0.6f);

        // --- DEFENSE ---
        if (GetShield())
        {
            if (currentState == State.Grounded || currentState == State.Shielding)
            {
                if (GetDownDown()) { ExecuteSpotDodge(); return; }
                if (GetLeftDown() || GetRightDown()) { ExecuteRoll(); return; }

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
            else if (currentState == State.Airborne && GetShieldDown())
            {
                int dirY = (GetUp() ? 1 : 0) - (GetDown() ? 1 : 0);
                ExecuteAirDodge(xInput, dirY);
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
        if (GetAttackDown())
        {
            if (currentState == State.Grounded) DetermineGroundAttack(xInput, isWalkModifier);
            else if (currentState == State.Airborne) DetermineAerialAttack(xInput);
            return;
        }

        // --- SPECIAL ATTACKS ---
        if (GetSpecialDown())
        {
            if (currentState == State.Grounded || currentState == State.Airborne) DetermineSpecialAttack(xInput);
            return;
        }

        // --- MOVEMENT & JUMPING ---
        if (GetUpDown())
        {
            if (currentState == State.Grounded || currentState == State.Shielding) StartJumpsquat();
            else if (currentState == State.Airborne && jumpsRemaining > 0) ExecuteJump(stats.doubleJumpHeight, true);
        }

        if (currentState == State.Airborne && GetDownDown() && velocity.y < 0)
        {
            isFastFalling = true;
        }
    }

    private void ApplyPhysics()
    {
        int xInput = (GetRight() ? 1 : 0) - (GetLeft() ? 1 : 0);
        float analogMag = useGamepad ? Mathf.Abs(Input.GetAxisRaw(hAxis)) : 1f;
        bool isWalkModifier = Input.GetKey(walkModKey) || (useGamepad && analogMag > 0.1f && analogMag < 0.6f);

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
        else if (currentState == State.Airborne || currentState == State.Helpless || currentState == State.Hitstun)
        {
            float maxFall = isFastFalling ? stats.fastFallSpeed : stats.fallSpeed;
            velocity.y -= stats.gravity;
            if (velocity.y < -maxFall) velocity.y = -maxFall;

            if (currentState != State.Hitstun)
            {
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
    }

    private void StartJumpsquat()
    {
        float height = GetUp() ? stats.jumpHeight : stats.shortHopHeight;
        velocity.x = Mathf.Clamp(velocity.x, -stats.airSpeed, stats.airSpeed);
        ExecuteJump(height, false);
    }

    private void ExecuteJump(float height, bool isDoubleJump)
    {
        currentState = State.Airborne;
        velocity.y = Mathf.Sqrt(2f * stats.gravity * height);
        if (isDoubleJump) jumpsRemaining--;
    }

    private void DetermineGroundAttack(int xInput, bool isWalkMod)
    {
        if (GetDown())
        {
            if (Time.time - lastDownPress <= smashWindow) StartChargeSmash("DownSmash", boxingGloveSprite, 25f, backBoxingGloveSprite);
            else ExecuteAttack("DownTilt", bootSprite, 7f);
        }
        else if (GetUp())
        {
            if (Time.time - lastUpPress <= smashWindow) StartChargeSmash("UpSmash", upBoxingGloveSprite, 20f);
            else ExecuteAttack("UpTilt", spikeHelmetSprite, 8f);
        }
        else if (xInput != 0)
        {
            if (Time.time - lastSidePress <= smashWindow) StartChargeSmash("ForwardSmash", hammerSprite, 30f);
            else if (Mathf.Abs(velocity.x) > stats.walkSpeed && !isWalkMod) ExecuteAttack("DashAttack", boxingGloveSprite, 9f);
            else ExecuteAttack("ForwardTilt", hammerSprite, 8f);
        }
        else ExecuteJab();
    }

    private void DetermineAerialAttack(int xInput)
    {
        if (GetDown()) StartChargeSmash("DownAir", downBoxingGloveSprite, 13f);
        else if (GetUp()) ExecuteAttack("UpAir", upBoxingGloveSprite, 7f);
        else if (xInput == 0) ExecuteAttack("NeutralAir", boxingGloveSprite, 8f);
        else
        {
            bool isHoldingForward = (xInput > 0 && isFacingRight) || (xInput < 0 && !isFacingRight);
            if (isHoldingForward) StartChargeSmash("ForwardAir", hammerSprite, 13f);
            else StartChargeSmash("BackAir", hammerSprite, 13f);
        }
    }

    private void DetermineSpecialAttack(int xInput)
    {
        if (GetUp())
        {
            if (playerType == PlayerID.Player1_Red)
            {
                ExecuteAttack("UpSpecialRed", redUpSpecialSprite, 25f);
                velocity = new Vector2(velocity.x * 0.5f, redUpSpecialPower);
            }
            else
            {
                ExecuteAttack("UpSpecialBlue", blueSideSpecialSprite, 5f);
                velocity = new Vector2(0f, blueUpSpecialPower);
                Invoke("HaltMomentum", 0.05f); 
            }
            currentState = State.Helpless;
            return;
        }
        else if (GetDown())
        {
            if (playerType == PlayerID.Player1_Red)
            {
                isReflecting = true;
                ExecuteAttack("DownSpecialRed", redReflectorSprite, 5f); 
                velocity = Vector2.zero;
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

        if (xInput == 0 && !GetUp() && !GetDown())
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
                ExecuteAttack("SpinAttack", spinSprite, 15f);
                Invoke("ResetReflect", 0.3f);
            }
            return;
        }
        else if (xInput != 0)
        {
            float moveDir = isFacingRight ? 1f : -1f;

            if (playerType == PlayerID.Player1_Red)
            {
                isReflecting = true;
                ExecuteAttack("SideSpecialRed", redSideSpecialSprite, 25f);
                velocity = new Vector2(moveDir * redLungeSpeed, velocity.y);
                Invoke("ResetReflect", 0.4f); 
            }
            else
            {
                ExecuteAttack("SideSpecialBlue", blueSideSpecialSprite, 5f);
                velocity = new Vector2(moveDir * blueDashSpeed, 0f);
                Invoke("HaltMomentum", 0.05f); 
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
        isAttacking = true;
        HideAllSprites();

        if (spriteToShow != null) spriteToShow.SetActive(true);
        if (secondarySprite != null) secondarySprite.SetActive(true);

        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(transform.position, 1.5f);
        foreach (Collider2D hit in hitPlayers)
        {
            PlayerController enemyPlayer = hit.GetComponent<PlayerController>();

            if (enemyPlayer != null && enemyPlayer != this)
            {
                Vector2 knockbackDir = (enemyPlayer.transform.position - transform.position).normalized;

                if (isMeteor && enemyPlayer.currentState == State.Airborne) knockbackDir = Vector2.down;
                else if (attackName == "UpAir" || attackName == "UpSmash" || attackName == "UpTilt") knockbackDir = Vector2.up;
                else if (attackName == "SideSpecialRed")
                {
                    float hitDir = (enemyPlayer.transform.position.x > transform.position.x) ? 1f : -1f;
                    knockbackDir = new Vector2(hitDir, 1f).normalized;
                }
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
                else knockbackDir.y += 0.5f;

                enemyPlayer.TakeHit(damage, knockbackDir, isInstaKill);
            }
        }

        Invoke("EndAttack", 0.3f);
    }

    public void TakeHit(float incomingDamage, Vector2 knockbackDir, bool isInstaKill = false)
    {
        isAttacking = false; 
        isChargingSpin = false;
        currentSpinCharge = 0;

        if (currentState == State.Dodging) return;

        if (Time.time <= parryWindowEnd) return;

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
            knockbackForce = 100f; 
            knockbackDir = Vector2.down; 
        }

        velocity = knockbackDir.normalized * knockbackForce * 0.2f;

        StartCoroutine(ApplyHitlag(incomingDamage));
        CancelInvoke("EndHitstun"); 
        
        float hitstunDuration = 0.3f + (knockbackForce * 0.015f);
        Invoke("EndHitstun", hitstunDuration);
    }

    private void ExecuteJab()
    {
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
        float finalDamage = pendingDamage * Mathf.Lerp(1f, maxChargeMultiplier, chargePercent);
        bool isMeteor = false;
        bool isInstaKill = false;

        if (pendingAttackName == "ForwardAir" || pendingAttackName == "BackAir" || pendingAttackName == "DownAir")
        {
            finalDamage = pendingDamage * Mathf.Lerp(1f, 3f, chargePercent);
            isMeteor = true; 
            if (chargePercent >= 1f) isInstaKill = true;
        }

        ExecuteAttack(pendingAttackName, pendingSprite, finalDamage, pendingSecondarySprite, isMeteor, isInstaKill);
    }

    private void ExecuteSpotDodge()
    {
        currentState = State.Dodging;
        shieldBubble.SetActive(false);
        velocity = Vector2.zero;
        Invoke("EndDodge", 0.4f);
    }

    private void ExecuteRoll()
    {
        currentState = State.Dodging;
        shieldBubble.SetActive(false);
        int dirX = GetLeft() ? -1 : 1;
        velocity = new Vector2(dirX * stats.runSpeed * 1.5f, 0);
        Invoke("EndDodge", 0.5f);
    }

    private void ExecuteAirDodge(int dirX, int dirY)
    {
        currentState = State.Dodging;
        shieldBubble.SetActive(false); 
        velocity = Vector2.zero;

        if (dirX != 0 || dirY != 0)
        {
            velocity = new Vector2(dirX, dirY).normalized * stats.airSpeed * 2f;
        }

        Invoke("EndAirDodge", 0.4f);
    }

    private void EndHitstun()
    {
        HideAllSprites();
        if (Mathf.Abs(rb.linearVelocity.y) > 0.1f) currentState = State.Airborne;
        else currentState = State.Grounded;
    }

    private void EndAirDodge()
    {
        HideAllSprites();
        if (currentState == State.Dodging)
        {
            currentState = State.Helpless; 
        }
    }

    private void EndDodge()
    {
        HideAllSprites();
        if (Mathf.Abs(rb.linearVelocity.y) > 0.1f) currentState = State.Airborne;
        else currentState = State.Grounded;
    }

    private void HaltMomentum()
    {
        velocity.x = 0f; 
        if (velocity.y > 0) velocity.y = 0f;
    }

    private IEnumerator ApplyHitlag(float damage)
    {
        float hitlagDuration = 0.02f + (damage * 0.005f);
        Time.timeScale = 0.0f;
        yield return new WaitForSecondsRealtime(hitlagDuration);
        Time.timeScale = 1.0f;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Ground"))
        {
            if (currentState == State.Helpless || currentState == State.Airborne || currentState == State.Hitstun)
            {
                jumpsRemaining = 1; 
                if (currentState != State.Hitstun) currentState = State.Grounded;
            }

            if (currentState == State.Dodging && velocity.y < 0)
            {
                if (Mathf.Abs(velocity.x) > 0.1f) velocity.x *= 1.5f;
            }

            if (currentState != State.Hitstun)
            {
                currentState = State.Grounded;
                velocity.y = 0;
            }
            
            isFastFalling = false;
        }
    }

    private void EndAttack()
    {
        HideAllSprites();
        isAttacking = false; 
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
        currentState = State.Hitstun;
        shieldBubble.SetActive(false);
        velocity = new Vector2(0, stats.jumpHeight * 1f);
        currentShieldHealth = maxShieldHealth;
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Ground"))
        {
            if (currentState != State.Hitstun && currentState != State.Helpless)
            {
                currentState = State.Airborne;
            }
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
        Time.timeScale = 1.0f; 
        
        currentDamage = 0f;
        isAttacking = false;
        isChargingSpin = false;

        if (damageUI != null) damageUI.text = "0%";

        velocity = Vector2.zero;
        rb.linearVelocity = Vector2.zero;

        rb.position = respawnPoint;
        transform.position = respawnPoint;

        currentState = State.Airborne;
        CancelInvoke();
        HideAllSprites();
        currentShieldHealth = maxShieldHealth;
    }

    private void HideAllSprites()
    {
        if (boxingGloveSprite != null) boxingGloveSprite.SetActive(false);
        if (backBoxingGloveSprite != null) backBoxingGloveSprite.SetActive(false);
        if (hammerSprite != null) hammerSprite.SetActive(false);
        if (spikeHelmetSprite != null) spikeHelmetSprite.SetActive(false);
        if (bootSprite != null) bootSprite.SetActive(false);
        if (upBoxingGloveSprite != null) upBoxingGloveSprite.SetActive(false);
        if (downBoxingGloveSprite != null) downBoxingGloveSprite.SetActive(false);
        if (spinSprite != null) spinSprite.SetActive(false);
        if (redSideSpecialSprite != null) redSideSpecialSprite.SetActive(false);
        if (blueSideSpecialSprite != null) blueSideSpecialSprite.SetActive(false);
        if (redUpSpecialSprite != null) redUpSpecialSprite.SetActive(false);
        if (redReflectorSprite != null) redReflectorSprite.SetActive(false);
    }
}