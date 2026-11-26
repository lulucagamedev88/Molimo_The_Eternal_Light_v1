using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]

    public class WeaponPickup : MonoBehaviour
    {

        public enum PickupType { Sword, Shield }

        private static readonly List<WeaponPickup> s_activePickups = new List<WeaponPickup>();

        [Header("Tipo")]
        [SerializeField] private PickupType pickupType = PickupType.Sword;

        [Header("references")]
        [SerializeField] private PlayerWeaponEquipment weaponEquip;

        [Header("Respawn in the Abismo")]
        [Tooltip("reference DIRECTA (not sobrevive the Instantiate).")]
        [SerializeField] private Transform respawnPoint;

        [Tooltip("TAG of the GameObject (Sobrevive the Clones). Ex: 'WeaponRespawn'.")]
        [SerializeField] private string respawnPointTag = "WeaponRespawn";

        [Tooltip("NOME of the GameObject in the scene (Fallback). Ex: 'RespawnpointWeapons'.")]
        [SerializeField] private string respawnPointName = "RespawnpointWeapons";

        [Header("visual of the Respawn")]
        [Tooltip("Offset of position somado when the RespawnPoint. Use the Y if quiser that the weapon " +
                 "surja the little more alta in the ar is cair naturalmente.")]
        [SerializeField] private Vector3 spawnPositionOffset = new Vector3(0f, 0.5f, 0f);

        [Tooltip("rotation local aplicada when the appear. is deitar the sword, tente (90, 0, 0) or (-90, 0, 0).")]
        [SerializeField] private Vector3 spawnRotationOffset = new Vector3(90f, 0f, 0f);

        [Tooltip("rotates the weapon aleatoriamente in the axis Y is parecer that caiu of forma natural.")]
        [SerializeField] private bool randomizeYRotation = true;

        [Header("Tempos of recovery")]
        [Tooltip("time (s) that the pickup remains imune when the abismo after be recuperado. " +
                 "must be > physicsEnableDelay + time of fall.")]
        [SerializeField] private float immunityDuration = 2.0f;

        [Tooltip("time (s) that the pickup remains Kinematic in the ar before falling (gives time when the " +
                 "Physics.SyncTransforms and prevents explosions).")]
        [SerializeField] private float physicsEnableDelay = 0.5f;

        [Header("Fallback of safety")]
        [Tooltip("if the pickup is below deste Y without the trigger of the abismo the apanhar, " +
                 "is recuperado automatically. must estar below of the your abismo.")]
        [SerializeField] private float voidFallbackY = -20f;

        private InputSystem_Actions inputActions;
        private Rigidbody rb;
        private Collider col;
        private PlayerBrain _playerBrain;

        private bool canPickup;
        private bool isSettling;
        private float settleTimer;
        private float _lastRecoveryTime = -100f;
        private Transform _cachedRespawnPoint;

        private float _immunityEndTime = 0f;
        private float _physicsEnableTime = 0f;
        private bool _isWaitingForPhysics = false;

        private bool _wasTransitioningDuringDisable;

        private bool _isHeld;

        public bool IsHeld => _isHeld;

        public bool IsImmuneToAbyss => Time.time < _immunityEndTime;

        public bool HasCustomRespawnPoint =>
            respawnPoint != null ||
            !string.IsNullOrEmpty(respawnPointTag) ||
            !string.IsNullOrEmpty(respawnPointName);

        public static void NotifyAllOfPlayerReset()
        {
            if (s_activePickups.Count == 0) return;
            var snapshot = new List<WeaponPickup>(s_activePickups);
            foreach (var p in snapshot)
                if (p != null) p.OnPlayerReset();
        }

        #region Unity Lifecycle

        private void Awake()
        {
            inputActions = new InputSystem_Actions();
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();

            TeleportManager.OnTeleportComplete += HandleTeleportComplete;
        }

        private void OnEnable()
        {
            if (!s_activePickups.Contains(this)) s_activePickups.Add(this);

            var pr = PlayerReference.Instance;
            if (pr != null && pr.Root != null)
            {
                if (_playerBrain == null) _playerBrain = pr.Root.GetComponentInChildren<PlayerBrain>();
                if (weaponEquip == null) weaponEquip = pr.Root.GetComponentInChildren<PlayerWeaponEquipment>();
            }
        }

        private void OnDisable()
        {
            s_activePickups.Remove(this);

            if (_isHeld)
            {
                _wasTransitioningDuringDisable = false;
                return;
            }

            if (TeleportManager.Instance != null && TeleportManager.Instance.IsTransitioning)
                _wasTransitioningDuringDisable = true;

            if (_isWaitingForPhysics)
            {
                _isWaitingForPhysics = false;

                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
            }
        }

        private void Update()
        {

            if (_isWaitingForPhysics)
            {
                if (Time.time >= _physicsEnableTime)
                {
                    _isWaitingForPhysics = false;
                    EnablePhysics();
                }
                return;
            }

            if (transform.position.y < voidFallbackY && !IsImmuneToAbyss)
            {
                RecoverFromAbyss();
                return;
            }

            if (isSettling)
            {
                settleTimer += Time.deltaTime;
                if (settleTimer > 0.4f && (rb.IsSleeping() || rb.linearVelocity.sqrMagnitude < 0.01f))
                    SettleAsPickup();
                return;
            }

            if (!canPickup) return;

            bool isGamepadNorthPressed = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
            bool isInteractPressed = inputActions != null && inputActions.Player.Interact.WasPressedThisFrame();

            if (isGamepadNorthPressed || isInteractPressed) PerformPickup();
        }

        private void OnDestroy()
        {
            TeleportManager.OnTeleportComplete -= HandleTeleportComplete;
            if (inputActions != null) { inputActions.Player.Disable(); inputActions.Dispose(); }
        }

        #endregion

        #region reaction when the end of the Teleporte (recover if ficou "stranded")

        private void HandleTeleportComplete(GameObject targetArea)
        {

            if (_isHeld) return;

            bool wasStranded =
                _wasTransitioningDuringDisable ||
                isSettling ||
                _isWaitingForPhysics;

            _wasTransitioningDuringDisable = false;

            if (!wasStranded) return;

            if (_isWaitingForPhysics) return;

            if (!gameObject.activeInHierarchy)
            {
                Transform respawnT = GetRespawnPoint();
                Transform safeParent = null;

                if (respawnT != null && respawnT.gameObject.activeInHierarchy)
                {
                    safeParent = respawnT.parent;
                    if (safeParent != null && !safeParent.gameObject.activeInHierarchy)
                        safeParent = null;
                }

                transform.SetParent(safeParent, worldPositionStays: false);
                gameObject.SetActive(true);
            }

            RecoverFromAbyss();
        }

        #endregion

        #region recovery of the Abismo (Kinematic → Teleport → Kinematic OFF)

        public void RecoverFromAbyss()
        {

            if (_isWaitingForPhysics)
            {
                return;
            }

            Transform target = GetRespawnPoint();
            Vector3 targetPos;
            Quaternion targetRot;

            if (target != null)
            {
                targetPos = target.position + target.TransformVector(spawnPositionOffset);
                targetRot = target.rotation * Quaternion.Euler(spawnRotationOffset);
                if (randomizeYRotation)
                    targetRot *= Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
            else
            {

                var pr = PlayerReference.Instance;
                if (pr != null && pr.Root != null)
                {
                    targetPos = pr.Root.position + Vector3.up * 1.5f + spawnPositionOffset;
                    targetRot = pr.Root.rotation * Quaternion.Euler(spawnRotationOffset);
                }
                else
                {

                    targetPos = transform.position + Vector3.up * 5f;
                    targetRot = transform.rotation;
                }
            }

            _lastRecoveryTime = Time.unscaledTime;
            _immunityEndTime = Time.time + immunityDuration;

            if (col != null) col.enabled = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            transform.SetPositionAndRotation(targetPos, targetRot);
            if (rb != null)
            {
                rb.position = targetPos;
                rb.rotation = targetRot;
            }
            Physics.SyncTransforms();

            if (col != null) col.enabled = true;

            _isWaitingForPhysics = true;
            _physicsEnableTime = Time.time + physicsEnableDelay;
        }

        private void EnablePhysics()
        {
            isSettling = true;
            settleTimer = 0f;
            canPickup = false;

            if (col != null) col.isTrigger = false;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.WakeUp();
            }
        }

        #endregion

        #region Respawn Point Resolution

        private Transform GetRespawnPoint()
        {
            if (respawnPoint != null) return respawnPoint;
            if (_cachedRespawnPoint != null) return _cachedRespawnPoint;

            if (!string.IsNullOrEmpty(respawnPointTag))
            {
                try
                {
                    var go = GameObject.FindWithTag(respawnPointTag);
                    if (go != null) { _cachedRespawnPoint = go.transform; return _cachedRespawnPoint; }
                }
                catch (UnityException) { }
            }

            if (!string.IsNullOrEmpty(respawnPointName))
            {
                var goByName = GameObject.Find(respawnPointName);
                if (goByName != null) { _cachedRespawnPoint = goByName.transform; return _cachedRespawnPoint; }
            }

            return null;
        }

        #endregion

        #region Physics Settle / Pickup

        public void ThrowWithPhysics(Vector3 force, ForceMode forceMode)
        {
            isSettling = true;
            settleTimer = 0f;
            canPickup = false;

            if (col != null) col.isTrigger = false;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.WakeUp();
                rb.AddForce(force, forceMode);
            }
        }

        private void SettleAsPickup()
        {
            isSettling = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            if (col != null) col.isTrigger = true;
            CheckOverlapPlayer();
        }

        public void OnPlayerReset()
        {
            if (_isWaitingForPhysics) return;
            if (_playerBrain != null && !_playerBrain.IsAlive) return;
            if (Time.unscaledTime - _lastRecoveryTime < 0.5f) return;
            if (IsImmuneToAbyss) return;

            bool isLost = isSettling || transform.position.y < voidFallbackY;
            if (!isLost) return;

            RecoverFromAbyss();
        }

        private void CheckOverlapPlayer()
        {
            if (col == null) return;
            Collider[] overlaps = Physics.OverlapBox(col.bounds.center, col.bounds.extents, transform.rotation);
            foreach (var overlap in overlaps)
            {
                if (TryResolveLiveWeaponEquip(overlap, out var targetEquip))
                {
                    canPickup = true; weaponEquip = targetEquip; break;
                }
            }
        }

        private bool TryResolveLiveWeaponEquip(Collider source, out PlayerWeaponEquipment targetEquip)
        {
            targetEquip = source != null ? source.GetComponentInParent<PlayerWeaponEquipment>() : null;
            if (targetEquip == null) return false;

            PlayerBrain targetBrain = targetEquip.GetComponentInParent<PlayerBrain>();
            return targetBrain == null || targetBrain.IsAlive;
        }

        private void PerformPickup()
        {
            if (weaponEquip == null) return;
            if (!IsClosestPickupForPlayer()) return;

            PlayerBrain targetBrain = weaponEquip.GetComponentInParent<PlayerBrain>();
            if (targetBrain != null && !targetBrain.IsAlive) return;

            weaponEquip.RegisterHeldPickup(pickupType, this);

            if (pickupType == PickupType.Shield) weaponEquip.EquipShield();
            else weaponEquip.EquipWeapon();

            HoldInInventory();
        }

        private void HoldInInventory()
        {
            _isHeld = true;
            canPickup = false;
            isSettling = false;
            _isWaitingForPhysics = false;
            _wasTransitioningDuringDisable = false;

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            if (col != null) col.isTrigger = true;

            transform.SetParent(null, true);
            gameObject.SetActive(false);
        }

        public void ReleaseFromPlayer(Vector3 position, Quaternion rotation, Vector3 force, ForceMode forceMode)
        {
            _isHeld = false;
            _isWaitingForPhysics = false;
            _immunityEndTime = 0f;
            canPickup = false;

            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);

            if (rb != null)
            {
                rb.position = position;
                rb.rotation = rotation;
            }
            Physics.SyncTransforms();

            ThrowWithPhysics(force, forceMode);
        }

        private bool IsClosestPickupForPlayer()
        {
            if (weaponEquip == null) return true;
            Vector3 playerPos = weaponEquip.transform.position;
            float myDistSqr = (transform.position - playerPos).sqrMagnitude;

            for (int i = 0; i < s_activePickups.Count; i++)
            {
                WeaponPickup other = s_activePickups[i];
                if (other == null || other == this || other.weaponEquip != weaponEquip || !other.canPickup) continue;
                if ((other.transform.position - playerPos).sqrMagnitude < myDistSqr) return false;
            }
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isSettling) return;
            if (TryResolveLiveWeaponEquip(other, out var targetEquip))
            {
                canPickup = true; weaponEquip = targetEquip;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var targetEquip = other.GetComponentInParent<PlayerWeaponEquipment>();
            if (targetEquip != null && targetEquip == weaponEquip) canPickup = false;
        }

        #endregion
    }
}
