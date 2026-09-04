using UnityEngine;
using TMPro;
using System;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    public enum PlayerID
    {
        Player1_Red,
        Player2_Blue
    }

    public PlayerID playerType;

    public enum State
    {
        Grounded,
        Airborne,
        Shielding,
        Dodging,
        Hitstun,
        ChargingSmash,
        Helpless
    }

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

    [Serializable]
    public struct CharacterStats
    {
        public float weight;
        public float initialDash;
        public float runSpeed;
        public float walkSpeed;
        public float traction;
        public float airFriction;
        public float airSpeed;

        public float baseAirAccel;
        public float addAirAccel;

        public float gravity;
        public float fallSpeed;
        public float fastFallSpeed;

        public int jumpsquatFrames;

        public float jumpHeight;
        public float shortHopHeight;
        public float doubleJumpHeight;
    }

    [Header("Facing Direction")]
    public bool isFacingRight = true;

    [Header("Current Status")]
    public float currentDamage = 0f;
    public Vector3 respawnPoint = new Vector3(0f, 5f, 0f);
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

    [Header("Attack Detection")]
    public float attackRange = 1.5f;

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

    private string hAxis;
    private string vAxis;

    private KeyCode gpAttack;
    private KeyCode gpSpecial;
    private KeyCode gpJump;
    private KeyCode gpShield;

    private KeyCode upKey;
    private KeyCode downKey;
    private KeyCode leftKey;
    private KeyCode rightKey;
    private KeyCode walkModKey;
    private KeyCode attackKey;
    private KeyCode shieldKey;
    private KeyCode specialKey;

    // Analog stick edge detection.
    private bool wasUp;
    private bool wasDown;
    private bool wasLeft;
    private bool wasRight;

    private bool isUp;
    private bool isDown;
    private bool isLeft;
    private bool isRight;

    // Physics.
    private Rigidbody2D rb;
    private Vector2 velocity;

    private bool isFastFalling = false;
    private int jumpsRemaining = 1;

    // Jab.
    private int currentJabCombo = 0;
    private float lastJabTime = -10f;

    // Smash charge.
    private float chargeTimer = 0f;
    private string pendingAttackName;
    private float pendingDamage;
    private GameObject pendingSprite;
    private GameObject pendingSecondarySprite;

    // Used to avoid stale invoke callbacks.
    private Coroutine hitlagCoroutine;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogError($"{name}: PlayerController requires a Rigidbody2D.");
            enabled = false;
            return;
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        if (shieldBubble != null)
        {
            originalShieldScale = shieldBubble.transform.localScale;
            shieldBubble.SetActive(false);
        }

        currentShieldHealth = maxShieldHealth;

        AssignInputsAndStats();
        HideAllSprites();
        UpdateDamageUI();
    }

    private void Update()
    {
        if (isSpinning)
        {
            transform.Rotate(
                0f,
                0f,
                spinRotationSpeed * Time.deltaTime
            );
        }

        UpdateGamepadAxes();
        HandleInputs();
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        if (currentState == State.Grounded ||
            currentState == State.Airborne ||
            currentState == State.Hitstun ||
            currentState == State.Helpless)
        {
            ApplyPhysics();
        }

        // Your character stats are measured in "per-frame" style units,
        // so convert them into Unity's units-per-second here.
        rb.linearVelocity =
            velocity * (1f / Time.fixedDeltaTime) * unitScale;
    }

    // ============================================================
    // INPUT / CHARACTER SETUP
    // ============================================================

    private void AssignInputsAndStats()
    {
        bool isMac =
            Application.platform == RuntimePlatform.OSXEditor ||
            Application.platform == RuntimePlatform.OSXPlayer;

        if (playerType == PlayerID.Player1_Red)
        {
            upKey = KeyCode.W;
            downKey = KeyCode.S;
            leftKey = KeyCode.A;
            rightKey = KeyCode.D;

            walkModKey = KeyCode.LeftShift;
            attackKey = KeyCode.F;
            shieldKey = KeyCode.E;
            specialKey = KeyCode.R;

            hAxis = "P1_Horizontal";
            vAxis = "P1_Vertical";

            if (isMac)
            {
                gpAttack = KeyCode.Joystick1Button1;
                gpSpecial = KeyCode.Joystick1Button2;
                gpJump = KeyCode.Joystick1Button3;
                gpShield = KeyCode.Joystick1Button5;
            }
            else
            {
                gpAttack = KeyCode.Joystick1Button0;
                gpSpecial = KeyCode.Joystick1Button1;
                gpJump = KeyCode.Joystick1Button3;
                gpShield = KeyCode.Joystick1Button5;
            }

            stats = new CharacterStats
            {
                weight = 98f,
                initialDash = 1.936f,
                runSpeed = 1.76f,
                walkSpeed = 1.155f,
                traction = 0.102f,
                airFriction = 0.015f,
                airSpeed = 1.208f,
                baseAirAccel = 0.01f,
                addAirAccel = 0.07f,
                gravity = 0.087f,
                fallSpeed = 1.5f,
                fastFallSpeed = 2.4f,
                jumpsquatFrames = 3,
                jumpHeight = 36.33f,
                shortHopHeight = 17.54f,
                doubleJumpHeight = 36.33f
            };
        }
        else
        {
            upKey = KeyCode.UpArrow;
            downKey = KeyCode.DownArrow;
            leftKey = KeyCode.LeftArrow;
            rightKey = KeyCode.RightArrow;

            walkModKey = KeyCode.RightShift;
            attackKey = KeyCode.RightControl;
            shieldKey = KeyCode.Keypad0;
            specialKey = KeyCode.RightAlt;

            hAxis = "P2_Horizontal";
            vAxis = "P2_Vertical";

            if (isMac)
            {
                gpAttack = KeyCode.Joystick2Button1;
                gpSpecial = KeyCode.Joystick2Button2;
                gpJump = KeyCode.Joystick2Button3;
                gpShield = KeyCode.Joystick2Button5;
            }
            else
            {
                gpAttack = KeyCode.Joystick2Button0;
                gpSpecial = KeyCode.Joystick2Button1;
                gpJump = KeyCode.Joystick2Button3;
                gpShield = KeyCode.Joystick2Button5;
            }

            stats = new CharacterStats
            {
                weight = 86f,
                initialDash = 2.31f,
                runSpeed = 3.85f,
                walkSpeed = 1.444f,
                traction = 0.138f,
                airFriction = 0.01f,
                airSpeed = 1.208f,
                baseAirAccel = 0.01f,
                addAirAccel = 0.04f,
                gravity = 0.09f,
                fallSpeed = 1.65f,
                fastFallSpeed = 2.64f,
                jumpsquatFrames = 3,
                jumpHeight = 35f,
                shortHopHeight = 16.89f,
                doubleJumpHeight = 35f
            };
        }
    }

    private void UpdateGamepadAxes()
    {
        if (!useGamepad)
            return;

        wasUp = isUp;
        wasDown = isDown;
        wasLeft = isLeft;
        wasRight = isRight;

        float x = Input.GetAxisRaw(hAxis);
        float y = Input.GetAxisRaw(vAxis);

        isRight = x > 0.4f;
        isLeft = x < -0.4f;
        isUp = y > 0.4f;
        isDown = y < -0.4f;
    }

    private bool GetUpDown()
    {
        if (!useGamepad)
            return Input.GetKeyDown(upKey);

        return (isUp && !wasUp) || Input.GetKeyDown(gpJump);
    }

    // Directional input ONLY. Jump button is intentionally excluded.
    private bool GetUpDirectionDown()
    {
        if (!useGamepad)
            return Input.GetKeyDown(upKey);

        return isUp && !wasUp;
    }

    private bool GetUpDirection()
    {
        if (!useGamepad)
            return Input.GetKey(upKey);

        return isUp;
    }

    private bool GetJumpHeld()
    {
        if (!useGamepad)
            return Input.GetKey(upKey);

        return Input.GetKey(gpJump) || isUp;
    }

    private bool GetDownDown()
    {
        if (!useGamepad)
            return Input.GetKeyDown(downKey);

        return isDown && !wasDown;
    }

    private bool GetDown()
    {
        if (!useGamepad)
            return Input.GetKey(downKey);

        return isDown;
    }

    private bool GetLeftDown()
    {
        if (!useGamepad)
            return Input.GetKeyDown(leftKey);

        return isLeft && !wasLeft;
    }

    private bool GetLeft()
    {
        if (!useGamepad)
            return Input.GetKey(leftKey);

        return isLeft;
    }

    private bool GetRightDown()
    {
        if (!useGamepad)
            return Input.GetKeyDown(rightKey);

        return isRight && !wasRight;
    }

    private bool GetRight()
    {
        if (!useGamepad)
            return Input.GetKey(rightKey);

        return isRight;
    }

    private bool GetAttackDown()
    {
        return useGamepad
            ? Input.GetKeyDown(gpAttack)
            : Input.GetKeyDown(attackKey);
    }

    private bool GetAttackUp()
    {
        return useGamepad
            ? Input.GetKeyUp(gpAttack)
            : Input.GetKeyUp(attackKey);
    }

    private bool GetSpecialDown()
    {
        return useGamepad
            ? Input.GetKeyDown(gpSpecial)
            : Input.GetKeyDown(specialKey);
    }

    private bool GetShieldDown()
    {
        return useGamepad
            ? Input.GetKeyDown(gpShield)
            : Input.GetKeyDown(shieldKey);
    }

    private bool GetShield()
    {
        return useGamepad
            ? Input.GetKey(gpShield)
            : Input.GetKey(shieldKey);
    }

    // ============================================================
    // MAIN INPUT HANDLING
    // ============================================================

    private void HandleInputs()
    {
        if (currentState == State.Helpless)
            return;

        // --------------------------------------------------------
        // BLUE DOWN SPECIAL CHARGE
        // --------------------------------------------------------

        if (isChargingSpin)
        {
            velocity.x = 0f;

            if (GetSpecialDown())
                currentSpinCharge++;

            if (currentSpinCharge >= maxSpinCharge || !GetDown())
            {
                isChargingSpin = false;

                ExecuteAttack(
                    "DownSpecialBlue",
                    spinSprite,
                    12f
                );

                float moveDir = isFacingRight ? 1f : -1f;

                float finalDashSpeed =
                    blueBaseSpinSpeed +
                    currentSpinCharge * 4f;

                velocity = new Vector2(
                    moveDir * finalDashSpeed,
                    velocity.y
                );

                currentSpinCharge = 0;
            }

            return;
        }

        int xInput =
            (GetRight() ? 1 : 0) -
            (GetLeft() ? 1 : 0);

        if (currentState == State.Grounded ||
            currentState == State.Airborne)
        {
            if (xInput > 0)
                isFacingRight = true;
            else if (xInput < 0)
                isFacingRight = false;
        }

        if (isAttacking)
            return;

        if (currentState == State.Hitstun)
            return;

        // --------------------------------------------------------
        // SMASH CHARGING
        // --------------------------------------------------------

        if (currentState == State.ChargingSmash)
        {
            chargeTimer += Time.deltaTime;

            if (chargeTimer < maxChargeTime)
            {
                float shakeAmount = 0.05f;

                transform.position = new Vector3(
                    rb.position.x +
                    UnityEngine.Random.Range(
                        -shakeAmount,
                        shakeAmount
                    ),
                    rb.position.y,
                    transform.position.z
                );
            }
            else
            {
                transform.position = new Vector3(
                    rb.position.x,
                    rb.position.y,
                    transform.position.z
                );
            }

            if (GetAttackUp())
                ReleaseSmash();

            return;
        }

        // --------------------------------------------------------
        // SMASH INPUT BUFFER
        // --------------------------------------------------------

        if (GetDownDown())
            lastDownPress = Time.time;

        if (GetUpDirectionDown())
            lastUpPress = Time.time;

        if (GetLeftDown() || GetRightDown())
            lastSidePress = Time.time;

        if (currentState == State.Dodging)
            return;

        // --------------------------------------------------------
        // WALKING
        // --------------------------------------------------------

        float analogMagnitude =
            useGamepad
                ? Mathf.Abs(Input.GetAxisRaw(hAxis))
                : 1f;

        bool isWalkModifier =
            Input.GetKey(walkModKey) ||
            (
                useGamepad &&
                analogMagnitude > 0.1f &&
                analogMagnitude < 0.6f
            );

        // --------------------------------------------------------
        // SHIELD / DEFENSE
        // --------------------------------------------------------

        if (GetShield())
        {
            if (currentState == State.Grounded ||
                currentState == State.Shielding)
            {
                if (GetDownDown())
                {
                    ExecuteSpotDodge();
                    return;
                }

                if (GetLeftDown() || GetRightDown())
                {
                    ExecuteRoll();
                    return;
                }

                currentState = State.Shielding;

                if (shieldBubble != null)
                    shieldBubble.SetActive(true);

                velocity.x = 0f;

                currentShieldHealth -=
                    shieldDepletionRate * Time.deltaTime;

                UpdateShieldVisual();

                if (currentShieldHealth <= 0f)
                {
                    TriggerShieldBreak();
                    return;
                }
            }
            else if (currentState == State.Airborne &&
                     GetShieldDown())
            {
                int dirY =
                    (GetUpDirection() ? 1 : 0) -
                    (GetDown() ? 1 : 0);

                ExecuteAirDodge(xInput, dirY);
                return;
            }
        }
        else
        {
            if (currentState == State.Shielding)
            {
                currentState = State.Grounded;

                if (shieldBubble != null)
                    shieldBubble.SetActive(false);

                // 5 frames at 60 FPS.
                parryWindowEnd =
                    Time.time + (5f / 60f);
            }

            if (currentShieldHealth < maxShieldHealth)
            {
                currentShieldHealth +=
                    shieldRegenRate * Time.deltaTime;

                currentShieldHealth =
                    Mathf.Min(
                        currentShieldHealth,
                        maxShieldHealth
                    );
            }

            if (shieldBubble != null)
                shieldBubble.SetActive(false);
        }

        // --------------------------------------------------------
        // ATTACK
        // --------------------------------------------------------

        if (GetAttackDown())
        {
            if (currentState == State.Grounded)
            {
                DetermineGroundAttack(
                    xInput,
                    isWalkModifier
                );
            }
            else if (currentState == State.Airborne)
            {
                DetermineAerialAttack(xInput);
            }

            return;
        }

        // --------------------------------------------------------
        // SPECIAL
        // --------------------------------------------------------

        if (GetSpecialDown())
        {
            if (currentState == State.Grounded ||
                currentState == State.Airborne)
            {
                DetermineSpecialAttack(xInput);
            }

            return;
        }

        // --------------------------------------------------------
        // JUMP
        // --------------------------------------------------------

        if (GetUpDown())
        {
            if (currentState == State.Grounded ||
                currentState == State.Shielding)
            {
                StartJumpsquat();
            }
            else if (currentState == State.Airborne &&
                     jumpsRemaining > 0)
            {
                ExecuteJump(
                    stats.doubleJumpHeight,
                    true
                );
            }
        }

        // --------------------------------------------------------
        // FAST FALL
        // --------------------------------------------------------

        if (currentState == State.Airborne &&
            GetDownDown() &&
            velocity.y < 0f)
        {
            isFastFalling = true;
        }
    }

    // ============================================================
    // PHYSICS
    // ============================================================

    private void ApplyPhysics()
    {
        int xInput =
            (GetRight() ? 1 : 0) -
            (GetLeft() ? 1 : 0);

        float analogMagnitude =
            useGamepad
                ? Mathf.Abs(Input.GetAxisRaw(hAxis))
                : 1f;

        bool isWalkModifier =
            Input.GetKey(walkModKey) ||
            (
                useGamepad &&
                analogMagnitude > 0.1f &&
                analogMagnitude < 0.6f
            );

        if (currentState == State.Grounded)
        {
            isFastFalling = false;
            jumpsRemaining = 1;
            velocity.y = 0f;

            if (xInput != 0)
            {
                float targetSpeed =
                    isWalkModifier
                        ? stats.walkSpeed
                        : stats.runSpeed;

                velocity.x = Mathf.MoveTowards(
                    velocity.x,
                    xInput * targetSpeed,
                    stats.initialDash
                );
            }
            else
            {
                velocity.x = Mathf.MoveTowards(
                    velocity.x,
                    0f,
                    stats.traction
                );
            }
        }
        else if (currentState == State.Airborne ||
                 currentState == State.Helpless ||
                 currentState == State.Hitstun)
        {
            float maximumFallSpeed =
                isFastFalling
                    ? stats.fastFallSpeed
                    : stats.fallSpeed;

            velocity.y -= stats.gravity;

            if (velocity.y < -maximumFallSpeed)
                velocity.y = -maximumFallSpeed;

            // Hitstun preserves knockback horizontal momentum.
            if (currentState != State.Hitstun)
            {
                if (xInput != 0)
                {
                    float acceleration =
                        stats.baseAirAccel +
                        stats.addAirAccel;

                    velocity.x = Mathf.MoveTowards(
                        velocity.x,
                        xInput * stats.airSpeed,
                        acceleration
                    );
                }
                else
                {
                    velocity.x = Mathf.MoveTowards(
                        velocity.x,
                        0f,
                        stats.airFriction
                    );
                }
            }
        }
    }

    private void StartJumpsquat()
    {
        float height =
            GetJumpHeld()
                ? stats.jumpHeight
                : stats.shortHopHeight;

        velocity.x = Mathf.Clamp(
            velocity.x,
            -stats.airSpeed,
            stats.airSpeed
        );

        ExecuteJump(height, false);
    }

    private void ExecuteJump(
        float height,
        bool isDoubleJump)
    {
        currentState = State.Airborne;

        velocity.y = Mathf.Sqrt(
            2f *
            stats.gravity *
            height
        );

        isFastFalling = false;

        if (isDoubleJump)
            jumpsRemaining--;
    }

    // ============================================================
    // GROUND ATTACKS
    // ============================================================

    private void DetermineGroundAttack(
        int xInput,
        bool isWalkMod)
    {
        if (GetDown())
        {
            if (Time.time - lastDownPress <= smashWindow)
            {
                StartChargeSmash(
                    "DownSmash",
                    boxingGloveSprite,
                    25f,
                    backBoxingGloveSprite
                );
            }
            else
            {
                ExecuteAttack(
                    "DownTilt",
                    bootSprite,
                    7f
                );
            }
        }
        else if (GetUpDirection())
        {
            if (Time.time - lastUpPress <= smashWindow)
            {
                StartChargeSmash(
                    "UpSmash",
                    upBoxingGloveSprite,
                    20f
                );
            }
            else
            {
                ExecuteAttack(
                    "UpTilt",
                    spikeHelmetSprite,
                    8f
                );
            }
        }
        else if (xInput != 0)
        {
            if (Time.time - lastSidePress <= smashWindow)
            {
                StartChargeSmash(
                    "ForwardSmash",
                    hammerSprite,
                    30f
                );
            }
            else if (
                Mathf.Abs(velocity.x) >
                stats.walkSpeed &&
                !isWalkMod
            )
            {
                ExecuteAttack(
                    "DashAttack",
                    boxingGloveSprite,
                    9f
                );
            }
            else
            {
                ExecuteAttack(
                    "ForwardTilt",
                    hammerSprite,
                    8f
                );
            }
        }
        else
        {
            ExecuteJab();
        }
    }

    // ============================================================
    // AERIAL ATTACKS
    // ============================================================

    private void DetermineAerialAttack(int xInput)
    {
        if (GetDown())
        {
            StartChargeSmash(
                "DownAir",
                downBoxingGloveSprite,
                13f
            );
        }
        else if (GetUpDirection())
        {
            ExecuteAttack(
                "UpAir",
                upBoxingGloveSprite,
                7f
            );
        }
        else if (xInput == 0)
        {
            ExecuteAttack(
                "NeutralAir",
                boxingGloveSprite,
                8f
            );
        }
        else
        {
            bool holdingForward =
                (xInput > 0 && isFacingRight) ||
                (xInput < 0 && !isFacingRight);

            if (holdingForward)
            {
                StartChargeSmash(
                    "ForwardAir",
                    hammerSprite,
                    13f
                );
            }
            else
            {
                StartChargeSmash(
                    "BackAir",
                    hammerSprite,
                    13f
                );
            }
        }
    }

    // ============================================================
    // SPECIAL ATTACKS
    // ============================================================

    private void DetermineSpecialAttack(int xInput)
    {
        // --------------------------------------------------------
        // UP SPECIAL
        // --------------------------------------------------------

        if (GetUpDirection())
        {
            if (playerType == PlayerID.Player1_Red)
            {
                ExecuteAttack(
                    "UpSpecialRed",
                    redUpSpecialSprite,
                    25f
                );

                velocity = new Vector2(
                    velocity.x * 0.5f,
                    redUpSpecialPower
                );
            }
            else
            {
                ExecuteAttack(
                    "UpSpecialBlue",
                    blueSideSpecialSprite,
                    5f
                );

                velocity = new Vector2(
                    0f,
                    blueUpSpecialPower
                );

                Invoke(
                    nameof(HaltMomentum),
                    0.05f
                );
            }

            currentState = State.Helpless;
            return;
        }

        // --------------------------------------------------------
        // DOWN SPECIAL
        // --------------------------------------------------------

        if (GetDown())
        {
            if (playerType == PlayerID.Player1_Red)
            {
                isReflecting = true;

                ExecuteAttack(
                    "DownSpecialRed",
                    redReflectorSprite,
                    5f
                );

                velocity = Vector2.zero;

                Invoke(
                    nameof(ResetReflect),
                    0.5f
                );
            }
            else
            {
                isChargingSpin = true;
                currentSpinCharge = 0;
                isAttacking = true;
            }

            return;
        }

        // --------------------------------------------------------
        // NEUTRAL SPECIAL
        // --------------------------------------------------------

        if (xInput == 0 &&
            !GetUpDirection() &&
            !GetDown())
        {
            if (playerType == PlayerID.Player1_Red)
            {
                ExecuteAttack(
                    "FireballThrow",
                    boxingGloveSprite,
                    0f
                );

                SpawnFireball();
            }
            else
            {
                isReflecting = true;
                isSpinning = true;

                ExecuteAttack(
                    "SpinAttack",
                    spinSprite,
                    15f
                );

                Invoke(
                    nameof(ResetReflect),
                    0.3f
                );
            }

            return;
        }

        // --------------------------------------------------------
        // SIDE SPECIAL
        // --------------------------------------------------------

        if (xInput != 0)
        {
            float moveDirection =
                isFacingRight ? 1f : -1f;

            if (playerType == PlayerID.Player1_Red)
            {
                isReflecting = true;

                ExecuteAttack(
                    "SideSpecialRed",
                    redSideSpecialSprite,
                    25f
                );

                velocity = new Vector2(
                    moveDirection * redLungeSpeed,
                    velocity.y
                );

                Invoke(
                    nameof(ResetReflect),
                    0.4f
                );
            }
            else
            {
                ExecuteAttack(
                    "SideSpecialBlue",
                    blueSideSpecialSprite,
                    5f
                );

                velocity = new Vector2(
                    moveDirection * blueDashSpeed,
                    0f
                );

                Invoke(
                    nameof(HaltMomentum),
                    0.05f
                );
            }
        }
    }

    private void SpawnFireball()
    {
        if (fireballPrefab == null)
            return;

        if (shootPoint == null)
        {
            Debug.LogWarning(
                $"{name}: Fireball shoot point is not assigned."
            );
            return;
        }

        GameObject fireball =
            Instantiate(
                fireballPrefab,
                shootPoint.position,
                Quaternion.identity
            );

        FireballScript fireballScript =
            fireball.GetComponent<FireballScript>();

        if (fireballScript != null)
        {
            fireballScript.Initialize(
                isFacingRight,
                this
            );
        }
        else
        {
            Debug.LogWarning(
                $"{name}: Fireball prefab does not contain FireballScript."
            );
        }
    }

    // ============================================================
    // ATTACK EXECUTION
    // ============================================================

    private void ExecuteAttack(
        string attackName,
        GameObject spriteToShow,
        float damage,
        GameObject secondarySprite = null,
        bool isMeteor = false,
        bool isInstaKill = false)
    {
        isAttacking = true;

        HideAllSprites();

        if (spriteToShow != null)
            spriteToShow.SetActive(true);

        if (secondarySprite != null)
            secondarySprite.SetActive(true);

        Collider2D[] hitPlayers =
            Physics2D.OverlapCircleAll(
                transform.position,
                attackRange
            );

        foreach (Collider2D hit in hitPlayers)
        {
            PlayerController enemyPlayer =
                hit.GetComponent<PlayerController>();

            if (enemyPlayer == null ||
                enemyPlayer == this)
            {
                continue;
            }

            Vector2 knockbackDirection =
                (
                    enemyPlayer.transform.position -
                    transform.position
                ).normalized;

            if (isMeteor &&
                enemyPlayer.currentState == State.Airborne)
            {
                knockbackDirection = Vector2.down;
            }
            else if (
                attackName == "UpAir" ||
                attackName == "UpSmash" ||
                attackName == "UpTilt")
            {
                knockbackDirection = Vector2.up;
            }
            else if (attackName == "SideSpecialRed")
            {
                float hitDirection =
                    enemyPlayer.transform.position.x >
                    transform.position.x
                        ? 1f
                        : -1f;

                knockbackDirection =
                    new Vector2(
                        hitDirection,
                        1f
                    ).normalized;
            }
            else if (attackName == "SideSpecialBlue")
            {
                float hitDirection =
                    enemyPlayer.transform.position.x >
                    transform.position.x
                        ? 1f
                        : -1f;

                knockbackDirection =
                    new Vector2(
                        hitDirection,
                        0.2f
                    ).normalized;
            }
            else if (attackName == "UpSpecialRed")
            {
                float hitDirection =
                    enemyPlayer.transform.position.x >
                    transform.position.x
                        ? 0.3f
                        : -0.3f;

                knockbackDirection =
                    new Vector2(
                        hitDirection,
                        1f
                    ).normalized;
            }
            else
            {
                knockbackDirection.y += 0.5f;
                knockbackDirection.Normalize();
            }

            enemyPlayer.TakeHit(
                damage,
                knockbackDirection,
                isInstaKill
            );
        }

        CancelInvoke(nameof(EndAttack));
        Invoke(nameof(EndAttack), 0.3f);
    }

    // ============================================================
    // DAMAGE / HITSTUN
    // ============================================================

    public void TakeHit(
        float incomingDamage,
        Vector2 knockbackDirection,
        bool isInstaKill = false)
    {
        if (currentState == State.Dodging)
            return;

        if (Time.time <= parryWindowEnd)
            return;

        // Shield damage.
        if (currentState == State.Shielding)
        {
            currentShieldHealth -= incomingDamage;

            UpdateShieldVisual();

            if (currentShieldHealth <= 0f)
                TriggerShieldBreak();

            return;
        }

        isAttacking = false;
        isChargingSpin = false;
        currentSpinCharge = 0;

        CancelInvoke(nameof(EndAttack));
        CancelInvoke(nameof(ResetReflect));

        isReflecting = false;
        isSpinning = false;

        HideAllSprites();

        currentDamage += incomingDamage;
        UpdateDamageUI();

        currentState = State.Hitstun;

        float knockbackForce =
            (
                currentDamage *
                incomingDamage
            ) / Mathf.Max(stats.weight, 1f);

        knockbackForce =
            Mathf.Clamp(
                knockbackForce,
                5f,
                100f
            );

        if (isInstaKill)
        {
            knockbackForce = 100f;
            knockbackDirection = Vector2.down;
        }

        velocity =
            knockbackDirection.normalized *
            knockbackForce *
            0.2f;

        if (hitlagCoroutine != null)
            StopCoroutine(hitlagCoroutine);

        hitlagCoroutine =
            StartCoroutine(
                ApplyHitlag(incomingDamage)
            );

        CancelInvoke(nameof(EndHitstun));

        float hitstunDuration =
            0.3f +
            knockbackForce * 0.015f;

        Invoke(
            nameof(EndHitstun),
            hitstunDuration
        );
    }

    private void UpdateDamageUI()
    {
        if (damageUI != null)
        {
            damageUI.text =
                Mathf.FloorToInt(currentDamage)
                .ToString() +
                "%";
        }
    }

    // ============================================================
    // JAB
    // ============================================================

    private void ExecuteJab()
    {
        if (Time.time - lastJabTime > 0.5f)
            currentJabCombo = 0;

        currentJabCombo++;
        lastJabTime = Time.time;

        float damage =
            currentJabCombo == 3
                ? 4f
                : 2f;

        ExecuteAttack(
            $"Jab hit {currentJabCombo}",
            boxingGloveSprite,
            damage
        );

        if (currentJabCombo >= 3)
            currentJabCombo = 0;
    }

    // ============================================================
    // SMASH CHARGING
    // ============================================================

    private void StartChargeSmash(
        string attackName,
        GameObject sprite,
        float baseDamage,
        GameObject secondary = null)
    {
        currentState = State.ChargingSmash;

        velocity = Vector2.zero;
        chargeTimer = 0f;

        pendingAttackName = attackName;
        pendingDamage = baseDamage;
        pendingSprite = sprite;
        pendingSecondarySprite = secondary;

        HideAllSprites();

        if (pendingSprite != null)
            pendingSprite.SetActive(true);

        if (pendingSecondarySprite != null)
            pendingSecondarySprite.SetActive(true);
    }

    private void ReleaseSmash()
    {
        transform.position = new Vector3(
            rb.position.x,
            rb.position.y,
            transform.position.z
        );

        bool isAerialCharge =
            pendingAttackName == "ForwardAir" ||
            pendingAttackName == "BackAir" ||
            pendingAttackName == "DownAir";

        currentState =
            isAerialCharge
                ? State.Airborne
                : State.Grounded;

        float chargePercent =
            Mathf.Clamp01(
                chargeTimer / maxChargeTime
            );

        float finalDamage =
            pendingDamage *
            Mathf.Lerp(
                1f,
                maxChargeMultiplier,
                chargePercent
            );

        bool isMeteor = false;
        bool isInstaKill = false;

        if (isAerialCharge)
        {
            finalDamage =
                pendingDamage *
                Mathf.Lerp(
                    1f,
                    3f,
                    chargePercent
                );

            isMeteor = true;

            if (chargePercent >= 1f)
                isInstaKill = true;
        }

        ExecuteAttack(
            pendingAttackName,
            pendingSprite,
            finalDamage,
            pendingSecondarySprite,
            isMeteor,
            isInstaKill
        );

        pendingAttackName = null;
        pendingSprite = null;
        pendingSecondarySprite = null;
        pendingDamage = 0f;
        chargeTimer = 0f;
    }

    // ============================================================
    // DODGES
    // ============================================================

    private void ExecuteSpotDodge()
    {
        currentState = State.Dodging;

        if (shieldBubble != null)
            shieldBubble.SetActive(false);

        velocity = Vector2.zero;

        CancelInvoke(nameof(EndDodge));

        Invoke(
            nameof(EndDodge),
            0.4f
        );
    }

    private void ExecuteRoll()
    {
        currentState = State.Dodging;

        if (shieldBubble != null)
            shieldBubble.SetActive(false);

        int direction =
            GetLeft()
                ? -1
                : 1;

        velocity = new Vector2(
            direction *
            stats.runSpeed *
            1.5f,
            0f
        );

        CancelInvoke(nameof(EndDodge));

        Invoke(
            nameof(EndDodge),
            0.5f
        );
    }

    private void ExecuteAirDodge(
        int dirX,
        int dirY)
    {
        currentState = State.Dodging;

        if (shieldBubble != null)
            shieldBubble.SetActive(false);

        velocity = Vector2.zero;

        if (dirX != 0 || dirY != 0)
        {
            velocity =
                new Vector2(
                    dirX,
                    dirY
                ).normalized *
                stats.airSpeed *
                2f;
        }

        CancelInvoke(nameof(EndAirDodge));

        Invoke(
            nameof(EndAirDodge),
            0.4f
        );
    }

    private void EndAirDodge()
    {
        HideAllSprites();

        if (currentState == State.Dodging)
            currentState = State.Helpless;
    }

    private void EndDodge()
    {
        HideAllSprites();

        if (Mathf.Abs(velocity.y) > 0.1f)
            currentState = State.Airborne;
        else
            currentState = State.Grounded;
    }

    // ============================================================
    // HITSTUN
    // ============================================================

    private void EndHitstun()
    {
        HideAllSprites();

        if (Mathf.Abs(velocity.y) > 0.1f)
            currentState = State.Airborne;
        else
            currentState = State.Grounded;
    }

    // ============================================================
    // SPECIAL HELPERS
    // ============================================================

    private void HaltMomentum()
    {
        velocity.x = 0f;

        if (velocity.y > 0f)
            velocity.y = 0f;
    }

    private void ResetReflect()
    {
        isReflecting = false;
        isSpinning = false;

        transform.rotation =
            Quaternion.identity;
    }

    // ============================================================
    // HITLAG
    // ============================================================

    private IEnumerator ApplyHitlag(float damage)
    {
        float hitlagDuration =
            0.02f +
            damage * 0.005f;

        float previousTimeScale =
            Time.timeScale;

        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(
            hitlagDuration
        );

        // Only restore the game if something else
        // hasn't deliberately changed the time scale.
        if (Time.timeScale == 0f)
            Time.timeScale = previousTimeScale <= 0f
                ? 1f
                : previousTimeScale;

        hitlagCoroutine = null;
    }

    // ============================================================
    // COLLISIONS
    // ============================================================

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Ground"))
            return;

        if (currentState == State.Helpless ||
            currentState == State.Airborne)
        {
            jumpsRemaining = 1;
            currentState = State.Grounded;
        }
        else if (currentState == State.Dodging)
        {
            if (velocity.y < 0f &&
                Mathf.Abs(velocity.x) > 0.1f)
            {
                velocity.x *= 1.5f;
            }

            currentState = State.Grounded;
        }

        // Hitstun is intentionally not immediately
        // canceled by touching the ground.
        if (currentState != State.Hitstun)
        {
            currentState = State.Grounded;
            velocity.y = 0f;
        }

        isFastFalling = false;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Ground"))
            return;

        if (currentState != State.Hitstun &&
            currentState != State.Helpless &&
            currentState != State.Dodging &&
            currentState != State.ChargingSmash)
        {
            currentState = State.Airborne;
        }
    }

    // ============================================================
    // BLAST ZONE / KO
    // ============================================================

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("BlastZone"))
        {
            ExecuteKO();
        }
    }

    private void ExecuteKO()
    {
        Time.timeScale = 1f;

        currentDamage = 0f;
        isAttacking = false;

        isChargingSpin = false;
        currentSpinCharge = 0;

        isReflecting = false;
        isSpinning = false;

        if (hitlagCoroutine != null)
        {
            StopCoroutine(hitlagCoroutine);
            hitlagCoroutine = null;
        }

        if (damageUI != null)
            damageUI.text = "0%";

        velocity = Vector2.zero;

        CancelInvoke();

        rb.linearVelocity = Vector2.zero;
        rb.position = respawnPoint;

        transform.position = respawnPoint;
        transform.rotation = Quaternion.identity;

        currentState = State.Airborne;

        currentShieldHealth =
            maxShieldHealth;

        isFastFalling = false;
        jumpsRemaining = 1;

        HideAllSprites();

        if (shieldBubble != null)
        {
            shieldBubble.SetActive(false);
            shieldBubble.transform.localScale =
                originalShieldScale;
        }
    }

    // ============================================================
    // SHIELD
    // ============================================================

    private void UpdateShieldVisual()
    {
        if (shieldBubble == null ||
            maxShieldHealth <= 0f)
            return;

        float percentage =
            Mathf.Clamp(
                currentShieldHealth /
                maxShieldHealth,
                0.2f,
                1f
            );

        shieldBubble.transform.localScale =
            originalShieldScale *
            percentage;
    }

    private void TriggerShieldBreak()
    {
        currentState = State.Hitstun;

        if (shieldBubble != null)
            shieldBubble.SetActive(false);

        // Shield break launches the player upward.
        velocity = new Vector2(
            0f,
            stats.jumpHeight
        );

        currentShieldHealth =
            maxShieldHealth;

        UpdateShieldVisual();
    }

    // ============================================================
    // SPRITE MANAGEMENT
    // ============================================================

    private void HideAllSprites()
    {
        if (boxingGloveSprite != null)
            boxingGloveSprite.SetActive(false);

        if (backBoxingGloveSprite != null)
            backBoxingGloveSprite.SetActive(false);

        if (hammerSprite != null)
            hammerSprite.SetActive(false);

        if (spikeHelmetSprite != null)
            spikeHelmetSprite.SetActive(false);

        if (bootSprite != null)
            bootSprite.SetActive(false);

        if (upBoxingGloveSprite != null)
            upBoxingGloveSprite.SetActive(false);

        if (downBoxingGloveSprite != null)
            downBoxingGloveSprite.SetActive(false);

        if (spinSprite != null)
            spinSprite.SetActive(false);

        if (redSideSpecialSprite != null)
            redSideSpecialSprite.SetActive(false);

        if (blueSideSpecialSprite != null)
            blueSideSpecialSprite.SetActive(false);

        if (redUpSpecialSprite != null)
            redUpSpecialSprite.SetActive(false);

        if (redReflectorSprite != null)
            redReflectorSprite.SetActive(false);
    }

    private void EndAttack()
    {
        HideAllSprites();
        isAttacking = false;
    }

    // ============================================================
    // DEBUG
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );
    }
}
