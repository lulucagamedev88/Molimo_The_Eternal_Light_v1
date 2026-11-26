using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [ExecuteAlways]
    [DisallowMultipleComponent]

    public class LandscapeTintController : MonoBehaviour
    {
        [Header("targets (Sprites of the Landscape)")]
        [SerializeField] private SpriteRenderer[] landscapeRenderers;
        [SerializeField] private bool autoFindChildren = true;

        [Header("response to the light ambient global")]
        [SerializeField, Range(0f, 2f)] private float tintMultiplier = 1f;
        [SerializeField] private Color baseTint = Color.white;

        [Header("brightness min/max (prevents preto total to the night)")]
        [SerializeField, Range(0f, 1f)] private float minBrightness = 0.15f;
        [SerializeField, Range(0f, 2f)] private float maxBrightness = 1.25f;

        [Header("Curva of response (x = lumin�ncia ambient, y = brightness)")]
        [SerializeField]
        private AnimationCurve responseCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 0.6f),
            new Keyframe(1f, 1f)
        );

        [Header("smoothing Temporal")]
        [SerializeField] private bool smoothTransitions = true;
        [SerializeField, Min(0.01f)] private float smoothingSpeed = 6f;

        [Header("Perspectiva atmospheric (depth)")]
        [Tooltip("0 = totalmente iluminado; 1 = totalmente fundido with atmosphericColor.")]
        [SerializeField, Range(0f, 1f)] private float atmosphericFade = 0f;
        [SerializeField] private Color atmosphericColor = new Color(0.60f, 0.70f, 0.85f, 1f);

        [Header("influence of the Sol (optional)")]
        [SerializeField] private bool blendSunColor = false;
        [SerializeField, Range(0f, 1f)] private float sunBlend = 0.25f;

        private Color _currentTint = Color.white;
        private DayNightLightingController _dayNight;

        private void OnEnable()
        {
            CacheRenderers();
            _dayNight = FindAnyObjectByType<DayNightLightingController>();
        }

        private void OnValidate() => CacheRenderers();

        private void Update()
        {
            if (landscapeRenderers == null || landscapeRenderers.Length == 0) return;

            Color ambient = RenderSettings.ambientLight;
            Color target = ComputeTint(ambient);

            if (smoothTransitions && Application.isPlaying)
            {
                float t = 1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);
                _currentTint = Color.Lerp(_currentTint, target, t);
            }
            else
            {
                _currentTint = target;
            }

            ApplyTint(_currentTint);
        }

        private Color ComputeTint(Color ambient)
        {
            float lum = Mathf.Max(ambient.grayscale, 0.0001f);

            float brightness = Mathf.Clamp(
                responseCurve.Evaluate(lum) * tintMultiplier,
                minBrightness, maxBrightness);

            Color huePreserved = ambient * (brightness / lum);

            Color tint = new Color(
                huePreserved.r * baseTint.r,
                huePreserved.g * baseTint.g,
                huePreserved.b * baseTint.b,
                1f);

            if (atmosphericFade > 0.0001f)
                tint = Color.Lerp(tint, atmosphericColor * brightness, atmosphericFade);

            if (blendSunColor && _dayNight != null && _dayNight.GlobalToonLight != null)
            {
                Color sunCol = _dayNight.GlobalToonLight.color;
                float sunI = Mathf.Clamp01(_dayNight.GlobalToonLight.intensity);
                tint = Color.Lerp(tint, tint * sunCol * 2f, sunBlend * sunI);
            }

            return tint;
        }

        private void ApplyTint(Color c)
        {
            for (int i = 0; i < landscapeRenderers.Length; i++)
            {
                var sr = landscapeRenderers[i];
                if (sr != null) sr.color = c;
            }
        }

        private void CacheRenderers()
        {
            if ((landscapeRenderers == null || landscapeRenderers.Length == 0) && autoFindChildren)
                landscapeRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }
}
