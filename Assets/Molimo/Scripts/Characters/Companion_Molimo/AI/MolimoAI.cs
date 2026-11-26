using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class MolimoAI : MonoBehaviour
    {
        #region Enums

        public enum MolimoState
        {
            Normal,
            AlertingPlayer,
            WaitingForSwordRespawn,
            GoToSword,
            WaitingAtSword,
            ReturnToPlayer,
            AttackingEnemy,
            HealingPlayer
        }

        private enum AlertStyle
        {
            BackAndForth,
            CirclePlayer
        }

        #endregion

        #region Inspector Fields

        [Header("references")]
        [Tooltip("Transform of the player.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("optional: WeaponEquip of the player. if null, it auto-detects.")]
        [SerializeField] private PlayerWeaponEquipment playerWeaponEquip;

        [Tooltip("optional: Movement of the player. if null, it auto-detects. " +
                 "used is detect Sprint and acelerar the Molimo.")]
        [SerializeField] private PlayerMovement playerMovement;

        [Header("modules")]
        [SerializeField] private MolimoCombat combat;
        [SerializeField] private MolimoHealer healer;
        [SerializeField] private MolimoHealthContainer healthContainer;

        [Header("detection of the sword")]
        [SerializeField] private string weaponTag = "Weapon";
        [SerializeField] private LayerMask weaponLayer = ~0;
        [SerializeField] private float weaponSearchRadius = 0f;
        [SerializeField, Range(0.1f, 5f)] private float weaponSearchInterval = 0.5f;
        [SerializeField] private float swordRespawnDistanceThreshold = 3f;

        [Header("protection contra Abismo")]
        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField] private float safeGroundRayDistance = 3f;
        [SerializeField] private float fallingVelocityThreshold = -3f;
        [SerializeField] private float abyssYDifferenceThreshold = 6f;
        [SerializeField, Range(0.1f, 2f)] private float respawnCheckInterval = 0.3f;

        [Header("Acompanhamento")]
        [Tooltip("Offset in space local of the player: X=right, Y=up, Z=forward.")]
        [SerializeField] private Vector3 followOffset = new Vector3(1.5f, 1.5f, 0f);
        [SerializeField] private float followSmoothTime = 0.35f;
        [SerializeField] private float maxFollowSpeed = 8f;
        [SerializeField] private float rotationSpeed = 3f;

        [Header("floating")]
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobFrequency = 2f;

        [Header("Sprint Follow")]
        [SerializeField] private float sprintReactionDelay = 2.5f;
        [SerializeField] private float sprintSpeedMultiplier = 2.2f;
        [SerializeField] private float sprintAheadDistance = 2.5f;
        [SerializeField] private float sprintRecoveryTime = 0.7f;

        [Header("Teleport Catch-Up")]
        [SerializeField] private float teleportDetectDistance = 20f;
        [SerializeField] private float teleportCatchUpSpeedMultiplier = 8f;
        [SerializeField] private float teleportCatchUpDuration = 4f;
        [SerializeField] private float teleportCatchUpEndDistance = 3.5f;

        [Header("Alerta - Vai-and-comes")]
        [SerializeField] private float alertDuration = 1.8f;
        [SerializeField] private float alertOscillationSpeed = 4f;
        [SerializeField] private float alertMoveSpeed = 6f;
        [SerializeField] private float alertHeight = 1.5f;

        [Header("Alerta - circle in torno of the player")]
        [SerializeField, Range(0f, 1f)] private float circleAlertChance = 0.5f;
        [SerializeField] private float alertCircleRadius = 1.5f;
        [SerializeField] private Vector3 alertCircleOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private float alertCircleAngularSpeed = 360f;
        [SerializeField] private float alertCircleMaxDuration = 4f;

        [Header("Ir is the sword")]
        [SerializeField] private float swordArrivalDistance = 1.2f;
        [SerializeField] private float swordApproachSpeed = 6f;
        [SerializeField] private float swordMoveSmooth = 0.3f;
        [SerializeField] private Vector3 swordOffset = new Vector3(0f, 1f, 0f);

        [Header("wait and Lembrete")]
        [SerializeField] private float timeToRemindAgain = 6f;
        [SerializeField] private float waitHoverAmplitude = 0.12f;
        [SerializeField] private float waitHoverSpeed = 1.5f;

        #endregion

        #region Internal State

        private MolimoState _state = MolimoState.Normal;

        private Vector3 _currentVelocity;
        private Vector3 _bobOffset;
        private float _bobTime;

        private float _stateTimer;
        private float _reminderTimer;
        private float _weaponSearchTimer;
        private float _respawnCheckTimer;

        private AlertStyle _alertStyle = AlertStyle.BackAndForth;
        private Vector3 _alertStartOffset;

        private float _alertCircleAngle;
        private float _alertCircleTotalRotation;
        private int _alertCircleDirection = 1;
        private float _alertCircleRadiusActual;

        private Transform _swordTransform;
        private Vector3 _lastKnownSwordPos;
        private bool _hasLastKnownSwordPos;

        private bool _wasHoldingSword;

        private float _playerSprintTimer;
        private bool _isSprintBoosting;
        private float _sprintBoostBlend;

        private float _catchUpBoostTimer;

        #endregion

        #region Public Properties

        public MolimoState CurrentState => _state;
        public Transform CurrentSwordTransform => _swordTransform;
        public Transform PlayerTransform => playerTransform;
        public Vector3 FollowOffset => followOffset;
        public Vector3 CurrentBobOffset => _bobOffset;

        public bool IsSprintBoosting => _isSprintBoosting;
        public bool IsCatchUpBoosting => _catchUpBoostTimer > 0f;

        public System.Func<bool> SwordCheckOverride;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ResolveReferences();

            if (combat != null)
                combat.Initialize(playerTransform, followOffset);

            if (healer != null)
                healer.Initialize(playerTransform, healthContainer);
        }

        private void Start()
        {
            bool alreadyPositioned = MolimoAIPositionFromPrologue.HasPendingProloguePosition;

            if (playerTransform != null && !alreadyPositioned)
                transform.position = GetFollowTargetPosition();

            FaceTargetOrPlayer(instant: true);

            RefreshSwordReference(force: true);
            _wasHoldingSword = IsPlayerHoldingSword();

            EnterState(_wasHoldingSword ? MolimoState.Normal : MolimoState.AlertingPlayer);
        }

        private void Update()
        {
            if (playerTransform == null) return;

            UpdateSwordPeriodicSearch();

            if (_state != MolimoState.HealingPlayer && healer != null)
            {
                if (healer.CanStartRescue())
                    EnterState(MolimoState.HealingPlayer);
                else
                    healer.TickRefill();
            }

            EvaluatePlayerSwordTransition();

            if (!_wasHoldingSword)
                UpdateSwordRespawnCheck();

            UpdateTimersAndBobbing();

            UpdateSprintFollow();
            UpdateTeleportCatchUp();

            ExecuteCurrentState();
            FaceTargetOrPlayer();
        }

        #endregion

        #region Reference Resolution

        private void ResolveReferences()
        {
            if (playerTransform == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            if (playerWeaponEquip == null && playerTransform != null)
                playerWeaponEquip = playerTransform.GetComponentInParent<PlayerWeaponEquipment>();

            if (playerMovement == null && playerTransform != null)
                playerMovement = playerTransform.GetComponentInParent<PlayerMovement>();

            if (combat == null) combat = GetComponent<MolimoCombat>();
            if (healer == null) healer = GetComponent<MolimoHealer>();
            if (healthContainer == null) healthContainer = GetComponent<MolimoHealthContainer>();
        }

        #endregion

        #region Sprint Follow & Teleport Catch-Up

        private void UpdateSprintFollow()
        {
            bool playerSprinting = playerMovement != null
                                   && playerMovement.IsSprinting
                                   && playerMovement.CurrentPhysicalSpeed > 0.1f;

            if (playerSprinting)
            {
                _playerSprintTimer += Time.deltaTime;
                if (_playerSprintTimer >= sprintReactionDelay)
                    _isSprintBoosting = true;
            }
            else
            {
                _playerSprintTimer -= Time.deltaTime;
                if (_playerSprintTimer <= 0f)
                {
                    _playerSprintTimer = 0f;
                    _isSprintBoosting = false;
                }
            }

            float targetBlend = _isSprintBoosting ? 1f : 0f;
            float blendSpeed = 1f / Mathf.Max(0.01f, sprintRecoveryTime);
            _sprintBoostBlend = Mathf.MoveTowards(_sprintBoostBlend, targetBlend, blendSpeed * Time.deltaTime);
        }

        private void UpdateTeleportCatchUp()
        {
            if (playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, GetFollowTargetPosition());

            if (dist >= teleportDetectDistance)
            {
                _catchUpBoostTimer = teleportCatchUpDuration;

                if (_state == MolimoState.AttackingEnemy ||
                    _state == MolimoState.GoToSword ||
                    _state == MolimoState.WaitingAtSword ||
                    _state == MolimoState.WaitingForSwordRespawn)
                {
                    if (combat != null) combat.Cancel();
                    EnterState(MolimoState.ReturnToPlayer);
                }
            }
            else if (_catchUpBoostTimer > 0f)
            {
                _catchUpBoostTimer -= Time.deltaTime;

                if (dist <= teleportCatchUpEndDistance)
                    _catchUpBoostTimer = 0f;
            }
        }

        private float GetEffectiveSmoothTime()
        {
            if (_catchUpBoostTimer > 0f) return 0.05f;
            if (_isSprintBoosting)
                return followSmoothTime * Mathf.Lerp(1f, 0.35f, _sprintBoostBlend);
            return followSmoothTime;
        }

        private float GetEffectiveMaxFollowSpeed()
        {
            if (_catchUpBoostTimer > 0f)
                return maxFollowSpeed * teleportCatchUpSpeedMultiplier;

            if (_isSprintBoosting)
                return maxFollowSpeed * Mathf.Lerp(1f, sprintSpeedMultiplier, _sprintBoostBlend);

            return maxFollowSpeed;
        }

        #endregion

        #region Sword Detection

        private void UpdateSwordPeriodicSearch()
        {
            _weaponSearchTimer += Time.deltaTime;
            if (_weaponSearchTimer < weaponSearchInterval) return;

            _weaponSearchTimer = 0f;
            RefreshSwordReference(force: false);
        }

        private void EvaluatePlayerSwordTransition()
        {
            bool holdingNow = IsPlayerHoldingSword();
            if (holdingNow == _wasHoldingSword) return;

            if (_state == MolimoState.HealingPlayer)
            {
                _wasHoldingSword = holdingNow;
                return;
            }

            _wasHoldingSword = holdingNow;

            if (holdingNow)
            {
                if (combat != null) combat.Cancel();
                EnterState(MolimoState.Normal);
            }
            else
            {
                if (combat != null) combat.Cancel();
                RefreshSwordReference(force: true);
                EnterState(EvaluateLostSwordState());
            }
        }

        private void UpdateSwordRespawnCheck()
        {
            _respawnCheckTimer += Time.deltaTime;
            if (_respawnCheckTimer < respawnCheckInterval) return;

            _respawnCheckTimer = 0f;
            HandleSwordStateWhilePlayerUnarmed();
        }

        private MolimoState EvaluateLostSwordState()
        {
            if (_swordTransform == null) return MolimoState.WaitingForSwordRespawn;

            return IsSwordInSafeGround()
                ? MolimoState.AlertingPlayer
                : MolimoState.WaitingForSwordRespawn;
        }

        private void HandleSwordStateWhilePlayerUnarmed()
        {
            if (_swordTransform == null)
            {
                if (_state != MolimoState.WaitingForSwordRespawn &&
                    _state != MolimoState.AlertingPlayer &&
                    _state != MolimoState.GoToSword &&
                    _state != MolimoState.WaitingAtSword &&
                    _state != MolimoState.ReturnToPlayer)
                {
                    EnterState(MolimoState.WaitingForSwordRespawn);
                }
                return;
            }

            bool safe = IsSwordInSafeGround();
            Vector3 currentPos = _swordTransform.position;
            float moved = _hasLastKnownSwordPos
                ? Vector3.Distance(currentPos, _lastKnownSwordPos)
                : 0f;
            bool respawned = moved >= swordRespawnDistanceThreshold;

            if (!safe && _state != MolimoState.WaitingForSwordRespawn)
            {
                EnterState(MolimoState.WaitingForSwordRespawn);
            }

            if (safe && respawned)
            {
                _lastKnownSwordPos = currentPos;
                _hasLastKnownSwordPos = true;

                if (_state == MolimoState.WaitingForSwordRespawn ||
                    _state == MolimoState.Normal)
                {
                    EnterState(MolimoState.AlertingPlayer);
                }
            }

            if (safe && !_hasLastKnownSwordPos)
            {
                _lastKnownSwordPos = currentPos;
                _hasLastKnownSwordPos = true;
            }
        }

        private bool IsSwordInSafeGround()
        {
            if (_swordTransform == null) return false;
            if (!_swordTransform.gameObject.activeInHierarchy) return false;

            if (_swordTransform.TryGetComponent<Rigidbody>(out var rb))
            {
                if (rb.linearVelocity.y < fallingVelocityThreshold) return false;
            }

            if (playerTransform != null)
            {
                float yDiff = playerTransform.position.y - _swordTransform.position.y;
                if (yDiff > abyssYDifferenceThreshold) return false;
            }

            Vector3 origin = _swordTransform.position + Vector3.up * 0.3f;
            return Physics.Raycast(origin, Vector3.down, out _,
                                   safeGroundRayDistance, groundLayer,
                                   QueryTriggerInteraction.Ignore);
        }

        private void RefreshSwordReference(bool force)
        {
            if (!force && IsPlayerHoldingSword())
            {
                _hasLastKnownSwordPos = false;
                return;
            }

            Transform found = FindWeaponInWorld();

            if (found != _swordTransform)
            {
                _swordTransform = found;
                _hasLastKnownSwordPos = false;
            }

            if (_swordTransform != null && !_hasLastKnownSwordPos && IsSwordInSafeGround())
            {
                _lastKnownSwordPos = _swordTransform.position;
                _hasLastKnownSwordPos = true;
            }
        }

        private Transform FindWeaponInWorld()
        {
            Transform found = null;

            if (weaponSearchRadius > 0f && playerTransform != null)
            {
                Collider[] hits = Physics.OverlapSphere(
                    playerTransform.position, weaponSearchRadius, weaponLayer,
                    QueryTriggerInteraction.Collide);

                float bestSqr = float.MaxValue;
                foreach (var hit in hits)
                {
                    if (!MatchesTag(hit.transform, weaponTag)) continue;

                    float sqr = (hit.transform.position - playerTransform.position).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; found = hit.transform; }
                }
            }

            if (found == null)
            {
                GameObject byTag = GameObject.FindWithTag(weaponTag);
                if (byTag != null) found = byTag.transform;
            }

            return found;
        }

        public bool IsPlayerHoldingSword()
        {
            if (SwordCheckOverride != null) return SwordCheckOverride();
            if (playerWeaponEquip != null) return playerWeaponEquip.IsWeaponEquipped;
            return true;
        }

        private bool MatchesTag(Transform t, string tag)
        {
            if (t == null || string.IsNullOrEmpty(tag)) return false;
            if (t.CompareTag(tag)) return true;
            if (t.root != t && t.root.CompareTag(tag)) return true;
            return false;
        }

        #endregion

        #region State Machine

        private void UpdateTimersAndBobbing()
        {
            _stateTimer += Time.deltaTime;
            _bobTime += Time.deltaTime * bobFrequency;
            _bobOffset = Vector3.up * (Mathf.Sin(_bobTime) * bobAmplitude);
        }

        private void ExecuteCurrentState()
        {
            switch (_state)
            {
                case MolimoState.Normal: UpdateNormal(); break;
                case MolimoState.AlertingPlayer: UpdateAlertingPlayer(); break;
                case MolimoState.WaitingForSwordRespawn: UpdateWaitingForSwordRespawn(); break;
                case MolimoState.GoToSword: UpdateGoToSword(); break;
                case MolimoState.WaitingAtSword: UpdateWaitingAtSword(); break;
                case MolimoState.ReturnToPlayer: UpdateReturnToPlayer(); break;
                case MolimoState.AttackingEnemy: UpdateAttackingEnemy(); break;
                case MolimoState.HealingPlayer: UpdateHealingPlayer(); break;
            }
        }

        private void EnterState(MolimoState newState)
        {
            if (_state == MolimoState.HealingPlayer && newState != MolimoState.HealingPlayer)
            {
                if (healer != null && healer.IsHealing)
                    healer.CancelHealing();
            }

            bool preserveVelocity =
                _state == MolimoState.AlertingPlayer &&
                newState == MolimoState.GoToSword;

            _state = newState;
            _stateTimer = 0f;

            if (!preserveVelocity)
                _currentVelocity = Vector3.zero;

            switch (newState)
            {
                case MolimoState.AlertingPlayer:
                    SetupAlertStyle();
                    break;

                case MolimoState.AttackingEnemy:
                    if (combat != null) combat.BeginCombat();
                    break;

                case MolimoState.HealingPlayer:
                    if (healer != null) healer.StartHealing();
                    break;

                case MolimoState.GoToSword:
                    if (_swordTransform == null) RefreshSwordReference(force: true);
                    break;

                case MolimoState.WaitingAtSword:
                    _reminderTimer = 0f;
                    break;

                case MolimoState.WaitingForSwordRespawn:
                case MolimoState.Normal:
                case MolimoState.ReturnToPlayer:
                    break;
            }
        }

        private void SetupAlertStyle()
        {
            _alertStyle = (Random.value < circleAlertChance)
                ? AlertStyle.CirclePlayer
                : AlertStyle.BackAndForth;

            if (_alertStyle == AlertStyle.BackAndForth)
            {
                _alertStartOffset = transform.position - playerTransform.position;
            }
            else
            {
                Vector3 circleCenter = playerTransform.position + alertCircleOffset;

                Vector3 fromCenter = transform.position - circleCenter;
                _alertCircleAngle = Mathf.Atan2(fromCenter.z, fromCenter.x);
                _alertCircleTotalRotation = 0f;
                _alertCircleDirection = Random.value < 0.5f ? 1 : -1;

                _alertCircleRadiusActual = Mathf.Max(
                    alertCircleRadius,
                    new Vector2(fromCenter.x, fromCenter.z).magnitude);
            }
        }

        #endregion

        #region State Updates

        private void UpdateNormal()
        {
            Vector3 target = GetFollowTargetPosition() + _bobOffset;
            transform.position = Vector3.SmoothDamp(
                transform.position, target, ref _currentVelocity,
                GetEffectiveSmoothTime(), GetEffectiveMaxFollowSpeed(), Time.deltaTime);

            if (combat != null && combat.HasAnyTargetsInRange())
            {
                EnterState(MolimoState.AttackingEnemy);
            }
        }

        private void UpdateAlertingPlayer()
        {
            switch (_alertStyle)
            {
                case AlertStyle.BackAndForth: UpdateAlertBackAndForth(); break;
                case AlertStyle.CirclePlayer: UpdateAlertCircle(); break;
            }
        }

        private void UpdateAlertBackAndForth()
        {
            Vector3 playerPos = playerTransform.position;

            Vector3 startPos = playerPos + _alertStartOffset;
            Vector3 approachPos = playerPos
                                  + Vector3.up * alertHeight
                                  + playerTransform.right * followOffset.x;

            float pingPong = (Mathf.Sin(_stateTimer * alertOscillationSpeed) + 1f) * 0.5f;
            Vector3 target = Vector3.Lerp(startPos, approachPos, pingPong) + _bobOffset;

            Vector3 previousPos = transform.position;
            transform.position = Vector3.MoveTowards(
                transform.position, target, alertMoveSpeed * Time.deltaTime);

            if (Time.deltaTime > 0f)
                _currentVelocity = (transform.position - previousPos) / Time.deltaTime;

            if (_stateTimer >= alertDuration)
                ProceedToSword();
        }

        private void UpdateAlertCircle()
        {
            float angularRadPerSec = alertCircleAngularSpeed * Mathf.Deg2Rad;
            float delta = angularRadPerSec * Time.deltaTime;
            _alertCircleAngle += delta * _alertCircleDirection;
            _alertCircleTotalRotation += delta;

            Vector3 circleCenter = playerTransform.position + alertCircleOffset;

            Vector3 offsetXZ = new Vector3(
                Mathf.Cos(_alertCircleAngle),
                0f,
                Mathf.Sin(_alertCircleAngle)) * _alertCircleRadiusActual;

            Vector3 targetPos = circleCenter + offsetXZ + _bobOffset;

            Vector3 previousPos = transform.position;
            transform.position = Vector3.MoveTowards(
                transform.position, targetPos, alertMoveSpeed * Time.deltaTime);

            if (Time.deltaTime > 0f)
                _currentVelocity = (transform.position - previousPos) / Time.deltaTime;

            bool fullRotation = _alertCircleTotalRotation >= Mathf.PI * 2f;
            bool safetyTimeout = _stateTimer >= alertCircleMaxDuration;

            if (fullRotation || safetyTimeout)
                ProceedToSword();
        }

        private void ProceedToSword()
        {
            if (_swordTransform == null) RefreshSwordReference(force: true);

            if (_swordTransform == null || !IsSwordInSafeGround())
                EnterState(MolimoState.WaitingForSwordRespawn);
            else
                EnterState(MolimoState.GoToSword);
        }

        private void UpdateWaitingForSwordRespawn()
        {
            Vector3 target = GetFollowTargetPosition() + _bobOffset;
            transform.position = Vector3.SmoothDamp(
                transform.position, target, ref _currentVelocity,
                GetEffectiveSmoothTime(), GetEffectiveMaxFollowSpeed(), Time.deltaTime);
        }

        private void UpdateGoToSword()
        {
            if (_swordTransform == null)
            {
                RefreshSwordReference(force: true);
                if (_swordTransform == null) { EnterState(MolimoState.ReturnToPlayer); return; }
            }

            if (!IsSwordInSafeGround())
            {
                EnterState(MolimoState.WaitingForSwordRespawn);
                return;
            }

            Vector3 target = _swordTransform.position + swordOffset + _bobOffset;
            transform.position = Vector3.SmoothDamp(
                transform.position, target, ref _currentVelocity,
                swordMoveSmooth, swordApproachSpeed, Time.deltaTime);

            float dist = Vector3.Distance(transform.position, _swordTransform.position + swordOffset);
            if (dist <= swordArrivalDistance)
                EnterState(MolimoState.WaitingAtSword);
        }

        private void UpdateWaitingAtSword()
        {
            if (_swordTransform == null)
            {
                RefreshSwordReference(force: true);
                if (_swordTransform == null) { EnterState(MolimoState.ReturnToPlayer); return; }
            }

            if (!IsSwordInSafeGround())
            {
                EnterState(MolimoState.WaitingForSwordRespawn);
                return;
            }

            float breath = Mathf.Sin(_stateTimer * waitHoverSpeed) * waitHoverAmplitude;
            Vector3 target = _swordTransform.position + swordOffset
                             + Vector3.up * breath + _bobOffset;

            transform.position = Vector3.SmoothDamp(
                transform.position, target, ref _currentVelocity,
                swordMoveSmooth, swordApproachSpeed, Time.deltaTime);

            _reminderTimer += Time.deltaTime;
            if (_reminderTimer >= timeToRemindAgain)
                EnterState(MolimoState.ReturnToPlayer);
        }

        private void UpdateReturnToPlayer()
        {
            Vector3 target = GetFollowTargetPosition() + _bobOffset;
            transform.position = Vector3.SmoothDamp(
                transform.position, target, ref _currentVelocity,
                GetEffectiveSmoothTime(), GetEffectiveMaxFollowSpeed(), Time.deltaTime);

            float dist = Vector3.Distance(transform.position, GetFollowTargetPosition());
            if (dist > 1.0f) return;

            if (_swordTransform != null && IsSwordInSafeGround())
                EnterState(MolimoState.AlertingPlayer);
            else
                EnterState(MolimoState.WaitingForSwordRespawn);
        }

        private void UpdateAttackingEnemy()
        {
            if (combat == null)
            {
                EnterState(MolimoState.Normal);
                return;
            }

            bool stillFighting = combat.Tick(_bobOffset);

            if (!stillFighting)
                RouteAfterCombat();
        }

        private void UpdateHealingPlayer()
        {
            if (healer == null)
            {
                EnterState(MolimoState.Normal);
                return;
            }

            bool stillHealing = healer.Tick();

            if (!stillHealing)
                RouteAfterHealing();
        }

        private void RouteAfterCombat()
        {
            if (!IsPlayerHoldingSword())
                EnterState(EvaluateLostSwordState());
            else
                EnterState(MolimoState.Normal);
        }

        private void RouteAfterHealing()
        {
            if (!IsPlayerHoldingSword())
                EnterState(EvaluateLostSwordState());
            else
                EnterState(MolimoState.Normal);
        }

        #endregion

        #region Positioning and Rotation

        public Vector3 GetFollowTargetPosition()
        {
            Vector3 baseTarget = playerTransform.position
                   + playerTransform.right * followOffset.x
                   + Vector3.up * followOffset.y
                   + playerTransform.forward * followOffset.z;

            if (_sprintBoostBlend > 0.001f)
            {
                baseTarget += playerTransform.forward * (sprintAheadDistance * _sprintBoostBlend);
            }

            return baseTarget;
        }

        private void FaceTargetOrPlayer(bool instant = false)
        {
            Vector3 lookTarget;

            if (_state == MolimoState.AttackingEnemy && combat != null && combat.CurrentTarget != null)
                lookTarget = combat.CurrentTarget.position;
            else if (playerTransform != null)
                lookTarget = playerTransform.position;
            else
                return;

            Vector3 lookDir = lookTarget - transform.position;
            if (lookDir.sqrMagnitude < 0.001f) return;

            Quaternion targetRot = Quaternion.LookRotation(lookDir);

            transform.rotation = instant
                ? targetRot
                : Quaternion.Slerp(transform.rotation, targetRot,
                                   Time.deltaTime * rotationSpeed);
        }

        #endregion

        #region Public API

        public void OnPlayerLostSword()
        {
            _wasHoldingSword = false;

            if (combat != null) combat.Cancel();

            if (_state == MolimoState.HealingPlayer)
            {
                if (healer != null) healer.CancelHealing();
                return;
            }

            RefreshSwordReference(force: true);
            EnterState(EvaluateLostSwordState());
        }

        public void OnPlayerPickedSword()
        {
            _wasHoldingSword = true;

            if (_state == MolimoState.HealingPlayer) return;
            EnterState(MolimoState.Normal);
        }

        public void OnSwordRespawned(Transform newSword)
        {
            UpdateSwordReference(newSword);

            if (_state == MolimoState.HealingPlayer) return;

            if (!IsPlayerHoldingSword())
            {
                _wasHoldingSword = false;
                EnterState(EvaluateLostSwordState());
            }
        }

        public void UpdateSwordReference(Transform newSword)
        {
            _swordTransform = newSword;
            _hasLastKnownSwordPos = false;

            if (newSword != null && IsSwordInSafeGround())
            {
                _lastKnownSwordPos = newSword.position;
                _hasLastKnownSwordPos = true;
            }
        }

        public void SetPlayer(Transform newPlayer)
        {
            playerTransform = newPlayer;

            if (playerTransform != null)
            {
                if (playerWeaponEquip == null)
                    playerWeaponEquip = playerTransform.GetComponentInParent<PlayerWeaponEquipment>();

                if (playerMovement == null)
                    playerMovement = playerTransform.GetComponentInParent<PlayerMovement>();
            }

            if (combat != null) combat.Initialize(playerTransform, followOffset);
            if (healer != null) healer.Initialize(playerTransform, healthContainer);
        }

        public void ForceReevaluate()
        {
            if (playerTransform == null) return;

            RefreshSwordReference(force: true);
            _wasHoldingSword = IsPlayerHoldingSword();

            EnterState(_wasHoldingSword
                ? MolimoState.Normal
                : EvaluateLostSwordState());
        }

        public void OnPlayerTeleported()
        {
            _catchUpBoostTimer = teleportCatchUpDuration;

            if (_state == MolimoState.AttackingEnemy ||
                _state == MolimoState.GoToSword ||
                _state == MolimoState.WaitingAtSword ||
                _state == MolimoState.WaitingForSwordRespawn)
            {
                if (combat != null) combat.Cancel();
                EnterState(MolimoState.ReturnToPlayer);
            }
        }

        #endregion

        #region Helpers

        private void OnDrawGizmosSelected()
        {
            if (playerTransform != null)
            {
                Gizmos.color = Color.green;
                Vector3 followTarget = playerTransform.position
                    + playerTransform.right * followOffset.x
                    + Vector3.up * followOffset.y
                    + playerTransform.forward * followOffset.z;
                Gizmos.DrawWireSphere(followTarget, 0.2f);

                Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.7f);
                Vector3 sprintTarget = followTarget
                    + playerTransform.forward * sprintAheadDistance;
                Gizmos.DrawWireSphere(sprintTarget, 0.15f);
                Gizmos.DrawLine(followTarget, sprintTarget);

                Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.4f);
                Gizmos.DrawWireSphere(
                    playerTransform.position + alertCircleOffset,
                    alertCircleRadius);
            }

            if (weaponSearchRadius > 0f && playerTransform != null)
            {
                Gizmos.color = new Color(0f, 0.8f, 1f, 0.25f);
                Gizmos.DrawWireSphere(playerTransform.position, weaponSearchRadius);
            }

            if (_swordTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_swordTransform.position + swordOffset, swordArrivalDistance);

                Gizmos.color = IsSwordInSafeGround() ? Color.green : Color.red;
                Vector3 origin = _swordTransform.position + Vector3.up * 0.3f;
                Gizmos.DrawLine(origin, origin + Vector3.down * safeGroundRayDistance);
            }

            if (playerTransform != null)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
                Gizmos.DrawWireSphere(playerTransform.position, teleportDetectDistance);
            }
        }

        #endregion
    }
}
