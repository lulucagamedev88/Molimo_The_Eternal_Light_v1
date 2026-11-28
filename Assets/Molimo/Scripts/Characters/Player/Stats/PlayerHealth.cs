using System;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class PlayerHealth : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;

        private const float EnergyHealSessionGap = 0.35f;

        [Header("References")]
        [SerializeField] private PlayerBrain brain;
        [SerializeField] private PlayerCombat combat;

        private PlayerHaptics _haptics;

        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        [Header("Knockdown Trigger")]
        [Range(0f, 1f)]
        [SerializeField] private float knockdownHealthThreshold = 0.5f;
        private bool hasKnockdownTriggered = false;

        [Header("Critical Health")]
        [Tooltip("percentage of health (0-1) the partir of the which the Health is considered critical. Ex: 0.25 = 25%.")]
        [Range(0f, 1f)]
        [SerializeField] private float criticalHealthThreshold = 0.40f;

        [Header("Heal Reserve (layers)")]
        [Tooltip("number of layers of the reserve Heal.")]
        [SerializeField, Range(1, 6)] private int healLayerCount = 3;

        [Tooltip("energy that each layer comporta.")]
        [SerializeField, Min(0.1f)] private float energyPerLayer = 5f;

        [Tooltip("energy with that the Heal starts the jogo (distributed layer the layer). 0 = empty.")]
        [SerializeField, Min(0f)] private float startingHealEnergy = 0f;

        [Header("Heal → Health")]
        [Tooltip("HP restaurados by 1 unidade of energy spends. with 5 energy by layer, 20 = the layer full restores 100 HP.")]
        [SerializeField, Min(0.01f)] private float healthPerEnergy = 20f;

        [Tooltip("speed of the recovery of Health when the load in Heal (HP per second).")]
        [SerializeField, Min(0.1f)] private float healthRecoveryRate = 25f;

        [Header("Stamina Settings")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float currentStamina = 100f;
        [SerializeField] private float staminaRegenRate = 15f;
        [SerializeField] private float staminaRegenDelay = 1.5f;
        [SerializeField] private float exhaustedStaminaRegenRate = 8f;

        [Header("Stamina Costs")]
        [SerializeField] private float sprintStaminaCostPerSecond = 15f;
        [SerializeField] private float rollStaminaCost = 5f;
        [SerializeField] private float blockStaminaCost = 5f;
        [SerializeField] private float jumpStaminaCost = 15f;

        [Header("Rage Settings")]
        [SerializeField] private float maxRage = 100f;
        [SerializeField] private float currentRage = 0f;

        [Header("Exhausted Settings")]
        [SerializeField] private float exhaustionRecoveryThreshold = 45f;

        private float lastStaminaUseTime;
        private bool isExhausted = false;

        private float[] healLayers;
        private bool isHealing;
        private float pendingHealHP;
        private float lastEnergyHealTime = -10f;

        public event Action<float> OnDamaged;

        public event Action<float> OnHealed;

        public event Action OnDeath;

        public event Action<float, float> OnHealthChanged;

        public event Action<float, float> OnStaminaChanged;

        public event Action<float, float> OnHealReserveChanged;

        public event Action OnHealingStarted;

        public event Action OnHealingStopped;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float MaxStamina => maxStamina;
        public float CurrentStamina => currentStamina;
        public float MaxRage => maxRage;
        public float CurrentRage => currentRage;
        public bool IsExhausted => isExhausted;

        public float SprintStaminaCostPerSecond => sprintStaminaCostPerSecond;
        public float RollStaminaCost => rollStaminaCost;
        public float BlockStaminaCost => blockStaminaCost;
        public float JumpStaminaCost => jumpStaminaCost;

        public float CriticalHealthThreshold => criticalHealthThreshold;
        public bool IsCritical =>
            brain != null && brain.IsAlive &&
            currentHealth > 0f &&
            currentHealth <= maxHealth * criticalHealthThreshold;

        public int HealLayerCount => EnsureHealLayers().Length;
        public float EnergyPerLayer => energyPerLayer;
        public float MaxHealEnergy => EnsureHealLayers().Length * energyPerLayer;
        public float HealthPerEnergy => healthPerEnergy;
        public float HealthRecoveryRate => healthRecoveryRate;
        public bool IsHealing => isHealing;

        public float CurrentHealEnergy
        {
            get
            {
                float[] layers = EnsureHealLayers();
                float sum = 0f;
                for (int i = 0; i < layers.Length; i++) sum += layers[i];
                return sum;
            }
        }

        public int FullHealLayers
        {
            get
            {
                float[] layers = EnsureHealLayers();
                int n = 0;
                for (int i = 0; i < layers.Length; i++)
                    if (layers[i] >= energyPerLayer - Epsilon) n++;
                return n;
            }
        }

        public bool HasFullHealLayer => FullHealLayers > 0;

        public bool IsHealEmpty => !HasFullHealLayer;
        public bool IsHealFull => MaxHealEnergy - CurrentHealEnergy <= Epsilon;

        private void Awake()
        {
            EnsureHealLayers();
        }

        public void Initialize(PlayerBrain brainRef)
        {
            brain = brainRef;
            if (combat == null) combat = GetComponent<PlayerCombat>();
            if (combat == null) combat = GetComponentInChildren<PlayerCombat>();
            ResetToFull();
        }

        public void ResetToFull()
        {
            StopHeal();

            currentHealth = maxHealth;
            currentStamina = maxStamina;
            currentRage = 0f;
            hasKnockdownTriggered = false;

            if (isExhausted)
            {
                isExhausted = false;
                if (brain != null) brain.SetExhausted(false);
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        public void TickHealth()
        {
            if (brain == null) return;

            if (brain.IsAlive)
            {
                RegenerateStamina();
                UpdateExhaustedState();
                TickManualHeal(Time.deltaTime);
            }
            else if (isHealing)
            {
                StopHeal();
            }
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, null);
        }

        public void TakeDamage(float amount, GameObject damageSource)
        {
            if (brain == null || !brain.IsAlive) return;

            bool isMotherSpiderDamage = IsBossSource(damageSource);

            if (combat != null && combat.IsBlocking)
            {
                if (isMotherSpiderDamage)
                {
                    float reduction = Mathf.Clamp01(combat.BlockDamageReduction);
                    amount *= (1f - reduction);
                }
                else
                {
                    if (_haptics == null) _haptics = GetComponentInParent<PlayerHaptics>();
                    _haptics?.PlayBlockImpact();

                    return;
                }
            }

            currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);

            OnDamaged?.Invoke(amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                StopHeal();
                brain.Die();
                OnDeath?.Invoke();
                return;
            }

            float thresholdHP = maxHealth * knockdownHealthThreshold;
            if (!hasKnockdownTriggered && currentHealth <= thresholdHP)
            {
                hasKnockdownTriggered = true;
                brain.TriggerKnockdown();
            }
        }

        private bool IsBossSource(GameObject source)
        {
            if (source == null) return false;
            return source.GetComponentInParent<EnemyMotherSpider>() != null;
        }

        public void Heal(float amount)
        {
            ApplyHealthGain(amount);
        }

        public void HealPercent(float percent)
        {
            Heal(maxHealth * Mathf.Clamp01(percent));
        }

        private float ApplyHealthGain(float amount)
        {
            if (brain == null || !brain.IsAlive) return 0f;
            if (amount <= 0f || currentHealth >= maxHealth) return 0f;

            float previousHealth = currentHealth;
            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);

            float applied = currentHealth - previousHealth;
            if (applied <= 0f) return 0f;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnHealed?.Invoke(applied);

            float thresholdHP = maxHealth * knockdownHealthThreshold;
            if (hasKnockdownTriggered && currentHealth > thresholdHP)
            {
                hasKnockdownTriggered = false;
            }

            return applied;
        }

        private float[] EnsureHealLayers()
        {
            int count = Mathf.Max(1, healLayerCount);
            if (healLayers != null && healLayers.Length == count) return healLayers;

            healLayers = new float[count];

            float remaining = Mathf.Max(0f, startingHealEnergy);
            for (int i = 0; i < count && remaining > 0f; i++)
            {
                float take = Mathf.Min(energyPerLayer, remaining);
                healLayers[i] = take;
                remaining -= take;
            }

            return healLayers;
        }

        public float GetHealLayerEnergy(int index)
        {
            float[] layers = EnsureHealLayers();
            if (index < 0 || index >= layers.Length) return 0f;
            return layers[index];
        }

        public float GetHealRoom()
        {
            float[] layers = EnsureHealLayers();
            float room = 0f;
            for (int i = 0; i < layers.Length; i++)
                room += Mathf.Max(0f, energyPerLayer - layers[i]);
            return room;
        }

        public float AddHealEnergy(float amount)
        {
            if (amount <= 0f) return 0f;
            if (brain == null || !brain.IsAlive) return 0f;

            float[] layers = EnsureHealLayers();
            float remaining = amount;

            for (int i = 0; i < layers.Length && remaining > 0f; i++)
            {
                float space = energyPerLayer - layers[i];
                if (space <= 0f) continue;

                float add = Mathf.Min(space, remaining);
                layers[i] += add;
                remaining -= add;

                if (layers[i] >= energyPerLayer - Epsilon) layers[i] = energyPerLayer;
            }

            float accepted = amount - remaining;
            if (accepted > 0f) RaiseHealReserveChanged();
            return accepted;
        }

        public float ApplyEnergyHeal(float hp)
        {
            float applied = ApplyHealthGain(hp);

            if (applied > 0f)
            {
                if (!isHealing && Time.time - lastEnergyHealTime > EnergyHealSessionGap)
                    OnHealingStarted?.Invoke();

                lastEnergyHealTime = Time.time;
            }

            return applied;
        }

        public bool TryStartHeal()
        {
            if (brain == null || !brain.IsAlive) return false;
            if (currentHealth + pendingHealHP >= maxHealth - Epsilon) return false;

            float[] layers = EnsureHealLayers();

            int index = -1;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] >= energyPerLayer - Epsilon) { index = i; break; }
            }

            if (index < 0) return false;

            layers[index] = 0f;
            pendingHealHP += energyPerLayer * healthPerEnergy;
            RaiseHealReserveChanged();

            if (!isHealing)
            {
                isHealing = true;
                OnHealingStarted?.Invoke();
            }

            return true;
        }

        public void StopHeal()
        {
            pendingHealHP = 0f;

            if (!isHealing) return;

            isHealing = false;
            OnHealingStopped?.Invoke();
        }

        private void TickManualHeal(float deltaTime)
        {
            if (!isHealing) return;

            if (pendingHealHP <= Epsilon || currentHealth >= maxHealth)
            {
                StopHeal();
                return;
            }

            float hp = Mathf.Min(healthRecoveryRate * deltaTime, pendingHealHP);
            if (hp <= 0f) return;

            ApplyHealthGain(hp);
            pendingHealHP -= hp;

            if (pendingHealHP <= Epsilon || currentHealth >= maxHealth)
                StopHeal();
        }

        private void RaiseHealReserveChanged()
        {
            OnHealReserveChanged?.Invoke(CurrentHealEnergy, MaxHealEnergy);
        }

        public void ConsumeStamina(float amount)
        {
            currentStamina = Mathf.Max(0f, currentStamina - amount);
            lastStaminaUseTime = Time.time;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        public void RegenerateStamina(float amount)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        private void RegenerateStamina()
        {
            if (Time.time >= lastStaminaUseTime + staminaRegenDelay && currentStamina < maxStamina)
            {
                float regenRate = isExhausted ? exhaustedStaminaRegenRate : staminaRegenRate;
                currentStamina = Mathf.Clamp(currentStamina + (regenRate * Time.deltaTime), 0f, maxStamina);
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }

        private void UpdateExhaustedState()
        {
            if (!isExhausted && currentStamina <= 0f)
            {
                isExhausted = true;
                if (brain != null) brain.SetExhausted(true);
            }
            else if (isExhausted && currentStamina >= exhaustionRecoveryThreshold)
            {
                isExhausted = false;
                if (brain != null) brain.SetExhausted(false);
            }
        }

        public void AddRage(float amount)
        {
            currentRage = Mathf.Clamp(currentRage + amount, 0f, maxRage);
        }

        public void ConsumeRage(float amount)
        {
            currentRage = Mathf.Clamp(currentRage - amount, 0f, maxRage);
        }
    }
}
