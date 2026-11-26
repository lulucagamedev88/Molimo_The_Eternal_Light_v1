using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Molimo By Luluca/Respawn Checkpoint Tracker")]

    public sealed class PlayerRespawnCheckpointTracker : MonoBehaviour
    {
        #region Inspector

        [Header("Rastreamento")]
        [Tooltip("interval minimum between gravacoes (seconds).")]
        [SerializeField] private float recordInterval = 0.5f;

        [Tooltip("distance minimum (units) that the Player precisa if move away of the last point " +
                 "is that the novo point is gravado.")]
        [SerializeField] private float minDistanceToUpdate = 0.5f;

        [Tooltip("time minimum (seconds) that the Player precisa estar Grounded is the point be considered safe.")]
        [SerializeField] private float minGroundedTimeToRecord = 0.1f;

        [Tooltip("if true, exige that the Player esteja Grounded is gravar.")]
        [SerializeField] private bool requireGrounded = true;

        [Header("validation of ground (anti-edge)")]
        [Tooltip("Layers that count the ground. if empty, the validation remains DESLIGADA " +
                 "(behavior old — respawn can cair in the abismo).")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("height above of the point candidato where the raycasts of ground start.")]
        [SerializeField] private float groundProbeHeight = 1.5f;

        [Tooltip("distance that the raycast falls procurando ground.")]
        [SerializeField] private float groundProbeDistance = 3f;

        [Tooltip("radius of the ring of amostragem when the redor of the candidato. " +
                 "all the points of the ring precisam ter ground is the point be valid.")]
        [SerializeField] private float validationRadius = 0.6f;

        [Tooltip("number of directions sampled in the ring. 8 = strict, 4 = light.")]
        [SerializeField] private int validationSampleCount = 8;

        [Tooltip("Offset above of the ground applied to the position refinada (prevents spawn inside the ground).")]
        [SerializeField] private float groundSnapOffsetY = 0.05f;

        [Tooltip("if true, when the point candidato fails, tries push it in the direction opposite " +
                 "at the edges detectadas, instead of simplesmente reject.")]
        [SerializeField] private bool pushAwayFromEdges = true;

        [Header("knockback in the Respawn")]
        [Tooltip("if true, when the calcular the respawn, recua the point in direction when the center of the " +
                 "platform (opposite at the edges), instead of only 'is back' of the personagem.")]
        [SerializeField] private bool smartBackUp = true;

        [Header("history")]
        [Tooltip("Quantos checkpoints valid keep in memory. if the more recente fail " +
                 "the validation in the respawn, tries the previous.")]
        [SerializeField] private int checkpointHistorySize = 5;

        [Header("Debug")]
        [Tooltip("Loga when the point is rejeitado or empurrado.")]
        [SerializeField] private bool debugValidation = false;

        #endregion

        #region state

        private PlayerBrain brain;
        private PlayerMovement movement;
        private Rigidbody rb;

        private Vector3 lastSafePosition;
        private Quaternion lastSafeRotation;
        private bool hasValidCheckpoint = false;

        private float recordTimer = 0f;
        private float groundedTimer = 0f;

        private struct CheckpointEntry
        {
            public Vector3 position;
            public Quaternion rotation;
            public CheckpointEntry(Vector3 p, Quaternion r) { position = p; rotation = r; }
        }

        private readonly List<CheckpointEntry> history = new List<CheckpointEntry>();

        #endregion

        #region properties public

        public bool HasValidCheckpoint => hasValidCheckpoint;
        public Vector3 LastSafePosition => lastSafePosition;
        public Quaternion LastSafeRotation => lastSafeRotation;

        #endregion

        #region Ciclo of health

        private void Awake()
        {
            brain = GetComponent<PlayerBrain>();
            if (brain == null) brain = GetComponentInParent<PlayerBrain>();

            movement = GetComponent<PlayerMovement>();
            if (movement == null) movement = GetComponentInParent<PlayerMovement>();

            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = GetComponentInParent<Rigidbody>();

            lastSafePosition = transform.position;
            lastSafeRotation = transform.rotation;
            hasValidCheckpoint = false;
        }

        private void Start()
        {

            if (groundLayer.value != 0 && TryValidateCheckpointPosition(transform.position, out Vector3 refined, out _))
            {
                RecordCheckpoint(refined, transform.rotation);
                if (debugValidation)
                    Debug.Log($"[checkpoint] seed initial recorded in {refined}.");
            }
        }

        private void Update()
        {

            if (brain != null && !brain.IsAlive) return;
            if (brain != null && brain.IsKnockedDown) return;
            if (movement != null && movement.IsSuspended) return;

            if (IsConsideredGrounded())
                groundedTimer += Time.deltaTime;
            else
                groundedTimer = 0f;

            recordTimer += Time.deltaTime;
            if (recordTimer < recordInterval) return;
            recordTimer = 0f;

            if (requireGrounded && groundedTimer < minGroundedTimeToRecord) return;

            Vector3 currentPos = transform.position;
            Vector3 candidatePos;
            Quaternion candidateRot = transform.rotation;

            if (TryValidateCheckpointPosition(currentPos, out Vector3 refinedPos, out Vector3 pushDir))
            {
                candidatePos = refinedPos;
            }
            else
            {

                if (pushAwayFromEdges && pushDir.sqrMagnitude > 0.001f)
                {
                    Vector3 pushed = currentPos + pushDir.normalized * validationRadius;

                    if (TryValidateCheckpointPosition(pushed, out Vector3 pushedRefined, out _))
                    {
                        candidatePos = pushedRefined;

                        if (debugValidation)
                            Debug.Log($"[checkpoint] position current in the edge — empurrado is {pushedRefined} (dir={pushDir.normalized}).");
                    }
                    else
                    {

                        if (debugValidation)
                            Debug.Log("[checkpoint] edge detectada; push falhou. Mantendo checkpoint previous.");
                        return;
                    }
                }
                else
                {

                    if (debugValidation)
                        Debug.Log("[checkpoint] without ground when the redor; mantendo checkpoint previous.");
                    return;
                }
            }

            if (!hasValidCheckpoint || Vector3.Distance(candidatePos, lastSafePosition) >= minDistanceToUpdate)
            {
                RecordCheckpoint(candidatePos, candidateRot);
            }
        }

        private void RecordCheckpoint(Vector3 position, Quaternion rotation)
        {
            lastSafePosition = position;
            lastSafeRotation = rotation;
            hasValidCheckpoint = true;

            if (checkpointHistorySize > 0)
            {
                if (history.Count >= checkpointHistorySize)
                    history.RemoveAt(0);

                history.Add(new CheckpointEntry(position, rotation));
            }
        }

        private bool IsConsideredGrounded()
        {
            if (movement != null) return movement.IsGrounded;

            if (rb != null) return Mathf.Abs(rb.linearVelocity.y) < 0.1f;

            return true;
        }

        #endregion

        #region validation of ground

        private bool TryValidateCheckpointPosition(Vector3 candidatePos, out Vector3 refinedPos, out Vector3 pushDir)
        {
            refinedPos = candidatePos;
            pushDir = Vector3.zero;

            if (groundLayer.value == 0) return true;

            Vector3 centerOrigin = candidatePos + Vector3.up * groundProbeHeight;
            float rayLength = groundProbeHeight + groundProbeDistance;

            if (!Physics.Raycast(centerOrigin, Vector3.down, out RaycastHit centerHit,
                                 rayLength, groundLayer, QueryTriggerInteraction.Ignore))
            {

                if (debugValidation)
                    Debug.LogWarning($"[checkpoint] without ground in the center of {candidatePos}.");
                return false;
            }

            int missingCount = CollectUnsafeGroundSamples(candidatePos, out Vector3 unsafeAccum);

            if (missingCount == 0)
            {
                refinedPos = new Vector3(
                    candidatePos.x,
                    centerHit.point.y + groundSnapOffsetY,
                    candidatePos.z);
                return true;
            }

            if (unsafeAccum.sqrMagnitude > 0.001f)
                pushDir = -unsafeAccum.normalized;

            if (debugValidation)
            {
                int samples = Mathf.Max(4, validationSampleCount);
                Debug.LogWarning($"[checkpoint] point rejeitado in {candidatePos}: " +
                                 $"{missingCount}/{samples} amostras without ground. " +
                                 $"pushDir sugerido = {pushDir}.");
            }

            return false;
        }

        private int CollectUnsafeGroundSamples(Vector3 center, out Vector3 unsafeAccum)
        {
            int samples = Mathf.Max(4, validationSampleCount);
            float rayLength = groundProbeHeight + groundProbeDistance;
            unsafeAccum = Vector3.zero;
            int missingCount = 0;

            for (int i = 0; i < samples; i++)
            {
                float angle = (360f / samples) * i;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 samplePos = center + dir * validationRadius;
                Vector3 origin = samplePos + Vector3.up * groundProbeHeight;

                bool hit = Physics.Raycast(origin, Vector3.down, rayLength,
                                           groundLayer, QueryTriggerInteraction.Ignore);

                if (!hit)
                {
                    unsafeAccum += dir;
                    missingCount++;
                }
            }

            return missingCount;
        }

        #endregion

        #region API Public

        public void GetRespawnPoint(float backUpDistance, out Vector3 position, out Quaternion rotation)
        {
            position = lastSafePosition;
            rotation = lastSafeRotation;

            if (backUpDistance > 0.001f)
            {
                Vector3 backupDir = smartBackUp
                    ? GetSafeBackupDirection()
                    : -(lastSafeRotation * Vector3.forward);

                position -= backupDir * backUpDistance;
            }

            if (groundLayer.value != 0)
            {
                if (TryValidateCheckpointPosition(position, out Vector3 validated, out _))
                {
                    position = validated;
                    return;
                }

                for (int i = history.Count - 1; i >= 0; i--)
                {
                    if (TryValidateCheckpointPosition(history[i].position, out Vector3 validPos, out _))
                    {
                        position = validPos;
                        rotation = history[i].rotation;

                        Debug.LogWarning($"[checkpoint] point current invalid in the respawn — using history [{i}] " +
                                         $"in {validPos}.");
                        return;
                    }
                }

                Debug.LogError("[checkpoint] none checkpoint valid available. " +
                               "Respawn can cair in the abismo. Verifique the 'groundLayer' and the level.");
            }
        }

        private Vector3 GetSafeBackupDirection()
        {
            if (groundLayer.value == 0)
                return -(lastSafeRotation * Vector3.forward);

            Vector3 unsafeAccum;
            int missingCount = CollectUnsafeGroundSamples(lastSafePosition, out unsafeAccum);

            if (missingCount == 0 || unsafeAccum.sqrMagnitude < 0.001f)
                return -(lastSafeRotation * Vector3.forward);

            return -unsafeAccum.normalized;
        }

        public void ForceCheckpoint(Vector3 position, Quaternion rotation)
        {
            if (groundLayer.value != 0)
            {
                if (TryValidateCheckpointPosition(position, out Vector3 validated, out _))
                {
                    position = validated;
                }
                else
                {
                    Debug.LogWarning($"[checkpoint] ForceCheckpoint recebeu position without ground suficiente: " +
                                     $"{position}. recording same thus by decision of the designer.");
                }
            }

            history.Clear();
            RecordCheckpoint(position, rotation);
        }

        public void ForceCheckpoint(Transform target)
        {
            if (target == null) return;
            ForceCheckpoint(target.position, target.rotation);
        }

        public void ClearCheckpoint()
        {
            hasValidCheckpoint = false;
            history.Clear();
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !hasValidCheckpoint) return;

            Gizmos.color = new Color(0.30f, 1f, 0.50f, 0.9f);
            Gizmos.DrawWireSphere(lastSafePosition, 0.4f);

            Gizmos.color = Color.yellow;
            Vector3 tip = lastSafePosition + lastSafeRotation * Vector3.forward * 1.2f;
            Gizmos.DrawLine(lastSafePosition, tip);
            Gizmos.DrawSphere(tip, 0.12f);

            if (groundLayer.value != 0 && validationRadius > 0f)
            {
                int samples = Mathf.Max(4, validationSampleCount);
                float rayLength = groundProbeHeight + groundProbeDistance;

                for (int i = 0; i < samples; i++)
                {
                    float angle = (360f / samples) * i;
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    Vector3 samplePos = lastSafePosition + dir * validationRadius;
                    Vector3 origin = samplePos + Vector3.up * groundProbeHeight;

                    bool hit = Physics.Raycast(origin, Vector3.down, rayLength,
                                               groundLayer, QueryTriggerInteraction.Ignore);

                    Gizmos.color = hit ? Color.green : Color.red;
                    Gizmos.DrawLine(origin, origin + Vector3.down * rayLength);
                    Gizmos.DrawWireSphere(samplePos, 0.05f);
                }
            }

            if (history.Count > 0)
            {
                Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
                foreach (var entry in history)
                    Gizmos.DrawWireSphere(entry.position, 0.2f);
            }
        }

        #endregion
    }
}
