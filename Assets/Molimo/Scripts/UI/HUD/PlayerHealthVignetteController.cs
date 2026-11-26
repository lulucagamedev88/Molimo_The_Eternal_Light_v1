using UnityEngine;
using UnityEngine.UI;

namespace MolimoByLulucaGameDev
{
    [System.Serializable]

    public class VignetteLayer
    {
        [Header("Image")]
        public Image image;

        [Header("visibility")]
        [Range(0f, 1f)] public float startHealth = 0.4f;
        public bool useFadeIn = true;

        [Header("Buraco")]
        public AnimationCurve holeSizeByHealth = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Range(0.01f, 0.5f)] public float holeFeather = 0.08f;

        [Header("pulse of the Buraco")]
        public bool pulseHoleWithBeat = true;
        [Range(0f, 0.3f)] public float holePulseAmount = 0.05f;

        [Header("pulse (Alpha)")]
        [Range(0f, 1f)] public float minPulseAlpha = 0f;

        [HideInInspector] public Material runtimeMaterial;
    }

    [ExecuteAlways]

    public class PlayerHealthVignetteController : MonoBehaviour
    {
        [Header("reference of the player")]
        [SerializeField] private PlayerHealth health;

        [Header("Materiais (is visualization in the Editor)")]
        [SerializeField] private Material forceVeinsMaterial;
        [SerializeField] private Material forcePlainMaterial;

        [Header("vignette of Veias (M_VeinsVignette)")]
        [SerializeField] private VignetteLayer veinsLayer = new VignetteLayer { startHealth = 0.4f, useFadeIn = true, holeFeather = 0.08f, minPulseAlpha = 0f };

        [Header("vignette without Veias (M_Vignette)")]
        [SerializeField] private VignetteLayer plainLayer = new VignetteLayer { startHealth = 0.3f, useFadeIn = true, holeFeather = 0.10f, minPulseAlpha = 0f };

        [Header("effect of damage (Knockback)")]
        [SerializeField, Range(0f, 1f)] private float hitVignetteBoost = 0.5f;
        [SerializeField, Min(0.1f)] private float hitVignetteFadeSpeed = 1.5f;

        [Header("smoothing of healing (Fade Out)")]
        [Tooltip("how fast the vignette and the sound reagem to the healing. (smaller = fades more devagar).")]
        [SerializeField, Min(0.1f)] private float healFadeSpeed = 0.5f;

        [Header("pulse cardiac (shared)")]
        [SerializeField] private AnimationCurve bpmByHealth = new AnimationCurve(new Keyframe(0f, 30f), new Keyframe(0.05f, 180f), new Keyframe(0.2f, 140f), new Keyframe(0.5f, 90f), new Keyframe(1f, 65f));
        [SerializeField, Range(0f, 1f)] private float dubIntensity = 0.65f;

        [Header("sound of the pulse (Heartbeat)")]
        [SerializeField] private bool enablePulseSound = true;
        [SerializeField] private SoundData pulseSound;
        [SerializeField] private SoundData dubSound;
        [SerializeField] private AudioSource pulseAudioSource;

        private static readonly int HoleRadiusID = Shader.PropertyToID("_HoleRadius");
        private static readonly int FeatherID = Shader.PropertyToID("_Feather");
        private static readonly int HoleAspectID = Shader.PropertyToID("_HoleAspect");
        private static readonly int PulseIntensityID = Shader.PropertyToID("_PulseIntensity");

        private const float LUB_CYCLE = 0.15f;
        private const float DUB_CYCLE = 0.35f;

        private float healthRatio = 1f;
        private float smoothedHealthRatio = 1f;
        private float pulsePhase;
        private float previousCycle;
        private bool lubFiredThisCycle;
        private bool dubFiredThisCycle;
        private float currentHitBoost = 0f;

        private float heartbeatOriginalVolume = 1f;
        private bool heartbeatMuted;
        private bool heartbeatFading;
        private float heartbeatFadeTimer;
        private float heartbeatFadeDuration = 1f;

        private bool vignetteRestoring;
        private float vignetteRestoreTimer;
        private float vignetteRestoreDuration = 1f;
        private float vignetteRestoreStartRatio;

        private void Awake()
        {
            if (!Application.isPlaying) return;

            if (health == null) health = FindAnyObjectByType<PlayerHealth>();
            if (health == null)
            {
                Debug.LogError("[PHVC] Health not found!", this);
                enabled = false;
                return;
            }

            if (veinsLayer.image != null) veinsLayer.image.raycastTarget = false;
            if (plainLayer.image != null) plainLayer.image.raycastTarget = false;

            SetupAudioSource();

            healthRatio = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 1f;
            smoothedHealthRatio = healthRatio;

            health.OnHealthChanged += HandleHealthChanged;
            health.OnDeath += HandleDied;
            health.OnDamaged += HandleDamaged;

            SetImagesEnabled(false);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (heartbeatFading) TickHeartbeatFade();
            if (health == null) return;

            healthRatio = health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;

            if (vignetteRestoring)
                TickVignetteRestore();
            else if (healthRatio < smoothedHealthRatio)
                smoothedHealthRatio = healthRatio;
            else if (healthRatio > smoothedHealthRatio)
                smoothedHealthRatio = Mathf.MoveTowards(smoothedHealthRatio, healthRatio, healFadeSpeed * Time.deltaTime);

            if (currentHitBoost > 0f)
                currentHitBoost = Mathf.MoveTowards(currentHitBoost, 0f, hitVignetteFadeSpeed * Time.deltaTime);

            float bpm = Mathf.Max(1f, bpmByHealth.Evaluate(smoothedHealthRatio));
            pulsePhase += Time.deltaTime * (bpm / 60f);

            float beat = ComputeHeartbeat(pulsePhase);
            float visVeins = ComputeVisibility(smoothedHealthRatio, veinsLayer);
            float visPlain = ComputeVisibility(smoothedHealthRatio, plainLayer);

            bool showVeins = (visVeins > 0.01f) || (currentHitBoost > 0.01f);
            bool showPlain = (visPlain > 0.01f) || (currentHitBoost > 0.01f);

            if (veinsLayer.image != null) veinsLayer.image.enabled = showVeins;
            if (plainLayer.image != null) plainLayer.image.enabled = showPlain;

            ApplyLayer(veinsLayer, beat, visVeins, forceVeinsMaterial);
            ApplyLayer(plainLayer, beat, visPlain, forcePlainMaterial);

            bool isVignetteVisible = showVeins || showPlain;
            DetectAndPlayPulseSound(pulsePhase % 1f, isVignetteVisible);
        }

        private void ApplyLayer(VignetteLayer layer, float beat, float visibility, Material forceMat)
        {
            float finalVisibility = Mathf.Clamp01(visibility + currentHitBoost);

            float hole = layer.holeSizeByHealth.Evaluate(smoothedHealthRatio);
            if (layer.pulseHoleWithBeat) hole *= 1f + beat * layer.holePulseAmount;

            float pulseAlpha = Mathf.Lerp(1f, layer.minPulseAlpha, beat);
            float finalIntensity = pulseAlpha * finalVisibility;

            if (layer.image != null && layer.image.material != null)
            {
                layer.image.material.SetFloat(HoleRadiusID, Mathf.Clamp01(hole));
                layer.image.material.SetFloat(FeatherID, layer.holeFeather);
                layer.image.material.SetFloat(PulseIntensityID, finalIntensity);
                layer.image.SetMaterialDirty();
            }

            if (forceMat != null)
            {
                forceMat.SetFloat(HoleRadiusID, Mathf.Clamp01(hole));
                forceMat.SetFloat(FeatherID, layer.holeFeather);
                forceMat.SetFloat(PulseIntensityID, finalIntensity);
            }
        }

        private void HandleHealthChanged(float current, float max) => healthRatio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        private void HandleDamaged(float amount) => currentHitBoost = hitVignetteBoost;

        private void HandleDied()
        {
            pulsePhase = 0f;
            currentHitBoost = 0f;
            smoothedHealthRatio = 0f;
            vignetteRestoring = false;
        }

        private void DetectAndPlayPulseSound(float currentCycle, bool isVignetteVisible)
        {
            if (!enablePulseSound || !isVignetteVisible)
            {
                previousCycle = currentCycle;
                return;
            }

            if (heartbeatMuted)
            {
                previousCycle = currentCycle;
                return;
            }

            bool wrapped = currentCycle < previousCycle;
            if (wrapped) { lubFiredThisCycle = false; dubFiredThisCycle = false; }

            if (!lubFiredThisCycle && CrossedThreshold(previousCycle, currentCycle, LUB_CYCLE, wrapped))
            {
                PlayPulseSound(false); lubFiredThisCycle = true;
            }
            if (!dubFiredThisCycle && dubSound != null && CrossedThreshold(previousCycle, currentCycle, DUB_CYCLE, wrapped))
            {
                PlayPulseSound(true); dubFiredThisCycle = true;
            }
            previousCycle = currentCycle;
        }

        private static bool CrossedThreshold(float prev, float curr, float threshold, bool wrapped)
            => wrapped ? (threshold > prev || threshold <= curr) : (prev < threshold && threshold <= curr);

        public void PlayPulseSound(bool isDub = false)
        {
            SoundData sound = isDub ? dubSound : pulseSound;
            if (sound == null || sound.GetClip() == null || pulseAudioSource == null) return;
            pulseAudioSource.pitch = sound.GetPitch();
            pulseAudioSource.PlayOneShot(sound.GetClip(), sound.GetVolume());
        }

        private void SetupAudioSource()
        {
            if (pulseAudioSource == null) pulseAudioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            pulseAudioSource.playOnAwake = false;
            pulseAudioSource.loop = false;
            pulseAudioSource.spatialBlend = 0f;
            heartbeatOriginalVolume = pulseAudioSource.volume;
        }

        // ---------------- Heartbeat mute (death / game over flow) ----------------

        /// <summary>Blocks new heartbeat sounds and fades the source volume to 0 (unscaled time). Used when the countdown reaches 00.</summary>
        public void FadeOutAndMuteHeartbeat(float fadeDuration = 1f)
        {
            if (pulseAudioSource == null) return;

            heartbeatMuted = true;
            heartbeatFading = true;
            heartbeatFadeDuration = Mathf.Max(0.01f, fadeDuration);
            heartbeatFadeTimer = 0f;
        }

        /// <summary>Restores the heartbeat to its normal state (used on revive/reset).</summary>
        public void RestoreHeartbeat()
        {
            heartbeatMuted = false;
            heartbeatFading = false;
            if (pulseAudioSource != null) pulseAudioSource.volume = heartbeatOriginalVolume;
        }

        /// <summary>
        /// Fades the death vignette back to the player's current (alive) health smoothly
        /// using unscaled time, instead of waiting for the slow heal fade. Used on revive.
        /// A new death cancels this fade automatically (HandleDied).
        /// </summary>
        public void FadeToAliveHealth(float fadeDuration)
        {
            if (health != null && health.MaxHealth > 0f)
            {
                healthRatio = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
                vignetteRestoreStartRatio = Mathf.Min(smoothedHealthRatio, healthRatio);
            }
            else
            {
                healthRatio = 1f;
                vignetteRestoreStartRatio = Mathf.Min(smoothedHealthRatio, 1f);
            }

            vignetteRestoreDuration = Mathf.Max(0.01f, fadeDuration);
            vignetteRestoreTimer = 0f;
            vignetteRestoring = true;
        }

        /// <summary>
        /// Snaps the vignette back to the player's current (alive) health immediately,
        /// instead of waiting for the slow heal fade. Used on reset/disable.
        /// </summary>
        public void ResetVignetteToAlive()
        {
            vignetteRestoring = false;
            RestoreHeartbeat();
            currentHitBoost = 0f;

            if (health != null && health.MaxHealth > 0f)
            {
                healthRatio = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
                smoothedHealthRatio = healthRatio;
            }
            else
            {
                healthRatio = 1f;
                smoothedHealthRatio = 1f;
            }

            SetImagesEnabled(false);
        }

        private void TickHeartbeatFade()
        {
            heartbeatFadeTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(heartbeatFadeTimer / heartbeatFadeDuration);

            if (pulseAudioSource != null)
                pulseAudioSource.volume = Mathf.Lerp(heartbeatOriginalVolume, 0f, t);

            if (t >= 1f) heartbeatFading = false;
        }

        private void TickVignetteRestore()
        {
            vignetteRestoreTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(vignetteRestoreTimer / vignetteRestoreDuration);

            float target = health != null && health.MaxHealth > 0f
                ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth)
                : 1f;

            smoothedHealthRatio = Mathf.Lerp(vignetteRestoreStartRatio, target, t);
            healthRatio = target;

            if (t >= 1f) vignetteRestoring = false;
        }

        private float ComputeVisibility(float ratio, VignetteLayer layer)
        {
            if (ratio >= layer.startHealth) return 0f;
            if (!layer.useFadeIn) return 1f;

            float t = Mathf.InverseLerp(layer.startHealth, 0f, ratio);

            return Mathf.SmoothStep(0.2f, 1f, t);
        }

        private float ComputeHeartbeat(float phase)
        {
            float cycle = phase % 1f;
            float lub = Mathf.Exp(-Mathf.Pow((cycle - LUB_CYCLE) / 0.08f, 2f));
            float dub = dubIntensity * Mathf.Exp(-Mathf.Pow((cycle - DUB_CYCLE) / 0.08f, 2f));
            return Mathf.Clamp01(Mathf.Max(lub, dub));
        }

        private void SetImagesEnabled(bool value)
        {
            if (veinsLayer?.image != null) veinsLayer.image.enabled = value;
            if (plainLayer?.image != null) plainLayer.image.enabled = value;
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying) return;
            if (health != null)
            {
                health.OnHealthChanged -= HandleHealthChanged;
                health.OnDeath -= HandleDied;
                health.OnDamaged -= HandleDamaged;
            }
        }
    }
}
