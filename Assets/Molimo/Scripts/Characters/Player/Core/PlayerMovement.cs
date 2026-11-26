using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace MolimoByLulucaGameDev
{

    public class PlayerMovement : MonoBehaviour
    {

        [Header("Camera & Orientation")]
        [Tooltip("If checked, movement is relative to Camera view (Isometric). If unchecked, moves directly along World axes (Grid / World).")]
        [SerializeField] private bool useCameraRelativeMovement = true;
        [Tooltip("Camera used to orient movement (if null, automatically searches is Camera.main).")]
        [SerializeField] private Transform cameraTransform;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        [Header("Parameters Names")]
        [SerializeField] private string isGroundedParameterName = "IsGrounded";
        [SerializeField] private string speedParameterName = "Speed";
        [SerializeField] private string isJumpingParameterName = "IsJumping";
        [SerializeField] private string isFallingParameterName = "IsFalling";
        [SerializeField] private string isLandingParameterName = "IsLanding";
        [SerializeField] private string isSprintingParameterName = "IsSprinting";
        [SerializeField] private string isRollingParameterName = "IsRolling";
        [SerializeField] private string isExhaustedParameterName = "IsExhausted";

        [Header("Ground Check")]
        [Tooltip("layers consideradas ground. the CheckSphere uses Ground + Roof combinados.")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Tooltip("layers treated as Roof/roof. Stepping on a Roof counts as grounded.")]
        [SerializeField] private LayerMask roofLayer;

        [SerializeField] private float groundCheckSkinWidth = 0.01f;

        private int groundAndRoofMask;

        [Header("Speed Settings")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float runSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;

        [Header("Sprint VFX")]
        [Tooltip("Ghost trail gerado continuamente while the Player is in Sprint.")]
        [SerializeField] private GhostTrail ghostTrail;
        [Tooltip("speed with that the personagem rotates is the nova direction.")]
        [SerializeField] private float rotationSpeed = 15f;

        [Header("Locomotion Settings")]
        [Tooltip("magnitude minimum of the analog stick is exit of Idle (0). below disto the Speed of the Animator is exactly 0.")]
        [SerializeField] private float walkThreshold = 0.1f;
        [Tooltip("magnitude of the analog stick the partir of the which the Speed of the Animator vai direct is Run (1). between walkThreshold and runThreshold the Speed remains exactly in Walk (0.5).")]
        [SerializeField] private float runThreshold = 0.6f;

        [Header("Jump Settings")]
        [SerializeField] private float jumpHeight = 4f;
        [SerializeField] private float landingDuration = 0.15f;
        [SerializeField] private float gravityScale = 2.5f;
        [SerializeField] private float fallingGravityMultiplier = 1.2f;

        [Header("Roll Settings")]
        [SerializeField] private float rollSpeed = 10f;
        [SerializeField] private float rollDuration = 0.3f;
        [SerializeField] private float rollCooldown = 1.5f;
        [Tooltip("window of time in seconds is registar the Roll if pressionado in the ar little before aterrar.")]
        [SerializeField] private float rollInputBufferTime = 0.25f;

        [Header("Exhausted Settings")]
        [Tooltip("Multiplicador applied to the speed physics (Walk/Run) while the Player is Exhausted.")]
        [SerializeField] private float exhaustedSpeedMultiplier = 0.5f;

        [Header("Knockback Settings")]
        [SerializeField] private float knockbackSpeed = 6f;
        [SerializeField] private float knockbackDuration = 0.18f;

        [Header("Block Recoil Settings")]
        [SerializeField] private float blockRecoilSpeed = 3.5f;
        [SerializeField] private float blockRecoilDuration = 0.12f;

        [Header("Roll Collision Ignore")]
        [Tooltip("during the Roll, the Player ignores collision physics with colliders nestas layers " +
                 "(ex.: 'Enemy'). Deixa empty is disable.")]
        [FormerlySerializedAs("rollDashIgnoreLayers")]
        [SerializeField] private LayerMask rollIgnoreLayers;

        [Header("Debug")]
        [Tooltip("writes in the Console the motivo by which the Sprint not arranca (only when the motivo changes).")]
        [SerializeField] private bool debugSprint = false;

        private string lastSprintDebugReason = null;

        private Rigidbody rb;
        private CapsuleCollider capsuleCollider;
        private PlayerBrain brain;

        private Vector2 moveInput;

        private bool isInitialized = false;
        private bool isActive = false;
        private bool isGrounded = true;

        private float currentAnimatorSpeed = 0f;
        private float currentPhysicalSpeed = 0f;

        private bool isJumping = false;
        private bool isFalling = false;
        private bool isLanding = false;
        private float landingTimer = 0f;

        private bool isMovementInputLocked = false;
        private bool isSuspended = false;

        private bool sprintInputPressed = false;
        private bool isSprinting = false;

        private bool isRolling = false;
        private float rollTimer = 0f;
        private float rollCooldownTimer = 0f;
        private float rollBufferTimer = 0f;
        private Vector3 rollDirection = Vector3.zero;

        private bool isDashAttacking = false;

        private bool isExhausted = false;

        private bool isKnockedBack = false;
        private float knockbackTimer = 0f;
        private float currentKnockbackMaxDuration = 0.18f;
        private float currentKnockbackMaxSpeed = 6f;
        private Vector3 knockbackDirection = Vector3.zero;

        private InputSystem_Actions inputActions;

        private int speedParameterHash;
        private int isGroundedParameterHash;
        private int isJumpingParameterHash;
        private int isFallingParameterHash;
        private int isLandingParameterHash;
        private int isSprintingParameterHash;
        private int isRollingParameterHash;
        private int isExhaustedParameterHash;

        private Collider[] playerSolidColliders;
        private LayerMask[] originalExcludeLayersCache;
        private bool rollExclusionApplied = false;

        public bool IsInitialized => isInitialized;
        public bool IsActive => isActive;
        public bool IsGrounded => isGrounded;
        public float CurrentSpeed => currentAnimatorSpeed;
        public float CurrentPhysicalSpeed => currentPhysicalSpeed;

        public bool UseCameraRelativeMovement
        {
            get => useCameraRelativeMovement;
            set => useCameraRelativeMovement = value;
        }

        public bool IsJumping => isJumping;
        public bool IsFalling => isFalling;
        public bool IsLanding => isLanding;
        public bool IsSprinting => isSprinting;

        public bool IsRolling => isRolling;
        public float RollCooldownRemaining => rollCooldownTimer;
        public bool IsDashAttacking => isDashAttacking;

        public bool IsExhausted => isExhausted;
        public bool IsKnockedBack => isKnockedBack;
        public bool IsSuspended => isSuspended;

        private void Awake()
        {
            InitializeAnimator();
        }

        public void Initialize(PlayerBrain brainReference, Rigidbody rigidbody)
        {
            if (rigidbody == null)
            {
                Debug.LogError("Movement cannot be initialized with the null Rigidbody.");
                return;
            }

            brain = brainReference;
            rb = rigidbody;
            capsuleCollider = GetComponent<CapsuleCollider>();

            if (ghostTrail == null) ghostTrail = GetComponent<GhostTrail>();
            if (ghostTrail == null) ghostTrail = GetComponentInChildren<GhostTrail>(true);

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            groundAndRoofMask = groundLayer.value | roofLayer.value;

            if (groundAndRoofMask == 0)
            {
                Debug.LogWarning("[Movement] groundLayer and roofLayer are AMBOS vazios. " +
                                 "the Ground Check never will detect surface.");
            }

            CachePlayerCollidersForExclusion();

            InitializeInputSystem();
            InitializeAnimator();

            isInitialized = true;
            SetActive(true);

            if (debugSprint)
            {
                Debug.Log("[Movement] Bindings -> Sprint: " + inputActions.Player.Sprint.GetBindingDisplayString() +
                          " | Roll: " + inputActions.Player.Roll.GetBindingDisplayString() +
                          " | Jump: " + inputActions.Player.Jump.GetBindingDisplayString(), this);
            }
        }

        private void InitializeInputSystem()
        {
            inputActions = new InputSystem_Actions();

            inputActions.Player.Move.performed += OnMovePerformed;
            inputActions.Player.Move.canceled += OnMoveCanceled;

            inputActions.Player.Jump.performed += OnJumpPerformed;
            inputActions.Player.Jump.canceled += OnJumpCanceled;

            inputActions.Player.Sprint.performed += OnSprintPerformed;
            inputActions.Player.Sprint.canceled += OnSprintCanceled;

            inputActions.Player.Roll.performed += OnRollPerformed;
            inputActions.Player.Roll.canceled += OnRollCanceled;
        }

        private void InitializeAnimator()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (animator != null)
            {
                speedParameterHash = Animator.StringToHash(speedParameterName);
                isGroundedParameterHash = Animator.StringToHash(isGroundedParameterName);
                isJumpingParameterHash = Animator.StringToHash(isJumpingParameterName);
                isFallingParameterHash = Animator.StringToHash(isFallingParameterName);
                isLandingParameterHash = Animator.StringToHash(isLandingParameterName);
                isSprintingParameterHash = Animator.StringToHash(isSprintingParameterName);
                isRollingParameterHash = Animator.StringToHash(isRollingParameterName);
                isExhaustedParameterHash = Animator.StringToHash(isExhaustedParameterName);
            }
        }

        private void CachePlayerCollidersForExclusion()
        {
            Collider[] all = GetComponentsInChildren<Collider>(true);
            List<Collider> solid = new List<Collider>(all.Length);

            for (int i = 0; i < all.Length; i++)
            {
                Collider c = all[i];
                if (c == null) continue;
                if (c.isTrigger) continue;
                solid.Add(c);
            }

            playerSolidColliders = solid.ToArray();
            originalExcludeLayersCache = new LayerMask[playerSolidColliders.Length];

            if (playerSolidColliders.Length == 0)
            {
                Debug.LogWarning("[Movement] none collider solid found in the Player is the " +
                                 "Roll Collision Ignore.");
            }
        }

        private void SyncRollCollisionExclusion()
        {
            bool shouldExclude = isRolling && rollIgnoreLayers.value != 0;

            if (shouldExclude == rollExclusionApplied) return;

            if (playerSolidColliders == null || playerSolidColliders.Length == 0)
            {
                rollExclusionApplied = false;
                return;
            }

            if (shouldExclude)
            {
                for (int i = 0; i < playerSolidColliders.Length; i++)
                {
                    Collider c = playerSolidColliders[i];
                    if (c == null) continue;

                    originalExcludeLayersCache[i] = c.excludeLayers;
                    c.excludeLayers = originalExcludeLayersCache[i] | rollIgnoreLayers;
                }
            }
            else
            {
                for (int i = 0; i < playerSolidColliders.Length; i++)
                {
                    Collider c = playerSolidColliders[i];
                    if (c == null) continue;

                    c.excludeLayers = originalExcludeLayersCache[i];
                }
            }

            rollExclusionApplied = shouldExclude;
        }

        private void ForceRemoveRollCollisionExclusion()
        {
            if (!rollExclusionApplied) return;

            if (playerSolidColliders != null)
            {
                for (int i = 0; i < playerSolidColliders.Length; i++)
                {
                    Collider c = playerSolidColliders[i];
                    if (c == null) continue;

                    c.excludeLayers = originalExcludeLayersCache[i];
                }
            }

            rollExclusionApplied = false;
        }

        public void SetActive(bool active)
        {
            isActive = active;

            if (isActive)
            {
                if (inputActions != null)
                {

                    inputActions.Player.Sprint.Enable();

                    if (!isMovementInputLocked)
                    {
                        inputActions.Player.Move.Enable();
                        inputActions.Player.Jump.Enable();
                        inputActions.Player.Roll.Enable();
                    }
                }
            }
            else
            {
                if (inputActions != null)
                {
                    inputActions.Player.Move.Disable();
                    inputActions.Player.Jump.Disable();
                    inputActions.Player.Sprint.Disable();
                    inputActions.Player.Roll.Disable();
                }

                moveInput = Vector2.zero;
                isMovementInputLocked = false;
                currentAnimatorSpeed = 0f;
                currentPhysicalSpeed = 0f;

                isJumping = false;
                isFalling = false;
                isLanding = false;
                landingTimer = 0f;

                sprintInputPressed = false;
                SetSprintingState(false);

                isRolling = false;
                rollTimer = 0f;
                rollBufferTimer = 0f;
                rollDirection = Vector3.zero;

                isDashAttacking = false;

                isExhausted = false;

                isKnockedBack = false;
                knockbackTimer = 0f;
                knockbackDirection = Vector3.zero;

                if (rb != null && !rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                UpdateAnimator();
                ForceRemoveRollCollisionExclusion();
            }
        }

        public void SetMovementInputLocked(bool locked)
        {
            isMovementInputLocked = locked;

            if (inputActions == null) return;

            if (locked)
            {
                inputActions.Player.Move.Disable();
                inputActions.Player.Jump.Disable();
                inputActions.Player.Roll.Disable();
                moveInput = Vector2.zero;
            }
            else if (isActive)
            {
                inputActions.Player.Move.Enable();
                inputActions.Player.Jump.Enable();
                inputActions.Player.Roll.Enable();
                inputActions.Player.Sprint.Enable();
            }
        }

        public void SetSuspended(bool suspended)
        {
            isSuspended = suspended;

            if (isSuspended)
            {
                isJumping = false;
                isFalling = false;
                isLanding = false;
                landingTimer = 0f;
                SetSprintingState(false);
                isDashAttacking = false;
                currentAnimatorSpeed = 0f;
                currentPhysicalSpeed = 0f;

                if (animator != null)
                {
                    if (speedParameterHash != 0) animator.SetFloat(speedParameterHash, 0f);
                    if (isJumpingParameterHash != 0) animator.SetBool(isJumpingParameterHash, false);
                    if (isFallingParameterHash != 0) animator.SetBool(isFallingParameterHash, false);
                    if (isLandingParameterHash != 0) animator.SetBool(isLandingParameterHash, false);
                    if (isSprintingParameterHash != 0) animator.SetBool(isSprintingParameterHash, false);
                }

                ForceRemoveRollCollisionExclusion();
                return;
            }

            isJumping = false;
            isFalling = false;
            isLanding = false;
            landingTimer = 0f;

            CheckGrounded();
            UpdateAnimator();
        }

        public void ResetMotionStateForTeleport()
        {
            moveInput = Vector2.zero;
            sprintInputPressed = false;

            isJumping = false;
            isFalling = false;
            isLanding = false;
            landingTimer = 0f;

            SetSprintingState(false);

            isRolling = false;
            rollTimer = 0f;
            rollBufferTimer = 0f;
            rollDirection = Vector3.zero;

            isDashAttacking = false;

            isKnockedBack = false;
            knockbackTimer = 0f;
            knockbackDirection = Vector3.zero;

            currentAnimatorSpeed = 0f;
            currentPhysicalSpeed = 0f;

            UpdateAnimator();
            ForceRemoveRollCollisionExclusion();
        }

        public void TickMovement()
        {
            if (!isInitialized || !isActive || isSuspended) return;
            if (brain == null || !brain.IsAlive) return;

            CheckGrounded();
            UpdateJumpState();
            UpdateLandingState();
            UpdateSprintState();

            UpdateRollCooldown();
            UpdateRollTimer();
            UpdateRollBuffer();
            UpdateKnockbackTimer();
            UpdateAnimator();

            SyncRollCollisionExclusion();
        }

        public void TickPhysics()
        {
            if (!isInitialized || !isActive || isSuspended) return;
            if (brain == null || !brain.IsAlive) return;

            if (isDashAttacking) return;

            ApplyJumpPhysics();

            if (isKnockedBack)
            {
                ApplyKnockbackMovement();
            }
            else if (isRolling)
            {
                ApplyRollMovement();
            }
            else
            {
                currentAnimatorSpeed = CalculateDiscreteSpeed();
                currentPhysicalSpeed = CalculatePhysicalSpeed();

                ApplyMovement();
                ApplyRotation();
            }
        }

        private bool IsSprintHeld()
        {
            if (sprintInputPressed) return true;
            if (inputActions == null) return false;

            InputAction sprint = inputActions.Player.Sprint;
            return sprint.enabled && sprint.IsPressed();
        }

        private string GetSprintBlockReason()
        {
            if (!IsSprintHeld())
            {
                bool actionEnabled = inputActions != null && inputActions.Player.Sprint.enabled;
                return "tecla of Sprint not detetada (Sprint.enabled = " + actionEnabled + ")";
            }
            if (isMovementInputLocked) return "movement locked (isMovementInputLocked)";
            if (moveInput.sqrMagnitude <= 0.01f) return "without input of movement (moveInput = 0)";
            if (!isGrounded) return "isGrounded = false";
            if (isExhausted) return "isExhausted = true";
            if (isRolling) return "isRolling = true";
            if (isJumping) return "isJumping = true";
            if (isKnockedBack) return "isKnockedBack = true";
            if (brain == null) return "brain = null";
            if (brain.stamina <= 0f) return "brain.stamina = " + brain.stamina;
            return null;
        }

        private void SetSprintingState(bool sprinting)
        {
            if (isSprinting == sprinting) return;

            isSprinting = sprinting;

            if (ghostTrail == null) return;

            if (isSprinting)
                ghostTrail.StartGhostTrail();
            else
                ghostTrail.StopGhostTrail();
        }

        private void UpdateSprintState()
        {
            string blockReason = GetSprintBlockReason();

            if (debugSprint && blockReason != lastSprintDebugReason)
            {
                lastSprintDebugReason = blockReason;
                Debug.Log("[Movement] Sprint: " + (blockReason ?? "OK, the correr in sprint"), this);
            }

            if (blockReason == null)
            {
                SetSprintingState(true);
                brain.ConsumeSprintStamina(Time.deltaTime);
            }
            else
            {
                SetSprintingState(false);
            }
        }

        private Vector3 GetInputDirection()
        {
            if (moveInput.sqrMagnitude <= 0.01f) return Vector3.zero;

            if (!useCameraRelativeMovement)
                return new Vector3(moveInput.x, 0f, moveInput.y);

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (cameraTransform == null)
                return new Vector3(moveInput.x, 0f, moveInput.y);

            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            return (camForward * moveInput.y + camRight * moveInput.x);
        }

        private void CheckGrounded()
        {
            if (capsuleCollider == null || rb == null)
            {
                isGrounded = rb != null && Mathf.Abs(rb.linearVelocity.y) < 0.01f;
                return;
            }

            Vector3 capsuleBottom = new Vector3(
                capsuleCollider.bounds.center.x,
                capsuleCollider.bounds.min.y + groundCheckSkinWidth,
                capsuleCollider.bounds.center.z
            );

            float checkRadius = capsuleCollider.radius * 0.9f;

            isGrounded = Physics.CheckSphere(capsuleBottom, checkRadius, groundAndRoofMask);

            if (isLanding && !isRolling) isGrounded = true;
        }

        private float CalculateDiscreteSpeed()
        {
            if (isMovementInputLocked) return 0f;
            if (isExhausted) return 0.5f;

            float inputMagnitude = moveInput.magnitude;

            if (inputMagnitude < walkThreshold) return 0f;
            if (inputMagnitude < runThreshold) return 0.5f;

            return 1f;
        }

        private float CalculatePhysicalSpeed()
        {
            if (isMovementInputLocked) return 0f;
            if (isExhausted) return walkSpeed * exhaustedSpeedMultiplier;

            if (currentAnimatorSpeed <= 0f) return 0f;

            if (isSprinting) return sprintSpeed;

            return currentAnimatorSpeed >= 1f ? runSpeed : walkSpeed;
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            if (speedParameterHash == 0)
            {
                InitializeAnimator();
                if (speedParameterHash == 0) return;
            }

            currentAnimatorSpeed = CalculateDiscreteSpeed();
            currentPhysicalSpeed = CalculatePhysicalSpeed();

            animator.SetFloat(speedParameterHash, currentAnimatorSpeed);
            animator.SetBool(isGroundedParameterHash, isGrounded);
            animator.SetBool(isJumpingParameterHash, isJumping);
            animator.SetBool(isFallingParameterHash, isFalling);
            animator.SetBool(isLandingParameterHash, isLanding);
            animator.SetBool(isSprintingParameterHash, isSprinting);
            animator.SetBool(isExhaustedParameterHash, isExhausted);
        }

        private void ApplyMovement()
        {
            if (rb == null || rb.isKinematic) return;

            if (isMovementInputLocked)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                return;
            }

            float currentY = rb.linearVelocity.y;
            if (!isJumping && currentY > 0f) currentY = 0f;

            if (isExhausted)
            {
                Vector3 forwardMovement = transform.forward * currentPhysicalSpeed;
                rb.linearVelocity = new Vector3(forwardMovement.x, currentY, forwardMovement.z);
                return;
            }

            if (moveInput.sqrMagnitude <= 0.01f)
            {
                rb.linearVelocity = new Vector3(0f, currentY, 0f);
                return;
            }

            Vector3 inputDir = GetInputDirection();
            Vector3 eightDirection = GetEightDirection(inputDir);

            Vector3 movement = eightDirection * currentPhysicalSpeed;

            rb.linearVelocity = new Vector3(movement.x, currentY, movement.z);
        }

        private void ApplyRotation()
        {
            if (rb == null || rb.isKinematic || isMovementInputLocked || moveInput.sqrMagnitude <= 0.01f) return;

            Vector3 inputDir = GetInputDirection();
            Vector3 eightDirection = GetEightDirection(inputDir);

            Quaternion targetRotation = Quaternion.LookRotation(eightDirection, Vector3.up);

            Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }

        private Vector3 GetEightDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f) return Vector3.zero;

            float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            angle = Mathf.Round(angle / 45f) * 45f;

            float radians = angle * Mathf.Deg2Rad;

            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)).normalized;
        }

        public void TryStartJump()
        {
            if (isMovementInputLocked || !isGrounded || isJumping || isFalling || isRolling || isKnockedBack) return;

            if (isLanding)
            {
                isLanding = false;
                landingTimer = 0f;
            }

            StartJump();
        }

        private void StartJump()
        {
            if (rb == null || rb.isKinematic) return;

            float gravity = Mathf.Abs(Physics.gravity.y) * gravityScale;
            float jumpVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);

            Vector3 velocity = rb.linearVelocity;
            velocity.y = jumpVelocity;
            rb.linearVelocity = velocity;

            isJumping = true;
            isFalling = false;
            isLanding = false;

            brain?.ConsumeJumpStamina();
        }

        private void UpdateJumpState()
        {
            if (rb == null || isLanding || isRolling) return;

            float verticalVelocity = rb.linearVelocity.y;

            if (isJumping && verticalVelocity < -0.01f)
            {
                isJumping = false;
                isFalling = true;
            }
            else if (!isJumping && !isFalling && !isGrounded && !isRolling && verticalVelocity < -0.1f)
            {
                isFalling = true;
            }
            else if (isFalling && isGrounded)
            {
                isFalling = false;
                isLanding = true;
                landingTimer = landingDuration;
            }
        }

        private void UpdateLandingState()
        {
            if (!isLanding) return;

            if (isRolling)
            {
                isLanding = false;
                landingTimer = 0f;
                return;
            }

            landingTimer -= Time.deltaTime;
            if (landingTimer <= 0f)
            {
                isLanding = false;
                landingTimer = 0f;
            }
        }

        private void ApplyJumpPhysics()
        {
            if (rb == null || rb.isKinematic || (!isJumping && !isFalling && !isLanding)) return;

            float gravity = Physics.gravity.y * gravityScale;

            if (isFalling && rb.linearVelocity.y < 0f)
                gravity *= fallingGravityMultiplier;

            Vector3 velocity = rb.linearVelocity;
            velocity.y += gravity * Time.fixedDeltaTime;

            if (velocity.y < -30f) velocity.y = -30f;

            rb.linearVelocity = velocity;
        }

        public void TryStartRoll()
        {
            if (isMovementInputLocked || isRolling || isKnockedBack || rollCooldownTimer > 0f || !isGrounded) return;

            rollBufferTimer = 0f;

            if (moveInput.sqrMagnitude < 0.01f)
            {
                Vector3 forwardDirection = transform.forward;
                rollDirection = GetEightDirection(forwardDirection);
            }
            else
            {
                Vector3 inputDir = GetInputDirection();
                rollDirection = GetEightDirection(inputDir);
            }

            StartRoll();
        }

        private void StartRoll()
        {
            isRolling = true;
            rollTimer = rollDuration;
            rollCooldownTimer = rollCooldown;

            isJumping = false;
            isFalling = false;
            isLanding = false;
            landingTimer = 0f;

            brain?.ConsumeRollStamina();

            if (animator != null)
            {
                if (isJumpingParameterHash != 0) animator.SetBool(isJumpingParameterHash, false);
                if (isFallingParameterHash != 0) animator.SetBool(isFallingParameterHash, false);
                if (isLandingParameterHash != 0) animator.SetBool(isLandingParameterHash, false);

                if (isRollingParameterHash != 0) animator.SetTrigger(isRollingParameterHash);
            }
        }

        private void EndRoll()
        {
            isRolling = false;
            rollDirection = Vector3.zero;
        }

        private void UpdateRollCooldown()
        {
            if (rollCooldownTimer > 0f)
            {
                rollCooldownTimer -= Time.deltaTime;
                if (rollCooldownTimer <= 0f) rollCooldownTimer = 0f;
            }
        }

        private void UpdateRollBuffer()
        {
            if (rollBufferTimer > 0f)
            {
                rollBufferTimer -= Time.deltaTime;
                if (rollBufferTimer <= 0f) rollBufferTimer = 0f;

                if (isGrounded && rollBufferTimer > 0f)
                    TryStartRoll();
            }
        }

        private void UpdateRollTimer()
        {
            if (!isRolling) return;

            rollTimer -= Time.deltaTime;
            if (rollTimer <= 0f) EndRoll();
        }

        private void ApplyRollMovement()
        {
            if (rb == null || rb.isKinematic) return;

            float verticalVelocity = rb.linearVelocity.y;
            rb.linearVelocity = new Vector3(rollDirection.x * rollSpeed, verticalVelocity, rollDirection.z * rollSpeed);
        }

        public void SetExhausted(bool exhausted)
        {
            isExhausted = exhausted;

            if (animator != null && isExhaustedParameterHash != 0)
                animator.SetBool(isExhaustedParameterHash, isExhausted);
        }

        public void SetDashAttacking(bool dashing)
        {
            if (isDashAttacking == dashing) return;

            isDashAttacking = dashing;

            if (dashing) SetSprintingState(false);
        }

        public void ApplyKnockback()
        {
            if (rb == null) return;

            knockbackDirection = GetEightDirection(-transform.forward);
            if (knockbackDirection == Vector3.zero) knockbackDirection = -transform.forward;

            currentKnockbackMaxSpeed = knockbackSpeed;
            currentKnockbackMaxDuration = knockbackDuration;

            isKnockedBack = true;
            knockbackTimer = knockbackDuration;

            isRolling = false;
            isJumping = false;
            isFalling = false;
            isLanding = false;
            landingTimer = 0f;
        }

        public void ApplyBlockRecoil(Vector3 attackerPosition)
        {
            if (rb == null) return;

            Vector3 dir = transform.position - attackerPosition;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.001f) dir = -transform.forward;
            else dir.Normalize();

            knockbackDirection = GetEightDirection(dir);
            if (knockbackDirection == Vector3.zero) knockbackDirection = dir;

            currentKnockbackMaxSpeed = blockRecoilSpeed;
            currentKnockbackMaxDuration = blockRecoilDuration;

            isKnockedBack = true;
            knockbackTimer = blockRecoilDuration;

            isRolling = false;
            isJumping = false;
            isFalling = false;
            isLanding = false;
            landingTimer = 0f;
        }

        private void UpdateKnockbackTimer()
        {
            if (!isKnockedBack) return;

            knockbackTimer -= Time.deltaTime;
            if (knockbackTimer <= 0f) EndKnockback();
        }

        private void EndKnockback()
        {
            isKnockedBack = false;
            knockbackDirection = Vector3.zero;
        }

        private void ApplyKnockbackMovement()
        {
            if (rb == null || rb.isKinematic) return;

            float remainingRatio = Mathf.Clamp01(knockbackTimer / currentKnockbackMaxDuration);
            float currentSpeed = currentKnockbackMaxSpeed * remainingRatio;

            float verticalVelocity = rb.linearVelocity.y;
            rb.linearVelocity = new Vector3(knockbackDirection.x * currentSpeed, verticalVelocity, knockbackDirection.z * currentSpeed);
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            if (!isActive) return;
            moveInput = context.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            if (!isActive) return;
            moveInput = Vector2.zero;
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            if (!isActive || !isInitialized) return;
            TryStartJump();
        }

        private void OnJumpCanceled(InputAction.CallbackContext context) { }

        private void OnSprintPerformed(InputAction.CallbackContext context)
        {
            if (debugSprint) Debug.Log("[Movement] Input Sprint: performed", this);

            if (!isActive || !isInitialized) return;
            sprintInputPressed = true;
        }

        private void OnSprintCanceled(InputAction.CallbackContext context)
        {
            if (debugSprint) Debug.Log("[Movement] Input Sprint: canceled", this);

            sprintInputPressed = false;
        }

        private void OnRollPerformed(InputAction.CallbackContext context)
        {
            if (!isActive || !isInitialized) return;
            rollBufferTimer = rollInputBufferTime;
            TryStartRoll();
        }

        private void OnRollCanceled(InputAction.CallbackContext context) { }

        private void OnDestroy()
        {
            if (ghostTrail != null) ghostTrail.StopGhostTrail();
            if (inputActions == null) return;

            inputActions.Player.Move.performed -= OnMovePerformed;
            inputActions.Player.Move.canceled -= OnMoveCanceled;

            inputActions.Player.Jump.performed -= OnJumpPerformed;
            inputActions.Player.Jump.canceled -= OnJumpCanceled;

            inputActions.Player.Sprint.performed -= OnSprintPerformed;
            inputActions.Player.Sprint.canceled -= OnSprintCanceled;

            inputActions.Player.Roll.performed -= OnRollPerformed;
            inputActions.Player.Roll.canceled -= OnRollCanceled;

            ForceRemoveRollCollisionExclusion();

            inputActions.Disable();
            inputActions.Dispose();
        }
    }
}