using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TMPro;

namespace MolimoByLulucaGameDev
{
    [ExecuteAlways]
    [SelectionBase]
    [DisallowMultipleComponent]

    public class DayNightLightingController : MonoBehaviour
    {

        public enum KemetPeriod { Khepri, Ra, Atum, Duat }

        public enum KemetClockStyle { SunDeities, TwelveHours, HybridFormat }

        #region Inspector

        [Header("light Direcional static (without Sombras)")]
        [Tooltip("light direcional single of the jogo. its rotation remains static and not gera sombras.")]
        [SerializeField] private Light globalToonLight;

        [Header("references of UI")]
        [SerializeField] private TMP_Text timeDisplayUI;

        [Header("Controle of speed Triplo (TSM)")]
        [SerializeField, Min(0.1f)] private float millisecondSpeed = 1.0f;
        [SerializeField, Min(10f)] private float msPerGameMinute = 1000f;
        [SerializeField, Min(0.01f)] private float hourScaleSpeed = 1.0f;

        [Header("time of the Universo")]
        [SerializeField, Range(0f, 24f)] private float currentTimeOfDay = 8.0f;
        [SerializeField] private bool isTimePaused = false;

        [Header("schedules of the Fases Kemet (hours)")]
        [SerializeField, Range(0f, 24f)] private float khepriStartHour = 5.0f;
        [SerializeField, Range(0f, 24f)] private float raStartHour = 11.0f;
        [SerializeField, Range(0f, 24f)] private float atumStartHour = 17.0f;
        [SerializeField, Range(0f, 24f)] private float duatStartHour = 20.0f;

        [Header("lighting and colors Globais")]
        [SerializeField] private Gradient lightColorGradient;
        [SerializeField] private Gradient ambientColorGradient;
        [SerializeField] private AnimationCurve lightIntensityCurve;

        [Header("Estilo of time Kemet (UI)")]
        [SerializeField] private KemetClockStyle kemetStyle = KemetClockStyle.SunDeities;

        [Header("Eventos of the Deuses / Fases")]
        public UnityEvent OnKhepriStarted;
        public UnityEvent OnRaStarted;
        public UnityEvent OnAtumStarted;
        public UnityEvent OnDuatStarted;

        [Header("diagnostics")]
        [SerializeField] private bool logDiagnostics = true;

        #endregion

        #region state

        private bool _isInDarkZone = false;
        private Color _weatherAmbientTint = Color.white;
        private float _weatherAmbientStrength = 0f;
        private Color _ambientFlashColor = Color.white;
        private float _ambientFlashStrength = 0f;

        public Light GlobalToonLight => globalToonLight;
        public float CurrentTimeOfDay => currentTimeOfDay;

        public KemetPeriod CurrentPeriod { get; private set; }
        public bool IsInDuat => CurrentPeriod == KemetPeriod.Duat;

        public bool IsInDarkZone => _isInDarkZone;

        private float _internalMillisecondAccumulator = 0f;
        private int _lastMinuteCalculated = -1;

        private Color _lastAppliedAmbient = new Color(-1f, -1f, -1f, -1f);
        private AmbientMode _lastAppliedAmbientMode;

        #endregion

