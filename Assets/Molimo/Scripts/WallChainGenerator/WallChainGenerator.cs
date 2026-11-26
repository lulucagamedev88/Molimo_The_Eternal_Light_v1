using UnityEngine;
using System.Collections.Generic;

namespace MolimoByLulucaGameDev
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]

    public class WallChainGenerator : MonoBehaviour
    {
        [Header("settings of the wall")]
        [Tooltip("height of the wall gerada.")]
        [Min(0.01f)]
        [SerializeField] private float wallHeight = 3f;

        [Tooltip("Extens�o extra in each corner shared is prevent gaps between segments.")]
        [Min(0f)]
        [SerializeField] private float cornerExtension = 0.1f;

        [Tooltip("if ativado, liga the last point when the first, closing the circuit closed (area closed).")]
        [SerializeField] private bool isLoop = false;

        [Header("visualization in the Editor (Gizmos)")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0f, 0.8f);
        [SerializeField] private bool showGizmos = true;

        [HideInInspector]
        [SerializeField] private List<Transform> points = new List<Transform>();

        public static readonly Color DefaultGizmoColor = new Color(1f, 0.5f, 0f, 0.8f);

        public IReadOnlyList<Transform> Points => points;
        public Color GizmoColor => gizmoColor;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;

        private Material solidBlackMaterial;
        private Vector3[] cachedPositions;
        private int cachedCount = -1;
        private bool dirtyFlag = true;

        private void OnEnable()
        {
            EnsureComponents();
            dirtyFlag = true;
            RegenerateIfNeeded();
        }

        private void OnValidate() => dirtyFlag = true;

        private void Update()
        {
            if (!Application.isPlaying) RegenerateIfNeeded();
        }

        private void EnsureComponents()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            EnsureSolidBlackMaterial();
        }

        private void EnsureSolidBlackMaterial()
        {
            if (meshRenderer == null) return;

            if (solidBlackMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    solidBlackMaterial = new Material(shader);
                    solidBlackMaterial.name = "SolidBlack_Unlit_Plane";
                    solidBlackMaterial.hideFlags = HideFlags.DontSave;

                    solidBlackMaterial.color = Color.black;
                    if (solidBlackMaterial.HasProperty("_BaseColor"))
                        solidBlackMaterial.SetColor("_BaseColor", Color.black);
                    if (solidBlackMaterial.HasProperty("_Metallic"))
                        solidBlackMaterial.SetFloat("_Metallic", 0f);
                    if (solidBlackMaterial.HasProperty("_Smoothness"))
                        solidBlackMaterial.SetFloat("_Smoothness", 0f);
                    else if (solidBlackMaterial.HasProperty("_Glossiness"))
                        solidBlackMaterial.SetFloat("_Glossiness", 0f);

                    if (solidBlackMaterial.HasProperty("_Cull"))
                        solidBlackMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
                }
            }

            if (solidBlackMaterial != null && meshRenderer.sharedMaterial != solidBlackMaterial)
                meshRenderer.sharedMaterial = solidBlackMaterial;
        }

        public void MarkDirty() => dirtyFlag = true;

        public void ResetGizmoColor()
        {
            gizmoColor = DefaultGizmoColor;
            MarkDirty();
        }

        private void RegenerateIfNeeded()
        {
            if (points == null) return;

            if (points.Count != cachedCount) dirtyFlag = true;
            else if (cachedPositions != null)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    if (points[i] == null || points[i].position != cachedPositions[i])
                    {
                        dirtyFlag = true;
                        break;
                    }
                }
            }

            if (!dirtyFlag) return;
            dirtyFlag = false;
            GenerateWall();
        }

        public void AddPoint()
        {
            GameObject pointObj = new GameObject($"Point_{points.Count}");
            pointObj.transform.SetParent(transform, worldPositionStays: true);

            if (points.Count > 0 && points[points.Count - 1] != null)
                pointObj.transform.position = points[points.Count - 1].position + transform.right * 2f;
            else
                pointObj.transform.position = transform.position;

            points.Add(pointObj.transform);

#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(pointObj, "Add Wall Point");
#endif
            dirtyFlag = true;
            RegenerateIfNeeded();
        }

        public void RemoveLastPoint()
        {
            if (points.Count == 0) return;

            Transform lastPoint = points[points.Count - 1];
            points.RemoveAt(points.Count - 1);

            if (lastPoint != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.DestroyObjectImmediate(lastPoint.gameObject);
                else Destroy(lastPoint.gameObject);
#else
                Destroy(lastPoint.gameObject);
#endif
            }

            dirtyFlag = true;
            RegenerateIfNeeded();
        }

        public void ClearAllPoints()
        {
            foreach (var p in points)
            {
                if (p == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.DestroyObjectImmediate(p.gameObject);
                else Destroy(p.gameObject);
#else
                Destroy(p.gameObject);
#endif
            }
            points.Clear();
            dirtyFlag = true;
            RegenerateIfNeeded();
        }

        public void GenerateWall()
        {
            EnsureComponents();
            points.RemoveAll(item => item == null);

            bool invalidConfiguration =
                points.Count < 2 ||
                (isLoop && points.Count < 3) ||
                wallHeight <= 0f;

            if (invalidConfiguration) { ClearMesh(); return; }

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            int segmentCount = isLoop ? points.Count : points.Count - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                Transform pA = points[i];
                Transform pB = points[(i + 1) % points.Count];
                if (pA == null || pB == null) continue;

                Vector3 posA = transform.InverseTransformPoint(pA.position);
                Vector3 posB = transform.InverseTransformPoint(pB.position);
                posA.y = 0f;
                posB.y = 0f;

                Vector3 dir = posB - posA;
                float segmentLength = dir.magnitude;
                if (segmentLength < 1e-6f) continue;
                dir /= segmentLength;

                bool startShared = isLoop || i > 0;
                bool endShared = isLoop || i < segmentCount - 1;
                float halfLen = segmentLength * 0.5f;
                float ext = Mathf.Min(cornerExtension, halfLen * 0.9f);

                Vector3 posA2 = posA + (startShared ? -dir * ext : Vector3.zero);
                Vector3 posB2 = posB + (endShared ? dir * ext : Vector3.zero);
                Vector3 topOffset = Vector3.up * wallHeight;
                float uLength = (posB2 - posA2).magnitude;

                int baseIndex = vertices.Count;

                vertices.Add(posA2);
                vertices.Add(posB2);
                vertices.Add(posA2 + topOffset);
                vertices.Add(posB2 + topOffset);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(uLength, 0f));
                uvs.Add(new Vector2(0f, wallHeight));
                uvs.Add(new Vector2(uLength, wallHeight));

                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }

            if (vertices.Count == 0) { ClearMesh(); return; }

            Mesh wallMesh = new Mesh { name = "ProceduralWallPlaneMesh" };
            wallMesh.SetVertices(vertices);
            wallMesh.SetUVs(0, uvs);
            wallMesh.SetTriangles(triangles, 0);
            wallMesh.RecalculateNormals();
            wallMesh.RecalculateBounds();

            ReplaceMesh(wallMesh);

            cachedCount = points.Count;
            cachedPositions = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
                cachedPositions[i] = points[i] != null ? points[i].position : Vector3.zero;
        }

        private void ReplaceMesh(Mesh newMesh)
        {
            Mesh oldMesh = meshFilter.sharedMesh;
            meshFilter.sharedMesh = newMesh;
            meshCollider.sharedMesh = newMesh;
            if (oldMesh != null && oldMesh != newMesh) DestroyMesh(oldMesh);
        }

        private void ClearMesh()
        {
            Mesh oldMesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (meshFilter != null) meshFilter.sharedMesh = null;
            if (meshCollider != null) meshCollider.sharedMesh = null;
            if (oldMesh != null) DestroyMesh(oldMesh);

            cachedCount = points != null ? points.Count : -1;
            cachedPositions = null;
        }

        private void DestroyMesh(Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos || points == null || points.Count < 2) return;

            Color previous = Gizmos.color;
            try
            {
                Gizmos.color = gizmoColor;
                int count = isLoop ? points.Count : points.Count - 1;
                for (int i = 0; i < count; i++)
                {
                    Transform pA = points[i];
                    Transform pB = points[(i + 1) % points.Count];
                    if (pA != null && pB != null)
                    {
                        Gizmos.DrawLine(pA.position, pB.position);
                        Gizmos.DrawWireSphere(pA.position, 0.3f);
                    }
                }
                if (!isLoop && points[points.Count - 1] != null)
                    Gizmos.DrawWireSphere(points[points.Count - 1].position, 0.3f);
            }
            finally { Gizmos.color = previous; }
        }
    }
}
