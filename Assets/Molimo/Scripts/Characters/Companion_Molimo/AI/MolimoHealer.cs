using UnityEngine;
using UnityEngine.InputSystem;

namespace MolimoByLulucaGameDev
{

    public class MolimoHealer : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;

        #region Enums

        private enum HealPhase { Approach, Pulsing }

        #endregion

        #region Inspector Fields

        [Header("references Externas")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerVFX playerVFX;
        [SerializeField] private MolimoHealthContainer healthContainer;

        [Header("Abastecimento of the Heal (Container → Heal)")]
        [Tooltip("energy of the Container spends by each 1 of energy that enters in the Heal (1 = without perdas). keeps in 1 with Container of 5.")]
        [SerializeField, Min(0.01f)] private float energyCostPerHealEnergy = 1f;

        [Header("rescue critical (Heal empty + Health critical)")]
        [Tooltip("energia do Container transferida por segundo durante o resgate crítico.")]
        [SerializeField, Min(0.01f)] private float rescueTransferRate = 20f;

        [Tooltip("share (0-1) da energia transferida no resgate que vai para a PlayerHealth diretamente. " +
                 "1 = tudo para a vida (o Heal fica intocado). 0 = tudo para o Heal. " +
                 "Design 'restabelecer a saúde até encher' → manter em 1.")]
        [SerializeField, Range(0f, 1f)] private float criticalHealthShare = 1f;

        [Header("movement")]
        [SerializeField] private float flyToChestSpeed = 8f;
        [SerializeField] private Vector3 chestOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private float arrivalDistance = 0.1f;

        [Header("pulse")]
        [SerializeField] private float pulseSpeed = 8f;
        [SerializeField, Range(0f, 0.5f)] private float pulseAmplitude = 0.15f;

        [Header("effects Globais")]
        [SerializeField] private bool freezeEnemiesWhileHealing = true;

        [Header("sound of start of the rescue")]
        [Tooltip("sound tocado when the Molimo starts the rescue critical. empty = without sound.")]
        [SerializeField] private SoundData[] readyToHealSounds;

        [Header("Debug")]
        [Tooltip("Loga in the Console each transfer Container → Heal and each passo of the rescue.")]
        [SerializeField] private bool logTransfers = false;

        [Header("Input of healing")]
        [Tooltip("Action 'Heal' of the Input System (Q / D-pad down). when the load, the Player spends the layer of the Heal.")]
        [SerializeField] private InputActionReference healInputAction;

        #endregion

        #region Internal State

        private Transform _playerTransform;
        private Vector3 _originalScale = Vector3.one;

        private HealPhase _phase = HealPhase.Approach;
        private bool _isHealing;
        private bool _pulseEffectsActive;

        public bool IsHealing => _isHealing;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (healInputAction != null && healInputAction.action != null)
            {
                healInputAction.action.Enable();
                healInputAction.action.performed += OnHealInputPerformed;
            }
        }

        private void OnDisable()
        {
            if (healInputAction != null && healInputAction.action != null)
            {
                healInputAction.action.performed -= OnHealInputPerformed;
            }

            if (_isHealing || _pulseEffectsActive)
                FinishHealing("desativado");
        }

        #endregion

        #region Initialization

        public void Initialize(Transform player, MolimoHealthContainer container)
        {
            _playerTransform = player;
            if (container != null) healthContainer = container;

            ResolveExternalReferences();

            if (healthContainer == null)
                healthContainer = GetComponent<MolimoHealthContainer>();

            _originalScale = transform.localScale;
        }

        private void ResolveExternalReferences()
        {
            if (_playerTransform == null) return;

            if (playerHealth == null)
                playerHealth = _playerTransform.GetComponentInParent<PlayerHealth>();
            if (playerVFX == null)
                playerVFX = _playerTransform.GetComponentInParent<PlayerVFX>();
        }

        private bool ResolveRefs()
        {
            ResolveExternalReferences();

            if (healthContainer == null)
                healthContainer = GetComponent<MolimoHealthContainer>();

            return playerHealth != null && healthContainer != null && _playerTransform != null;
        }

        #endregion

        #region Input

        private void OnHealInputPerformed(InputAction.CallbackContext ctx)
        {
            RequestHeal();
        }

        public bool RequestHeal()
        {
            if (Time.timeScale <= 0f) return false;

            ResolveExternalReferences();
            if (playerHealth == null) return false;

            return playerHealth.TryStartHeal();
        }

        #endregion

        #region Refill (Container → Heal)

        public void TickRefill()
        {
            if (_isHealing) return;
            if (!ResolveRefs()) return;
            if (playerHealth.CurrentHealth <= 0f) return;

            float room = playerHealth.GetHealRoom();
            if (room <= Epsilon) return;

            float cost = Mathf.Max(0.01f, energyCostPerHealEnergy);

            float chunk = Mathf.Min(playerHealth.EnergyPerLayer, room);
            if (healthContainer.ExactEnergy + Epsilon < chunk * cost) return;

            float containerBefore = healthContainer.ExactEnergy;
            float accepted = playerHealth.AddHealEnergy(chunk);
            if (accepted > 0f)
            {
                healthContainer.ConsumeEnergy(accepted * cost);

                if (logTransfers)
                    Debug.Log($"[MolimoHealer] Container → Heal: passed {accepted:0.##} of energy " +
                              $"(Container before: {containerBefore:0.##}, after: {healthContainer.ExactEnergy:0.##} | " +
                              $"EnergyPerLayer: {playerHealth.EnergyPerLayer} | cost: {cost} | " +
                              $"Heal total: {playerHealth.CurrentHealEnergy:0.##}/{playerHealth.MaxHealEnergy:0.##})", this);
            }
        }

