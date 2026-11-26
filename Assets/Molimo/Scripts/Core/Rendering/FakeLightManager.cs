using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Simple Toon/Fake Light Manager")]

    public class FakeLightManager : MonoBehaviour
    {

        public const int MAX_LIGHTS = 20;

        [Header("Materiais target")]
        [Tooltip("all the materiais with the shader Simple Toon/URP/STTerrain that must receive the luzes")]
        public Material[] terrainMats = new Material[0];

        [Header("that tipos of Light accept")]
        public bool includePointLights = true;
        public bool includeSpotLights = false;

        [Header("Mapeamento Light -> Shader")]
        [Tooltip("Multiplicador applied when the Light.range for get the radius no shader")]
        [Min(0f)] public float rangeMultiplier = 1f;

        [Tooltip("Multiplicador applied when the Light.intensity")]
        [Min(0f)] public float intensityMultiplier = 1f;

        [Tooltip("Suavidade of the borda of the circle (0=duríssima, 1=muito suave)")]
        [Range(0.001f, 1f)] public float softness = 0.4f;

        [Header("Culling (the N more next)")]
        [Tooltip("point of reference for ordenar. if empty, uses the câmara primary.")]
        public Transform referencePoint;

        [Tooltip("Intervalo in seconds for re-escanear the scene à search of luzes.")]
        [Min(0.05f)] public float rescanInterval = 0.5f;

        static readonly Vector4[] _pos = new Vector4[MAX_LIGHTS];
        static readonly Vector4[] _col = new Vector4[MAX_LIGHTS];
        static readonly Vector4[] _data = new Vector4[MAX_LIGHTS];

        static readonly int ID_Pos = Shader.PropertyToID("_FakeLightPosArray");
        static readonly int ID_Col = Shader.PropertyToID("_FakeLightColorArray");
        static readonly int ID_Data = Shader.PropertyToID("_FakeLightDataArray");

        readonly List<Light> _lights = new List<Light>(64);
        readonly List<(float dist, Light light)> _sorted = new List<(float, Light)>(64);

        float _nextScan;

        static int CompareByDistance((float dist, Light light) the, (float dist, Light light) b)
            => the.dist.CompareTo(b.dist);

        void LateUpdate()
        {
            if (terrainMats == null || terrainMats.Length == 0) return;

            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + rescanInterval;
                ScanLights();
            }

            Vector3 refPos = referencePoint != null
                ? referencePoint.position
                : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

            _sorted.Clear();
            for (int i = 0; i < _lights.Count; i++)
            {
                Light l = _lights[i];
                if (l == null) continue;
                float d = (l.transform.position - refPos).sqrMagnitude;
                _sorted.Add((d, l));
            }
            _sorted.Sort(CompareByDistance);

            int n = Mathf.Min(_sorted.Count, MAX_LIGHTS);

            for (int i = 0; i < MAX_LIGHTS; i++)
            {
                if (i < n)
                {
                    Light l = _sorted[i].light;

                    Vector3 p = l.transform.position;
                    Color c = l.color;
                    float inten = l.intensity * intensityMultiplier;
                    float radius = l.range * rangeMultiplier;

                    _pos[i] = new Vector4(p.x, p.y, p.z, 1f);
                    _col[i] = new Vector4(c.r * inten, c.g * inten, c.b * inten, 1f);
                    _data[i] = new Vector4(radius, softness, 0f, 0f);
                }
                else
                {

                    _pos[i] = new Vector4(0f, 0f, 0f, 0f);
                    _col[i] = Vector4.zero;
                    _data[i] = Vector4.zero;
                }
            }

            for (int m = 0; m < terrainMats.Length; m++)
            {
                Material mat = terrainMats[m];
                if (mat == null) continue;

                mat.SetVectorArray(ID_Pos, _pos);
                mat.SetVectorArray(ID_Col, _col);
                mat.SetVectorArray(ID_Data, _data);
            }
        }

        void ScanLights()
        {
            _lights.Clear();

#if UNITY_2023_1_OR_NEWER
            var all = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
#else
            var all = FindObjectsOfType<Light>();
#endif

            foreach (var l in all)
            {
                if (l == null || !l.isActiveAndEnabled || !l.enabled) continue;

                if (l.intensity <= 0.01f) continue;

                if (l.type == LightType.Point && includePointLights)
                    _lights.Add(l);
                else if (l.type == LightType.Spot && includeSpotLights)
                    _lights.Add(l);
            }
        }

        void OnValidate()
        {
            if (rescanInterval < 0.05f) rescanInterval = 0.05f;
            _nextScan = 0f;
        }
    }
}
