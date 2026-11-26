using UnityEngine;
using UnityEngine.UI;

namespace MolimoByLulucaGameDev
{

    public class PlayerStatsHUDController : MonoBehaviour
    {

        [Header("reference of the player")]
        [Tooltip("leave empty is localizar the Health of the object with the tag Player.")]
        [SerializeField] private PlayerHealth health;

        [Header("reference of Molimo")]
        [Tooltip("leave empty is localizar the MolimoHealthContainer in the scene.")]
        [SerializeField] private MolimoHealthContainer healthContainer;

        [Header("Barras")]
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Image staminaFillImage;

        [Header("Barras of energy")]
        [Tooltip("the Image (Filled) by layer of the Heal, by order of the layers (1.ª, 2.ª, 3.ª).")]
        [SerializeField] private Image[] healLayerFillImages;

        [Tooltip("bar of the Container of Molimo (Filled).")]
        [SerializeField] private Image containerFillImage;

        [Header("animation of the Barras")]
        [Tooltip("speed of fill/esvaziamento (0–1 per second). Ex: 2 = 50% per second.")]
        [SerializeField, Min(0f)] private float barChangeSpeed = 2f;

        [Tooltip("if checked, the drain (loss) is instant — only the regeneration is suave.")]
        [SerializeField] private bool instantDrain = true;

        [Header("alerts of health")]
        [Tooltip("Activa the logs of alerta in the console when the health baixa.")]
        [SerializeField] private bool enableHealthAlerts = true;

        [SerializeField] private float alertThreshold = 30f;
        [SerializeField] private float criticalThreshold = 25f;
        [SerializeField] private float severThreshold = 10f;
        [SerializeField] private float terminalThreshold = 5f;

        private enum HealthAlertLevel { None, Alert, Critical, Severe, Terminal, Dead }
        private HealthAlertLevel currentAlertLevel = HealthAlertLevel.None;

        private float targetHealth = 1f;
        private float targetStamina = 1f;
        private float[] targetHealLayers;
        private float targetContainer;

        private void Awake()
        {
            if (health == null) health = FindPlayerHealth();
            if (health == null)
            {
                enabled = false;
                return;
            }

            if (healthContainer == null)
                healthContainer = FindAnyObjectByType<MolimoHealthContainer>();

            targetHealLayers = new float[healLayerFillImages != null ? healLayerFillImages.Length : 0];

            health.OnHealthChanged += HandleHealthChanged;
            health.OnStaminaChanged += HandleStaminaChanged;
            health.OnHealReserveChanged += HandleHealReserveChanged;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnHealthChanged -= HandleHealthChanged;
                health.OnStaminaChanged -= HandleStaminaChanged;
                health.OnHealReserveChanged -= HandleHealReserveChanged;
            }
        }

        private void Start() => SnapBars();

        private void Update()
        {
            if (healthFillImage != null)
                healthFillImage.fillAmount = Smooth(healthFillImage.fillAmount, targetHealth);

            if (staminaFillImage != null)
                staminaFillImage.fillAmount = Smooth(staminaFillImage.fillAmount, targetStamina);

            if (healLayerFillImages != null)
            {
                for (int i = 0; i < healLayerFillImages.Length; i++)
                {
                    Image img = healLayerFillImages[i];
                    if (img == null) continue;
                    img.fillAmount = targetHealLayers[i];
                }
            }

            if (containerFillImage != null && healthContainer != null)
            {
                targetContainer = ContainerNormalized();
                containerFillImage.fillAmount = Smooth(containerFillImage.fillAmount, targetContainer);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            targetHealth = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (enableHealthAlerts)
                EvaluateHealthAlert(current);
        }

        private void HandleStaminaChanged(float current, float max)
            => targetStamina = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        private void HandleHealReserveChanged(float current, float max)
            => RefreshHealLayerTargets();

        private void RefreshHealLayerTargets()
        {
            if (health == null || targetHealLayers == null) return;

            int fullLayerCount = Mathf.Min(health.FullHealLayers, targetHealLayers.Length);
            for (int i = 0; i < targetHealLayers.Length; i++)
            {

                targetHealLayers[i] = i < fullLayerCount ? 1f : 0f;
            }
        }

        private float ContainerNormalized()
        {
            if (healthContainer == null || healthContainer.MaxHP <= 0) return 0f;
            return Mathf.Clamp01(healthContainer.ExactEnergy / healthContainer.MaxHP);
        }

        private void EvaluateHealthAlert(float current)
        {
            HealthAlertLevel newLevel;

            if (current <= 0f)
                newLevel = HealthAlertLevel.Dead;
            else if (current <= terminalThreshold)
                newLevel = HealthAlertLevel.Terminal;
            else if (current <= severThreshold)
                newLevel = HealthAlertLevel.Severe;
            else if (current <= criticalThreshold)
                newLevel = HealthAlertLevel.Critical;
            else if (current <= alertThreshold)
                newLevel = HealthAlertLevel.Alert;
            else
                newLevel = HealthAlertLevel.None;

            if (newLevel == HealthAlertLevel.None)
            {
                currentAlertLevel = HealthAlertLevel.None;
                return;
            }

            if (newLevel <= currentAlertLevel) return;

            for (HealthAlertLevel level = currentAlertLevel + 1; level <= newLevel; level++)
                FireAlert(level, current);

            currentAlertLevel = newLevel;
        }

        private void FireAlert(HealthAlertLevel level, float current)
        {
            switch (level)
            {
                case HealthAlertLevel.Alert:
                    break;

                case HealthAlertLevel.Critical:
                    break;

                case HealthAlertLevel.Severe:
                    break;

                case HealthAlertLevel.Terminal:
                    break;

                case HealthAlertLevel.Dead:
                    break;
            }
        }

        private float Smooth(float current, float target)
        {
            if (instantDrain && target < current) return target;
            return Mathf.MoveTowards(current, target, barChangeSpeed * Time.deltaTime);
        }

        private static PlayerHealth FindPlayerHealth()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                PlayerHealth h = player.GetComponentInParent<PlayerHealth>();
                if (h == null) h = player.GetComponentInChildren<PlayerHealth>();
                if (h != null) return h;
            }

            return FindAnyObjectByType<PlayerHealth>();
        }

        public void SnapBars()
        {
            if (health == null) return;

            targetHealth = health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;
            targetStamina = health.MaxStamina > 0f ? Mathf.Clamp01(health.CurrentStamina / health.MaxStamina) : 0f;
            RefreshHealLayerTargets();
            targetContainer = ContainerNormalized();

            if (healthFillImage != null) healthFillImage.fillAmount = targetHealth;
            if (staminaFillImage != null) staminaFillImage.fillAmount = targetStamina;

            if (healLayerFillImages != null && targetHealLayers != null)
            {
                for (int i = 0; i < healLayerFillImages.Length; i++)
                {
                    if (healLayerFillImages[i] != null)
                        healLayerFillImages[i].fillAmount = targetHealLayers[i];
                }
            }

            if (containerFillImage != null) containerFillImage.fillAmount = targetContainer;
        }
    }
}
