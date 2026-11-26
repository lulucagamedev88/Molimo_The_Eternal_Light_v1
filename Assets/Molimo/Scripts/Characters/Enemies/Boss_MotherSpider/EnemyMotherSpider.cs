using Unity.Cinemachine;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(Rigidbody))]

    public partial class EnemyMotherSpider : MonoBehaviour, IDamageable
    {

        public enum RotationMode { Locked, EightDirections }

        public enum State { Idle, Chase, Prepare, Jump, LandAttack, SecondaryAttack, Rest, Turn, ReturnHome, Stagger, Dead }

        [Header("Path / Chase Linear")]
        [Tooltip("path primary (lado the). must start in Home (center).")]
        [SerializeField] private Transform[] pathWaypoints;

        [Tooltip("path secondary (lado B). must start in Home (center). " +
                 "the spider chooses between primary and secondary conforme the lado in that the Player is.")]
        [SerializeField] private Transform[] secondaryPathWaypoints;

        [SerializeField] private bool constrainToPath = true;

        [Header("References")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private PlayerBrain playerBrain;
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Collider bodyCollider;

        [Header("Rotation")]
        [SerializeField] private RotationMode rotationMode = RotationMode.EightDirections;
        [SerializeField] private float rotationOffsetY = 0f;

        [Header("detection")]
        [SerializeField] private float detectionRadius = 12f;
        [SerializeField] private float disengageRadius = 15f;
        [SerializeField] private float attackZoneRadius = 4f;

        [Header("speed of Chase")]
        [SerializeField] private float chaseSpeed = 4.5f;

        [Header("Idle")]
        [SerializeField] private float idleDuration = 1.5f;

        [Header("Prepare (Spider_WaitAttack)")]
        [SerializeField] private float prepareDuration = 1.8f;

        [Header("Primary Attack — Jump (Player far)")]
        [SerializeField] private float jumpDuration = 0.9f;
        [SerializeField] private float jumpHeight = 4.5f;
        [SerializeField] private float jumpAimError = 0.25f;
        [SerializeField] private float landRecovery = 0.35f;

        [Header("Secondary Attack — Spider_Attack (Player near)")]
        [SerializeField] private float secondaryAttackRange = 3f;
        [SerializeField] private float secondaryAttackDuration = 0.7f;
        [SerializeField] private float secondaryAttackRecovery = 0.2f;

        [Header("Damage to Player (% of the MaxHealth)")]
        [Range(0f, 2f)][SerializeField] private float attackHitboxDamagePercent = 0.4f;
        [Range(0f, 2f)][SerializeField] private float ramDamagePercent = 1f;
        [Range(0f, 2f)][SerializeField] private float stompDamagePercent = 0.5f;

        [Header("Damage Cooldowns")]
        [SerializeField] private float ramDamageCooldown = 1f;
        [SerializeField] private float attackHitboxDamageCooldown = 0.5f;

        [Header("Attack Hitbox (SphereCollider Trigger frontal)")]
        [SerializeField] private Collider attackHitbox;
        [SerializeField] private LayerMask playerLayers;

        [Header("Land Attack (area of the stomp)")]
        [SerializeField] private float landAttackRadius = 2.2f;

        [Header("Rest")]
        [SerializeField] private float restDuration = 2.5f;
        [SerializeField] private float attackCooldown = 0.8f;

        [Header("Turn")]
        [SerializeField] private bool turnAfterRest = true;
        [SerializeField] private float turnMaxDuration = 0.1f;

        [Header("Reactions")]
        [SerializeField] private float staggerDuration = 0.25f;

        [Header("Return Home (returns the casa when Player outside of alcance or dead)")]
        [SerializeField] private bool returnHomeOnPlayerDeath = true;

        [Tooltip("speed (Time Speed) of regresso of the spider the Home.")]
        [SerializeField] private float returnHomeSpeed = 4.5f;

        [SerializeField] private float returnHomeArriveDistance = 0.5f;
        [SerializeField] private bool logReturnHome = false;

        [Header("Player Collision")]
        [SerializeField] private bool ignorePlayerCollisionDuringJump = true;
        [SerializeField] private bool ignorePlayerCollisionWhenDead = true;
        [SerializeField] private float minDistanceFromPlayerOnLand = 1.8f;
        [SerializeField] private bool logPlayerCollision = false;

        [Header("Breakables")]
        [SerializeField] private LayerMask breakableLayers;
        [SerializeField] private float breakableDamage = 100f;
        [SerializeField] private bool breakOnContact = true;
        [SerializeField] private bool breakOnLand = true;
        [SerializeField] private bool logBreakables = false;

        [Header("Boss Physics")]
        [SerializeField] private float bossMass = 40f;
        [SerializeField] private bool enforceMassOnAwake = true;
        [SerializeField] private bool enforceMovementPhysics = true;
        [Range(0f, 1f)][SerializeField] private float groundFriction = 0.6f;
        [SerializeField] private bool useFrictionlessMaterial = false;

        [Header("Health")]
        [SerializeField] private float maxHealth = 300f;
        [SerializeField] private float currentHealth = 300f;
        [SerializeField] private float defaultKnockbackDistance = 0.15f;
        [SerializeField] private float knockbackMultiplier = 0.15f;
        [SerializeField] private float knockbackVerticalBias = 0f;

        [Header("Weak Point (belly)")]
        [SerializeField] private EnemyMotherSpiderBelly belly;
        [SerializeField] private bool onlyDamageableViaWeakPoint = true;
        [SerializeField] private bool onlyWeakPointActiveDuringRest = true;
        [SerializeField] private bool restIsUninterruptibleByDamage = true;

        [Header("Death / Debris")]
        [SerializeField] private GameObject debrisPrefab;
        [SerializeField] private bool spawnDebrisOnDeath = true;
        [SerializeField] private bool hideVisualsOnDeath = true;
        [SerializeField] private float debrisSpawnYOffset = 0f;
        [SerializeField] private bool debrisInheritsRotation = true;

        [Header("Death / Destroy")]
        [SerializeField] private float destroyDelayOnDeath = 6f;
        [SerializeField] private GameObject parentObjectToDestroy;
        [SerializeField] private bool destroyParentInsteadOfSelf = false;

        [Header("Camera Shake (Cinemachine Impulse)")]
        [SerializeField] private CinemachineImpulseSource impulseSource;

        [Header("Walk Impulse")]
        [SerializeField] private bool walkImpulseEnabled = true;
        [SerializeField] private float walkImpulseInterval = 0.5f;

        [Header("Animation Params")]
        [SerializeField] private string animWalk = "Spider_Walk";
        [SerializeField] private string animWaitAttack = "Spider_WaitAttack";
        [SerializeField] private string animAttack = "Spider_Attack";
        [SerializeField] private string animJump = "Spider_Jump";
        [SerializeField] private string animDeath = "Spider_Death";
        [SerializeField] private string animTakeDamage = "Spider_Damage";
        [SerializeField] private string animIsGrounded = "IsGrounded";

        [Header("Ground Detection")]
        [Tooltip("Layers that count the ground. Excluir Player/triggers is prevent falsos negativos when the spider is about the Player.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("SFX (SoundData)")]
        [SerializeField] private SoundData walkSfx;
        [SerializeField] private SoundData waitAttackSfx;
        [SerializeField] private SoundData attackSfx;
        [SerializeField] private SoundData impactSfx;
        [SerializeField] private SoundData deathSfx;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource loopAudioSource;
        [SerializeField] private AudioSource sfxAudioSource;

        [Header("VFX")]
        [SerializeField] private ParticleSystem hitParticle;
        [SerializeField] private bool playHitParticleOnDeath = true;

        [Header("Debug")]
        [SerializeField] private bool logDamage = false;
        [SerializeField] private bool logStates = false;
        [SerializeField] private bool logMovement = false;
        [SerializeField] private bool logPlayerDamage = false;
        [SerializeField] private bool logAttackHitbox = false;
        [SerializeField] private bool logAttackDecision = false;
        [SerializeField] private bool logPath = false;

        public event System.Action<float, float> OnHealthChanged;

        public event System.Action OnDeath;

        public event System.Action OnRestStart;

        public event System.Action OnRestEnd;

        public event System.Action OnPlayerDetected;

        public event System.Action OnPlayerLost;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => _state != State.Dead && currentHealth > 0f;
        public float HealthPercent => maxHealth > 0f ? currentHealth / maxHealth : 0f;
        public bool OnlyDamageableViaWeakPoint => onlyDamageableViaWeakPoint;
        public bool IsBellyExposed => _state == State.Rest;
        public bool IsResting => _state == State.Rest;
        public State CurrentState => _state;
        public bool IsPlayerDetected => HasPlayer() && DistanceToPlayer() <= detectionRadius;

        private State _state = State.Idle;
        private bool _isDead;

        private int _hWalk, _hWaitAttack, _hAttack, _hJump, _hDeath, _hTakeDamage, _hIsGrounded;

        private void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider>();
            if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
            if (belly == null) belly = GetComponentInChildren<EnemyMotherSpiderBelly>(true);

            if (enforceMassOnAwake && rb != null) rb.mass = bossMass;

            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (enforceMovementPhysics) ApplyMovementPhysics();

            _snappedRotation = SnapQuaternion(rb.rotation);
            rb.MoveRotation(_snappedRotation);

            _hWalk = Animator.StringToHash(animWalk);
            _hWaitAttack = Animator.StringToHash(animWaitAttack);
            _hAttack = Animator.StringToHash(animAttack);
            _hJump = Animator.StringToHash(animJump);
            _hDeath = Animator.StringToHash(animDeath);
            _hTakeDamage = Animator.StringToHash(animTakeDamage);
            _hIsGrounded = Animator.StringToHash(animIsGrounded);

            CacheSpiderSolidColliders();
            SetAttackHitboxActive(false);

            EnsureAudioSources();
            currentHealth = maxHealth;
        }

        private void Start()
        {
            _homePosition = transform.position;
            _homeRotation = SnapQuaternion(transform.rotation);

            RecalculatePathLength();
            InitializePathDistance();

            ResolvePlayer();
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            EnterIdle();
        }

        private void Update()
        {
            if (_state == State.Dead) return;

            _stateTimer -= Time.deltaTime;
            _attackCooldownTimer -= Time.deltaTime;
            _ramDamageCooldownTimer -= Time.deltaTime;
            _attackHitboxCooldownTimer -= Time.deltaTime;

            RetryResolvePlayer();
            WarnIfBrainNotOperational();
            TrackDetectionEvents();
            TrackPlayerLifeState();

            UpdateGroundedState();

            switch (_state)
            {
                case State.Idle: TickIdle(); break;
                case State.Chase: TickChase(); break;
                case State.Prepare: TickPrepare(); break;
                case State.Jump: break;
                case State.LandAttack: TickLandAttack(); break;
                case State.SecondaryAttack: TickSecondaryAttack(); break;
                case State.Rest: TickRest(); break;
                case State.Turn: TickTurn(); break;
                case State.ReturnHome: TickReturnHome(); break;
                case State.Stagger: TickStagger(); break;
            }
        }

        private void FixedUpdate()
        {
            if (_state == State.Dead) return;

            UpdateSnappedRotation();

            if (rb != null)
            {
                if (!rb.isKinematic) rb.angularVelocity = Vector3.zero;
                rb.MoveRotation(_snappedRotation);
            }

            if (_state == State.Chase)
            {
                ApplyMovementToward(chaseSpeed);
                MonitorStuck();
            }
            else if (_state == State.ReturnHome)
            {
                ApplyMovementToward(returnHomeSpeed);
            }
            else if (_state != State.Jump && rb != null && !rb.isKinematic)
            {
                Vector3 v = rb.linearVelocity;
                if (Mathf.Abs(v.x) > 0.0001f || Mathf.Abs(v.z) > 0.0001f)
                    rb.linearVelocity = new Vector3(0f, v.y, 0f);
            }

            if (_state == State.SecondaryAttack)
                TickAttackHitbox();
        }

        private void UpdateGroundedState()
        {
            if (animator == null) return;

            if (_state == State.Jump)
            {
                animator.SetBool(_hIsGrounded, false);
                return;
            }

            int mask = groundLayers.value != 0 ? groundLayers.value : ~0;
            mask &= ~playerLayers.value;

            bool isGrounded = Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                Vector3.down,
                1.5f,
                mask,
                QueryTriggerInteraction.Ignore);

            animator.SetBool(_hIsGrounded, isGrounded);
        }

        private void ChangeState(State newState)
        {
            State previous = _state;
            if (logStates && previous != newState)
                Debug.Log($"[EnemyMotherSpider] {previous} → {newState}");

            _state = newState;

            if (animator != null)
            {
                bool shouldWalk = (newState == State.Chase || newState == State.ReturnHome);
                animator.SetBool(_hWalk, shouldWalk);

                if (newState == State.Idle || newState == State.Chase || newState == State.ReturnHome)
                {
                    animator.ResetTrigger(_hJump);
                    animator.ResetTrigger(_hAttack);
                    animator.ResetTrigger(_hTakeDamage);
                    animator.ResetTrigger(_hWaitAttack);
                }
            }

            if (belly != null)
                belly.SetExposed(!onlyWeakPointActiveDuringRest || newState == State.Rest);
        }

        private void StopHorizontal()
        {
            if (rb == null) return;
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
        }

        private void SetBool(int hash, bool value) { if (animator != null) animator.SetBool(hash, value); }
        private void SetTrigger(int hash) { if (animator != null) animator.SetTrigger(hash); }
    }
}
