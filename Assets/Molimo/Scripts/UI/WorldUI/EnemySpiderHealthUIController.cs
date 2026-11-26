using UnityEngine;
using UnityEngine.UI;

namespace MolimoByLulucaGameDev
{

    public class EnemySpiderHealthUIController : MonoBehaviour
    {
        [Header("references of the UI")]
        [Tooltip("Imagem of the bar of health that if esgota (Image Type must be Filled).")]
        [SerializeField] private Image healthFillImage;

        [Tooltip("reference when the EnemySpiderAI. if empty, search in the parent.")]
        [SerializeField] private EnemySpiderAI enemy;

        [Header("settings of display")]
        [Tooltip("rotates the Canvas is estar always facing is the camera primary.")]
        [SerializeField] private bool billboardToCamera = true;

        [Tooltip("Esconde the bar when the health of the enemy is full (100%).")]
        [SerializeField] private bool hideWhenFullHealth = false;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private Camera mainCamera;

        private void Awake()
        {
            if (enemy == null)
                enemy = GetComponentInParent<EnemySpiderAI>();

            mainCamera = Camera.main;

            if (healthFillImage == null)
                Debug.LogWarning($"[EnemySpiderHealthUI] '{name}': healthFillImage not assigned. " +
                                 "Arrasta the Image of the bar in the Inspector.");

            if (healthFillImage != null && healthFillImage.type != Image.Type.Filled)
                Debug.LogWarning($"[EnemySpiderHealthUI] '{name}': the Image '{healthFillImage.name}' " +
                                 "not is with Image Type = Filled — the fillAmount not vai ter effect visual!");

            if (enemy == null)
                Debug.LogWarning($"[EnemySpiderHealthUI] none EnemySpiderAI found " +
                                 $"is '{name}'. the bar not vai update.");
        }

        private void OnEnable()
        {
            if (enemy == null) return;

            enemy.OnHealthChanged += UpdateHealthBar;
            enemy.OnDeath += HandleEnemyDeath;

            UpdateHealthBar(enemy.CurrentHealth, enemy.MaxHealth);
        }

        private void OnDisable()
        {
            if (enemy == null) return;

            enemy.OnHealthChanged -= UpdateHealthBar;
            enemy.OnDeath -= HandleEnemyDeath;
        }

        private void LateUpdate()
        {
            if (billboardToCamera && mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }

        private void UpdateHealthBar(float currentHealth, float maxHealth)
        {
            float fillAmount = maxHealth > 0f
                ? Mathf.Clamp01(currentHealth / maxHealth)
                : 0f;

            if (debugLogs)
                Debug.Log($"[EnemySpiderHealthUI] EVENTO received of '{(enemy != null ? enemy.name : "?")}': " +
                          $"{currentHealth}/{maxHealth} (fill={fillAmount:F2})");

            if (healthFillImage == null) return;

            healthFillImage.fillAmount = fillAmount;

            if (hideWhenFullHealth)
            {
                bool isFull = currentHealth >= maxHealth;

                if (healthFillImage.transform.parent != null)
                    healthFillImage.transform.parent.gameObject.SetActive(!isFull);
                else
                    healthFillImage.enabled = !isFull;
            }

        }

        private void HandleEnemyDeath()
        {
            if (debugLogs)
                Debug.Log($"[EnemySpiderHealthUI] {name} — death detetada. the hide UI.");

            gameObject.SetActive(false);
        }
    }
}
