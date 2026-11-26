using System.Collections;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class PlayerBrain : MonoBehaviour
    {

        [Header("Player Systems")]
        [SerializeField] private PlayerMovement movementState;
        [SerializeField] private PlayerCombat combatState;
        [SerializeField] private PlayerHealth healthState;
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerWeaponEquipment weaponEquip;

        [Tooltip("service central of detection of surface (ground/roof).")]
        [SerializeField] private PlayerSurfaceProbe surfaceProbe;

        [Header("Combat State")]
        [SerializeField] private bool isInCombat = false;

        [Header("Brain Status")]
        [SerializeField] private bool isAlive = true;
        [SerializeField] private bool isKnockedDown = false;

        [Header("Knockdown Safety")]
        [Tooltip("time limit of safety is force the recovery of the Knockdown caso the animation stalls.")]
        [SerializeField] private float knockdownSafetyDuration = 4f;
        private float knockdownSafetyTimer = 0f;

        [Header("Knockdown Animation")]
        [Tooltip("amount of animations of Knockdown in the Animator Controller.")]
        [SerializeField] private int knockdownVariantCount = 2;
        private int lastKnockdownVariant = 0;

        [Header("Wake Up (Prologue)")]
        [Tooltip("duration of the sequence of wake (seconds). " +
                 "during this time the MovementState remains totalmente inativo.")]
        [SerializeField] private float wakeUpDuration = 1f;

        [Tooltip("when the wake, the Player remains with movement liberado. " +
                 "if false, continues locked until other call.")]
        [SerializeField] private bool unlockMovementAfterWake = true;

        [Tooltip("Nome of the state Idle in the Animator Controller (layer 0). " +
                 "the Player only is desbloqueado when the Animator enter neste state after the Wake.")]
        [SerializeField] private string wakeIdleStateName = "Idle";

        [Tooltip("time maximum (seconds) to the wait of the transition is Idle after the Wake. " +
                 "Fallback of safety caso the Animator not chegue there.")]
        [SerializeField] private float wakeIdleMaxWait = 3f;

        [Tooltip("if true, the Brain starts 'adormecido' and the MovementState " +
                 "not funciona until BeginWakeUpSequence() end. " +
                 "Deixa disabled in the fluxo normal of jogo.")]
        [SerializeField] private bool startAsleep = false;

        private int sleepParameterHash;
        private int wakeParameterHash;
        private bool hasSleepParameter = false;
        private bool hasWakeParameter = false;

        private bool _isAwakened = true;

        private bool isWakingUp = false;

        [Header("Movement Lock")]
        [SerializeField] private float reviveLockDuration = 1f;

        private bool isMovementLocked = false;
        private float movementLockTimer = 0f;

        private Rigidbody playerRigidbody;

        private int deathTriggerHash;
        private int respawnTriggerHash;
        private int knockdownParameterHash;

        private bool hasKnockdownParameter = false;

        public bool IsAlive => isAlive;
        public bool IsInCombat => isInCombat;
        public bool IsKnockedDown => isKnockedDown;
        public bool IsMovementLocked => isMovementLocked;
        public bool IsExhausted => (movementState != null && movementState.IsExhausted);
        public bool IsSprinting => (movementState != null && movementState.IsSprinting);
        public bool IsGrounded => (movementState != null && movementState.IsGrounded);
        public Rigidbody PlayerRigidbody => playerRigidbody;

        public bool IsWakingUp => isWakingUp;
        public bool IsAwakened => _isAwakened;

        public PlayerSurfaceProbe PlayerSurfaceProbe => surfaceProbe;
        public bool IsOnRoof => surfaceProbe != null && surfaceProbe.IsOnRoof;
        public bool IsOnGround => surfaceProbe != null && surfaceProbe.IsOnGround;
        public float AltitudeToGround => surfaceProbe != null ? surfaceProbe.AltitudeToGround : Mathf.Infinity;

        public float currentHealth => (healthState != null ? healthState.CurrentHealth : 0f);
        public float maxHealth => (healthState != null ? healthState.MaxHealth : 100f);
        public float stamina => (healthState != null ? healthState.CurrentStamina : 0f);
        public float maxStamina => (healthState != null ? healthState.MaxStamina : 100f);
        public float currentRage => (healthState != null ? healthState.CurrentRage : 0f);
        public float maxRage => (healthState != null ? healthState.MaxRage : 100f);

        private void Awake()
        {
            playerRigidbody = GetComponent<Rigidbody>();

            if (movementState == null) movementState = GetComponent<PlayerMovement>();
            if (combatState == null) combatState = GetComponent<PlayerCombat>();
            if (healthState == null) healthState = GetComponent<PlayerHealth>();
            if (animator == null) animator = GetComponent<Animator>();

            if (weaponEquip == null) weaponEquip = GetComponent<PlayerWeaponEquipment>();
            if (weaponEquip == null) weaponEquip = GetComponentInChildren<PlayerWeaponEquipment>();

            if (surfaceProbe == null) surfaceProbe = GetComponent<PlayerSurfaceProbe>();
            if (surfaceProbe == null) surfaceProbe = GetComponentInChildren<PlayerSurfaceProbe>();

            deathTriggerHash = Animator.StringToHash("Death");
            respawnTriggerHash = Animator.StringToHash("Respawn");
            knockdownParameterHash = Animator.StringToHash("Knockdown");
            sleepParameterHash = Animator.StringToHash("Sleep");
            wakeParameterHash = Animator.StringToHash("Wake");

            _isAwakened = !startAsleep;

            ValidateKnockdownParameter();
            ValidateSleepWakeParameters();
        }

        private void ValidateKnockdownParameter()
        {
            hasKnockdownParameter = false;

            if (animator == null) return;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == knockdownParameterHash)
                {
                    if (parameter.type == AnimatorControllerParameterType.Int)
                        hasKnockdownParameter = true;
                    return;
                }
            }
        }

        private void ValidateSleepWakeParameters()
        {
            hasSleepParameter = false;
            hasWakeParameter = false;

            if (animator == null) return;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == sleepParameterHash)
                {
                    if (parameter.type == AnimatorControllerParameterType.Bool)
                        hasSleepParameter = true;
                }
                else if (parameter.nameHash == wakeParameterHash)
                {
                    if (parameter.type == AnimatorControllerParameterType.Bool)
                        hasWakeParameter = true;
                }
            }
        }

        private void Start()
        {
            InitializePlayerSystems();

            if (isAlive && _isAwakened) ActivateAllSystems();
        }

        private void OnEnable()
        {
            if (isAlive && _isAwakened) ActivateAllSystems();
        }

        private void OnDisable()
        {
            DeactivateAllSystems();
        }

        private void Update()
        {
            if (healthState != null) healthState.TickHealth();

            if (isKnockedDown)
            {
                knockdownSafetyTimer -= Time.deltaTime;
                if (knockdownSafetyTimer <= 0f)
                {
                    EndKnockdown();
                }
            }

            if (isMovementLocked)
            {
                movementLockTimer -= Time.deltaTime;
                if (movementLockTimer <= 0f) EndMovementLock();
            }

            if (!isAlive) return;

            if (_isAwakened && movementState != null && movementState.IsActive)
                movementState.TickMovement();

            if (combatState != null) combatState.TickCombat();
        }

        private void FixedUpdate()
        {
            if (!isAlive) return;

            if (isWakingUp && playerRigidbody != null && !playerRigidbody.isKinematic)
            {
                Vector3 v = playerRigidbody.linearVelocity;
                v.x = 0f;
                v.z = 0f;
                playerRigidbody.linearVelocity = v;
            }

            if (_isAwakened && movementState != null && movementState.IsActive)
                movementState.TickPhysics();

            if (combatState != null) combatState.TickCombatPhysics();
            if (combatState != null) combatState.TickCombatLatePhysics();
        }

        private void InitializePlayerSystems()
        {
            if (playerRigidbody != null)
            {
                if (movementState != null) movementState.Initialize(this, playerRigidbody);
                if (combatState != null) combatState.Initialize(this, playerRigidbody);
                if (healthState != null) healthState.Initialize(this);
            }
        }

        public void ActivateAllSystems()
        {

            if (!_isAwakened) return;

            if (movementState != null && movementState.IsInitialized)
                movementState.SetActive(true);
        }

        public void DeactivateAllSystems()
        {
            if (movementState != null) movementState.SetActive(false);

            if (playerRigidbody != null && !playerRigidbody.isKinematic)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void StartMovementLock(float duration)
        {
            isMovementLocked = true;
            movementLockTimer = duration;
            SetMovementInputLocked(true);
        }

        private void EndMovementLock()
        {
            isMovementLocked = false;
            movementLockTimer = 0f;

            if (isAlive && !isKnockedDown && !isWakingUp && _isAwakened)
                SetMovementInputLocked(false);
        }

        public void PrepareWakeUpHandoff()
        {
            _isAwakened = false;

            if (movementState != null)
                movementState.SetActive(false);

            if (playerRigidbody != null && !playerRigidbody.isKinematic)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }
        }

        public void BeginWakeUpSequence()
        {
            if (isWakingUp) return;
            if (!isAlive) return;

            isWakingUp = true;

            _isAwakened = false;
            if (movementState != null) movementState.SetActive(false);

            SetMovementInputLocked(true);

            if (animator != null)
            {
                if (hasSleepParameter) animator.SetBool(sleepParameterHash, true);
                if (hasWakeParameter) animator.SetBool(wakeParameterHash, false);
            }

            StartCoroutine(WakeUpRoutine());
        }

        private IEnumerator WakeUpRoutine()
        {

            yield return new WaitForSeconds(wakeUpDuration);

            if (animator != null)
            {
                if (hasSleepParameter) animator.SetBool(sleepParameterHash, false);
                if (hasWakeParameter) animator.SetBool(wakeParameterHash, true);
            }

            yield return WaitForIdleState();

            isWakingUp = false;

            _isAwakened = true;

            if (movementState != null && movementState.IsInitialized)
                movementState.SetActive(true);

            if (unlockMovementAfterWake)
            {
                SetMovementInputLocked(false);
            }
        }

        private IEnumerator WaitForIdleState()
        {
            if (animator == null) yield break;

            yield return null;

            int idleHash = Animator.StringToHash(wakeIdleStateName);
            float timer = 0f;

            while (timer < wakeIdleMaxWait)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

                if (state.shortNameHash == idleHash || state.fullPathHash == idleHash)
                    yield break;

                timer += Time.deltaTime;
                yield return null;
            }
        }

        private int RollKnockdownVariant()
        {
            if (knockdownVariantCount <= 1)
            {
                lastKnockdownVariant = 1;
                return 1;
            }

            int variant;
            do
            {
                variant = Random.Range(1, knockdownVariantCount + 1);
            }
            while (variant == lastKnockdownVariant);

            lastKnockdownVariant = variant;
            return variant;
        }

        public void TriggerKnockdown()
        {
            if (!isAlive || isKnockedDown) return;

            isKnockedDown = true;
            knockdownSafetyTimer = knockdownSafetyDuration;

            if (movementState != null) movementState.ApplyKnockback();
            SetMovementInputLocked(true);

            if (weaponEquip != null) weaponEquip.DropWeapon();

            if (animator != null && hasKnockdownParameter)
            {
                int variant = RollKnockdownVariant();
                animator.SetInteger(knockdownParameterHash, variant);
            }
        }

        public void EndKnockdown()
        {
            if (!isKnockedDown) return;

            isKnockedDown = false;
            knockdownSafetyTimer = 0f;

            if (animator != null && hasKnockdownParameter)
                animator.SetInteger(knockdownParameterHash, 0);

            if (movementState != null)
            {
                if (!movementState.IsActive && _isAwakened) movementState.SetActive(true);
                if (movementState.IsSuspended) movementState.SetSuspended(false);
            }

            SetMovementInputLocked(false);
        }

        public void Die()
        {
            if (!isAlive) return;

            if (isKnockedDown) EndKnockdown();

            if (isMovementLocked)
            {
                isMovementLocked = false;
                movementLockTimer = 0f;
            }

            isAlive = false;

            if (playerRigidbody != null)
            {
                if (!playerRigidbody.isKinematic)
                {
                    playerRigidbody.linearVelocity = Vector3.zero;
                    playerRigidbody.angularVelocity = Vector3.zero;
                }
                playerRigidbody.isKinematic = true;
            }

            DeactivateAllSystems();

            if (weaponEquip != null) weaponEquip.DropWeapon();

            if (animator != null) animator.SetTrigger(deathTriggerHash);
        }

        public void Revive()
        {
            if (isAlive) return;

            isAlive = true;

            if (healthState != null) healthState.ResetToFull();

            if (playerRigidbody != null)
            {
                playerRigidbody.isKinematic = false;
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }

            StartMovementLock(reviveLockDuration);

            if (animator != null)
            {
                animator.ResetTrigger(respawnTriggerHash);
                animator.SetTrigger(respawnTriggerHash);
            }

            if (_isAwakened) ActivateAllSystems();
        }

        public void ReviveAt(Vector3 position, Quaternion rotation)
        {
            if (isAlive) return;

            isAlive = true;

            if (healthState != null) healthState.ResetToFull();

            if (playerRigidbody != null)
            {
                playerRigidbody.isKinematic = false;
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }

            ApplyPhysicsSafeTeleport(position, rotation);

            StartMovementLock(reviveLockDuration);

            if (animator != null)
            {
                animator.ResetTrigger(respawnTriggerHash);
                animator.SetTrigger(respawnTriggerHash);
            }

            if (_isAwakened) ActivateAllSystems();
        }

        public void OnWeaponEquipped()
        {
            isInCombat = true;
            if (combatState != null) combatState.SetHasWeapon(true);
        }

        public void OnWeaponUnequipped()
        {
            isInCombat = false;
            if (combatState != null) combatState.SetHasWeapon(false);
        }

        public void OnShieldEquipped()
        {
            if (combatState != null) combatState.SetHasShield(true);
        }

        public void OnShieldUnequipped()
        {
            if (combatState != null) combatState.SetHasShield(false);
        }

        public void SetExhausted(bool exhausted)
        {
            if (movementState != null) movementState.SetExhausted(exhausted);
        }

        public void SetMovementInputLocked(bool locked)
        {

            if (!locked && (!isAlive || isKnockedDown || isMovementLocked || isWakingUp || !_isAwakened))
                return;

            if (movementState != null) movementState.SetMovementInputLocked(locked);
        }

        public void ConsumeJumpStamina()
        {
            if (healthState != null) healthState.ConsumeStamina(healthState.JumpStaminaCost);
        }

        public void ConsumeRollStamina()
        {
            if (healthState != null) healthState.ConsumeStamina(healthState.RollStaminaCost);
        }

        public void ConsumeSprintStamina(float deltaTime)
        {
            if (healthState != null)
                healthState.ConsumeStamina(healthState.SprintStaminaCostPerSecond * deltaTime);
        }

        public void ConsumeBlockStamina()
        {
            if (healthState != null) healthState.ConsumeStamina(healthState.BlockStaminaCost);
        }

        public PlayerMovement GetMovementState() => movementState;

        public PlayerCombat GetCombatState() => combatState;

        public PlayerHealth GetHealthState() => healthState;

        public void RespawnFromAbyss(Vector3 position, Quaternion rotation)
        {
            if (!isAlive) return;

            ApplyPhysicsSafeTeleport(position, rotation);
        }

        private void ApplyPhysicsSafeTeleport(Vector3 position, Quaternion rotation)
        {
            if (movementState != null) movementState.ResetMotionStateForTeleport();

            if (isKnockedDown) EndKnockdown();

            if (playerRigidbody != null)
            {
                bool wasKinematic = playerRigidbody.isKinematic;
                RigidbodyInterpolation originalInterpolation = playerRigidbody.interpolation;

                playerRigidbody.interpolation = RigidbodyInterpolation.None;
                playerRigidbody.isKinematic = true;

                playerRigidbody.position = position;
                playerRigidbody.rotation = rotation;

                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;

                Physics.SyncTransforms();

                StartCoroutine(RestorePhysicsAfterTeleport(wasKinematic, originalInterpolation));
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
                Physics.SyncTransforms();
            }
        }

        private IEnumerator RestorePhysicsAfterTeleport(bool wasKinematic, RigidbodyInterpolation interpolation)
        {
            yield return new WaitForFixedUpdate();

            if (playerRigidbody != null)
            {
                playerRigidbody.isKinematic = wasKinematic;
                playerRigidbody.interpolation = interpolation;
            }
        }
    }
}
