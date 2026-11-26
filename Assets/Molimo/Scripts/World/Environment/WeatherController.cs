using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace MolimoByLulucaGameDev
{

    public enum WeatherState { Desligado, cold, QuenteSeco, rainy }

    [System.Serializable]

    public struct ParticleDisableEntry
    {
        [Tooltip("ParticleSystem the silenciar during the rain.")]
        public ParticleSystem particle;
        [Tooltip("if enabled, does SetActive(false) in the GameObject. if disabled, only Stop().")]
        public bool disableGameObject;
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]

    public class WeatherController : MonoBehaviour
    {
        [Header("state of the Clima (Interruptor primary)")]
        [SerializeField] private bool weatherEnabled = true;
        [SerializeField] private WeatherState currentWeather = WeatherState.QuenteSeco;

        [Header("reference — Controlador day/night")]
        [SerializeField] private DayNightLightingController dayNightController;

        [Header("rain (ParticleSystem)")]
        [SerializeField] private ParticleSystem[] rainParticles;
        [SerializeField] private float rainHeavyRate = 700f;

        [SerializeField] private ParticleSystem[] splashParticles;
        [SerializeField] private float splashHeavyRate = 2000f;

        [Header("rain — Curva of Intensidade by hour")]
        [SerializeField]
        private AnimationCurve rainIntensityCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(6f, 0.5f), new Keyframe(14f, 1.0f),
            new Keyframe(18f, 0.6f), new Keyframe(24f, 0.3f)
        );

        [Header("rain — Vento (reaction when the movement of the camera)")]
        [SerializeField] private Transform cameraReference;
        [SerializeField] private bool enableCameraWind = true;
        [SerializeField, Range(0f, 5f)] private float windSensitivity = 1.0f;
        [SerializeField, Range(0f, 30f)] private float maxWind = 6f;
        [SerializeField, Range(0f, 20f)] private float windSmoothing = 5f;
        [SerializeField, Range(0f, 5f)] private float windDeadZone = 0.05f;
        [SerializeField, Min(0.1f)] private float rainFallSpeedReference = 10f;
        [SerializeField, Range(0f, 80f)] private float maxTiltDegrees = 35f;

        [Header("thunder (Flash in the AmbientLight)")]
        [SerializeField] private bool enableThunder = true;
        [SerializeField] private Color lightningColor = new Color(0.85f, 0.92f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float flashStrength = 0.9f;
        [SerializeField, Range(0.05f, 1.5f)] private float flashDuration = 0.30f;
        [SerializeField] private Vector2 strikeInterval = new Vector2(6f, 18f);
        [SerializeField, Range(0f, 1f)] private float doubleFlashChance = 0.4f;
        [SerializeField, Range(0.05f, 1f)] private float doubleFlashDelay = 0.18f;

        [Header("ParticleSystems is disable in the rain")]
        [SerializeField] private ParticleDisableEntry[] particlesToDisableOnRain;

        [Header("Tint global — cold")]
        [SerializeField] private Color coldAmbientTint = new Color(0.55f, 0.72f, 1.00f, 1f);
        [SerializeField, Range(0f, 1f)] private float coldAmbientStrength = 0.85f;

        [Header("Tint global — hot/dry")]
        [SerializeField] private Color hotAmbientTint = new Color(1.00f, 0.90f, 0.70f, 1f);
        [SerializeField, Range(0f, 1f)] private float hotAmbientStrength = 0.35f;

        [Header("Tint global — rainy")]
        [SerializeField] private Color rainyAmbientTint = new Color(0.50f, 0.56f, 0.66f, 1f);
        [SerializeField, Range(0f, 1f)] private float rainyAmbientStrength = 0.90f;

        [Header("Eventos")]
        public UnityEvent OnWeatherChanged;
        public UnityEvent OnRainStart;
        public UnityEvent OnRainStop;
        public UnityEvent OnThunderStrike;

        private bool _rainActive;
        private bool[] _psInitialStates;

        private float _lastCameraX;
        private float _cameraVelocityX;
        private float _currentWind;
        private bool _cameraInitialized;

        private float _thunderTimer;
        private float _flashTimer;
        private bool _doubleFlashPending;
        private float _doubleFlashTimer;

        private float _calculatedRainRate;

        public WeatherState CurrentWeather => currentWeather;
        public bool WeatherEnabled => weatherEnabled;
        public bool IsRaining => _rainActive;
        public float CurrentWind => _currentWind;
        public float CurrentRainRate => _rainActive ? _calculatedRainRate : 0f;

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
            }
            InitWeatherController();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
            }

            if (dayNightController != null)
                dayNightController.SetAmbientFlash(lightningColor, 0f);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InitWeatherController();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            bool dirty = false;

            if (cameraReference != null && cameraReference.gameObject.scene == scene)
            {
                cameraReference = null;
                _cameraInitialized = false;
                dirty = true;
            }

            if (dayNightController != null && dayNightController.gameObject.scene == scene)
            {
                dayNightController = null;
                dirty = true;
            }

            if (!dirty) return;

            if (cameraReference == null && Camera.main != null)
                cameraReference = Camera.main.transform;

            if (dayNightController == null)
                dayNightController = FindAnyObjectByType<DayNightLightingController>();

            CacheParticleDisableStates();
        }

        private void InitWeatherController()
        {
            if (dayNightController == null)
                dayNightController = FindAnyObjectByType<DayNightLightingController>();

            if (cameraReference == null && Camera.main != null)
                cameraReference = Camera.main.transform;

            CacheParticleDisableStates();
            ApplyWeatherColors();
            InitCameraTracking();

            _thunderTimer = Random.Range(strikeInterval.x, strikeInterval.y);
        }

        private void Update()
        {
            ApplyWeatherColors();
            UpdateParticlesToDisable();
            UpdateCameraWind(Time.deltaTime);
            UpdateThunder(Time.deltaTime);

            if (Application.isPlaying)
                UpdateRain();
        }

        private void UpdateRain()
        {
            bool wantRain = weatherEnabled && currentWeather == WeatherState.rainy;
            float currentTime = dayNightController != null ? dayNightController.CurrentTimeOfDay : 12f;
            float curveMultiplier = Mathf.Clamp01(rainIntensityCurve.Evaluate(currentTime));

            _calculatedRainRate = rainHeavyRate * curveMultiplier;
            float currentSplashRate = splashHeavyRate * curveMultiplier;

            if (rainParticles != null)
            {
                for (int i = 0; i < rainParticles.Length; i++)
                {
                    var ps = rainParticles[i];
                    if (ps == null) continue;
                    var emission = ps.emission;
                    emission.enabled = true;
                    emission.rateOverTime = wantRain ? _calculatedRainRate : 0f;
                    if (wantRain) { if (!ps.isPlaying) ps.Play(); }
                    else { if (ps.isPlaying) ps.Stop(); }
                }
            }

            if (splashParticles != null)
            {
                for (int i = 0; i < splashParticles.Length; i++)
                {
                    var ps = splashParticles[i];
                    if (ps == null) continue;
                    var emission = ps.emission;
                    emission.enabled = true;
                    emission.rateOverTime = wantRain ? currentSplashRate : 0f;
                    if (wantRain) { if (!ps.isPlaying) ps.Play(); }
                    else { if (ps.isPlaying) ps.Stop(); }
                }
            }

            if (wantRain != _rainActive)
            {
                _rainActive = wantRain;
                if (wantRain) OnRainStart?.Invoke();
                else OnRainStop?.Invoke();
            }
        }

        private void UpdateThunder(float dt)
        {
            bool active = Application.isPlaying && enableThunder && weatherEnabled && currentWeather == WeatherState.rainy;

            if (!active)
            {
                _flashTimer = 0f;
                _doubleFlashPending = false;
                if (dayNightController != null)
                    dayNightController.SetAmbientFlash(lightningColor, 0f);
                return;
            }

            if (_doubleFlashPending)
            {
                _doubleFlashTimer -= dt;
                if (_doubleFlashTimer <= 0f)
                {
                    _doubleFlashPending = false;
                    _flashTimer = flashDuration;
                }
            }
            else if (_flashTimer <= 0f)
            {
                _thunderTimer -= dt;
                if (_thunderTimer <= 0f)
                {
                    _thunderTimer = Random.Range(strikeInterval.x, strikeInterval.y);
                    _flashTimer = flashDuration;
                    OnThunderStrike?.Invoke();

                    if (Random.value < doubleFlashChance)
                    {
                        _doubleFlashPending = true;
                        _doubleFlashTimer = flashDuration + doubleFlashDelay;
                    }
                }
            }

            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                float t = 1f - Mathf.Clamp01(_flashTimer / flashDuration);
                float envelope = Mathf.Sin(t * Mathf.PI);

                if (dayNightController != null)
                    dayNightController.SetAmbientFlash(lightningColor, envelope * flashStrength);
            }
            else if (dayNightController != null)
            {
                dayNightController.SetAmbientFlash(lightningColor, 0f);
            }
        }

        private void CacheParticleDisableStates()
        {
            if (particlesToDisableOnRain == null)
            {
                _psInitialStates = null;
                return;
            }

            _psInitialStates = new bool[particlesToDisableOnRain.Length];

            for (int i = 0; i < particlesToDisableOnRain.Length; i++)
            {
                var ps = particlesToDisableOnRain[i].particle;
                if (ps == null) { _psInitialStates[i] = false; continue; }
                _psInitialStates[i] = ps.gameObject.activeSelf && ps.isPlaying;
            }
        }

        private void UpdateParticlesToDisable()
        {
            if (particlesToDisableOnRain == null) return;

            bool rainy = weatherEnabled && currentWeather == WeatherState.rainy;

            for (int i = 0; i < particlesToDisableOnRain.Length; i++)
            {
                var entry = particlesToDisableOnRain[i];
                var ps = entry.particle;
                if (ps == null) continue;

                bool wasActive = _psInitialStates != null && i < _psInitialStates.Length ? _psInitialStates[i] : true;

                if (rainy)
                {
                    if (entry.disableGameObject) { if (ps.gameObject.activeSelf) ps.gameObject.SetActive(false); }
                    else { if (ps.isPlaying) ps.Stop(); }
                }
                else
                {
                    if (entry.disableGameObject) { if (ps.gameObject.activeSelf != wasActive) ps.gameObject.SetActive(wasActive); }
                    else { if (wasActive && !ps.isPlaying) ps.Play(); }
                }
            }
        }

        private void InitCameraTracking()
        {
            if (cameraReference == null) return;
            _lastCameraX = cameraReference.position.x;
            _cameraVelocityX = 0f;
            _currentWind = 0f;
            _cameraInitialized = true;
        }

        private void UpdateCameraWind(float dt)
        {
            if (!enableCameraWind || cameraReference == null || dt <= 0f)
            {
                _cameraVelocityX = 0f;
                _currentWind = Mathf.MoveTowards(_currentWind, 0f, windSmoothing * dt);
                ApplyWindToParticles(_currentWind);
                return;
            }

            if (!_cameraInitialized) InitCameraTracking();

            float currentX = cameraReference.position.x;
            _cameraVelocityX = (currentX - _lastCameraX) / dt;
            _lastCameraX = currentX;

            if (Mathf.Abs(_cameraVelocityX) < windDeadZone) _cameraVelocityX = 0f;

            float targetWind = Mathf.Clamp(-_cameraVelocityX * windSensitivity, -maxWind, maxWind);
            _currentWind = Mathf.Lerp(_currentWind, targetWind, 1f - Mathf.Exp(-windSmoothing * dt));

            ApplyWindToParticles(_currentWind);
        }

        private void ApplyWindToParticles(float wind)
        {
            if (rainParticles == null) return;
            float fallSpeed = Mathf.Max(rainFallSpeedReference, 0.001f);
            float angleRad = Mathf.Atan2(wind, fallSpeed);
            float angleDeg = Mathf.Clamp(angleRad * Mathf.Rad2Deg, -maxTiltDegrees, maxTiltDegrees);
            angleRad = angleDeg * Mathf.Deg2Rad;

            for (int i = 0; i < rainParticles.Length; i++)
            {
                var ps = rainParticles[i];
                if (ps == null) continue;
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = wind;
                var main = ps.main;
                if (main.startRotation3D) main.startRotation3D = false;
                main.startRotation = new ParticleSystem.MinMaxCurve(angleRad);
            }
        }

        private void ApplyWeatherColors()
        {
            if (dayNightController == null) return;
            WeatherState effective = weatherEnabled ? currentWeather : WeatherState.Desligado;

            Color ambientTint = Color.white;
            float ambientStr = 0f;

            switch (effective)
            {
                case WeatherState.cold: ambientTint = coldAmbientTint; ambientStr = coldAmbientStrength; break;
                case WeatherState.QuenteSeco: ambientTint = hotAmbientTint; ambientStr = hotAmbientStrength; break;
                case WeatherState.rainy: ambientTint = rainyAmbientTint; ambientStr = rainyAmbientStrength; break;
                default: break;
            }

            dayNightController.SetWeatherModifiers(ambientTint, ambientStr);

            if (effective != _lastWeather)
            {
                _lastWeather = effective;
                OnWeatherChanged?.Invoke();
            }
        }

        private WeatherState _lastWeather = (WeatherState)(-1);

        public void ToggleWeather() => SetWeatherEnabled(!weatherEnabled);

        public void SetWeatherEnabled(bool v) { weatherEnabled = v; }

        public void SetWeatherByIndex(int idx) => SetWeather((WeatherState)idx);

        public void SetWeather(WeatherState w) { currentWeather = w; }

        public void SetColdWeather() => SetWeather(WeatherState.cold);

        public void SetHotDryWeather() => SetWeather(WeatherState.QuenteSeco);

        public void SetRainyWeather() => SetWeather(WeatherState.rainy);

        public void SetWeatherOff() => SetWeather(WeatherState.Desligado);

        public void SetCameraReference(Transform cam) => cameraReference = cam;

        public void TriggerThunderStrike()
        {
            _flashTimer = flashDuration;
            OnThunderStrike?.Invoke();
        }
    }
}
