using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [ExecuteInEditMode]
    [SelectionBase]
    [DisallowMultipleComponent]

    public class ForestGenerator : MonoBehaviour
    {

        public enum ToolMode { Point, Paint, Erase }

        public enum CategoryType { Everything, Trees, Vegetation, Rocks }

        [Header("Modo of Ferramenta")]
        [Tooltip("Point: selection/movement free.\nPaint: Pintar objects.\nErase: Apagar objects.")]
        public ToolMode activeToolMode = ToolMode.Point;

        [Header("Filtro of Pintura / Apagador")]
        [Tooltip("Escolha the category that deseja pintar or apagar.")]
        public CategoryType selectedCategory = CategoryType.Everything;

        [Header("settings of the Pincel (Brush)")]
        [Range(1f, 30f)] public float brushRadius = 5f;
        [Range(1, 20)] public int treesPerStroke = 5;
        [Tooltip("distance minimum between each object is prevent overlap.")]
        public float minDistanceBetweenTrees = 2f;

        [Header("configuration of Solo (Raycast)")]
        public LayerMask groundLayer = ~0;
        public float raycastStartHeight = 100f;
        public float raycastDistance = 200f;

        #region Masking System (NOVO)
        [Header("Masking of generation (texture P&B)")]
        [Tooltip("the texture in scale of cinza. blank = can gerar, Preto = not gera. requires UVs in the terrain.")]
        public Texture2D generationMask;
        [Tooltip("Tiling of the mask in the world. Ajuste is coincidir with the size of the terrain.")]
        public Vector2 maskScale = Vector2.one;
        [Tooltip("Offset of the mask in the world.")]
        public Vector2 maskOffset = Vector2.zero;
        [Range(0f, 1f)]
        [Tooltip("threshold is decidir the that is blank. above deste value is allowed.")]
        public float maskThreshold = 0.5f;
        #endregion

        [Header("randomization visual")]
        [Range(0f, 45f)] public float maxTiltAngle = 10f;
        [Range(0f, 1f)] public float normalAlignment = 0.5f;
        public bool uniformScale = true;
        public Vector3 minScale = new Vector3(0.8f, 0.8f, 0.8f);
        public Vector3 maxScale = new Vector3(1.4f, 1.4f, 1.4f);

        [Header("lists of Prefabs by category")]
        public GameObject[] treePrefabs;
        public GameObject[] vegetationPrefabs;
        public GameObject[] rockPrefabs;

        [HideInInspector] public List<GameObject> spawnedTrees = new List<GameObject>();
        [HideInInspector] public List<GameObject> spawnedVegetation = new List<GameObject>();
        [HideInInspector] public List<GameObject> spawnedRocks = new List<GameObject>();

        #region Brush Logic

        public void PaintAt(Vector3 centerWorldPos)
        {
            List<GameObject> availablePrefabs = GetAvailablePrefabs();

            if (availablePrefabs.Count == 0)
            {
                Debug.LogWarning("ForestGenerator: none prefab configured in the category selecionada!");
                return;
            }

            List<Vector3> occupied = GetCurrentSpawnedPositions();

            for (int i = 0; i < treesPerStroke; i++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * brushRadius;
                Vector3 candidatePos = centerWorldPos + new Vector3(randomCircle.x, 0f, randomCircle.y);

                TrySpawnAtPosition(candidatePos, occupied, availablePrefabs);
            }
        }

        private List<GameObject> GetAvailablePrefabs()
        {
            List<GameObject> list = new List<GameObject>();

            if (selectedCategory == CategoryType.Everything || selectedCategory == CategoryType.Trees)
            {
                if (treePrefabs != null) list.AddRange(treePrefabs);
            }

            if (selectedCategory == CategoryType.Everything || selectedCategory == CategoryType.Vegetation)
            {
                if (vegetationPrefabs != null) list.AddRange(vegetationPrefabs);
            }

            if (selectedCategory == CategoryType.Everything || selectedCategory == CategoryType.Rocks)
            {
                if (rockPrefabs != null) list.AddRange(rockPrefabs);
            }

            list.RemoveAll(item => item == null);
            return list;
        }

        public void EraseAt(Vector3 centerWorldPos)
        {
            CleanNulls();
            float sqrRadius = brushRadius * brushRadius;

            if (selectedCategory == CategoryType.Everything || selectedCategory == CategoryType.Trees)
                EraseFromList(spawnedTrees, centerWorldPos, sqrRadius);

            if (selectedCategory == CategoryType.Everything || selectedCategory == CategoryType.Vegetation)
                EraseFromList(spawnedVegetation, centerWorldPos, sqrRadius);

            if (selectedCategory == CategoryType.Everything || selectedCategory == CategoryType.Rocks)
                EraseFromList(spawnedRocks, centerWorldPos, sqrRadius);
        }

        private void EraseFromList(List<GameObject> list, Vector3 centerWorldPos, float sqrRadius)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                GameObject obj = list[i];
                if (obj != null)
                {
                    if ((obj.transform.position - centerWorldPos).sqrMagnitude <= sqrRadius)
                    {
#if UNITY_EDITOR
                        UnityEditor.Undo.DestroyObjectImmediate(obj);
#else
                        DestroyImmediate(obj);
#endif
                        list.RemoveAt(i);
                    }
                }
            }
        }

        private bool TrySpawnAtPosition(Vector3 worldPos, List<Vector3> occupied, List<GameObject> availablePrefabs)
        {

            float minSqrDist = minDistanceBetweenTrees * minDistanceBetweenTrees;
            foreach (var occ in occupied)
            {
                if ((worldPos - occ).sqrMagnitude < minSqrDist) return false;
            }

            Vector3 rayStart = worldPos + Vector3.up * raycastStartHeight;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
            {

                if (!IsSpawnAllowedByMask(hit)) return false;

                if (SpawnSingleObjectAt(hit.point, hit.normal, availablePrefabs))
                {
                    occupied.Add(hit.point);
                    return true;
                }
            }
            return false;
        }

        #region Masking System Implementation (NOVO)
        private bool IsSpawnAllowedByMask(RaycastHit hit)
        {

            if (generationMask == null) return true;

            Vector2 uv = hit.textureCoord;

            Vector2 finalUV = new Vector2(
                (uv.x * maskScale.x) + maskOffset.x,
                (uv.y * maskScale.y) + maskOffset.y
            );

            float sampleX = Mathf.Repeat(finalUV.x, 1f);
            float sampleY = Mathf.Repeat(finalUV.y, 1f);

            try
            {

                Color maskColor = generationMask.GetPixelBilinear(sampleX, sampleY);

                float intensity = (maskColor.r + maskColor.g + maskColor.b) / 3f;

                return intensity >= maskThreshold;
            }
            catch (System.Exception and)
            {

                Debug.LogError("ForestGenerator: error when the read the mask. Verifique if the texture has 'Read/Write Enabled' checked. " + and.Message);
                return false;
            }
        }
        #endregion

        private bool SpawnSingleObjectAt(Vector3 worldPosition, Vector3 surfaceNormal, List<GameObject> availablePrefabs)
        {
            GameObject prefab = availablePrefabs[Random.Range(0, availablePrefabs.Count)];
            if (prefab == null) return false;

            CategoryType itemCategory = GetCategoryForPrefab(prefab);
            Transform container = GetOrCreateContainerForCategory(itemCategory);

#if UNITY_EDITOR
            GameObject obj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#else
            GameObject obj = Instantiate(prefab);
#endif
            obj.transform.position = worldPosition;

            ApplyRandomTransform(obj.transform, surfaceNormal);

            obj.transform.SetParent(container);

#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(obj, "Pintar Elemento");
#endif

            GetListForCategory(itemCategory).Add(obj);
            return true;
        }

        private CategoryType GetCategoryForPrefab(GameObject prefab)
        {
            if (System.Array.Exists(treePrefabs, p => p == prefab)) return CategoryType.Trees;
            if (System.Array.Exists(vegetationPrefabs, p => p == prefab)) return CategoryType.Vegetation;
            if (System.Array.Exists(rockPrefabs, p => p == prefab)) return CategoryType.Rocks;

            return CategoryType.Trees;
        }

        private List<GameObject> GetListForCategory(CategoryType category)
        {
            switch (category)
            {
                case CategoryType.Trees: return spawnedTrees;
                case CategoryType.Vegetation: return spawnedVegetation;
                case CategoryType.Rocks: return spawnedRocks;
                default: return spawnedTrees;
            }
        }

        private void ApplyRandomTransform(Transform targetTransform, Vector3 surfaceNormal)
        {
            Vector3 upDir = Vector3.Lerp(Vector3.up, surfaceNormal, normalAlignment);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, upDir);
            rotation *= Quaternion.Euler(Random.Range(-maxTiltAngle, maxTiltAngle), Random.Range(0f, 360f), Random.Range(-maxTiltAngle, maxTiltAngle));
            targetTransform.rotation = rotation;

            if (uniformScale)
            {
                float s = Random.Range(minScale.x, maxScale.x);
                targetTransform.localScale = new Vector3(s, s, s);
            }
            else
            {
                targetTransform.localScale = new Vector3(
                    Random.Range(minScale.x, maxScale.x),
                    Random.Range(minScale.y, maxScale.y),
                    Random.Range(minScale.z, maxScale.z)
                );
            }
        }

        public void UpdateForestTransforms()
        {
            CleanNulls();

            List<GameObject> targetObjects = new List<GameObject>();
            if (selectedCategory == CategoryType.Everything || selectedCategory == CategoryType.Trees) targetObjects.AddRange(spawnedTrees);
            if (selectedCategory == CategoryType.Everything || selectedCategory == CategoryType.Vegetation) targetObjects.AddRange(spawnedVegetation);
            if (selectedCategory == CategoryType.Everything || selectedCategory == CategoryType.Rocks) targetObjects.AddRange(spawnedRocks);

            foreach (var obj in targetObjects)
            {
                if (obj == null) continue;

#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(obj.transform, "update Transform of the forest");
#endif

                Vector3 surfaceNormal = Vector3.up;
                Vector3 rayStart = obj.transform.position + Vector3.up * raycastStartHeight;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
                {
                    surfaceNormal = hit.normal;
                }

                ApplyRandomTransform(obj.transform, surfaceNormal);
            }
        }

        #region Container Helpers

        public Transform GetMainContainer()
        {
            Transform mainContainer = transform.Find("Generated_Forest");
            if (mainContainer == null)
            {
                mainContainer = new GameObject("Generated_Forest").transform;
                mainContainer.SetParent(transform);
                mainContainer.localPosition = Vector3.zero;
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(mainContainer.gameObject, "create Container forest");
#endif
            }
            return mainContainer;
        }

        public Transform GetOrCreateContainerForCategory(CategoryType category)
        {
            Transform mainContainer = GetMainContainer();
            string subContainerName = "Generated_" + category.ToString();

            Transform subContainer = mainContainer.Find(subContainerName);
            if (subContainer == null)
            {
                subContainer = new GameObject(subContainerName).transform;
                subContainer.SetParent(mainContainer);
                subContainer.localPosition = Vector3.zero;
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(subContainer.gameObject, "create Container category");
#endif
            }
            return subContainer;
        }

        #endregion

        private List<Vector3> GetCurrentSpawnedPositions()
        {
            CleanNulls();
            List<Vector3> positions = new List<Vector3>();

            foreach (var obj in spawnedTrees) if (obj != null) positions.Add(obj.transform.position);
            foreach (var obj in spawnedVegetation) if (obj != null) positions.Add(obj.transform.position);
            foreach (var obj in spawnedRocks) if (obj != null) positions.Add(obj.transform.position);

            return positions;
        }

        private void CleanNulls()
        {
            if (spawnedTrees != null) spawnedTrees.RemoveAll(item => item == null);
            if (spawnedVegetation != null) spawnedVegetation.RemoveAll(item => item == null);
            if (spawnedRocks != null) spawnedRocks.RemoveAll(item => item == null);
        }

        public void ClearForest()
        {
            CleanNulls();

            ClearList(spawnedTrees);
            ClearList(spawnedVegetation);
            ClearList(spawnedRocks);

            Transform mainContainer = transform.Find("Generated_Forest");
            if (mainContainer != null)
            {
#if UNITY_EDITOR
                UnityEditor.Undo.DestroyObjectImmediate(mainContainer.gameObject);
#else
                DestroyImmediate(mainContainer.gameObject);
#endif
            }
        }

        private void ClearList(List<GameObject> list)
        {
            if (list == null) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] != null)
                {
#if UNITY_EDITOR
                    UnityEditor.Undo.DestroyObjectImmediate(list[i]);
#else
                    DestroyImmediate(list[i]);
#endif
                }
            }
            list.Clear();
        }

        #endregion
    }
}
