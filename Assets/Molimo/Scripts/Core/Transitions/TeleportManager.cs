using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    [DisallowMultipleComponent]
    [AddComponentMenu("Molimo/Teleport/Teleport Manager")]
    public class TeleportManager : MonoBehaviour
    {

        public static TeleportManager Instance { get; private set; }

        public static event System.Action<GameObject> OnTeleportComplete;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"[TeleportManager] already existe the instance enables ({Instance.name}). " +
                    $"Destruindo duplicata in '{name}'.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public System.Func<TeleportNode, bool> TeleportValidator;

        [Header("=== transition visual ===")]
        [Tooltip("component GridScreenTransition of the Image of the Canvas. " +
                 "if empty, teleporta without fade (not recommended).")]
        [SerializeField] private GridScreenTransition transition;

        [Header("=== physics of the player ===")]
        [SerializeField] private bool freezePlayerDuringTransition = true;
        [SerializeField] private bool resetVelocity = true;
        [Range(0, 10)][SerializeField] private int physicsSettleFrames = 2;
        [SerializeField] private float settleAfterTeleport = 0.15f;

        [Header("=== Snap-To-Ground ===")]
        [SerializeField] private bool snapToGround = true;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundRayStartHeight = 2.0f;
        [SerializeField] private float groundCheckDistance = 50f;
        [SerializeField] private bool useSphereCastFallback = true;
        [SerializeField] private float sphereCastRadius = 0.3f;
        [SerializeField] private float groundSinkOffset = 0.02f;

        [Header("=== Facing (direction when the appear) ===")]
        [SerializeField] private bool enforceFacing = true;
        [SerializeField] private bool unfreezeRotationToApplyFacing = true;

        [Header("=== persistence ===")]
        [Tooltip("Parent optional is where the objects in 'Keep Active' are " +
                 "movidos temporariamente. if empty, will is the root of the scene.")]
        [SerializeField] private Transform persistentParent;

        [Header("=== safety ===")]
        [SerializeField] private float globalCooldown = 0.5f;

        [Header("=== Debug ===")]
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private bool drawDebugRay = true;

        private bool _isTransitioning;
        private float _lastTeleportTime = -999f;

        public bool IsTransitioning => _isTransitioning;

        private class KeptObjectRecord
        {
            public GameObject Obj;
            public GameObject SourceArea;
            public Transform OriginalParent;
            public Vector3 OriginalLocalPos;
            public Quaternion OriginalLocalRot;
        }

        private readonly List<KeptObjectRecord> _keptObjects = new();

        private void OnValidate()
        {
            if (snapToGround && groundLayer.value == 0)
            {
                Debug.LogWarning(
                    "[TeleportManager] 'Ground Layer' is empty. Snap-To-Ground " +
                    "not vai funcionar and the Player will appear the cair.", this);
            }

            if (globalCooldown < 0f) globalCooldown = 0f;
            if (settleAfterTeleport < 0f) settleAfterTeleport = 0f;
            if (groundCheckDistance < 0f) groundCheckDistance = 0f;
            if (groundRayStartHeight < 0f) groundRayStartHeight = 0f;
            if (sphereCastRadius < 0f) sphereCastRadius = 0f;
        }

        public bool RequestTeleport(TeleportNode node, Rigidbody playerRb, Collider playerCollider)
        {
            if (node == null || playerRb == null)
            {
                LogWarn("RequestTeleport recebeu parameters nulos.");
                return false;
            }

            if (_isTransitioning)
            {
                Log($"request of '{node.name}' REJEITADA: transition in andamento.");
                return false;
            }

            if (Time.time - _lastTeleportTime < globalCooldown)
            {
                Log($"request of '{node.name}' REJEITADA: cooldown global ({globalCooldown:F2}s).");
                return false;
            }

            if (node.TargetArea == null)
            {
                LogWarn($"'{node.name}' does not have 'Target area' assigned.");
                return false;
            }

            if (node.SpawnPoint == null)
            {
                LogWarn($"'{node.name}' does not have 'Spawn Point' assigned.");
                return false;
            }

            if (TeleportValidator != null)
            {
                bool allowed = true;
                try
                {
                    System.Delegate[] listeners = TeleportValidator.GetInvocationList();
                    for (int i = 0; i < listeners.Length; i++)
                    {
                        var fn = (System.Func<TeleportNode, bool>)listeners[i];
                        if (!fn.Invoke(node))
                        {
                            allowed = false;
                            break;
                        }
                    }
                }
                catch (System.Exception and)
                {
                    Debug.LogException(and);
                }

                if (!allowed)
                {
                    Log($"request of '{node.name}' REJEITADA by validator external.");
                    return false;
                }
            }

            if (playerCollider == null)
                playerCollider = playerRb.GetComponent<Collider>();

            _lastTeleportTime = Time.time;
            StartCoroutine(ExecuteTransitionRoutine(node, playerRb, playerCollider));
            return true;
        }

        private IEnumerator ExecuteTransitionRoutine(
            TeleportNode node, Rigidbody playerRb, Collider playerCollider)
        {
            _isTransitioning = true;
            bool prevKinematic = playerRb.isKinematic;

            string currentAreaName = (node.CurrentArea != null) ? node.CurrentArea.name : "(without area)";
            Log($"▶ transition: '{currentAreaName}' → '{node.TargetArea.name}' (via {node.name}).");

            if (freezePlayerDuringTransition)
            {
                if (resetVelocity)
                {
                    playerRb.linearVelocity = Vector3.zero;
                    playerRb.angularVelocity = Vector3.zero;
                }
                playerRb.isKinematic = true;
            }

            if (transition != null)
                yield return transition.Cover();
            else
                LogWarn("without GridScreenTransition: pulando Fade IN.");

            GameObject targetArea = node.TargetArea;
            if (!targetArea.activeSelf)
                targetArea.SetActive(true);

            float footOffset = ComputeFootOffset(playerRb, playerCollider);
            Vector3 spawnPos = ResolveSpawnPosition(node.SpawnPoint.position, footOffset);

            playerRb.position = spawnPos;
            playerRb.transform.position = spawnPos;

            ApplyFacing(node, playerRb);

            Physics.SyncTransforms();

            ReleaseKeptObjectsOutside(targetArea);

            GameObject area = node.CurrentArea;
            if (area != null && area != targetArea && area.activeSelf)
            {
                ExtractKeepActive(node, area);
                area.SetActive(false);
            }
            else if (area != null && area == targetArea)
            {
                LogWarn($"'{node.name}': Current area is igual the Target area. area not will be desativada.");
            }

            for (int i = 0; i < physicsSettleFrames; i++)
                yield return new WaitForFixedUpdate();

            ForceGroundSnap(playerRb, playerCollider, footOffset);
            ForceFacing(node, playerRb);

            float holdTime = (transition != null ? transition.HoldDuration : 0f) + settleAfterTeleport;
            if (holdTime > 0f)
                yield return new WaitForSecondsRealtime(holdTime);

            if (transition != null)
                yield return transition.Reveal();

            ForceGroundSnap(playerRb, playerCollider, footOffset);
            ForceFacing(node, playerRb);

            if (freezePlayerDuringTransition)
                playerRb.isKinematic = prevKinematic;

            if (OnTeleportComplete != null)
            {
                try
                {
                    OnTeleportComplete.Invoke(targetArea);
                }
                catch (System.Exception and)
                {
                    Debug.LogException(and);
                }
            }

            _isTransitioning = false;

            Log($"✔ transition completed. Player in '{targetArea.name}' @ {spawnPos} " +
                $"(Y-rot {playerRb.rotation.eulerAngles.y:F1}°).");
        }

        private void ApplyFacing(TeleportNode node, Rigidbody playerRb)
        {
            if (!enforceFacing || playerRb == null) return;

            if (TryResolveFacing(node, playerRb.position, out Quaternion rot))
            {
                ApplyRotationSafe(playerRb, rot);
                if (resetVelocity)
                    playerRb.angularVelocity = Vector3.zero;
            }
            else
            {
                LogWarn($"'{node.name}': not was possible resolver the facing " +
                        "(without FacingTarget and without SpawnPoint valid).");
            }
        }

        private void ForceFacing(TeleportNode node, Rigidbody playerRb)
        {
            if (!enforceFacing || playerRb == null) return;

            if (TryResolveFacing(node, playerRb.position, out Quaternion rot))
            {
                float delta = Quaternion.Angle(playerRb.rotation, rot);
                if (delta > 0.01f)
                {
                    ApplyRotationSafe(playerRb, rot);
                    Physics.SyncTransforms();
                    Log($"Facing re-applied (Δ {delta:F2}°).");
                }
            }
        }

        private void ApplyRotationSafe(Rigidbody playerRb, Quaternion rot)
        {
            if (!unfreezeRotationToApplyFacing)
            {
                playerRb.rotation = rot;
                playerRb.transform.rotation = rot;
                return;
            }

            RigidbodyConstraints prev = playerRb.constraints;
            bool hadFreezeY = (prev & RigidbodyConstraints.FreezeRotationY) != 0;

            if (hadFreezeY)
                playerRb.constraints = prev & ~RigidbodyConstraints.FreezeRotationY;

            playerRb.rotation = rot;
            playerRb.transform.rotation = rot;

            if (hadFreezeY)
                playerRb.constraints = prev;
        }

        private bool TryResolveFacing(TeleportNode node, Vector3 playerPos, out Quaternion rotation)
        {
            rotation = Quaternion.identity;

            Transform target = node.FacingTarget;
            if (target != null)
            {
                Vector3 dir = target.position - playerPos;
                dir.y = 0f;
                if (dir.sqrMagnitude >= 0.0001f)
                {
                    rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    return true;
                }
            }

            Transform spawn = node.SpawnPoint;
            if (spawn != null)
            {
                Vector3 forward = spawn.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude >= 0.0001f)
                {
                    rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                    return true;
                }
                rotation = Quaternion.Euler(0f, spawn.eulerAngles.y, 0f);
                return true;
            }

            return false;
        }

        private void ExtractKeepActive(TeleportNode node, GameObject area)
        {
            GameObject[] keepSet = node.KeepActive;
            if (keepSet == null || keepSet.Length == 0) return;

            Transform dest = persistentParent;

            string destName = (dest != null) ? dest.name : "(root of the scene)";

            for (int i = 0; i < keepSet.Length; i++)
            {
                GameObject obj = keepSet[i];
                if (obj == null) continue;
                if (!obj.transform.IsChildOf(area.transform)) continue;

                if (_keptObjects.Exists(r => r.Obj == obj))
                    continue;

                _keptObjects.Add(new KeptObjectRecord
                {
                    Obj = obj,
                    SourceArea = area,
                    OriginalParent = obj.transform.parent,
                    OriginalLocalPos = obj.transform.localPosition,
                    OriginalLocalRot = obj.transform.localRotation
                });

                obj.transform.SetParent(dest, worldPositionStays: true);

                Log($"KeepActive: '{obj.name}' extracted of '{area.name}' → '{destName}'.");
            }
        }

        private void ReleaseKeptObjectsOutside(GameObject targetArea)
        {
            if (_keptObjects.Count == 0) return;

            for (int i = _keptObjects.Count - 1; i >= 0; i--)
            {
                KeptObjectRecord rec = _keptObjects[i];

                if (rec.Obj == null || rec.SourceArea == null)
                {
                    _keptObjects.RemoveAt(i);
                    continue;
                }

                bool stillInScope =
                    targetArea != null &&
                    (targetArea.transform == rec.SourceArea.transform ||
                     targetArea.transform.IsChildOf(rec.SourceArea.transform));

                if (stillInScope) continue;

                if (rec.OriginalParent != null)
                {
                    rec.Obj.transform.SetParent(rec.OriginalParent, worldPositionStays: false);
                    rec.Obj.transform.localPosition = rec.OriginalLocalPos;
                    rec.Obj.transform.localRotation = rec.OriginalLocalRot;

                }
                else
                {
                    rec.Obj.SetActive(false);
                }

                Log($"KeepActive libertado: '{rec.Obj.name}' (we exited of '{rec.SourceArea.name}').");

                _keptObjects.RemoveAt(i);
            }
        }

        private float ComputeFootOffset(Rigidbody playerRb, Collider playerCollider)
        {
            if (playerRb == null || playerCollider == null) return 1f;
            return Mathf.Max(playerRb.position.y - playerCollider.bounds.min.y, 0.01f);
        }

        private Vector3 ResolveSpawnPosition(Vector3 spawnPosition, float footOffset)
        {
            if (!snapToGround) return spawnPosition;

            Vector3 rayOrigin = spawnPosition + Vector3.up * groundRayStartHeight;
            if (TryGetGroundPoint(rayOrigin, out Vector3 groundPoint))
            {
                Vector3 result = spawnPosition;
                result.y = groundPoint.y + footOffset - groundSinkOffset;
                return result;
            }

            LogWarn("Snap-To-Ground FALHOU: without ground below of the SpawnPoint.");
            return spawnPosition;
        }

        private void ForceGroundSnap(Rigidbody playerRb, Collider playerCollider, float footOffset)
        {
            if (!snapToGround || playerRb == null) return;

            Vector3 probe = playerRb.position + Vector3.up * groundRayStartHeight;
            if (TryGetGroundPoint(probe, out Vector3 groundPoint))
            {
                float targetY = groundPoint.y + footOffset - groundSinkOffset;
                if (playerRb.position.y > targetY + 0.001f)
                {
                    Vector3 snapped = playerRb.position;
                    snapped.y = targetY;
                    playerRb.position = snapped;
                    playerRb.transform.position = snapped;
                    Physics.SyncTransforms();
                    Log($"Re-snap: Y {playerRb.position.y:F3} → {snapped.y:F3}");
                }
            }
        }

        private bool TryGetGroundPoint(Vector3 origin, out Vector3 groundPoint)
        {
            groundPoint = Vector3.zero;

            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    groundCheckDistance,
                    groundLayer,
                    QueryTriggerInteraction.Ignore))
            {
                groundPoint = hit.point;
                if (drawDebugRay)
                    Debug.DrawLine(origin, hit.point, Color.green, 1f);
                return true;
            }

            if (useSphereCastFallback &&
                Physics.SphereCast(
                    origin,
                    sphereCastRadius,
                    Vector3.down,
                    out RaycastHit sphereHit,
                    groundCheckDistance,
                    groundLayer,
                    QueryTriggerInteraction.Ignore))
            {
                groundPoint = sphereHit.point;
                if (drawDebugRay)
                    Debug.DrawLine(origin, sphereHit.point, Color.yellow, 1f);
                return true;
            }

            if (drawDebugRay)
                Debug.DrawLine(origin, origin + Vector3.down * groundCheckDistance, Color.red, 1f);

            return false;
        }

        private void Log(string msg)
        {
            if (debugLogs) Debug.Log($"[TeleportManager] {msg}", this);
        }

        private void LogWarn(string msg)
        {
            if (debugLogs) Debug.LogWarning($"[TeleportManager] {msg}", this);
        }
    }
}
