using UnityEngine;

namespace MolimoByRodrigoLuluca
{

    public class Silhouette : MonoBehaviour
    {
        #region Raycast Settings

        [Header("Raycast Settings")]
        [SerializeField] private LayerMask environmentLayer;
        [SerializeField] private Transform raycastOrigin;

        [Header("Line the")]
        [SerializeField, Min(0.01f)]
        private float lineADistance = 2f;

        [Header("Line B")]
        [SerializeField]
        private float lineBHeightOffset = 2f;

        [SerializeField, Min(0.01f)]
        private float lineBDistance = 10f;

        [Header("Line C")]
        [SerializeField]
        private float lineCHeightOffset = 2f;

        [SerializeField, Min(0.01f)]
        private float lineCDistance = 10f;

        [Header("Volume")]
        [SerializeField, Min(0.01f)]
        private float castWidth = 0.5f;

        [SerializeField, Min(0.01f)]
        private float castHeight = 0.5f;

        private Vector3 fixedRaycastDirection;

        private Vector3 lineAEnd;
        private Vector3 lineBOrigin;
        private Vector3 lineBEnd;
        private Vector3 lineCOrigin;
        private Vector3 lineCEnd;

        private bool lineADetected;
        private bool lineBDetected;
        private bool lineCDetected;

        private RaycastHit lineAHit;
        private RaycastHit lineBHit;
        private RaycastHit lineCHit;

        #endregion

        #region Opacity Settings

        [Header("Opacity Settings")]
        [SerializeField, Range(0f, 1f)]
        private float maxFillOpacity = 1f;

        [SerializeField] private float fadeSpeed = 5f;

        #endregion

        #region Private Variables

        private static readonly int FillOpacityID =
            Shader.PropertyToID("_FillOpacity");

        private Renderer[] objectRenderers;

        private MaterialPropertyBlock propertyBlock;

        private float currentOpacity;

        #endregion

        #region Public Properties

        public bool IsSilhouetteVisible => currentOpacity > 0.01f;

        #endregion

        #region Initialization

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();

            objectRenderers = GetComponentsInChildren<Renderer>(true);

            if (raycastOrigin == null)
                raycastOrigin = transform;

            fixedRaycastDirection =
                raycastOrigin.forward.normalized;
        }

        #endregion

        #region Raycast Detection

        private void Update()
        {
            CheckEnvironment();
            UpdateOpacity();
        }

        private void CheckEnvironment()
        {
            lineADetected = false;
            lineBDetected = false;
            lineCDetected = false;

            lineAHit = default;
            lineBHit = default;
            lineCHit = default;

            Vector3 origin = raycastOrigin.position;

            lineAEnd = origin +
                       fixedRaycastDirection * lineADistance;

            lineBOrigin = origin +
                          Vector3.up * lineBHeightOffset;

            lineBEnd = lineBOrigin +
                       fixedRaycastDirection * lineBDistance;

            lineCOrigin = origin +
                          Vector3.up * lineCHeightOffset;

            lineCEnd = lineCOrigin +
                       fixedRaycastDirection * lineCDistance;

            lineADetected = DetectEnvironment(
                origin,
                lineAEnd,
                out lineAHit
            );

            lineBDetected = DetectEnvironment(
                lineBOrigin,
                lineBEnd,
                out lineBHit
            );

            lineCDetected = DetectEnvironment(
                lineCOrigin,
                lineCEnd,
                out lineCHit
            );
        }

