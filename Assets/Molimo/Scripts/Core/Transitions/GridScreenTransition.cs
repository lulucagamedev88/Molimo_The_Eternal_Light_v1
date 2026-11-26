using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MolimoByLulucaGameDev
{

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public class GridScreenTransition : MonoBehaviour
    {
        [Header("=== reference ===")]
        [Tooltip("Image of the Canvas that uses the material GridScaleTransition. " +
                 "if empty, pega of the own GameObject.")]
        [SerializeField] private Image targetImage;

        [Header("=== Progress ===")]
        [Tooltip("_Progress when the tela is TOTALMENTE revealed (jogo visible).")]
        [SerializeField] private float revealedProgress = 0.0f;

        [Tooltip("_Progress when the tela is TOTALMENTE covered (tela preta).")]
        [SerializeField] private float coveredProgress = 1.5f;

        [Header("=== Angle (graus) ===")]
        [Tooltip("_Angle applied in the Cover (fade in / cobrir the tela).")]
        [SerializeField] private float coverAngle = 40f;

        [Tooltip("_Angle applied in the Reveal (fade out / revelar the tela).")]
        [SerializeField] private float revealAngle = 140f;

        [Header("=== durations (seconds, time real) ===")]
        [Tooltip("time is the grade COBRIR the tela (0 -> 1.5).")]
        [SerializeField] private float coverDuration = 0.6f;

        [Tooltip("time with the tela 100% covered (tela preta), before revelar.")]
        [SerializeField] private float holdDuration = 2.0f;

        [Tooltip("time is the grade REVELAR the tela (1.5 -> 0).")]
        [SerializeField] private float revealDuration = 0.6f;

        [Header("=== curves ===")]
        [SerializeField] private AnimationCurve coverCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Material _instanceMat;
        private int _progressId;
        private int _angleId;
        private bool _isBusy;

        public bool IsBusy => _isBusy;
        public float CoverDuration => coverDuration;
        public float HoldDuration => holdDuration;
        public float RevealDuration => revealDuration;

        private void Awake()
        {
            if (targetImage == null) targetImage = GetComponent<Image>();

            if (targetImage != null && targetImage.material != null)
            {

                _instanceMat = new Material(targetImage.material);
                targetImage.material = _instanceMat;
            }

            _progressId = Shader.PropertyToID("_Progress");
            _angleId = Shader.PropertyToID("_Angle");

            SetProgressInstant(revealedProgress);
            if (targetImage != null) targetImage.enabled = false;
        }

        private void OnDestroy()
        {
            if (_instanceMat != null) Destroy(_instanceMat);
        }

        public void SetProgressInstant(float value)
        {
            if (_instanceMat != null)
                _instanceMat.SetFloat(_progressId, value);
        }

        public void SetAngleInstant(float value)
        {
            if (_instanceMat != null)
                _instanceMat.SetFloat(_angleId, value);
        }

        public float GetProgress()
        {
            return _instanceMat != null ? _instanceMat.GetFloat(_progressId) : 0f;
        }

        public IEnumerator Cover()
        {
            if (targetImage != null) targetImage.enabled = true;
            SetAngleInstant(coverAngle);
            yield return Animate(revealedProgress, coveredProgress, coverDuration, coverCurve);
        }

        public IEnumerator Reveal()
        {
            SetAngleInstant(revealAngle);
            yield return Animate(coveredProgress, revealedProgress, revealDuration, revealCurve);
            if (targetImage != null) targetImage.enabled = false;
        }

        public IEnumerator PlayFullSequence(Action onCovered = null)
        {
            _isBusy = true;

            yield return Cover();

            onCovered?.Invoke();

            if (holdDuration > 0f)
                yield return new WaitForSecondsRealtime(holdDuration);

            yield return Reveal();

            _isBusy = false;
        }

        private IEnumerator Animate(float from, float to, float duration, AnimationCurve curve)
        {
            if (_instanceMat == null) yield break;

            if (duration <= 0f)
            {
                SetProgressInstant(to);
                yield break;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float and = curve != null ? curve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
                SetProgressInstant(Mathf.Lerp(from, to, and));
                yield return null;
            }

            SetProgressInstant(to);
        }
    }
}