        #region Ciclo of health

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
            }
        }

        private void Awake()
        {
            ValidateAndFindGlobalLight();
            EvaluatePeriod(true);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ValidateAndFindGlobalLight();
            ApplyCelestialLighting();
            UpdateUI();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (globalToonLight != null && globalToonLight.gameObject.scene == scene)
            {
                globalToonLight = null;
                if (logDiagnostics)
                    Debug.Log($"[{nameof(DayNightLightingController)}] scene '{scene.name}' descarregada — reference of light clears.", this);
            }
        }

        private void Reset()
        {
            ValidateAndFindGlobalLight();
            SetupTopDownPresets();
        }

        private void Update()
        {
            AdvanceTime();
            ApplyCelestialLighting();
            UpdateUI();
        }

        #endregion

        #region API Public

        public void SetDarkZone(bool isDark)
        {
            _isInDarkZone = isDark;
            ApplyCelestialLighting();
        }

        public void RegisterGlobalLight(Light directionalLight)
        {
            if (directionalLight != null && directionalLight.type == LightType.Directional)
                globalToonLight = directionalLight;
            ApplyCelestialLighting();
        }

        public void SetWeatherModifiers(Color ambientTint, float ambientStrength)
        {
            _weatherAmbientTint = ambientTint;
            _weatherAmbientStrength = Mathf.Clamp01(ambientStrength);
        }

        public void SetAmbientFlash(Color flashColor, float flashStrength)
        {
            _ambientFlashColor = flashColor;
            _ambientFlashStrength = Mathf.Clamp01(flashStrength);
        }

        public void SetTime(float hour)
        {
            currentTimeOfDay = Mathf.Clamp(hour, 0f, 24f);
            EvaluatePeriod(false);
            ApplyCelestialLighting();
            UpdateUI();
        }

        #endregion

        #region Ciclo of time

        private void AdvanceTime()
        {
            if (!Application.isPlaying || isTimePaused) return;

            float deltaMS = (Time.deltaTime * 1000f) * millisecondSpeed;
            _internalMillisecondAccumulator += deltaMS;

            if (_internalMillisecondAccumulator >= msPerGameMinute)
            {
                float minutesToAdvance = _internalMillisecondAccumulator / msPerGameMinute;
                _internalMillisecondAccumulator %= msPerGameMinute;

                float hoursToAdvance = (minutesToAdvance / 60f) * hourScaleSpeed;
                currentTimeOfDay = (currentTimeOfDay + hoursToAdvance) % 24f;
            }

            EvaluatePeriod(false);
        }

        private void EvaluatePeriod(bool forceSilent)
        {
            KemetPeriod detected;

            if (currentTimeOfDay >= khepriStartHour && currentTimeOfDay < raStartHour)
                detected = KemetPeriod.Khepri;
            else if (currentTimeOfDay >= raStartHour && currentTimeOfDay < atumStartHour)
                detected = KemetPeriod.Ra;
            else if (currentTimeOfDay >= atumStartHour && currentTimeOfDay < duatStartHour)
                detected = KemetPeriod.Atum;
            else
                detected = KemetPeriod.Duat;

            if (detected != CurrentPeriod || forceSilent)
            {
                CurrentPeriod = detected;
                if (!forceSilent)
                {
                    switch (CurrentPeriod)
                    {
                        case KemetPeriod.Khepri: OnKhepriStarted?.Invoke(); break;
                        case KemetPeriod.Ra: OnRaStarted?.Invoke(); break;
                        case KemetPeriod.Atum: OnAtumStarted?.Invoke(); break;
                        case KemetPeriod.Duat: OnDuatStarted?.Invoke(); break;
                    }
                }
            }
        }

        #endregion

        #region lighting Simplificada

        private void ApplyCelestialLighting()
        {
            float normalizedTime = currentTimeOfDay / 24f;

            if (globalToonLight != null)
            {

                globalToonLight.enabled = !_isInDarkZone;

                if (!_isInDarkZone)
                {
                    if (lightColorGradient != null)
                        globalToonLight.color = lightColorGradient.Evaluate(normalizedTime);

                    if (lightIntensityCurve != null)
                        globalToonLight.intensity = Mathf.Max(0f, lightIntensityCurve.Evaluate(normalizedTime));

                    globalToonLight.shadows = LightShadows.None;
                }
            }

            ApplyAmbient(normalizedTime);
        }

        private void ApplyAmbient(float normalizedTime)
        {
            if (ambientColorGradient == null) return;

            Color baseAmbient = ambientColorGradient.Evaluate(normalizedTime);

            if (_isInDarkZone)
            {
                baseAmbient = new Color(0.05f, 0.05f, 0.08f, 1f);
            }

            float baseLum = Mathf.Max(baseAmbient.grayscale, 0.001f);
            float tintLum = Mathf.Max(_weatherAmbientTint.grayscale, 0.001f);
            Color tintedAmbient = _weatherAmbientTint * (baseLum / tintLum);

            Color finalAmbient = Color.Lerp(baseAmbient, tintedAmbient, _weatherAmbientStrength);

            if (_ambientFlashStrength > 0.0001f)
                finalAmbient = Color.Lerp(finalAmbient, _ambientFlashColor, _ambientFlashStrength);

            if (finalAmbient == _lastAppliedAmbient && _lastAppliedAmbientMode == AmbientMode.Flat)
                return;

            _lastAppliedAmbient = finalAmbient;
            _lastAppliedAmbientMode = AmbientMode.Flat;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = finalAmbient;
        }

        #endregion

        #region Interface Kemet and Presets (Mantidos)

        private void UpdateUI()
        {
            if (timeDisplayUI == null) return;
            int rawHours = Mathf.FloorToInt(currentTimeOfDay);
            int minutes = Mathf.FloorToInt((currentTimeOfDay - rawHours) * 60f);

            if (minutes != _lastMinuteCalculated || !Application.isPlaying)
            {
                _lastMinuteCalculated = minutes;
                switch (kemetStyle)
                {
                    case KemetClockStyle.SunDeities:
                        string deityTag = CurrentPeriod.ToString().ToUpper();
                        timeDisplayUI.text = $"{rawHours:00}:{minutes:00} {deityTag}";
                        break;
                    case KemetClockStyle.TwelveHours:
                        timeDisplayUI.text = GetEgyptianHourString(rawHours);
                        break;
                    case KemetClockStyle.HybridFormat:
                        string hs = GetEgyptianHourString(rawHours);
                        timeDisplayUI.text = $"{rawHours:00}:{minutes:00} ({hs})";
                        break;
                }
            }
        }

        private string GetEgyptianHourString(int hour)
        {
            bool isSunHour = hour >= 5 && hour < 17;
            int egyptianHour = isSunHour ? (hour - 4) : (hour >= 17 ? hour - 16 : hour + 8);
            string realm = isSunHour ? "Sol" : "Duat";
            return $"{egyptianHour}ª hour of the {realm}";
        }

        private void ValidateAndFindGlobalLight()
        {
            if (globalToonLight != null) return;
            Light[] allLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            Light fallbackDirectional = null;
            foreach (var l in allLights)
            {
                if (l == null || l.type != LightType.Directional) continue;
                if (l.CompareTag("Sun")) { globalToonLight = l; return; }
                if (fallbackDirectional == null) fallbackDirectional = l;
            }
            if (fallbackDirectional != null) globalToonLight = fallbackDirectional;
        }

        private void SetupTopDownPresets()
        {
            lightIntensityCurve = new AnimationCurve(
                new Keyframe(0.00f, 0.25f), new Keyframe(0.24f, 0.35f), new Keyframe(0.33f, 1.00f),
                new Keyframe(0.50f, 1.15f), new Keyframe(0.70f, 0.95f), new Keyframe(0.80f, 0.35f), new Keyframe(1.00f, 0.25f)
            );
            lightColorGradient = new Gradient
            {
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.25f, 0.35f, 0.60f), 0.00f),
                    new GradientColorKey(new Color(1.00f, 0.60f, 0.35f), 0.25f),
                    new GradientColorKey(new Color(1.00f, 0.98f, 0.90f), 0.35f),
                    new GradientColorKey(new Color(1.00f, 1.00f, 0.95f), 0.50f),
                    new GradientColorKey(new Color(1.00f, 0.55f, 0.25f), 0.75f),
                    new GradientColorKey(new Color(0.25f, 0.35f, 0.60f), 0.88f)
                },
                alphaKeys = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            };
            ambientColorGradient = new Gradient
            {
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.05f, 0.08f, 0.15f), 0.00f),
                    new GradientColorKey(new Color(0.30f, 0.20f, 0.20f), 0.25f),
                    new GradientColorKey(new Color(0.48f, 0.52f, 0.55f), 0.33f),
                    new GradientColorKey(new Color(0.55f, 0.55f, 0.55f), 0.50f),
                    new GradientColorKey(new Color(0.40f, 0.25f, 0.15f), 0.75f),
                    new GradientColorKey(new Color(0.05f, 0.08f, 0.15f), 0.85f)
                },
                alphaKeys = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            };
        }

        #endregion
    }
}
