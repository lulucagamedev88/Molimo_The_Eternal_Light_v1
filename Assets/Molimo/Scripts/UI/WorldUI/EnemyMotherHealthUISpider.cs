using UnityEngine;
using UnityEngine.UI;

namespace MolimoByLulucaGameDev
{

    public class EnemyMotherHealthUISpider : MonoBehaviour
    {
        [Header("references of the UI")]
        [Tooltip("Imagem of the bar of health (Image Type = Filled).")]
        [SerializeField] private Image healthFillImage;

        [Tooltip("reference when the EnemyMotherSpider. if empty, search in the parent.")]
        [SerializeField] private EnemyMotherSpider enemy;

        [Header("settings of display")]
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private bool hideWhenFullHealth = false;

        [Header("Destaque during the Rest")]
        [Tooltip("if true, the bar gets the color special when the boss is in Rest (vulnerable).")]
        [SerializeField] private bool highlightDuringRest = true;
        [SerializeField] private Color restHighlightColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color defaultFillColor = Color.white;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private Camera mainCamera;

        private void Awake()
        {
            if (enemy == null)
                enemy = GetComponentInParent<EnemyMotherSpider>();

            mainCamera = Camera.main;

            if (enemy == null)
                Debug.LogWarning($"[EnemyMotherHealthUI] none EnemyMotherSpider found " +
                                 $"is '{name}'. the bar not vai update.");

            if (healthFillImage != null && highlightDuringRest)
                healthFillImage.color = defaultFillColor;
        }

        private void OnEnable()
        {
            if (enemy == null) return;

            enemy.OnHealthChanged += UpdateHealthBar;
            enemy.OnDeath += HandleEnemyDeath;

            UpdateHealthBar(enemy.CurrentHealth, enemy.MaxHealth);
            RefreshRestHighlight();
        }

        private void OnDisable()
        {
            if (enemy == null) return;

            enemy.OnHealthChanged -= UpdateHealthBar;
            enemy.OnDeath -= HandleEnemyDeath;
        }

        private void Update()
        {

            RefreshRestHighlight();
        }

        private void LateUpdate()
        {
            if (billboardToCamera && mainCamera != null)
                transform.rotation = mainCamera.transform.rotation;
        }

        private void UpdateHealthBar(float currentHealth, float maxHealth)
        {
            if (healthFillImage == null) return;

            float fillAmount = maxHealth > 0f
                ? Mathf.Clamp01(currentHealth / maxHealth)
                : 0f;

            healthFillImage.fillAmount = fillAmount;

            if (hideWhenFullHealth)
            {
                bool isFull = currentHealth >= maxHealth;

                if (healthFillImage.transform.parent != null)
                    healthFillImage.transform.parent.gameObject.SetActive(!isFull);
                else
                    healthFillImage.enabled = !isFull;
            }

            if (debugLogs)
                Debug.Log($"[EnemyMotherHealthUI] {name} → {currentHealth}/{maxHealth} (fill={fillAmount:F2})");
        }

        private void RefreshRestHighlight()
        {
            if (!highlightDuringRest || healthFillImage == null || enemy == null) return;

            healthFillImage.color = enemy.IsResting ? restHighlightColor : defaultFillColor;
        }

        private void HandleEnemyDeath()
        {
            if (debugLogs)
                Debug.Log($"[EnemyMotherHealthUI] {name} — death detetada. the hide UI.");

            gameObject.SetActive(false);
        }
    }
}
