using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MolimoByLulucaGameDev
{

    [DisallowMultipleComponent]
    public class MolimoUIHUD : MonoBehaviour
    {
        #region Inspector Fields

        [Header("references of UI")]
        [Tooltip("text that shows the value current (formato 'D2', ex: '00', '01', ..., '10').")]
        [SerializeField] private TextMeshProUGUI hpCounterText;

        [Tooltip("icon of the basket / energy. optional.")]
        [SerializeField] private Image hpIcon;

        [Tooltip("CanvasGroup is fade/opacidade. optional.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Feedback visual")]
        [Tooltip("color of the text when the basket is full.")]
        [SerializeField] private Color fullColor = new Color(1f, 0.85f, 0.2f);

        [Tooltip("color of the text normal.")]
        [SerializeField] private Color normalColor = Color.white;

        [Tooltip("Pulsa the scale of the icon when the value rises.")]
        [SerializeField] private bool pulseOnIncrease = true;

        [Tooltip("Fator of scale in the PICO of the pulse. Ex: 1.15 = 15% larger. " +
                 "the icon always returns to the scale original when the final.")]
        [SerializeField, Range(1f, 2f)] private float pulseScale = 1.15f;

        [Tooltip("duration total of the pulse (ida and returns).")]
        [SerializeField, Range(0.05f, 1f)] private float pulseDuration = 0.25f;

        [Header("visibility")]
        [Tooltip("Esconde the HUD automatically when the value is zero.")]
        [SerializeField] private bool hideWhenZero = false;

        [Tooltip("duration of the fade when the show/hide.")]
        [SerializeField, Range(0f, 1f)] private float fadeDuration = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        #endregion

        #region Internal State

        private int _currentHP;
        private int _maxHP = 10;

        private Vector3 _originalIconScale = Vector3.one;
        private bool _hasCapturedIconScale = false;

        private bool _isPulsing;
        private float _pulseTimer;

        private bool _isFading;
        private float _fadeTimer;
        private float _fadeTarget = 1f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {

            CaptureOriginalIconScale();
        }

        private void OnEnable()
        {
            MolimoHealthContainer.OnMolimoHPChanged += HandleHPChanged;

            RestoreIconScale();

            SyncWithContainer();
        }

        private void OnDisable()
        {
            MolimoHealthContainer.OnMolimoHPChanged -= HandleHPChanged;

            RestoreIconScale();
        }

        private void Update()
        {
            UpdatePulse();
            UpdateFade();
        }

        #endregion

        #region Event Handling

        private void HandleHPChanged(int current, int max)
        {
            bool increased = current > _currentHP;

            _currentHP = current;
            _maxHP = max;

            RefreshText();
            RefreshColor();

            if (increased && pulseOnIncrease)
                StartPulse();

            if (hideWhenZero)
                SetVisible(current > 0);

            Log($"HP updated: {current}/{max}");
        }

        private void SyncWithContainer()
        {
            var container = FindAnyObjectByType<MolimoHealthContainer>();
            if (container == null) return;

            _currentHP = container.CurrentHP;
            _maxHP = container.MaxHP;

            RefreshText();
            RefreshColor();

            if (hideWhenZero)
                SetVisible(_currentHP > 0);
        }

        #endregion

        #region visual Updates

        private void RefreshText()
        {
            if (hpCounterText == null) return;

            hpCounterText.text = _currentHP.ToString("D2");
        }

        private void RefreshColor()
        {
            if (hpCounterText == null) return;

            bool isFull = _currentHP >= _maxHP;
            hpCounterText.color = isFull ? fullColor : normalColor;
        }

        #endregion

        #region Icon Scale Management

        private void CaptureOriginalIconScale()
        {
            if (_hasCapturedIconScale) return;
            if (hpIcon == null) return;

            _originalIconScale = hpIcon.transform.localScale;
            _hasCapturedIconScale = true;

            Log($"scale original of the icon captured: {_originalIconScale}");
        }

        private void RestoreIconScale()
        {
            if (hpIcon == null || !_hasCapturedIconScale) return;

            hpIcon.transform.localScale = _originalIconScale;
            _isPulsing = false;
        }

        #endregion

        #region Pulse Animation

        private void StartPulse()
        {
            if (hpIcon == null) return;

            if (!_hasCapturedIconScale) CaptureOriginalIconScale();
            if (!_hasCapturedIconScale) return;

            _isPulsing = true;
            _pulseTimer = 0f;
        }

        private void UpdatePulse()
        {
            if (!_isPulsing || hpIcon == null) return;

            _pulseTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_pulseTimer / pulseDuration);

            float triangle = 1f - Mathf.Abs(t * 2f - 1f);
            float factor = 1f + (pulseScale - 1f) * triangle;

            hpIcon.transform.localScale = _originalIconScale * factor;

            if (t >= 1f)
            {
                _isPulsing = false;
                hpIcon.transform.localScale = _originalIconScale;
            }
        }

        #endregion

        #region Fade

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                if (hpCounterText != null) hpCounterText.gameObject.SetActive(visible);
                if (hpIcon != null) hpIcon.gameObject.SetActive(visible);
                return;
            }

            _fadeTarget = visible ? 1f : 0f;
            _isFading = true;
            _fadeTimer = 0f;
        }

        private void UpdateFade()
        {
            if (!_isFading || canvasGroup == null) return;

            _fadeTimer += Time.deltaTime;
            float speed = fadeDuration > 0f ? 1f / fadeDuration : 999f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _fadeTarget, speed * Time.deltaTime);

            if (Mathf.Approximately(canvasGroup.alpha, _fadeTarget))
            {
                _isFading = false;
                canvasGroup.blocksRaycasts = _fadeTarget > 0f;
                canvasGroup.interactable = _fadeTarget > 0f;
            }
        }

        #endregion

        #region Helpers

        private void Log(string msg)
        {
            if (debugLogs) Debug.Log($"[MolimoUIHUD] {msg}");
        }

        #endregion
    }
}
