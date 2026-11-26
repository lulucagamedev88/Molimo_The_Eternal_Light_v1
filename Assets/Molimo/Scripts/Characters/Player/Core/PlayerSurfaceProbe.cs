using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public enum SurfaceType { None, Ground, Roof }

    public class PlayerSurfaceProbe : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerBrain brain;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private Rigidbody rb;

        [Header("Surface Layers")]
        [Tooltip("layers consideradas ground.")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("layers consideradas roof/Roof.")]
        [SerializeField] private LayerMask roofLayer;

        [Header("Grounded Detection")]
        [Tooltip("height above of the player of where the cast starts.")]
        [SerializeField] private float groundedOriginHeight = 0.5f;

        [Tooltip("distance extra is down beyond of the height is achar surface.")]
        [SerializeField] private float groundedDownDistance = 1.0f;

        [Tooltip("radius of the SphereCast. 0 = Raycast puro.")]
        [SerializeField] private float groundedRadius = 0.25f;

        [Tooltip("slope maximum (graus) is considerar surface the walkable.")]
        [SerializeField] private float maxWalkableSlope = 60f;

        [Header("Edge Check — Ground")]
        [SerializeField] private float groundEdgeAheadDistance = 1.2f;
        [SerializeField] private float groundEdgeDownDistance = 2.0f;

        [Header("Edge Check — Roof")]
        [SerializeField] private float roofEdgeAheadDistance = 1.0f;
        [SerializeField] private float roofEdgeDownDistance = 1.5f;

        [Header("Fall Damage")]
        [Tooltip("height minimum of fall before start the apply damage.")]
        [SerializeField] private float minimumFallHeight = 3f;

        [Tooltip("height of fall that mata instantaneamente.")]
        [SerializeField] private float lethalFallHeight = 10f;

        [Tooltip("damage applied by meter adicional above of the minimum.")]
        [SerializeField] private float fallDamagePerMeter = 12f;

        [Tooltip("if the player falls from a roof to ground over this minimum distance, death instant.")]
        [SerializeField] private float roofToGroundLethalDistance = 6f;

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;
        [SerializeField] private bool drawGizmos = true;

        public SurfaceType CurrentSurface { get; private set; } = SurfaceType.None;
        public bool IsGrounded => CurrentSurface != SurfaceType.None;
        public bool IsOnRoof => CurrentSurface == SurfaceType.Roof;
        public bool IsOnGround => CurrentSurface == SurfaceType.Ground;

        public float AltitudeToGround { get; private set; } = Mathf.Infinity;

        public RaycastHit LastGroundHit { get; private set; }

        private static readonly RaycastHit[] hitBuffer = new RaycastHit[8];

        private float peakAirborneY;
        private bool wasGroundedLastFrame;

        private void Awake()
        {
            if (brain == null) brain = GetComponentInParent<PlayerBrain>();
            if (brain != null && rb == null) rb = brain.PlayerRigidbody;
            if (rb == null) rb = GetComponentInParent<Rigidbody>();
            if (health == null) health = GetComponentInParent<PlayerHealth>();
        }

        private void Update()
        {
            UpdateAltitude();
            UpdateCurrentSurface();
            UpdateFallTracking();
        }

        private void UpdateCurrentSurface()
        {
            float bestDist = float.MaxValue;

            if (DetectSurface(roofLayer, SurfaceType.Roof, ref bestDist))
            {
                CurrentSurface = SurfaceType.Roof;
                return;
            }

            if (DetectSurface(groundLayer, SurfaceType.Ground, ref bestDist))
            {
                CurrentSurface = SurfaceType.Ground;
                return;
            }

            CurrentSurface = SurfaceType.None;
        }

        private bool DetectSurface(LayerMask mask, SurfaceType type, ref float bestDist)
        {
            if (mask.value == 0) return false;

            Vector3 origin = transform.position + Vector3.up * groundedOriginHeight;
            float maxDist = groundedOriginHeight + groundedDownDistance;

            int count = Physics.SphereCastNonAlloc(
                origin,
                groundedRadius,
                Vector3.down,
                hitBuffer,
                maxDist,
                mask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                RaycastHit h = hitBuffer[i];
                if (h.collider == null) continue;

                if (brain != null && h.collider.transform.IsChildOf(brain.transform)) continue;

                float slope = Vector3.Angle(h.normal, Vector3.up);
                if (slope > maxWalkableSlope) continue;

                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    return true;
                }
            }

            return false;
        }

        private void UpdateAltitude()
        {
            if (groundLayer.value == 0)
            {
                AltitudeToGround = Mathf.Infinity;
                return;
            }

            Vector3 origin = transform.position + Vector3.up * 0.1f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, groundLayer, QueryTriggerInteraction.Ignore))
            {
                AltitudeToGround = hit.distance;
                LastGroundHit = hit;
            }
            else
            {
                AltitudeToGround = Mathf.Infinity;
                LastGroundHit = default;
            }
        }

        public bool HasSurfaceAhead(Vector3 direction, float overrideAhead = -1f)
        {
            LayerMask mask = ActiveSurfaceMask();
            if (mask.value == 0) return true;

            float ahead = overrideAhead > 0f
                ? overrideAhead
                : (CurrentSurface == SurfaceType.Roof ? roofEdgeAheadDistance : groundEdgeAheadDistance);

            float down = CurrentSurface == SurfaceType.Roof
                ? roofEdgeDownDistance
                : groundEdgeDownDistance;

            Vector3 flatDir = direction;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude < 0.0001f) flatDir = transform.forward;
            flatDir.Normalize();

            Vector3 origin = transform.position
                           + flatDir * ahead
                           + Vector3.up * groundedOriginHeight;

            float maxDown = groundedOriginHeight + down;

            bool hit;
            if (groundedRadius > 0f)
            {
                hit = Physics.SphereCast(origin, groundedRadius, Vector3.down, out _, maxDown, mask,
                                         QueryTriggerInteraction.Ignore);
            }
            else
            {
                hit = Physics.Raycast(origin, Vector3.down, maxDown, mask,
                                      QueryTriggerInteraction.Ignore);
            }

            if (debugLog && !hit)
                Debug.LogWarning($"[SurfaceProbe] without surface to the forward. surface={CurrentSurface} mask={mask.value}");

            return hit;
        }

        public LayerMask ActiveSurfaceMask()
        {
            return CurrentSurface == SurfaceType.Roof ? roofLayer : groundLayer;
        }

        public float MeasureDistanceToGround(Vector3 worldPoint)
        {
            if (groundLayer.value == 0) return Mathf.Infinity;

            if (Physics.Raycast(worldPoint + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 500f,
                                groundLayer, QueryTriggerInteraction.Ignore))
            {
                return hit.distance;
            }
            return Mathf.Infinity;
        }

        private void UpdateFallTracking()
        {
            float y = transform.position.y;

            if (IsGrounded)
            {
                if (!wasGroundedLastFrame)
                {

                    float fallDistance = peakAirborneY - y;

                    if (CurrentSurface == SurfaceType.Ground
                        && fallDistance >= roofToGroundLethalDistance)
                    {
                        if (debugLog)
                            Debug.LogWarning($"[SurfaceProbe] fall letal Roof→Ground ({fallDistance:F2}m).");
                        KillFromFall();
                    }
                    else if (fallDistance >= lethalFallHeight)
                    {
                        if (debugLog)
                            Debug.LogWarning($"[SurfaceProbe] fall letal ({fallDistance:F2}m).");
                        KillFromFall();
                    }
                    else if (fallDistance >= minimumFallHeight && health != null)
                    {
                        float dmg = (fallDistance - minimumFallHeight) * fallDamagePerMeter;
                        if (debugLog)
                            Debug.Log($"[SurfaceProbe] damage by fall: {dmg:F1} ({fallDistance:F2}m).");
                        health.TakeDamage(dmg);
                    }
                }

                peakAirborneY = y;
            }
            else
            {
                if (y > peakAirborneY) peakAirborneY = y;
            }

            wasGroundedLastFrame = IsGrounded;
        }

        private void KillFromFall()
        {
            if (brain != null) brain.Die();
            else if (health != null) health.TakeDamage(99999f);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.color = Color.green;
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            Gizmos.DrawLine(origin, origin + Vector3.down * 5f);

            Gizmos.color = Color.cyan;
            Vector3 sOrigin = transform.position + Vector3.up * groundedOriginHeight;
            Gizmos.DrawWireSphere(sOrigin, groundedRadius);

            DrawEdgeCheck(groundEdgeAheadDistance, groundEdgeDownDistance, Color.yellow);
            DrawEdgeCheck(roofEdgeAheadDistance, roofEdgeDownDistance, Color.magenta);
        }

        private void DrawEdgeCheck(float ahead, float down, Color color)
        {
            Gizmos.color = color;
            Vector3 the = transform.position
                      + transform.forward * ahead
                      + Vector3.up * groundedOriginHeight;
            Vector3 and = the + Vector3.down * (groundedOriginHeight + down);
            Gizmos.DrawLine(the, and);
            Gizmos.DrawWireSphere(the, groundedRadius);
            Gizmos.DrawWireSphere(and, groundedRadius);
        }
    }
}
