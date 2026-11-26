using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(Rigidbody))]

    public class EnemySpiderAI : MonoBehaviour, IDamageable
    {

        public enum State
        {
            Idle,
            Chase,
            Prepare,
            JumpAttack,
            DashAttack,
            Cooldown,
            Stagger,
            Dead
        }

        public enum RotationMode
        {
            Locked,
            EightDirections
        }

        [Header("references Player")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private PlayerBrain playerBrain;

        [Header("references EnemySpiderAI")]
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody rb;

        [Tooltip("Collider of physics of the enemy (ex: BoxCollider not-trigger). " +
                 "used only is collision physics and IgnoreCollision with the Player. " +
                 "not receives damage of the sword.")]
        [SerializeField] private Collider bodyCollider;

        [Tooltip("Collider EXCLUSIVO is receive damage of the sword (ex: SphereCollider with Is Trigger = ON). " +
                 "if empty, is auto-detectado (first trigger in children). " +
                 "recommended: place it num GameObject child, in layer own (ex: 'EnemyHurtbox'), " +
                 "and configurar the sword of the player is atingir only essa layer.")]
        [SerializeField] private Collider damageCollider;

        [Header("camera Shake (Cinemachine Impulse)")]
        [Tooltip("component CinemachineImpulseSource anexado the this spider. only gera impulso when the spider LEVA damage.")]
        [SerializeField] private CinemachineImpulseSource impulseSource;

        [Header("Gate of the prologue")]
        [SerializeField] private bool waitForPlayerAwakened = true;

        [Header("rotation")]
        [SerializeField] private RotationMode rotationMode = RotationMode.EightDirections;
        [SerializeField] private float rotationOffsetY = 0f;

        [Header("detection")]
        [SerializeField] private float detectionRadius = 10f;
        [SerializeField] private float disengageRadius = 14f;

        [Header("movement")]
        [SerializeField] private float chaseSpeed = 3.5f;

        [Header("attacks (Rays of Action)")]
        [Tooltip("distance maximum is start any attack.")]
        [SerializeField] private float attackInitiationRange = 7f;

        [Tooltip("if the Player is the this distance or less, the spider does the attack fast in the ground (Dash). if is more far, does the pulo.")]
        [SerializeField] private float groundAttackRange = 3.5f;

        [Tooltip("time of telegrafo (Spider_WaitAttack) before decidir and execute the attack.")]
        [SerializeField] private float attackWindup = 0.3f;

        [Header("attack: distance of Paragem (prevents stop in the center of the player)")]
        [Tooltip("the spider never advances is beyond desta distance of the center of the player when the chase. " +
                 "before start any attack body-the-body, has of estar by less the this distance. " +
                 "if the Player if approach more that isto, the spider recua is restore the distance.")]
        [Min(0.1f)]
        [SerializeField] private float attackStopDistance = 1.5f;

        [Tooltip("margin of hysteresis (meters) is prevent jitter between advance/back away. " +
                 "the spider advances if is the more that (stop + advanceMargin), " +
                 "recua if is the less that (stop - backoffMargin).")]
        [Min(0f)]
        [SerializeField] private float stopDistanceHysteresis = 0.15f;

        [Header("attack: Pulo (Jump)")]
        [SerializeField] private float jumpHeight = 3f;
        [SerializeField] private float jumpDuration = 0.6f;
        [Tooltip("force of the salto/knockback of the spider after bater in the ground.")]
        [SerializeField] private float jumpRecoilForce = 5f;
        [Tooltip("distance before of the player where the spider aterrissa. prevents aterrar inside the collider of the player (causa of it voar is up). Mantenha smaller that attackHitboxRadius * 1.5.")]
        [SerializeField] private float jumpLandingStopDistance = 1.2f;

        [Header("attack: Dash (Investida fast)")]
        [SerializeField] private float dashSpeed = 14f;
        [SerializeField] private float dashMaxDuration = 0.3f;
        [Tooltip("force of the knockback of the spider after of the the dash.")]
        [SerializeField] private float dashRecoilForce = 6f;
        [Tooltip("distance minimum that the spider must travel during the dash before the hit can connect. prevents the bug of the 'attack stopped' when the Player already is in up dela.")]
        [SerializeField] private float dashMinDistanceBeforeHit = 0.6f;

        [Header("attack: general")]
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private float attackHitboxRadius = 0.9f;
        [SerializeField] private float attackHitboxForwardOffset = 0.7f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private LayerMask playerLayers;

        [Header("Health")]
        [SerializeField] private float maxHealth = 40f;
        [SerializeField] private float currentHealth = 40f;
        [SerializeField] private float staggerDuration = 0.2f;

        [Header("VFX — Hit / Impact")]
        [Tooltip("ParticleSystem (reference) that toca whenever the spider LEVA damage. " +
                 "can be the child of the spider (follows the movement) or the system separado. " +
                 "Clear+Play is called is reset of the zero in hits consecutivos.")]
        [SerializeField] private ParticleSystem hitVfxParticle;

        [Tooltip("(optional) Prefab of VFX instanciado in the world the each hit, independente of the movement of the spider. " +
                 "Complementar or alternativo when the hitVfxParticle. Auto-destroyed.")]
        [SerializeField] private GameObject hitVfxPrefab;

        [Tooltip("Offset local (relativo to the spider) where the hitVfxPrefab is instanciado. " +
                 "uses TransformPoint (roda with the spider). ignored if hitVfxPrefab is null.")]
        [SerializeField] private Vector3 hitVfxSpawnOffset = new Vector3(0f, 0.8f, 0f);

        [Tooltip("time (s) before of the prefab of VFX be destroyed. <= 0 = auto (duration of the ParticleSystem, if houver).")]
        [SerializeField] private float hitVfxPrefabLifetime = 2f;

        [Tooltip("if ON, the hitVfxParticle (if is child) is detached of the spider in Die() is that the impact final termine of play.")]
        [SerializeField] private bool detachHitVfxOnDeath = true;

        [Header("Death / destroy")]
        [SerializeField] private GameObject debrisPrefab;
        [SerializeField] private bool spawnDebrisOnDeath = true;
        [SerializeField] private bool hideVisualsOnDeath = true;
        [SerializeField] private float destroyDelayOnDeath = 4f;
        [SerializeField] private GameObject parentObjectToDestroy;
        [SerializeField] private bool destroyParentInsteadOfSelf = false;

        [Header("collision with the Player")]
        [Tooltip("if ON, ignores the collision physics between the spider and the Player. prevents that the Player is empurrado (depenetration) is up of the spider.")]
        [SerializeField] private bool ignorePlayerCollision = true;

        [Tooltip("sets the Layer of the body of the player (ex: 'PlayerBody'). " +
                 "the script only vai ignore the collision with the colliders that estiverem nesta Layer. " +
                 "Deixa 'Nothing' is ignore all the colliders not-trigger of the player.")]
        [SerializeField] private LayerMask playerBodyLayers;

        [Header("Animation Parameters")]
        [SerializeField] private string animWalk = "Spider_Walk";
        [SerializeField] private string animWaitAttack = "Spider_WaitAttack";
        [SerializeField] private string animAttack = "Spider_Attack";
        [SerializeField] private string animJump = "Spider_Jump";
        [SerializeField] private string animDeath = "Spider_Death";
        [SerializeField] private string animTakeDamage = "Spider_Damage";
        [SerializeField] private string animIsGrounded = "IsGrounded";

        [Header("Audio Settings (SoundData)")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("sounds of caminhada/passos of the enemy.")]
        [SerializeField] private SoundData[] walkSounds;
        [Tooltip("sounds of patrulha (used the fallback if walkSounds is empty).")]
        [SerializeField] private SoundData[] patrolSounds;
        [Tooltip("interval default between passos during the caminhada.")]
        [SerializeField] private float stepSoundInterval = 0.35f;
        [SerializeField] private SoundData[] prepareAttackSounds;
        [SerializeField] private SoundData[] attackSounds;
        [SerializeField] private SoundData[] hitSounds;
        [SerializeField] private SoundData[] deathSounds;

        [Header("Debug")]
        [SerializeField] private bool logStates = false;
        [SerializeField] private bool logDamage = false;
        [SerializeField] private bool logAttack = false;
        [SerializeField] private bool logVfx = false;

        public event System.Action<float, float> OnHealthChanged;

        public event System.Action OnDeath;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => _state != State.Dead && currentHealth > 0f;
        public State CurrentState => _state;

        public Collider DamageCollider => damageCollider;
        public Collider BodyCollider => bodyCollider;

        private State _state = State.Idle;
        private float _stateTimer;
        private float _attackCooldownTimer;
        private Vector3 _chaseDir;
        private Vector3 _dashDirection;
        private Vector3 _dashStartPos;
        private Quaternion _snappedRotation;
        private bool _isDead;
        private bool _hasDealtDamageThisAttack;
        private float _resolvePlayerTimer;
        private bool _warnedNoPlayer;

        private KnockbackReceiver _knockback;
        private bool IsKnockedBack => _knockback != null && _knockback.IsStunned;

        private float _stepSoundTimer;

        private readonly HashSet<Collider> _ignoredColliders = new HashSet<Collider>();

        private int _hWalk, _hWaitAttack, _hAttack, _hJump, _hDeath, _hTakeDamage, _hIsGrounded;

        private void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider>();
            if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
            _knockback = GetComponent<KnockbackReceiver>();

            if (damageCollider == null) damageCollider = AutoFindDamageCollider();
            ValidateColliderSetup();

            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearDamping = 0f;

            _snappedRotation = SnapQuaternion(rb.rotation);
            rb.MoveRotation(_snappedRotation);

            _hWalk = Animator.StringToHash(animWalk);
            _hWaitAttack = Animator.StringToHash(animWaitAttack);
            _hAttack = Animator.StringToHash(animAttack);
            _hJump = Animator.StringToHash(animJump);
            _hDeath = Animator.StringToHash(animDeath);
            _hTakeDamage = Animator.StringToHash(animTakeDamage);
            _hIsGrounded = Animator.StringToHash(animIsGrounded);

            EnsureAudioSources();
            currentHealth = maxHealth;
        }

        private void Start()
        {
            ResolvePlayer();
            TryIgnorePlayerCollision();
            SetBool(_hIsGrounded, true);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            EnterIdle();
        }

        private void Update()
        {
            if (_state == State.Dead) return;

            _stateTimer -= Time.deltaTime;
            _attackCooldownTimer -= Time.deltaTime;

            RetryResolvePlayer();

            switch (_state)
            {
                case State.Idle: TickIdle(); break;
                case State.Chase: TickChase(); break;
                case State.Prepare: TickPrepare(); break;
                case State.JumpAttack: break;
                case State.DashAttack: TickDashAttack(); break;
                case State.Cooldown: TickCooldown(); break;
                case State.Stagger: TickStagger(); break;
            }

            if (_state == State.Chase)
            {
                TickWalkSound();
            }
            else
            {
                _stepSoundTimer = 0f;
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

            if (IsKnockedBack) return;

            if (_state == State.Chase)
            {
                ApplyMovementToward(chaseSpeed);
            }
            else if (_state == State.DashAttack)
            {
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 v = rb.linearVelocity;
                    rb.linearVelocity = new Vector3(_dashDirection.x * dashSpeed, v.y, _dashDirection.z * dashSpeed);
                }
            }
            else if (_state != State.JumpAttack && rb != null && !rb.isKinematic)
            {
                Vector3 v = rb.linearVelocity;
                if (Mathf.Abs(v.x) > 0.0001f || Mathf.Abs(v.z) > 0.0001f)
                    rb.linearVelocity = new Vector3(0f, v.y, 0f);
            }
        }

        private void EnterIdle()
        {
            ChangeState(State.Idle);
            SetWalking(false);
            StopHorizontal();
        }

        private void TickIdle()
        {
            if (!HasPlayer()) return;

            float d = DistanceToPlayer();
            if (d <= attackStopDistance) return;
            if (d <= detectionRadius) EnterChase();
        }

        private void EnterChase()
        {
            ChangeState(State.Chase);
            SetWalking(true);
            if (rb != null && !rb.isKinematic) rb.WakeUp();
            UpdateChaseDirection();
        }

        private void TickChase()
        {
            if (!HasPlayer()) { EnterIdle(); return; }

            float d = DistanceToPlayer();
            if (d > disengageRadius) { EnterIdle(); return; }

            bool atAttackDistance = d >= attackStopDistance - 0.05f;

            if (d <= attackInitiationRange && atAttackDistance && _attackCooldownTimer <= 0f)
            {
                SetWalking(false);
                EnterPrepare();
                return;
            }

            UpdateChaseDirection();
        }

        private void EnterPrepare()
        {
            ChangeState(State.Prepare);
            _stateTimer = attackWindup;
            _hasDealtDamageThisAttack = false;
            StopHorizontal();

            PlayRandomSound(prepareAttackSounds);
            SetTrigger(_hWaitAttack);

            if (logStates) Debug.Log("[EnemySpiderAI] PREPARE (Spider_WaitAttack).");
        }

        private void TickPrepare()
        {
            StopHorizontal();
            if (!HasPlayer()) { EnterIdle(); return; }

            SnapRotationToPlayer();
            if (_stateTimer > 0f) return;

            float d = DistanceToPlayer();
            if (d <= groundAttackRange) EnterDashAttack();
            else StartCoroutine(JumpAttackRoutine());
        }

        private void EnterDashAttack()
        {
            ChangeState(State.DashAttack);
            _stateTimer = dashMaxDuration;
            _hasDealtDamageThisAttack = false;
            _dashStartPos = rb != null ? rb.position : transform.position;

            PlayRandomSound(attackSounds);

            SetTrigger(_hAttack);

            _dashDirection = (playerTarget.position - transform.position);
            _dashDirection.y = 0f;

            if (_dashDirection.sqrMagnitude < 0.0001f)
            {
                Vector3 fwd = transform.forward; fwd.y = 0f;
                _dashDirection = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
            }
            else
            {
                _dashDirection.Normalize();
            }

            _snappedRotation = ComputeFacingRotation(_dashDirection);
            if (rb != null) rb.MoveRotation(_snappedRotation);

            if (logStates) Debug.Log($"[EnemySpiderAI] DASH ATTACK (dir={_dashDirection}, speed={dashSpeed}).");
        }

        private void TickDashAttack()
        {
            Vector3 curPos = rb != null ? rb.position : transform.position;
            float traveled = Vector3.Distance(_dashStartPos, curPos);

            if (!_hasDealtDamageThisAttack && traveled >= dashMinDistanceBeforeHit)
            {
                if (TryHitPlayerDuringAttack(attackDamage, attackHitboxRadius, attackHitboxForwardOffset))
                {
                    _stateTimer = 0f;
                    if (logAttack) Debug.Log($"[EnemySpiderAI] DASH HIT after {traveled:F2}m percorridos.");
                }
            }

            if (_stateTimer <= 0f)
            {
                Vector3 recoilDir = (playerTarget != null) ? (transform.position - playerTarget.position) : -transform.forward;
                recoilDir.y = 0f;
                if (recoilDir.sqrMagnitude < 0.01f) recoilDir = -transform.forward;
                ApplySelfRecoil(recoilDir.normalized, dashRecoilForce);

                _attackCooldownTimer = attackCooldown;
                EnterCooldown();
            }
        }

        private System.Collections.IEnumerator JumpAttackRoutine()
        {
            ChangeState(State.JumpAttack);
            StopHorizontal();

            SetTrigger(_hJump);
            SetBool(_hIsGrounded, false);

            Vector3 start = rb.position;

            float landingDistance = Mathf.Max(jumpLandingStopDistance, attackStopDistance);

            Vector3 playerPos = playerTarget.position;
            Vector3 flatToPlayer = playerPos - start;
            flatToPlayer.y = 0f;
            float flatDist = flatToPlayer.magnitude;

            Vector3 target;
            if (flatDist > landingDistance && flatDist > 0.0001f)
                target = playerPos - (flatToPlayer / flatDist) * landingDistance;
            else
                target = start;

            target.y = start.y;

            bool savedGravity = rb.useGravity;
            rb.useGravity = false;

            float t = 0f;
            while (t < jumpDuration)
            {
                if (_state != State.JumpAttack)
                {
                    rb.useGravity = savedGravity;
                    SetBool(_hIsGrounded, true);
                    yield break;
                }

                t += Time.fixedDeltaTime;
                float k = Mathf.Clamp01(t / jumpDuration);

                Vector3 flatPos = Vector3.Lerp(start, target, k);
                float y = start.y + 4f * jumpHeight * k * (1f - k);

                rb.MovePosition(new Vector3(flatPos.x, y, flatPos.z));
                yield return new WaitForFixedUpdate();
            }

            rb.position = new Vector3(target.x, start.y, target.z);
            rb.useGravity = savedGravity;
            SetBool(_hIsGrounded, true);

            yield return new WaitForSeconds(0.15f);

            if (_state != State.JumpAttack) yield break;

            if (!_hasDealtDamageThisAttack && HasPlayer())
                TryHitPlayerDuringAttack(attackDamage, attackHitboxRadius * 1.5f, 0f);

            Vector3 recoilDir = (playerTarget != null) ? (transform.position - playerTarget.position) : -transform.forward;
            recoilDir.y = 0f;
            if (recoilDir.sqrMagnitude < 0.01f) recoilDir = -transform.forward;
            ApplySelfRecoil(recoilDir.normalized, jumpRecoilForce);

            _attackCooldownTimer = attackCooldown;
            EnterCooldown();
        }

        private void EnterCooldown()
        {
            ChangeState(State.Cooldown);
            _stateTimer = Mathf.Max(0.05f, attackCooldown * 0.35f);
        }

        private void TickCooldown()
        {
            StopHorizontal();
            if (_stateTimer > 0f) return;

            if (HasPlayer() && DistanceToPlayer() <= detectionRadius) EnterChase();
            else EnterIdle();
        }

        private void EnterStagger()
        {
            ChangeState(State.Stagger);
            _stateTimer = staggerDuration;
            SetTrigger(_hTakeDamage);
            SetWalking(false);
            StopHorizontal();
        }

        private void TickStagger()
        {
            StopHorizontal();
            if (_stateTimer > 0f) return;

            if (HasPlayer() && DistanceToPlayer() <= detectionRadius) EnterChase();
            else EnterIdle();
        }

        private bool TryHitPlayerDuringAttack(float damage, float radius, float forwardOffset)
        {
            if (!HasPlayer()) return false;

            Vector3 center = transform.position + transform.forward * forwardOffset + Vector3.up * 0.5f;
            int mask = playerLayers.value != 0 ? playerLayers.value : ~0;
            Collider[] hits = Physics.OverlapSphere(center, radius, mask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i];
                if (c == null) continue;

                PlayerBrain b = c.GetComponentInParent<PlayerBrain>();
                if (b == null) continue;
                if (playerBrain != null && b != playerBrain) continue;
                if (waitForPlayerAwakened && (!b.IsAwakened || b.IsWakingUp)) continue;

                PlayerHealth hp = b.GetComponent<PlayerHealth>();
                if (hp == null) continue;

                hp.TakeDamage(damage);
                _hasDealtDamageThisAttack = true;
                PlayRandomSound(attackSounds);

                if (logAttack) Debug.Log($"[EnemySpiderAI] Atingiu Player: {damage} dmg.");
                return true;
            }
            return false;
        }

        private void ApplySelfRecoil(Vector3 dir, float force)
        {
            if (rb == null || rb.isKinematic) return;
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
            rb.AddForce(dir * force, ForceMode.VelocityChange);
        }

        public void TakeDamage(float amount, float knockbackDistance = -1f)
        {
            if (_isDead || _state == State.Dead || amount <= 0f) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            PlayRandomSound(hitSounds);
            GenerateImpulse();

            PlayHitVfx();

            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (logDamage) Debug.Log($"[EnemySpiderAI] Levou {amount} dmg. HP: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0f) { Die(); return; }

            if (_state != State.Stagger) EnterStagger();
        }

        public bool IsDamageCollider(Collider c) => c != null && c == damageCollider;

        public bool TryTakeDamageFromCollider(Collider hitCollider, float amount, float knockbackDistance = -1f)
        {
            if (hitCollider == null || hitCollider != damageCollider) return false;
            TakeDamage(amount, knockbackDistance);
            return true;
        }

        private void PlayHitVfx()
        {

            if (hitVfxParticle != null)
            {
                if (!hitVfxParticle.gameObject.activeSelf)
                    hitVfxParticle.gameObject.SetActive(true);

                hitVfxParticle.Clear(true);
                hitVfxParticle.Play(true);

                if (logVfx) Debug.Log("[EnemySpiderAI] Hit VFX (in-place) played.");
            }

            if (hitVfxPrefab != null)
            {
                Vector3 worldPos = transform.TransformPoint(hitVfxSpawnOffset);
                GameObject go = Instantiate(hitVfxPrefab, worldPos, Quaternion.identity);

                float lifetime = hitVfxPrefabLifetime;
                if (lifetime <= 0f)
                {
                    var ps = go.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        var main = ps.main;
                        lifetime = main.duration + main.startLifetime.constantMax + 0.5f;
                    }
                    else
                    {
                        lifetime = 2f;
                    }
                }
                Destroy(go, lifetime);

                if (logVfx) Debug.Log($"[EnemySpiderAI] Hit VFX (prefab) spawned at {worldPos} (destroy in {lifetime:F2}s).");
            }
        }

        private void GenerateImpulse()
        {
            if (impulseSource != null)
                impulseSource.GenerateImpulse();
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;
            _state = State.Dead;

            if (damageCollider != null) damageCollider.enabled = false;

            PlayDeathSound(deathSounds);

            SetWalking(false);
            SetBool(_hIsGrounded, false);
            SetTrigger(_hDeath);

            StopHorizontal();
            if (rb != null) rb.useGravity = true;

            if (spawnDebrisOnDeath && debrisPrefab != null)
                Instantiate(debrisPrefab, transform.position, transform.rotation);

            if (detachHitVfxOnDeath && hitVfxParticle != null && hitVfxParticle.transform.IsChildOf(transform))
            {
                hitVfxParticle.transform.SetParent(null, true);

                float vfxLife = 2f;
                var vfxMain = hitVfxParticle.main;
                if (vfxMain.duration > 0f)
                    vfxLife = vfxMain.duration + vfxMain.startLifetime.constantMax + 0.25f;

                Destroy(hitVfxParticle.gameObject, vfxLife);
                hitVfxParticle = null;

                if (logVfx) Debug.Log($"[EnemySpiderAI] Hit VFX detached (destroy in {vfxLife:F2}s).");
            }

            if (hideVisualsOnDeath)
            {
                Renderer[] rends = GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    if (r == null) continue;

                    if (r is ParticleSystemRenderer) continue;
                    r.enabled = false;
                }
            }

            OnDeath?.Invoke();

            bool hasParent = parentObjectToDestroy != null && parentObjectToDestroy != gameObject;
            GameObject primary = (hasParent && destroyParentInsteadOfSelf) ? parentObjectToDestroy : gameObject;
            GameObject secondary = (hasParent && !destroyParentInsteadOfSelf) ? parentObjectToDestroy : null;

            if (destroyDelayOnDeath > 0f)
            {
                Destroy(primary, destroyDelayOnDeath);
                if (secondary != null) Destroy(secondary, destroyDelayOnDeath);
            }
            else
            {
                Destroy(primary);
                if (secondary != null) Destroy(secondary);
            }
        }

        private void UpdateSnappedRotation()
        {
            if (rotationMode == RotationMode.Locked || !HasPlayer() || _state == State.Prepare || _state == State.DashAttack || _state == State.JumpAttack) return;

            if (_state == State.Chase)
            {
                Vector3 dir = playerTarget.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) return;
                _snappedRotation = ComputeFacingRotation(dir);
            }
        }

        private void SnapRotationToPlayer()
        {
            if (!HasPlayer()) return;
            Vector3 dir = playerTarget.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            _snappedRotation = ComputeFacingRotation(dir);
            if (rb != null) rb.MoveRotation(_snappedRotation);
        }

        private Quaternion ComputeFacingRotation(Vector3 flatDir)
        {
            float angle = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;
            if (rotationMode == RotationMode.EightDirections) angle = Mathf.Round(angle / 45f) * 45f;
            return Quaternion.Euler(0f, angle + rotationOffsetY, 0f);
        }

        private Quaternion SnapQuaternion(Quaternion rot)
        {
            if (rotationMode == RotationMode.Locked) return rot;
            float y = Mathf.Round(rot.eulerAngles.y / 45f) * 45f;
            return Quaternion.Euler(0f, y, 0f);
        }

        private void ApplyMovementToward(float speed)
        {
            if (_chaseDir.sqrMagnitude < 0.0001f) { StopHorizontal(); return; }
            if (rb.isKinematic) { rb.MovePosition(rb.position + _chaseDir * speed * Time.fixedDeltaTime); return; }

            Vector3 v = rb.linearVelocity;
            Vector3 flat = _chaseDir * speed;
            rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
        }

        private void UpdateChaseDirection()
        {
            if (playerTarget == null) { _chaseDir = Vector3.zero; return; }
            _chaseDir = ComputeChaseDirectionRespectingStopDistance(playerTarget.position);
        }

        private Vector3 ComputeChaseDirectionRespectingStopDistance(Vector3 targetPos)
        {
            Vector3 dir = targetPos - transform.position;
            dir.y = 0f;
            float flatDist = dir.magnitude;

            if (flatDist < 0.0001f) return Vector3.zero;

            Vector3 normalized = dir / flatDist;

            float stop = Mathf.Max(0.1f, attackStopDistance);
            float hysteresis = Mathf.Max(0f, stopDistanceHysteresis);

            float backoffThreshold = Mathf.Max(0f, stop - hysteresis);
            float advanceThreshold = stop + hysteresis;

            if (flatDist < backoffThreshold) return -normalized;
            if (flatDist <= advanceThreshold) return Vector3.zero;
            return normalized;
        }

        private void StopHorizontal()
        {
            if (IsKnockedBack) return;
            if (rb == null || rb.isKinematic) return;
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
        }

        private void ResolvePlayer()
        {
            if (playerBrain == null) playerBrain = Object.FindAnyObjectByType<PlayerBrain>();

            if (playerBrain != null && playerTarget == null)
                playerTarget = playerBrain.transform;

            if (playerTarget == null)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) playerTarget = go.transform;
            }
        }

        private void RetryResolvePlayer()
        {
            _resolvePlayerTimer -= Time.deltaTime;
            if (_resolvePlayerTimer > 0f) return;
            _resolvePlayerTimer = 1f;

            if (playerTarget == null) ResolvePlayer();
            TryIgnorePlayerCollision();
        }

        private Transform GetPlayerPhysicsRoot()
        {
            if (playerBrain != null)
            {
                Rigidbody prb = playerBrain.GetComponentInParent<Rigidbody>();
                if (prb != null) return prb.transform;

                CharacterController cc = playerBrain.GetComponentInParent<CharacterController>();
                if (cc != null) return cc.transform;
            }

            if (playerTarget != null)
            {
                Rigidbody prb = playerTarget.GetComponentInParent<Rigidbody>();
                if (prb != null) return prb.transform;

                CharacterController cc = playerTarget.GetComponentInParent<CharacterController>();
                if (cc != null) return cc.transform;
            }

            return playerTarget;
        }

        private void TryIgnorePlayerCollision()
        {
            if (!ignorePlayerCollision) return;
            if (bodyCollider == null || playerTarget == null) return;

            Transform root = GetPlayerPhysicsRoot();
            if (root == null) return;

            Collider[] playerColliders = root.GetComponentsInChildren<Collider>(true);
            if (playerColliders == null || playerColliders.Length == 0) return;

            int newlyIgnored = 0;
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider pc = playerColliders[i];
                if (pc == null || pc == bodyCollider) continue;
                if (_ignoredColliders.Contains(pc)) continue;
                if (pc.isTrigger) continue;

                if (playerBodyLayers.value != 0)
                {
                    if ((playerBodyLayers.value & (1 << pc.gameObject.layer)) == 0) continue;
                }

                Physics.IgnoreCollision(bodyCollider, pc, true);
                _ignoredColliders.Add(pc);
                newlyIgnored++;
            }

            if (newlyIgnored > 0 && logStates)
                Debug.Log($"[EnemySpiderAI] collision ignored with +{newlyIgnored} collider(s) of the player (total {_ignoredColliders.Count}, root: {root.name}).");
        }

        private bool HasPlayer()
        {
            if (playerTarget == null) return false;
            if (playerBrain != null)
            {
                if (!playerBrain.IsAlive) return false;
                if (waitForPlayerAwakened && (!playerBrain.IsAwakened || playerBrain.IsWakingUp)) return false;
            }
            return true;
        }

        private float DistanceToPlayer()
        {
            return playerTarget == null ? Mathf.Infinity : Vector3.Distance(transform.position, playerTarget.position);
        }

        #region Hurtbox / Collider Validation

        private Collider AutoFindDamageCollider()
        {

            Collider[] all = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Collider c = all[i];
                if (c == null || c == bodyCollider) continue;
                if (c.isTrigger) return c;
            }
            return null;
        }

        private void ValidateColliderSetup()
        {
            if (damageCollider == null)
            {
                Debug.LogWarning(
                    "[EnemySpiderAI] damageCollider not configured and none trigger found. " +
                    "the sword not conseguira distinguir the hurtbox of the body collider.");
                return;
            }

            if (damageCollider == bodyCollider)
            {
                Debug.LogWarning(
                    "[EnemySpiderAI] bodyCollider and damageCollider are the same collider. " +
                    "Coloque the trigger of damage num GameObject child (layer 'EnemyHurtbox') " +
                    "for separar physics of hurtbox.");
            }

            if (!damageCollider.isTrigger)
            {
                Debug.LogWarning(
                    $"[EnemySpiderAI] damageCollider '{damageCollider.name}' not is the Is Trigger. " +
                    "Ative 'Is Trigger' for not empurrar fisicamente the Player.");
            }
        }

        #endregion

        #region Audio System
        private void EnsureAudioSources()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 1f;
                }
            }
        }

        private void TickWalkSound()
        {
            _stepSoundTimer -= Time.deltaTime;

            if (_stepSoundTimer <= 0f)
            {
                _stepSoundTimer = stepSoundInterval;
                SoundData[] soundsToPlay = (walkSounds != null && walkSounds.Length > 0) ? walkSounds : patrolSounds;
                PlayRandomSound(soundsToPlay);
            }
        }

        private void PlayRandomSound(SoundData[] sounds)
        {
            if (sounds == null || sounds.Length == 0) return;

            SoundData selectedSound = sounds[Random.Range(0, sounds.Length)];
            if (selectedSound == null || selectedSound.clip == null) return;

            if (audioSource != null)
            {
                audioSource.pitch = selectedSound.GetPitch();
                audioSource.minDistance = selectedSound.minDistance;
                audioSource.maxDistance = selectedSound.maxDistance;
                audioSource.PlayOneShot(selectedSound.clip, selectedSound.volume);
            }
        }

        private void PlayDeathSound(SoundData[] sounds)
        {
            if (sounds == null || sounds.Length == 0) return;

            GameObject tempAudioGO = new GameObject("TempDeathAudio");
            tempAudioGO.transform.position = transform.position;

            float maxDuration = 0f;

            for (int i = 0; i < sounds.Length; i++)
            {
                SoundData selectedSound = sounds[i];
                if (selectedSound == null || selectedSound.clip == null) continue;

                AudioSource tempSource = tempAudioGO.AddComponent<AudioSource>();

                tempSource.clip = selectedSound.clip;
                tempSource.volume = selectedSound.volume;
                tempSource.pitch = selectedSound.GetPitch();
                tempSource.minDistance = selectedSound.minDistance;
                tempSource.maxDistance = selectedSound.maxDistance;
                tempSource.spatialBlend = 1f;

                tempSource.Play();

                if (selectedSound.clip.length > maxDuration)
                    maxDuration = selectedSound.clip.length;
            }

            if (maxDuration > 0f) Destroy(tempAudioGO, maxDuration + 0.1f);
            else Destroy(tempAudioGO);
        }
        #endregion

        private void ChangeState(State newState)
        {
            if (logStates && _state != newState) Debug.Log($"[EnemySpiderAI] {_state} → {newState}");
            _state = newState;
        }

        private void SetBool(int hash, bool value) { if (animator != null) animator.SetBool(hash, value); }
        private void SetTrigger(int hash) { if (hash != 0 && animator != null) animator.SetTrigger(hash); }

        private void SetWalking(bool isWalking) { SetBool(_hWalk, isWalking); }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, disengageRadius);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, attackInitiationRange);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, groundAttackRange);

            Gizmos.color = new Color(1f, 1f, 0f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, attackStopDistance);

            Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, attackStopDistance - stopDistanceHysteresis));
        }
    }
}
