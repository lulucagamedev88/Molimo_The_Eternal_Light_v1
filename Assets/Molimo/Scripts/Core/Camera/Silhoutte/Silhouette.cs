using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    public class Silhouette : MonoBehaviour
    {
        #region Raycast Settings

        [Header("Raycast Settings")]
        [SerializeField] private LayerMask environmentLayer;
        [SerializeField] private Transform raycastOrigin;

        [Header("Line A")]
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

        [Tooltip("Altura/grossura VERTICAL da caixa de deteção de cada linha (volume na direção do eixo Y do mundo). Aumenta o alcance vertical do raycast conforme o valor configurado.")]
        [SerializeField, Min(0.01f)]
        private float lineHeight = 0.5f;

        [Header("World Raycast Direction (world-locked)")]
        [Tooltip("Direção dos raycasts em ESPAÇO MUNDO. É totalmente independente da rotação do objeto — o objeto pode rodar livremente sem afetar a deteção.")]
        [SerializeField]
        private Vector3 worldRaycastDirection = Vector3.forward;

        private Vector3 cachedWorldDirection;

        [Header("Fill Material (auto-attach)")]
        [Tooltip("Material com o shader Molimo/Render/SilhouetteFill (ex.: M_SilhouetteOutline). Se atribuído, é aplicado aos renderers filhos que ainda não tenham o pass de fill — sem editar os materiais de arte. Em malhas multi-submesh, os submeshes sem material próprio recebem o fill; os restantes são preenchidos pelo RenderObjects override do PC_Renderer_Silhouette.")]
        [SerializeField]
        private Material fillMaterial;

        [Header("Ignored Renderers")]
        [Tooltip("Renderers que devem ser completamente ignorados pelo efeito de silhueta: não recebem o material de fill nem a opacidade.")]
        [SerializeField]
        private List<Renderer> ignoredRenderers = new List<Renderer>();

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

        // Visibilidade partilhada por todas as instâncias: a Camera_Silhouette é única
        // e deve ficar ativa enquanto QUALQUER silhueta (Player ou Enemy) estiver visível.
        private static int visibleCount;

        private Renderer[] objectRenderers;

        private HashSet<Renderer> ignoredRendererSet;

        private MaterialPropertyBlock propertyBlock;

        private float currentOpacity;
        private float lastAppliedOpacity = -1f;
        private bool registeredVisible;

        #endregion

        #region Public Properties

        public bool IsSilhouetteVisible => currentOpacity > 0.01f;

        public static bool AnySilhouetteVisible => visibleCount > 0;

        #endregion

        #region Initialization

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();

            BuildIgnoredSet();
            CollectRenderers();

            if (raycastOrigin == null)
                raycastOrigin = transform;

            if (fillMaterial != null)
                AppendFillMaterial();
        }

        private void BuildIgnoredSet()
        {
            ignoredRendererSet = new HashSet<Renderer>();

            if (ignoredRenderers == null)
                return;

            foreach (Renderer ignored in ignoredRenderers)
            {
                if (ignored != null)
                    ignoredRendererSet.Add(ignored);
            }
        }

        private void CollectRenderers()
        {
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);

            if (ignoredRendererSet.Count == 0)
            {
                objectRenderers = allRenderers;
                return;
            }

            List<Renderer> filtered = new List<Renderer>(allRenderers.Length);

            foreach (Renderer objectRenderer in allRenderers)
            {
                if (!ignoredRendererSet.Contains(objectRenderer))
                    filtered.Add(objectRenderer);
            }

            objectRenderers = filtered.ToArray();
        }

        // Garante que cada renderer filho consegue visualizar o fill: se nenhum dos
        // seus materiais atuais tiver a propriedade _FillOpacity (shader SilhouetteFill),
        // o material de fill é aplicado — sem editar os materiais de arte.
        //
        // Mapeamento do Unity (docs Unity 6): slot de material i -> submesh i, e slots
        // EXTRA (para além do nº de submeshes) acumulam-se apenas sobre o ÚLTIMO submesh.
        // Por isso o fill é atribuído por submesh:
        //  - mais submeshes que materiais: os slots em falta recebem o fill (cobertura 100%);
        //  - malha de 1 submesh: o slot extra anexado cobre a malha inteira;
        //  - malha multi-submesh já totalmente mapeada: anexar só cobriria o último submesh
        //    (fill parcial — o bug antigo). Nesse caso o fill é desenhado pelo RenderObjects
        //    override do PC_Renderer_Silhouette, que redesenha TODOS os submeshes com o
        //    material de fill (ZTest GreaterEqual) apenas na Camera_Silhouette.
        private void AppendFillMaterial()
        {
            foreach (Renderer objectRenderer in objectRenderers)
            {
                if (objectRenderer == null)
                    continue;

                Material[] materials = objectRenderer.sharedMaterials;

                bool hasFillPass = false;
                foreach (Material material in materials)
                {
                    if (material != null && material.HasProperty(FillOpacityID))
                    {
                        hasFillPass = true;
                        break;
                    }
                }

                if (hasFillPass)
                    continue;

                int subMeshCount = GetSubMeshCount(objectRenderer);

                if (subMeshCount > materials.Length)
                {
                    Material[] padded = new Material[subMeshCount];
                    materials.CopyTo(padded, 0);

                    for (int i = materials.Length; i < subMeshCount; i++)
                        padded[i] = fillMaterial;

                    objectRenderer.sharedMaterials = padded;
                }
                else if (subMeshCount == 1)
                {
                    Material[] appended = new Material[materials.Length + 1];
                    materials.CopyTo(appended, 0);
                    appended[materials.Length] = fillMaterial;
                    objectRenderer.sharedMaterials = appended;
                }
            }
        }

        private static int GetSubMeshCount(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : 1;

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();

            return meshFilter != null && meshFilter.sharedMesh != null
                ? meshFilter.sharedMesh.subMeshCount
                : 1;
        }

        private void OnDisable()
        {
            // Liberta o registo partilhado de visibilidade (evita contador preso entre runs).
            if (registeredVisible)
            {
                registeredVisible = false;
                visibleCount--;
            }

            currentOpacity = 0f;
            lastAppliedOpacity = -1f;
        }

        #endregion

        #region Raycast Detection

        private void Update()
        {
            if (raycastOrigin == null)
                raycastOrigin = transform;

            UpdateWorldDirection();
            CheckEnvironment();
            UpdateOpacity();
        }

        private void UpdateWorldDirection()
        {
            // Normalizada com segurança; nunca lê a rotação do objeto — apenas o valor
            // de inspetor em espaço-mundo. Default: (0, 0, 1).
            cachedWorldDirection = worldRaycastDirection.sqrMagnitude > 0.0001f
                ? worldRaycastDirection.normalized
                : Vector3.forward;
        }

        private void CheckEnvironment()
        {
            // Direção 100% vinculada ao mundo: nunca deriva da rotação do objeto.
            lineADetected = false;
            lineBDetected = false;
            lineCDetected = false;

            lineAHit = default;
            lineBHit = default;
            lineCHit = default;

            Vector3 origin = raycastOrigin.position;

            lineAEnd = origin +
                       cachedWorldDirection * lineADistance;

            lineBOrigin = origin +
                          Vector3.up * lineBHeightOffset;

            lineBEnd = lineBOrigin +
                       cachedWorldDirection * lineBDistance;

            lineCOrigin = origin +
                          Vector3.up * lineCHeightOffset;

            lineCEnd = lineCOrigin +
                       cachedWorldDirection * lineCDistance;

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
                lineHeight * 0.5f,
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
            if (objectRenderers == null || objectRenderers.Length == 0)
                return;

            bool silhouetteActive =
                lineADetected || lineBDetected || lineCDetected;

            float targetOpacity = silhouetteActive
                ? maxFillOpacity
                : 0f;

            currentOpacity = Mathf.MoveTowards(
                currentOpacity,
                targetOpacity,
                fadeSpeed * Time.unscaledDeltaTime
            );

            // Registo partilhado: a única Camera_Silhouette do cenário deve ficar
            // ativa enquanto qualquer silhueta (Player ou Enemy) estiver visível.
            bool visible = currentOpacity > 0.01f;
            if (visible != registeredVisible)
            {
                registeredVisible = visible;
                visibleCount += visible ? 1 : -1;
            }

            // Evita reescrever property blocks todos os frames quando nada mudou.
            if (Mathf.Approximately(currentOpacity, lastAppliedOpacity))
                return;

            lastAppliedOpacity = currentOpacity;

            foreach (Renderer objectRenderer in objectRenderers)
            {
                if (objectRenderer == null)
                    continue;

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

            Vector3 direction = worldRaycastDirection.sqrMagnitude > 0.0001f
                ? worldRaycastDirection.normalized
                : Vector3.forward;

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
                new Vector3(castWidth, lineHeight, lineADistance)
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
                new Vector3(castWidth, lineHeight, lineBDistance)
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
                new Vector3(castWidth, lineHeight, lineCDistance)
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
            // Mantém a direção mundial normalizada no inspetor (sem afetar a rotação do objeto).
            if (worldRaycastDirection.sqrMagnitude > 0.0001f)
                worldRaycastDirection = worldRaycastDirection.normalized;
            else
                worldRaycastDirection = Vector3.forward;

            if (lineADistance < 0.01f)
                lineADistance = 0.01f;

            if (lineBDistance < 0.01f)
                lineBDistance = 0.01f;

            if (lineCDistance < 0.01f)
                lineCDistance = 0.01f;

            if (castWidth < 0.01f)
                castWidth = 0.01f;

            if (lineHeight < 0.01f)
                lineHeight = 0.01f;
        }

        #endregion
    }
}
