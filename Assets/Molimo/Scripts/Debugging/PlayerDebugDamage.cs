using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MolimoByLulucaGameDev
{

    public class PlayerDebugDamage : MonoBehaviour
    {
        [Header("settings of Teste")]
        [Tooltip("Quanto of damage the key K applies.")]
        [SerializeField] private float damageAmount = 15f;

        [Tooltip("Quanto of healing the key L applies. the sound of HealSounds toca via Health.OnHealed.")]
        [SerializeField] private float healAmount = 15f;

        [Tooltip("if checked, the teclas of debug ficam ativas.")]
        [SerializeField] private bool enableDebugKeys = true;

        [Tooltip("Loga in the Console each application of damage/healing.")]
        [SerializeField] private bool logActions = true;

        private PlayerHealth playerHealth;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
            if (playerHealth == null) playerHealth = GetComponentInChildren<PlayerHealth>();

            if (playerHealth == null)
            {
                Debug.LogError("[PlayerDebugDamage] the component Health not was found in the Player!", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!enableDebugKeys || playerHealth == null) return;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return;

            if (Keyboard.current.kKey.wasPressedThisFrame)
                ApplyDamage();

            if (Keyboard.current.lKey.wasPressedThisFrame)
                ApplyHeal();
#else
            if (Input.GetKeyDown(KeyCode.K))
                ApplyDamage();

            if (Input.GetKeyDown(KeyCode.L))
                ApplyHeal();
#endif
        }

        private void ApplyDamage()
        {

            playerHealth.TakeDamage(damageAmount);

            if (logActions)
                Debug.Log($"[PlayerDebugDamage] K → -{damageAmount} HP | HP current: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}", this);
        }

        private void ApplyHeal()
        {
            float hpBefore = playerHealth.CurrentHealth;

            playerHealth.Heal(healAmount);

            float healed = playerHealth.CurrentHealth - hpBefore;

            if (!logActions) return;

            if (healed > 0f)
                Debug.Log($"[PlayerDebugDamage] L → +{healed:0.##} HP | HP current: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}", this);
            else
                Debug.LogWarning($"[PlayerDebugDamage] L → nothing curado (health already full or Player dead). without healing not toca HealSounds. HP current: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}", this);
        }
    }
}