        #endregion

        #region Rescue (Heal empty + PlayerHealth critical)

        public bool CanStartRescue()
        {
            if (_isHealing) return false;
            if (!ResolveRefs()) return false;

            float hp = playerHealth.CurrentHealth;
            if (hp <= 0f || hp >= playerHealth.MaxHealth) return false;

            if (!playerHealth.IsCritical) return false;
            if (!playerHealth.IsHealEmpty) return false;

            return healthContainer.ExactEnergy > Epsilon;
        }

        public void StartHealing()
        {
            _isHealing = true;
            _phase = HealPhase.Approach;
            _pulseEffectsActive = false;

            PlayRescueStartSound();
        }

        public bool Tick()
        {
            if (!_isHealing) return false;

            if (!ResolveRefs())
            {
                FinishHealing("references missing");
                return false;
            }

            float hp = playerHealth.CurrentHealth;

            if (hp <= 0f)
            {
                FinishHealing("Player dead");
                return false;
            }

            if (hp >= playerHealth.MaxHealth)
            {
                FinishHealing("Player with health full");
                return false;
            }

            if (healthContainer.ExactEnergy <= Epsilon)
            {
                FinishHealing("Container empty");
                return false;
            }

            Vector3 chestPos = _playerTransform.position + chestOffset;

            switch (_phase)
            {
                case HealPhase.Approach: UpdateApproach(chestPos); break;
                case HealPhase.Pulsing: UpdatePulsing(chestPos); break;
            }

            return true;
        }

        public void CancelHealing()
        {
            if (!_isHealing)
            {
                _pulseEffectsActive = false;
                transform.localScale = _originalScale;
                return;
            }

            FinishHealing("cancelada externamente");
        }

        private void UpdateApproach(Vector3 chestPos)
        {
            float dist = Vector3.Distance(transform.position, chestPos);

            if (dist <= arrivalDistance)
            {
                OnReachedChest();
                return;
            }

            transform.position = Vector3.MoveTowards(
                transform.position, chestPos, flyToChestSpeed * Time.deltaTime);
        }

        private void OnReachedChest()
        {
            _phase = HealPhase.Pulsing;

            if (_pulseEffectsActive) return;
            _pulseEffectsActive = true;

            if (freezeEnemiesWhileHealing)
                EnemyFreezeController.SetAllEnemiesFrozen(true);

            if (playerVFX != null)
            {
                playerVFX.AuthorizeChargeVFX();
                playerVFX.SetChargeVFXState(true);
            }
        }

        private void UpdatePulsing(Vector3 chestPos)
        {
            transform.position = chestPos;

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
            transform.localScale = _originalScale * pulse;

            RescueTransferStep();
        }

        private void RescueTransferStep()
        {
            float budget = Mathf.Min(rescueTransferRate * Time.deltaTime, healthContainer.ExactEnergy);
            if (budget <= 0f) return;

            float cost = Mathf.Max(0.01f, energyCostPerHealEnergy);
            float hpPerEnergy = playerHealth.HealthPerEnergy;

            float healthEnergyBudget = budget * criticalHealthShare;
            float hpRoom = playerHealth.MaxHealth - playerHealth.CurrentHealth;
            float hpPlanned = Mathf.Min(healthEnergyBudget * hpPerEnergy, hpRoom);

            float healEnergyBudget = budget - healthEnergyBudget;
            float healPlanned = Mathf.Min(healEnergyBudget / cost, playerHealth.GetHealRoom());

            float hpApplied = hpPlanned > 0f ? playerHealth.ApplyEnergyHeal(hpPlanned) : 0f;
            float healApplied = healPlanned > 0f ? playerHealth.AddHealEnergy(healPlanned) : 0f;

            float spent = (hpApplied / hpPerEnergy) + (healApplied * cost);
            if (spent > 0f)
            {
                healthContainer.ConsumeEnergy(spent);

                if (logTransfers)
                    Debug.Log($"[MolimoHealer] rescue: Health +{hpApplied:0.##} HP, Heal +{healApplied:0.##}, " +
                              $"Container gastou {spent:0.##} (remains {healthContainer.ExactEnergy:0.##})", this);
            }
        }

        private void FinishHealing(string reason)
        {
            _isHealing = false;
            transform.localScale = _originalScale;

            if (_pulseEffectsActive)
            {
                if (freezeEnemiesWhileHealing)
                    EnemyFreezeController.SetAllEnemiesFrozen(false);

                if (playerVFX != null)
                    playerVFX.SetChargeVFXState(false);
            }

            _pulseEffectsActive = false;
        }

        #endregion

        #region Audio

        private void PlayRescueStartSound()
        {
            if (readyToHealSounds == null || readyToHealSounds.Length == 0) return;

            SoundData data = readyToHealSounds[Random.Range(0, readyToHealSounds.Length)];
            if (data == null) return;

            AudioClip clip = data.GetClip();
            if (clip == null) return;

            var go = new GameObject("MolimoReadyHealSFX");
            go.transform.position = transform.position;

            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = data.volume;
            src.pitch = data.GetPitch();
            src.spatialBlend = 1f;
            src.minDistance = data.minDistance;
            src.maxDistance = data.maxDistance;
            src.playOnAwake = false;
            src.Play();

            Destroy(go, clip.length + 0.1f);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (_playerTransform != null)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
                Gizmos.DrawWireSphere(
                    _playerTransform.position + chestOffset,
                    arrivalDistance);
            }
        }

        #endregion
    }
}
