using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MolimoByLulucaGameDev
{

    [DisallowMultipleComponent]
    public class PlayerHaptics : MonoBehaviour
    {
        [System.Serializable]
        public struct HapticStep
        {
            [Range(0f, 1f)] public float lowFrequency;
            [Range(0f, 1f)] public float highFrequency;
            [Min(0.01f)] public float duration;
        }

        [Header("Global Settings")]
        [SerializeField] private bool hapticsEnabled = true;

        [Tooltip("Escala global aplicada a todas as intensidades (acessibilidade / depuração).")]
        [SerializeField, Range(0f, 1f)] private float intensityScale = 1f;

        [Header("Presets — Golpes")]
        [SerializeField] private HapticStep[] hitLight = new HapticStep[]
        {
            new HapticStep { lowFrequency = 0.30f, highFrequency = 0.50f, duration = 0.08f }
        };

        [SerializeField] private HapticStep[] hitHeavy = new HapticStep[]
        {
            new HapticStep { lowFrequency = 0.60f, highFrequency = 0.50f, duration = 0.15f }
        };

        [Header("Presets — Defesa")]
        [SerializeField] private HapticStep[] blockStart = new HapticStep[]
        {
            new HapticStep { lowFrequency = 0.10f, highFrequency = 0.25f, duration = 0.05f }
        };

        [SerializeField] private HapticStep[] blockImpact = new HapticStep[]
        {
            new HapticStep { lowFrequency = 0.50f, highFrequency = 0.60f, duration = 0.12f }
        };

        [Header("Presets — Dano / Estados")]
        [SerializeField] private HapticStep[] hurtPulse = new HapticStep[]
        {
            new HapticStep { lowFrequency = 0.70f, highFrequency = 0.40f, duration = 0.07f },
            new HapticStep { lowFrequency = 0.45f, highFrequency = 0.30f, duration = 0.07f },
            new HapticStep { lowFrequency = 0.25f, highFrequency = 0.20f, duration = 0.12f }
        };

        [SerializeField] private HapticStep[] dashAttackLanding = new HapticStep[]
        {
            new HapticStep { lowFrequency = 0.80f, highFrequency = 0.40f, duration = 0.20f }
        };

        [SerializeField] private HapticStep[] deathPulse = new HapticStep[]
        {
            new HapticStep { lowFrequency = 0.90f, highFrequency = 0.20f, duration = 0.25f },
            new HapticStep { lowFrequency = 0.60f, highFrequency = 0.10f, duration = 0.25f },
            new HapticStep { lowFrequency = 0.30f, highFrequency = 0.05f, duration = 0.50f }
        };

        [Header("Dash Attack (rampa contínua)")]
        [SerializeField, Range(0f, 1f)] private float dashRampPeakLow = 0.40f;
        [SerializeField, Range(0f, 1f)] private float dashRampPeakHigh = 0.15f;

        private PlayerHealth health;
        private Coroutine _pulseRoutine;
        private Coroutine _continuousRoutine;
        private float _pulseLow;
        private float _pulseHigh;
        private float _continuousLow;
        private float _continuousHigh;
        private float _appliedLow = -1f;
        private float _appliedHigh = -1f;

        public bool HapticsEnabled
        {
            get => hapticsEnabled;
            set
            {
                hapticsEnabled = value;
                if (!hapticsEnabled) StopAllHaptics();
            }
        }

        private void Awake()
        {
            if (health == null)
                health = GetComponentInParent<PlayerHealth>();

            if (health == null)
                health = GetComponentInChildren<PlayerHealth>();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnDamaged += HandleHealthDamaged;
                health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDamaged -= HandleHealthDamaged;
                health.OnDeath -= HandleDeath;
            }

            StopAllHaptics();
        }

        private void Update()
        {
            float low = Mathf.Max(_continuousLow, _pulseLow);
            float high = Mathf.Max(_continuousHigh, _pulseHigh);

            if (!hapticsEnabled || Time.timeScale <= 0f)
            {
                low = 0f;
                high = 0f;
            }

            ApplyMotors(low, high);
        }

        #region Public Triggers

        public void PlayHitLight() => PlayPulse(hitLight);

        public void PlayHitHeavy() => PlayPulse(hitHeavy);

        public void PlayBlockStart() => PlayPulse(blockStart);

        public void PlayBlockImpact() => PlayPulse(blockImpact);

        public void PlayHurtPulse() => PlayPulse(hurtPulse);

        public void PlayDashAttackLanding() => PlayPulse(dashAttackLanding);

        public void PlayDeath() => PlayPulse(deathPulse);

        public void PlayDashAttack(float duration)
        {
            if (!hapticsEnabled || duration <= 0f) return;

            if (_continuousRoutine != null)
                StopCoroutine(_continuousRoutine);

            _continuousRoutine = StartCoroutine(DashRampRoutine(duration));
        }

        public void StopDashAttack()
        {
            if (_continuousRoutine != null)
            {
                StopCoroutine(_continuousRoutine);
                _continuousRoutine = null;
            }

            _continuousLow = 0f;
            _continuousHigh = 0f;
        }

        public void StopAllHaptics()
        {
            StopDashAttack();

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            _pulseLow = 0f;
            _pulseHigh = 0f;
            ApplyMotors(0f, 0f);
        }

        #endregion

        #region Internal

        private void HandleHealthDamaged(float amount) => PlayHurtPulse();

        private void HandleDeath() => PlayDeath();

        private void PlayPulse(HapticStep[] steps)
        {
            if (!hapticsEnabled || steps == null || steps.Length == 0) return;

            if (_pulseRoutine != null)
                StopCoroutine(_pulseRoutine);

            _pulseRoutine = StartCoroutine(PulseRoutine(steps));
        }

        private IEnumerator PulseRoutine(HapticStep[] steps)
        {
            foreach (HapticStep step in steps)
            {
                _pulseLow = step.lowFrequency;
                _pulseHigh = step.highFrequency;
                yield return new WaitForSeconds(step.duration);
            }

            _pulseLow = 0f;
            _pulseHigh = 0f;
            _pulseRoutine = null;
        }

        private IEnumerator DashRampRoutine(float duration)
        {
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / duration);

                _continuousLow = dashRampPeakLow * norm;
                _continuousHigh = dashRampPeakHigh * norm;
                yield return null;
            }

            _continuousLow = 0f;
            _continuousHigh = 0f;
            _continuousRoutine = null;
        }

        private void ApplyMotors(float low, float high)
        {
            low *= intensityScale;
            high *= intensityScale;

            if (Mathf.Approximately(low, _appliedLow) && Mathf.Approximately(high, _appliedHigh))
                return;

            Gamepad gamepad = Gamepad.current;
            if (gamepad == null) return;

            gamepad.SetMotorSpeeds(low, high);
            _appliedLow = low;
            _appliedHigh = high;
        }

        #endregion
    }
}