        private bool DetectEnvironment(
            Vector3 start,
            Vector3 end,
            out RaycastHit validHit)
        {
            validHit = default;

            Vector3 direction = end - start;
            float distance = direction.magnitude;

            if (distance <= 0.001f)
                return false;

            direction /= distance;

            Vector3 halfExtents = new Vector3(
                castWidth * 0.5f,
                castHeight * 0.5f,
                0f
            );

            RaycastHit[] hits = Physics.BoxCastAll(
                start,
                halfExtents,
                direction,
                Quaternion.LookRotation(direction),
                distance,
                environmentLayer,
                QueryTriggerInteraction.Ignore
            );

            float closestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                if (hit.collider.transform == transform ||
                    hit.collider.transform.IsChildOf(transform))
                    continue;

                int layer = hit.collider.gameObject.layer;

                if ((environmentLayer.value & (1 << layer)) == 0)
                    continue;

                if (hit.distance < 0f || hit.distance > distance)
                    continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    validHit = hit;
                }
            }

            return closestDistance != float.MaxValue;
        }

        #endregion

        #region Opacity Control

        private void UpdateOpacity()
        {
            bool silhouetteActive =
                lineADetected || lineBDetected || lineCDetected;

            float targetOpacity = silhouetteActive
                ? maxFillOpacity
                : 0f;

            currentOpacity = Mathf.MoveTowards(
                currentOpacity,
                targetOpacity,
                fadeSpeed * Time.deltaTime
            );

            foreach (Renderer objectRenderer in objectRenderers)
            {
                objectRenderer.GetPropertyBlock(propertyBlock);

                propertyBlock.SetFloat(
                    FillOpacityID,
                    currentOpacity
                );

                objectRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Transform originTransform = raycastOrigin != null
                ? raycastOrigin
                : transform;

            Vector3 direction = Application.isPlaying
                ? fixedRaycastDirection
                : originTransform.forward.normalized;

            Vector3 origin = originTransform.position;

            Vector3 gizmoAEnd = origin +
                direction * lineADistance;

            Vector3 gizmoBOrigin = origin +
                Vector3.up * lineBHeightOffset;

            Vector3 gizmoBEnd = gizmoBOrigin +
                direction * lineBDistance;

            Vector3 gizmoCOrigin = origin +
                Vector3.up * lineCHeightOffset;

            Vector3 gizmoCEnd = gizmoCOrigin +
                direction * lineCDistance;

            Quaternion rotA = Quaternion.LookRotation(direction);

            Vector3 boxSize = new Vector3(
                castWidth,
                castHeight,
                0.05f
            );

            Gizmos.color = lineADetected
                ? new Color(0, 1, 0, 0.3f)
                : new Color(1, 0, 0, 0.3f);

            Gizmos.matrix = Matrix4x4.TRS(
                (origin + gizmoAEnd) * 0.5f,
                rotA,
                Vector3.one
            );

            Gizmos.DrawCube(
                Vector3.zero,
                new Vector3(castWidth, castHeight, lineADistance)
            );

            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = lineADetected ? Color.green : Color.red;
            Gizmos.DrawWireSphere(gizmoAEnd, 0.04f);

            if (lineADetected)
                Gizmos.DrawSphere(lineAHit.point, 0.06f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin, gizmoBOrigin);
            Gizmos.DrawWireSphere(gizmoBOrigin, 0.04f);

            Gizmos.color = lineBDetected
                ? new Color(0, 1, 0, 0.3f)
                : new Color(1, 0, 0, 0.3f);

            Gizmos.matrix = Matrix4x4.TRS(
                (gizmoBOrigin + gizmoBEnd) * 0.5f,
                rotA,
                Vector3.one
            );

            Gizmos.DrawCube(
                Vector3.zero,
                new Vector3(castWidth, castHeight, lineBDistance)
            );

            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = lineBDetected ? Color.green : Color.red;
            Gizmos.DrawWireSphere(gizmoBEnd, 0.04f);

            if (lineBDetected)
                Gizmos.DrawSphere(lineBHit.point, 0.06f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin, gizmoCOrigin);
            Gizmos.DrawWireSphere(gizmoCOrigin, 0.04f);

            Gizmos.color = lineCDetected
                ? new Color(0, 1, 0, 0.3f)
                : new Color(1, 0, 0, 0.3f);

            Gizmos.matrix = Matrix4x4.TRS(
                (gizmoCOrigin + gizmoCEnd) * 0.5f,
                rotA,
                Vector3.one
            );

            Gizmos.DrawCube(
                Vector3.zero,
                new Vector3(castWidth, castHeight, lineCDistance)
            );

            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = lineCDetected ? Color.green : Color.red;
            Gizmos.DrawWireSphere(gizmoCEnd, 0.04f);

            if (lineCDetected)
                Gizmos.DrawSphere(lineCHit.point, 0.06f);
        }

        #endregion

        #region Validation

        private void OnValidate()
        {
            if (lineADistance < 0.01f)
                lineADistance = 0.01f;

            if (lineBDistance < 0.01f)
                lineBDistance = 0.01f;

            if (lineCDistance < 0.01f)
                lineCDistance = 0.01f;

            if (castWidth < 0.01f)
                castWidth = 0.01f;

            if (castHeight < 0.01f)
                castHeight = 0.01f;
        }

        #endregion
    }
}
