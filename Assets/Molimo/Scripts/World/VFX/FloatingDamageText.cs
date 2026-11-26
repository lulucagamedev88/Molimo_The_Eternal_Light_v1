using UnityEngine;
using TMPro;

namespace MolimoByLulucaGameDev
{

    public class FloatingDamageText : MonoBehaviour
    {
        [Header("settings of movement/duration")]
        [Tooltip("speed of subida of the text in the axis Y")]
        [SerializeField] private float moveSpeed = 2f;

        [Tooltip("duration total (seconds) until the text be destroyed")]
        [SerializeField] private float duration = 1.2f;

        private const float SpawnHeightOffset = 0.5f;
        private const int TextSortingOrder = 500;

        private TextMeshPro _textMesh;
        private Camera _mainCamera;
        private Color _startColor;
        private float _elapsedTime = 0f;

        #region Static Factory

        public static FloatingDamageText Create(Vector3 worldPosition, float damageValue, float fontSize = 6f, float moveSpeed = 2f, float duration = 1.2f)
        {
            GameObject textObject = new GameObject("FloatingDamageText");

            if (VFXGlobalContainer.Instance != null)
            {
                textObject.transform.SetParent(VFXGlobalContainer.Instance, true);
            }

            textObject.transform.position = worldPosition + (Vector3.up * SpawnHeightOffset);

            FloatingDamageText instance = textObject.AddComponent<FloatingDamageText>();
            instance.Initialize(damageValue, fontSize, moveSpeed, duration);

            return instance;
        }
        #endregion

        #region Initialization
        private void Initialize(float damageValue, float fontSize, float moveSpeedParam, float durationParam)
        {
            moveSpeed = moveSpeedParam;
            duration = durationParam;
            _mainCamera = Camera.main;

            _textMesh = gameObject.AddComponent<TextMeshPro>();
            ConfigureTextAppearance(damageValue, fontSize);
        }

        private void ConfigureTextAppearance(float damageValue, float fontSize)
        {

            if (_textMesh.font == null)
            {
                _textMesh.font = TMP_Settings.defaultFontAsset;
            }

            _textMesh.text = FormatDamageText(damageValue);
            _textMesh.fontSize = fontSize;
            _textMesh.color = Color.red;
            _textMesh.fontStyle = FontStyles.Bold;
            _textMesh.alignment = TextAlignmentOptions.Center;

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = TextSortingOrder;
            }

            _startColor = _textMesh.color;
        }

        private string FormatDamageText(float damageValue)
        {
            int roundedValue = Mathf.RoundToInt(damageValue);

            if (roundedValue > 0) return $"+{roundedValue}";
            return roundedValue.ToString();
        }
        #endregion

        #region Update Loop
        private void Update()
        {
            _elapsedTime += Time.deltaTime;

            MoveUpward();
            FaceCamera();
            FadeOut();

            if (_elapsedTime >= duration)
            {
                Destroy(gameObject);
            }
        }

        private void MoveUpward()
        {
            transform.position += Vector3.up * moveSpeed * Time.deltaTime;
        }

        private void FaceCamera()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            transform.rotation = Quaternion.LookRotation(transform.position - _mainCamera.transform.position);
        }

        private void FadeOut()
        {
            if (_textMesh == null) return;

            float normalizedTime = Mathf.Clamp01(_elapsedTime / duration);
            float alpha = Mathf.Lerp(1f, 0f, normalizedTime);

            Color currentColor = _startColor;
            currentColor.a = alpha;
            _textMesh.color = currentColor;
        }
        #endregion
    }
}
