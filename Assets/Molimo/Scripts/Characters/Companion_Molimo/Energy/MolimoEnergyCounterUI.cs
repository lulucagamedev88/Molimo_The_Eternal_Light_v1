using TMPro;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    [DisallowMultipleComponent]
    public class MolimoEnergyCounterUI : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Referências")]
        [Tooltip("Componente TextMeshPro (UI) onde o valor será apresentado.")]
        [SerializeField] private TMP_Text energyText;

        [Tooltip("Container do Molimo. Se ficar vazio, é procurado automaticamente uma única vez na cena.")]
        [SerializeField] private MolimoHealthContainer healthContainer;

        #endregion

        #region Internal State

        private bool _subscribed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {

            if (energyText == null)
                energyText = GetComponent<TMP_Text>();

            ResolveContainerIfNeeded();
        }

        private void OnEnable()
        {

            ResolveContainerIfNeeded();

            if (!_subscribed)
            {
                MolimoHealthContainer.OnMolimoHPChanged += HandleHPChanged;
                _subscribed = true;
            }

            RefreshNow();
        }

        private void OnDisable()
        {

            if (_subscribed)
            {
                MolimoHealthContainer.OnMolimoHPChanged -= HandleHPChanged;
                _subscribed = false;
            }
        }

        #endregion

        #region Update Logic

        private void HandleHPChanged(int current, int max)
        {
            UpdateText(current);
        }

        private void RefreshNow()
        {
            if (healthContainer != null)
            {
                UpdateText(healthContainer.CurrentHP);
            }
            else
            {

                UpdateText(0);
            }
        }

        private void UpdateText(int current)
        {
            if (energyText == null) return;
            energyText.text = current.ToString();
        }

        #endregion

        #region Helpers

        private void ResolveContainerIfNeeded()
        {
            if (healthContainer != null) return;

            healthContainer = FindAnyObjectByType<MolimoHealthContainer>();
        }

        #endregion
    }
}
