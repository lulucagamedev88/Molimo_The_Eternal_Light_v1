using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Molimo/Enemies/Witch Eye")]

    public class WitchEye : MonoBehaviour
    {

        private enum BodyRotationPlane
        {
            Plane3D_XZ_YawAxis = 0,
            Plane2D_XY_RollAxis = 1
        }

        [Header("=== Modo of rotation of the body ===")]
        [SerializeField] private BodyRotationPlane rotationPlane = BodyRotationPlane.Plane3D_XZ_YawAxis;
        [SerializeField] private float bodyRotationOffset = 0f;
        [SerializeField] private bool onlyRotateWhenPlayerDetected = true;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private bool syncRigidbodyRotation = true;
        [SerializeField] private bool disableAnimatorRootMotion = true;

        [Header("=== floating (circle X/Y) ===")]
        [SerializeField] private float floatRadiusX = 0.15f;
        [SerializeField] private float floatRadiusY = 0.20f;
        [SerializeField] private float floatFrequency = 0.5f;
        [Range(0f, 1f)][SerializeField] private float floatPhaseOffset = 0f;

        [Header("=== detection of the player ===")]
        [SerializeField] private float playerDetectionRadius = 6f;
        [SerializeField] private float playerLoseDetectionRadius = 8f;

        [Header("=== Follow of the player ===")]
        [SerializeField] private bool followPlayer = true;
        [SerializeField] private float followDistance = 4f;
        [SerializeField] private float followSpeed = 3f;
        [SerializeField] private float returnToPatrolSpeed = 2f;
        [SerializeField] private bool lockYToOriginal = true;

        [Header("=== rotation of the body (Retro) ===")]
        [SerializeField] private bool rotateBodyToPlayer = true;
        [Range(15f, 90f)][SerializeField] private float bodySnapAngle = 45f;
        [SerializeField] private bool bodySnapInstant = true;
        [SerializeField] private float bodyTurnSpeed = 720f;

        [Header("=== Olho ===")]
        [SerializeField] private Transform eyeTransform;
        [Range(0f, 180f)][SerializeField] private float eyeMaxYaw = 75f;
        [Range(0f, 90f)][SerializeField] private float eyeMaxPitch = 45f;

        [Header("=== Olho — Snap Retro ===")]
        [Range(5f, 90f)][SerializeField] private float eyeYawSnap = 45f;
        [Range(5f, 90f)][SerializeField] private float eyePitchSnap = 15f;
        [SerializeField] private bool eyeSnapInstant = true;
        [SerializeField] private float eyeTurnSpeed = 720f;

        [Header("=== attack (Bola of energy) ===")]
        [SerializeField] private GameObject energyBallPrefab;
        [SerializeField] private Transform energySpawnPoint;
        [SerializeField] private float energyBallSpeed = 12f;
        [SerializeField] private float attackCooldown = 3f;
        [SerializeField] private float attackRange = 7f;
        [SerializeField] private Vector3 playerTargetOffset = new Vector3(0f, 0.8f, 0f);
        [Tooltip("Desloca the point of spawn in the direction of the shot, evitando that the bola spawn inside the collider of the WitchEye.")]
        [SerializeField] private float spawnForwardOffset = 0.35f;
        [Tooltip("Desliga the gravidade of the bola — does it ir reto until the Player instead of of the arco.")]
        [SerializeField] private bool disableGravityOnEnergyBall = true;

        [Header("=== VFX Container ===")]
        [Tooltip("Transform (folder empty) where all the VFX gerados by WitchEye (bola of energy, pluff, etc.) will be parenteados. " +
                 "leave the the object empty in the root of the scene, with position (0,0,0), rotation (0,0,0) and scale (1,1,1), " +
                 "is not afetar the physics of the bolas of energy.")]
        [SerializeField] private Transform vfxContainer;

        [Header("=== creatures (mission) ===")]
        [SerializeField] private GameObject[] creatures;
        [SerializeField] private bool useHealthComponentFallback = true;

        [Tooltip("uses EnemySpiderAI the fallback is detect death (substitui the old SpiderEnemyAI).")]
        [SerializeField] private bool useSpiderEnemyAIFallback = true;

        [Header("=== pull of the player ===")]
        [SerializeField] private float pullSpeed = 12f;
        [SerializeField] private float pullStopDistance = 1.5f;
        [SerializeField] private float pullMaxDuration = 2.5f;
        [SerializeField] private float pullRecoverDelay = 0.25f;
        [SerializeField] private bool ignoreCreatureCollisionsDuringPull = true;
        [SerializeField] private bool useGhostTrailDuringPull = true;

        [Header("=== death (Pluff) ===")]
        [SerializeField] private GameObject pluffVfxPrefab;
        [SerializeField] private AudioClip pluffSound;
        [SerializeField] private float pluffDelay = 0.6f;
        [SerializeField] private bool destroyAfterPluff = true;

        [Header("=== integration ===")]
        [SerializeField] private bool blockTeleportWhileCreaturesAlive = true;
        [SerializeField] private bool rotatePlayerDuringPull = true;

        [Header("=== Animator ===")]
        [SerializeField] private Animator animator;
        [SerializeField] private string isFlyingParam = "IsFlying";
        [SerializeField] private string isPullingParam = "IsPulling";
        [SerializeField] private string waitAttackTrigger = "WaitAttack";
        [SerializeField] private string prepareAttackTrigger = "PrepareAttack";
        [SerializeField] private string attackTrigger = "Attack";

        private int hashIsFlying;
        private int hashIsPulling;
        private int hashWaitAttack;
        private int hashPrepareAttack;
        private int hashAttack;

        [Header("=== Debug ===")]
        [SerializeField] private bool debugLogs = true;

        private Vector3 originalPosition;
        private Vector3 basePosition;
        private float floatTime;

        private bool isDead;
        private bool isPulling;
        private bool isPlayerDetected;
        private bool isAttacking;
        private float lastAttackTime;

        private Coroutine pullRoutine;
        private Coroutine attackRoutine;

        private PlayerBrain playerBrain;
        private Rigidbody playerRigidbody;
        private Transform playerTransform;
        private GhostTrail playerGhostTrail;

        private Collider[] playerColliders;
        private readonly List<Collider> ignoredCreatureColliders = new List<Collider>();
        private bool collisionsIgnored;

        private Rigidbody selfRigidbody;
        private Animator selfAnimator;

        private Quaternion initialBodyRotation;
        private float currentSnappedBodyAngle = 0f;

        private bool vfxContainerWarned;

        private class CreatureInfo
        {
            public GameObject go;
            public IDamageable damageable;
            public EnemyMotherSpider enemyMotherSpider;
            public System.Action deathHandler;
            public System.Action<float, float> healthHandler;
            public bool defeatReported;
            public EnemySpiderAI spiderAI;
            public PlayerHealth health;
        }

        private CreatureInfo[] creatureInfos;

        public bool IsDead => isDead;
        public bool IsPulling => isPulling;
        public bool IsPlayerDetected => isPlayerDetected;
        public bool AllCreaturesDefeated => AreAllCreaturesDefeated();

        private void Awake()
        {
            originalPosition = transform.position;
            basePosition = originalPosition;
            floatTime = floatPhaseOffset * Mathf.PI * 2f;

            if (playerLoseDetectionRadius < playerDetectionRadius)
                playerLoseDetectionRadius = playerDetectionRadius;

            if (eyeTransform == null)
            {
                Transform t = transform.Find("Eye");
                if (t != null) eyeTransform = t;
            }

            if (eyeTransform == null)
                Debug.LogWarning("[WitchEye] none 'Eye' assigned (nor child called 'Eye').", this);

            selfRigidbody = GetComponent<Rigidbody>();

            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            selfAnimator = animator;

            hashIsFlying = Animator.StringToHash(isFlyingParam);
            hashIsPulling = Animator.StringToHash(isPullingParam);
            hashWaitAttack = Animator.StringToHash(waitAttackTrigger);
            hashPrepareAttack = Animator.StringToHash(prepareAttackTrigger);
            hashAttack = Animator.StringToHash(attackTrigger);

            if (animator != null)
            {
                ValidateAnimatorParameter(isFlyingParam, AnimatorControllerParameterType.Bool);
                ValidateAnimatorParameter(isPullingParam, AnimatorControllerParameterType.Bool);
                ValidateAnimatorParameter(waitAttackTrigger, AnimatorControllerParameterType.Trigger);
                ValidateAnimatorParameter(prepareAttackTrigger, AnimatorControllerParameterType.Trigger);
                ValidateAnimatorParameter(attackTrigger, AnimatorControllerParameterType.Trigger);
            }
            else
            {
                Debug.LogWarning("[WitchEye] none Animator found.", this);
            }

            if (disableAnimatorRootMotion && selfAnimator != null && selfAnimator.applyRootMotion)
            {
                selfAnimator.applyRootMotion = false;
                Log("Animator.applyRootMotion disabled.");
            }

            if (syncRigidbodyRotation && selfRigidbody != null && !selfRigidbody.isKinematic)
            {
                selfRigidbody.constraints |= RigidbodyConstraints.FreezeRotationX
                                           | RigidbodyConstraints.FreezeRotationY
                                           | RigidbodyConstraints.FreezeRotationZ;
                selfRigidbody.interpolation = RigidbodyInterpolation.None;
                Log("Rigidbody: rotation congelada is prevent conflito.");
            }

            CaptureInitialRotation();
        }

        private void OnEnable()
        {
            RefreshCreatureCache();
            CachePlayer();
            SubscribeValidator();
        }

        private void Start()
        {
            RefreshCreatureCache();
            CachePlayer();
            SubscribeValidator();
        }

        private void OnDisable()
        {
            UnsubscribeValidator();
            UnsubscribeCreatureEvents();

            if (pullRoutine != null)
            {
                StopCoroutine(pullRoutine);
                pullRoutine = null;
                isPulling = false;
                SetAnimatorBool(hashIsPulling, false);
                EndPullEffects();
                ReleasePlayer();
            }
            else
            {
                EndPullEffects();
            }

            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
                isAttacking = false;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeValidator();
            UnsubscribeCreatureEvents();
            EndPullEffects();
        }

        private void Update()
        {
            if (isDead) return;

            UpdateDetection();
            UpdateFollowBase();
            UpdateFloat();
            UpdateAnimator();
            CheckAttackConditions();
            CheckCreaturesStatus();
        }

        private void LateUpdate()
        {
            if (isDead) return;

            UpdateBodyRotation();
            UpdateEyeLook();
        }

        private void ValidateAnimatorParameter(string paramName, AnimatorControllerParameterType expected)
        {
            if (animator == null || string.IsNullOrEmpty(paramName)) return;

            foreach (var p in animator.parameters)
            {
                if (p.name == paramName)
                {
                    if (p.type != expected)
                    {
                        Debug.LogWarning(
                            $"[WitchEye] Par�metro '{paramName}' existe mas � of the tipo {p.type}, " +
                            $"esperado {expected}.", this);
                    }
                    return;
                }
            }

            Debug.LogWarning(
                $"[WitchEye] Par�metro '{paramName}' does not exist in the Animator.", this);
        }

        private void SetAnimatorBool(int hash, bool value)
        {
            if (animator == null) return;
            animator.SetBool(hash, value);
        }

        private void SetAnimatorTrigger(int hash)
        {
            if (animator == null) return;
            animator.SetTrigger(hash);
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            bool shouldFly = !isPlayerDetected && !isPulling && !isAttacking;
            SetAnimatorBool(hashIsFlying, shouldFly);
        }

        private void CheckAttackConditions()
        {
            if (isDead || isPulling || isAttacking || !isPlayerDetected) return;
            if (Time.time < lastAttackTime + attackCooldown) return;

            if (playerTransform == null) CachePlayer();
            if (playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= attackRange)
            {
                attackRoutine = StartCoroutine(AttackSequenceRoutine());
            }
        }

        private IEnumerator AttackSequenceRoutine()
        {
            isAttacking = true;
            lastAttackTime = Time.time;

            SetAnimatorTrigger(hashWaitAttack);
            Log("attack: WaitAttack");
            yield return new WaitForSeconds(0.4f);

            SetAnimatorTrigger(hashPrepareAttack);
            Log("attack: PrepareAttack");
            yield return new WaitForSeconds(0.3f);

            SetAnimatorTrigger(hashAttack);
            Log("attack: Attack");

            yield return new WaitForSeconds(0.5f);
            isAttacking = false;
            attackRoutine = null;
        }

        public void TriggerEnergyAttack()
        {
            if (energyBallPrefab == null)
            {
                Log("Prefab of the bola of energy not assigned in the Inspector!");
                return;
            }

            if (playerTransform == null)
            {
                CachePlayer();
                if (playerTransform == null) return;
            }

            Vector3 baseSpawnPos;
            if (energySpawnPoint != null) baseSpawnPos = energySpawnPoint.position;
            else if (eyeTransform != null) baseSpawnPos = eyeTransform.position;
            else baseSpawnPos = transform.position;

            Vector3 targetPos = playerTransform.position + playerTargetOffset;

            Vector3 toTarget = targetPos - baseSpawnPos;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                Log("TriggerEnergyAttack: direction invalid (spawn == target).");
                return;
            }
            Vector3 throwDirection = toTarget.normalized;

            Vector3 spawnPos = baseSpawnPos + throwDirection * spawnForwardOffset;
            Quaternion spawnRotation = Quaternion.LookRotation(throwDirection, Vector3.up);

            GameObject energyBall = InstantiateInVfxContainer(energyBallPrefab, spawnPos, spawnRotation);

            Rigidbody ballRb = energyBall.GetComponent<Rigidbody>();
            if (ballRb == null) ballRb = energyBall.GetComponentInChildren<Rigidbody>();

            if (ballRb == null)
            {
                ballRb = energyBall.AddComponent<Rigidbody>();
                Log("WitchEye: Rigidbody adicionado to the bola (not existia in the prefab).");
            }

            if (ballRb.isKinematic)
            {
                ballRb.isKinematic = false;
                Log("WitchEye: Rigidbody of the bola estava Kinematic — desativado.");
            }

            if (ballRb.constraints != RigidbodyConstraints.None)
            {
                Log($"WitchEye: Rigidbody of the bola tinha constraints '{ballRb.constraints}' — removidas.");
                ballRb.constraints = RigidbodyConstraints.None;
            }

            if (disableGravityOnEnergyBall) ballRb.useGravity = false;

            ballRb.linearDamping = 0f;
            ballRb.angularDamping = 0f;
            ballRb.interpolation = RigidbodyInterpolation.Interpolate;
            ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            MagicBolt bolt = energyBall.GetComponent<MagicBolt>();
            if (bolt == null) bolt = energyBall.GetComponentInChildren<MagicBolt>();

            if (bolt != null)
            {
                bolt.Initialize(throwDirection, energyBallSpeed);
                Log($"MagicBolt inicializado: dir={throwDirection}, speed={energyBallSpeed}");
            }
            else
            {

                ballRb.linearVelocity = Vector3.zero;
                ballRb.angularVelocity = Vector3.zero;
                ballRb.linearVelocity = throwDirection * energyBallSpeed;

                Debug.LogWarning(
                    "[WitchEye] the prefab of the bola does not have the component MagicBolt! " +
                    "the bola vai mover-if mas not causar� damage, VFX nem auto-destrui��o.", this);
            }

            Log($"Bola launched: dir={throwDirection}, speed={energyBallSpeed}, " +
                $"velFinal={ballRb.linearVelocity}, kinematic={ballRb.isKinematic}, " +
                $"temMagicBolt={(bolt != null)}");
        }

        private GameObject InstantiateInVfxContainer(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            GameObject go = Instantiate(prefab, pos, rot);

            if (vfxContainer != null)
            {
                go.transform.SetParent(vfxContainer, true);

                if (!vfxContainerWarned)
                {
                    bool badScale = (vfxContainer.localScale - Vector3.one).sqrMagnitude > 0.0001f;
                    bool badRot = Quaternion.Angle(vfxContainer.rotation, Quaternion.identity) > 0.1f;
                    if (badScale || badRot)
                    {
                        Debug.LogWarning(
                            "[WitchEye] VFX Container has scale/rota��o not-identidade. " +
                            "Isso can distorcer the physics of the bolas of energy. " +
                            "recommended: create the GameObject empty na root with pos (0,0,0), rot (0,0,0) and scale (1,1,1).", this);
                        vfxContainerWarned = true;
                    }
                }
            }

            return go;
        }

        private void CaptureInitialRotation()
        {
            Transform target = visualRoot != null ? visualRoot : transform;
            initialBodyRotation = target.localRotation;
        }

        private void UpdateDetection()
        {
            if (!followPlayer)
            {
                isPlayerDetected = false;
                return;
            }

            if (isPulling) return;

            if (playerTransform == null)
            {
                CachePlayer();
                if (playerTransform == null)
                {
                    isPlayerDetected = false;
                    return;
                }
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            if (isPlayerDetected)
            {
                if (dist > playerLoseDetectionRadius)
                {
                    isPlayerDetected = false;
                    Log("Player outside the zona — regressando to the patrulha.");
                }
            }
            else
            {
                if (dist <= playerDetectionRadius)
                {
                    isPlayerDetected = true;
                    Log("Player detected — the follow.");
                }
            }
        }

        private void UpdateFollowBase()
        {
            if (isPulling || isAttacking) return;

            if (!followPlayer || !isPlayerDetected)
            {
                if ((basePosition - originalPosition).sqrMagnitude > 0.0001f)
                {
                    basePosition = Vector3.MoveTowards(
                        basePosition, originalPosition,
                        returnToPatrolSpeed * Time.deltaTime);
                }
                return;
            }

            if (playerTransform == null)
            {
                CachePlayer();
                if (playerTransform == null) return;
            }

            Vector3 toWitch = basePosition - playerTransform.position;
            toWitch.y = 0f;

            Vector3 dir = toWitch.sqrMagnitude > 0.0001f
                ? toWitch.normalized
                : Vector3.forward;

            Vector3 idealBase = playerTransform.position + dir * followDistance;
            idealBase.y = lockYToOriginal ? originalPosition.y : playerTransform.position.y;

            basePosition = Vector3.MoveTowards(
                basePosition, idealBase, followSpeed * Time.deltaTime);
        }

        private void UpdateFloat()
        {
            floatTime += Time.deltaTime * floatFrequency * Mathf.PI * 2f;

            float offsetX = Mathf.Sin(floatTime) * floatRadiusX;
            float offsetY = Mathf.Cos(floatTime) * floatRadiusY;

            transform.position = basePosition + new Vector3(offsetX, offsetY, 0f);
        }

        private void UpdateBodyRotation()
        {
            if (!rotateBodyToPlayer) return;

            Transform target = visualRoot != null ? visualRoot : transform;

            bool shouldRotateNow = isPlayerDetected || isPulling || isAttacking;

            if (onlyRotateWhenPlayerDetected && !shouldRotateNow)
                return;

            if (playerTransform == null)
            {
                CachePlayer();
                if (playerTransform == null) return;
            }

            Vector3 toPlayerWorld = playerTransform.position - transform.position;

            float targetAngle;
            Quaternion targetRot;

            if (rotationPlane == BodyRotationPlane.Plane3D_XZ_YawAxis)
            {
                toPlayerWorld.y = 0f;
                if (toPlayerWorld.sqrMagnitude < 0.0001f) return;

                targetAngle = Mathf.Atan2(toPlayerWorld.x, toPlayerWorld.z) * Mathf.Rad2Deg;
            }
            else
            {
                Vector2 v2 = new Vector2(toPlayerWorld.x, toPlayerWorld.y);
                if (v2.sqrMagnitude < 0.0001f) return;

                targetAngle = Mathf.Atan2(v2.y, v2.x) * Mathf.Rad2Deg - 90f;
            }

            targetAngle += bodyRotationOffset;

            float snapped = SnapAngle(targetAngle, bodySnapAngle);
            currentSnappedBodyAngle = snapped;

            if (rotationPlane == BodyRotationPlane.Plane3D_XZ_YawAxis)
                targetRot = Quaternion.Euler(0f, snapped, 0f);
            else
                targetRot = Quaternion.Euler(0f, 0f, snapped);

            if (bodySnapInstant)
            {
                ApplyRotation(target, targetRot);
            }
            else
            {
                Quaternion current = target.localRotation;
                Quaternion stepped = Quaternion.RotateTowards(
                    current, targetRot, bodyTurnSpeed * Time.deltaTime);
                ApplyRotation(target, stepped);
            }
        }

        private void ApplyRotation(Transform target, Quaternion rot)
        {
            if (target == transform)
            {
                transform.rotation = rot;

                if (syncRigidbodyRotation && selfRigidbody != null)
                {
                    if (selfRigidbody.isKinematic) selfRigidbody.MoveRotation(rot);
                    else selfRigidbody.rotation = rot;
                }
            }
            else
            {
                target.localRotation = rot;

                if (syncRigidbodyRotation)
                {
                    Rigidbody rb = target.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        if (rb.isKinematic) rb.MoveRotation(rot);
                        else rb.rotation = rot;
                    }
                }
            }
        }

        private void UpdateEyeLook()
        {
            if (eyeTransform == null) return;

            if (playerTransform == null) CachePlayer();
            if (playerTransform == null) return;

            if (onlyRotateWhenPlayerDetected && !isPlayerDetected && !isPulling && !isAttacking)
                return;

            Vector3 toPlayer = playerTransform.position - eyeTransform.position;

            Transform reference = eyeTransform.parent != null ? eyeTransform.parent : transform;
            Vector3 localDir = reference.InverseTransformDirection(toPlayer);

            float planarLen = new Vector2(localDir.x, localDir.z).magnitude;

            float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(localDir.y, Mathf.Max(planarLen, 0.0001f)) * Mathf.Rad2Deg;

            yaw = Mathf.Clamp(yaw, -eyeMaxYaw, eyeMaxYaw);
            pitch = Mathf.Clamp(pitch, -eyeMaxPitch, eyeMaxPitch);

            yaw = SnapAngle(yaw, eyeYawSnap);
            pitch = SnapAngle(pitch, eyePitchSnap);

            yaw = Mathf.Clamp(yaw, -eyeMaxYaw, eyeMaxYaw);
            pitch = Mathf.Clamp(pitch, -eyeMaxPitch, eyeMaxPitch);

            Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);

            if (eyeSnapInstant)
                eyeTransform.localRotation = targetRot;
            else
                eyeTransform.localRotation = Quaternion.RotateTowards(
                    eyeTransform.localRotation, targetRot,
                    eyeTurnSpeed * Time.deltaTime);
        }

        private static float SnapAngle(float angle, float step)
        {
            if (step <= 0.001f) return angle;
            return Mathf.Round(angle / step) * step;
        }

        private void RefreshCreatureCache()
        {
            UnsubscribeCreatureEvents();

            if (creatures == null || creatures.Length == 0)
            {
                creatureInfos = null;
                return;
            }

            creatureInfos = new CreatureInfo[creatures.Length];

            for (int i = 0; i < creatures.Length; i++)
            {
                GameObject c = creatures[i];
                CreatureInfo info = new CreatureInfo();
                info.go = c;

                if (c != null)
                {
                    info.damageable = c.GetComponent<IDamageable>();
                    if (info.damageable == null) info.damageable = c.GetComponentInParent<IDamageable>();

                    info.enemyMotherSpider = c.GetComponent<EnemyMotherSpider>();
                    if (info.enemyMotherSpider == null) info.enemyMotherSpider = c.GetComponentInParent<EnemyMotherSpider>();

                    if (useSpiderEnemyAIFallback)
                    {
                        info.spiderAI = c.GetComponent<EnemySpiderAI>();
                        if (info.spiderAI == null) info.spiderAI = c.GetComponentInParent<EnemySpiderAI>();
                    }

                    if (useHealthComponentFallback)
                    {
                        info.health = c.GetComponent<PlayerHealth>();
                        if (info.health == null) info.health = c.GetComponentInParent<PlayerHealth>();
                    }
                }

                creatureInfos[i] = info;
            }

            SubscribeCreatureEvents();
        }

        private void SubscribeCreatureEvents()
        {
            if (creatureInfos == null) return;

            for (int i = 0; i < creatureInfos.Length; i++)
            {
                CreatureInfo info = creatureInfos[i];
                if (info == null) continue;

                if (info.spiderAI != null)
                {
                    info.deathHandler = () => OnCreatureDied(info);
                    info.healthHandler = (cur, max) => OnCreatureHealthChanged(info, cur);

                    info.spiderAI.OnDeath += info.deathHandler;
                    info.spiderAI.OnHealthChanged += info.healthHandler;
                }
                else if (info.enemyMotherSpider != null)
                {
                    info.deathHandler = () => OnCreatureDied(info);
                    info.healthHandler = (cur, max) => OnCreatureHealthChanged(info, cur);

                    info.enemyMotherSpider.OnDeath += info.deathHandler;
                    info.enemyMotherSpider.OnHealthChanged += info.healthHandler;
                }
            }
        }

        private void UnsubscribeCreatureEvents()
        {
            if (creatureInfos == null) return;

            for (int i = 0; i < creatureInfos.Length; i++)
            {
                CreatureInfo info = creatureInfos[i];
                if (info == null) continue;

                if (info.spiderAI != null)
                {
                    if (info.deathHandler != null)
                        info.spiderAI.OnDeath -= info.deathHandler;
                    if (info.healthHandler != null)
                        info.spiderAI.OnHealthChanged -= info.healthHandler;
                }
                else if (info.enemyMotherSpider != null)
                {
                    if (info.deathHandler != null)
                        info.enemyMotherSpider.OnDeath -= info.deathHandler;
                    if (info.healthHandler != null)
                        info.enemyMotherSpider.OnHealthChanged -= info.healthHandler;
                }

                info.deathHandler = null;
                info.healthHandler = null;
            }
        }

        private void OnCreatureDied(CreatureInfo info)
        {
            if (info == null) return;
            if (info.defeatReported) return;

            info.defeatReported = true;
            Log($"Criatura '{info.go?.name}' morreu (OnDeath).");
            CheckCreaturesStatus();
        }

        private void OnCreatureHealthChanged(CreatureInfo info, float current)
        {
            if (info == null) return;
            if (info.defeatReported) return;
            if (current > 0f) return;

            info.defeatReported = true;
            Log($"Criatura '{info.go?.name}' chegou the 0 HP (OnHealthChanged).");
            CheckCreaturesStatus();
        }

        private bool AreAllCreaturesDefeated()
        {
            if (creatureInfos == null || creatureInfos.Length == 0) return false;

            for (int i = 0; i < creatureInfos.Length; i++)
            {
                if (IsCreatureDefeated(creatureInfos[i])) continue;
                return false;
            }
            return true;
        }

        private bool IsCreatureDefeated(CreatureInfo info)
        {
            if (info == null) return true;
            if (info.defeatReported) return true;
            if (info.go == null) return true;

            if (info.damageable != null && !info.damageable.IsAlive) return true;

            if (info.spiderAI != null) return !info.spiderAI.IsAlive;

            if (useHealthComponentFallback && info.health != null)
                return info.health.CurrentHealth <= 0f;

            if (!info.go.activeInHierarchy) return true;
            return false;
        }

        private void CheckCreaturesStatus()
        {
            if (creatureInfos == null || creatureInfos.Length == 0) return;
            if (isDead) return;
            if (!AreAllCreaturesDefeated()) return;
            DiePluff();
        }

        private void DiePluff()
        {
            if (isDead) return;
            isDead = true;

            SetAnimatorBool(hashIsPulling, false);
            SetAnimatorBool(hashIsFlying, false);

            Log("all the creatures eliminadas. WitchEye: Pluff!");
            UnsubscribeValidator();
            UnsubscribeCreatureEvents();

            if (pullRoutine != null)
            {
                StopCoroutine(pullRoutine);
                pullRoutine = null;
                isPulling = false;
            }

            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
                isAttacking = false;
            }

            EndPullEffects();
            ReleasePlayer();

            StartCoroutine(PluffRoutine());
        }

        private IEnumerator PluffRoutine()
        {
            if (pluffDelay > 0f) yield return new WaitForSeconds(pluffDelay);

            if (pluffVfxPrefab != null)
                InstantiateInVfxContainer(pluffVfxPrefab, transform.position, Quaternion.identity);

            if (pluffSound != null)
                AudioSource.PlayClipAtPoint(pluffSound, transform.position);

            if (destroyAfterPluff) Destroy(gameObject);
            else gameObject.SetActive(false);
        }

        private void SubscribeValidator()
        {
            if (!blockTeleportWhileCreaturesAlive) return;
            if (TeleportManager.Instance == null) return;

            TeleportManager.Instance.TeleportValidator -= ValidateTeleport;
            TeleportManager.Instance.TeleportValidator += ValidateTeleport;
        }

        private void UnsubscribeValidator()
        {
            if (TeleportManager.Instance == null) return;
            TeleportManager.Instance.TeleportValidator -= ValidateTeleport;
        }

        private bool ValidateTeleport(TeleportNode node)
        {
            if (isDead) return true;
            if (creatureInfos == null || creatureInfos.Length == 0) return true;
            if (AreAllCreaturesDefeated()) return true;

            Log($"Player tentou exit via '{node.name}' with creatures vivas — the puxar.");

            if (pullRoutine == null)
                pullRoutine = StartCoroutine(PullPlayerRoutine());

            return false;
        }

        private IEnumerator PullPlayerRoutine()
        {
            CachePlayer();

            if (playerBrain == null || playerRigidbody == null || !playerBrain.IsAlive)
            {
                pullRoutine = null;
                yield break;
            }

            isPulling = true;

            SetAnimatorBool(hashIsPulling, true);

            BeginPullEffects();

            PlayerMovement movement = playerBrain.GetMovementState();
            if (movement != null) movement.SetSuspended(true);
            playerBrain.SetMovementInputLocked(true);

            float timer = 0f;
            Vector3 pullTarget = basePosition;

            while (timer < pullMaxDuration)
            {
                if (playerBrain == null || playerRigidbody == null) break;
                if (!playerBrain.IsAlive || isDead) break;

                Vector3 toWitch = pullTarget - playerRigidbody.position;
                toWitch.y = 0f;

                float dist = toWitch.magnitude;
                if (dist <= pullStopDistance) break;

                Vector3 dir = dist > 0.0001f ? toWitch / dist : Vector3.zero;
                float speed = Mathf.Min(pullSpeed, dist * 4f);

                float currentY = playerRigidbody.linearVelocity.y;
                playerRigidbody.linearVelocity = new Vector3(dir.x * speed, currentY, dir.z * speed);

                if (rotatePlayerDuringPull && dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
                    playerRigidbody.MoveRotation(Quaternion.Slerp(
                        playerRigidbody.rotation, lookRot,
                        Time.fixedDeltaTime * 12f));
                }

                timer += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (playerRigidbody != null && !playerRigidbody.isKinematic)
                playerRigidbody.linearVelocity = new Vector3(0f, playerRigidbody.linearVelocity.y, 0f);

            EndPullEffects();

            if (pullRecoverDelay > 0f)
                yield return new WaitForSeconds(pullRecoverDelay);

            ReleasePlayer();

            isPulling = false;
            SetAnimatorBool(hashIsPulling, false);
            pullRoutine = null;

            Log("pull terminado.");
        }

        private void BeginPullEffects()
        {
            if (ignoreCreatureCollisionsDuringPull) SetCreatureCollisionsIgnored(true);
            if (useGhostTrailDuringPull && playerGhostTrail != null)
                playerGhostTrail.ShowGhostTrail(pullMaxDuration);
        }

        private void EndPullEffects()
        {
            if (collisionsIgnored) SetCreatureCollisionsIgnored(false);
            if (playerGhostTrail != null) playerGhostTrail.StopGhostTrail();
        }

        private void SetCreatureCollisionsIgnored(bool ignore)
        {
            if (playerTransform == null) return;

            if (playerColliders == null || playerColliders.Length == 0)
                playerColliders = playerTransform.GetComponentsInChildren<Collider>(true);
            if (playerColliders == null || playerColliders.Length == 0) return;

            if (ignore)
            {
                ignoredCreatureColliders.Clear();

                if (creatures != null)
                {
                    foreach (var creature in creatures)
                    {
                        if (creature == null) continue;
                        Collider[] cCols = creature.GetComponentsInChildren<Collider>(true);
                        foreach (var cc in cCols)
                        {
                            if (cc == null) continue;
                            ignoredCreatureColliders.Add(cc);
                            foreach (var pc in playerColliders)
                            {
                                if (pc == null) continue;
                                Physics.IgnoreCollision(pc, cc, true);
                            }
                        }
                    }
                }

                collisionsIgnored = true;
                Log($"collisions ignoradas with {ignoredCreatureColliders.Count} collider(s).");
            }
            else
            {
                foreach (var cc in ignoredCreatureColliders)
                {
                    if (cc == null) continue;
                    foreach (var pc in playerColliders)
                    {
                        if (pc == null) continue;
                        Physics.IgnoreCollision(pc, cc, false);
                    }
                }
                ignoredCreatureColliders.Clear();
                collisionsIgnored = false;
                Log("collisions with creatures restauradas.");
            }
        }

        private void ReleasePlayer()
        {
            if (playerBrain == null) return;
            if (!playerBrain.IsAlive) return;

            PlayerMovement movement = playerBrain.GetMovementState();
            if (movement != null) movement.SetSuspended(false);
            playerBrain.SetMovementInputLocked(false);
        }

        private void CachePlayer()
        {
            if (playerBrain != null && playerRigidbody != null && playerTransform != null) return;

            GameObject playerGo = null;

            try { playerGo = GameObject.FindGameObjectWithTag("Player"); }
            catch { }

            if (playerGo == null)
            {
                playerBrain = FindAnyObjectByType<PlayerBrain>();
                if (playerBrain != null) playerGo = playerBrain.gameObject;
            }

            if (playerGo == null) return;

            playerBrain = playerGo.GetComponent<PlayerBrain>();
            playerRigidbody = playerGo.GetComponent<Rigidbody>();
            playerTransform = playerGo.transform;

            playerGhostTrail = playerGo.GetComponent<GhostTrail>();
            if (playerGhostTrail == null)
                playerGhostTrail = playerGo.GetComponentInChildren<GhostTrail>(true);

            playerColliders = playerGo.GetComponentsInChildren<Collider>(true);
        }

        private void Log(string msg)
        {
            if (debugLogs) Debug.Log($"[WitchEye:{name}] {msg}", this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? basePosition : transform.position;

            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, playerDetectionRadius);

            if (playerLoseDetectionRadius > playerDetectionRadius)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, playerLoseDetectionRadius);
            }

            Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.4f);
            const int segments = 24;
            Vector3 prev = center + new Vector3(floatRadiusX, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float the = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 p = center + new Vector3(
                    Mathf.Cos(the) * floatRadiusX,
                    Mathf.Sin(the) * floatRadiusY, 0f);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }

            if (followPlayer)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.35f);
                const int ringSegs = 32;
                Vector3 prevRing = Vector3.zero;
                for (int i = 0; i <= ringSegs; i++)
                {
                    float the = (i / (float)ringSegs) * Mathf.PI * 2f;
                    Vector3 p = center + new Vector3(
                        Mathf.Cos(the) * followDistance,
                        0f,
                        Mathf.Sin(the) * followDistance);
                    if (i > 0) Gizmos.DrawLine(prevRing, p);
                    prevRing = p;
                }
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(center, pullStopDistance);

            if (rotateBodyToPlayer)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.6f);
                int dirs = Mathf.Max(1, Mathf.RoundToInt(360f / bodySnapAngle));
                for (int i = 0; i < dirs; i++)
                {
                    float the = (i * bodySnapAngle) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Sin(the), 0f, Mathf.Cos(the));
                    Gizmos.DrawRay(transform.position, dir * 1.2f);
                }
            }

            if (eyeTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(eyeTransform.position, eyeTransform.forward * 1.5f);
            }

            if (vfxContainer != null)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.3f);
                Gizmos.DrawWireCube(vfxContainer.position, Vector3.one * 0.5f);
            }
        }
#endif
    }
}
