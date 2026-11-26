using System;
using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class MolimoHealthContainer : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;

        #region Events

        public static event Action<int, int> OnMolimoHPChanged;
        public static event Action<EnergyTier> OnEnergyTierRaised;

        public event Action<int> OnItemCollected;

        public enum EnergyTier
        {
            Low = 0,
            Medium = 1,
            Powerful = 2
        }

        public void RaiseHPChanged()
        {
            OnMolimoHPChanged?.Invoke(currentHP, maxHP);
        }

        #endregion

        #region Inspector Fields

        [Header("basket of HP")]
        [Tooltip("capacity of the Container. recommended: 5 (= energy of the layer of the Heal).")]
        [SerializeField, Range(1, 1000)] private int maxHP = 5;
        [SerializeField, Range(0, 1000)] private int currentHP = 0;

        [Tooltip("Se ativo, quando o container atinge a capacidade máxima deixa de atrair itens " +
                 "até voltar a 0. Recria o ciclo 'encher → esvaziar → voltar a encher'.")]
        [SerializeField] private bool latchAttractionUntilEmpty = true;

        [Header("levels of energy (alerts HUD)")]
        [Tooltip("have of be <= to the capacity of the Container. Ex: Medium 3, Powerful 5.")]
        [SerializeField, Range(1, 1000)] private int mediumEnergyThreshold = 3;
        [SerializeField, Range(1, 1000)] private int powerfulEnergyThreshold = 5;

        [Header("Attraction of Items of health")]
        [SerializeField] private LayerMask itemLayer = ~0;
        [SerializeField, Range(0.5f, 30f)] private float attractionRadius = 5f;
        [SerializeField] private Vector3 centerOffset = Vector3.zero;
        [SerializeField] private float pullSpeed = 8f;

        [Header("Gizmos")]
        [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0.5f, 0.3f);

        #endregion

        #region Public Properties

        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public bool HasHP => currentHP > 0;
        public bool IsFull => currentHP >= maxHP;
        public float AttractionRadius => attractionRadius;

        public float ExactEnergy => Mathf.Max(0f, currentHP - _spentFraction);

        #endregion

        #region Internal State

        private readonly Collider[] _overlapResults = new Collider[20];

        private float _spentFraction;

        private readonly List<MolimoCollectableHP> _incoming = new List<MolimoCollectableHP>();

        private bool _attractionLatched;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            RaiseHPChanged();
        }

        private void Update()
        {

            if (_attractionLatched && currentHP <= 0)
                _attractionLatched = false;

            if (IsFull) return;
            if (latchAttractionUntilEmpty && _attractionLatched) return;

            DetectAndAttractItems();
        }

        private void OnValidate()
        {
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            mediumEnergyThreshold = Mathf.Clamp(mediumEnergyThreshold, 1, maxHP);
            powerfulEnergyThreshold = Mathf.Clamp(powerfulEnergyThreshold, mediumEnergyThreshold, maxHP);
        }

        #endregion

        #region Item Detection

        private void DetectAndAttractItems()
        {
            PruneIncoming();

            int room = maxHP - currentHP - IncomingCharge();
            if (room <= 0) return;

            Vector3 center = transform.position + centerOffset;
            int count = Physics.OverlapSphereNonAlloc(
                center, attractionRadius, _overlapResults, itemLayer,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapResults[i];
                if (col == null) continue;

                MolimoCollectableHP item = col.GetComponentInParent<MolimoCollectableHP>();
                if (item == null) continue;
                if (item.IsAttracted) continue;
                if (item.HealCharge > room) continue;

                item.AttractTo(transform, pullSpeed);

                if (item.IsAttracted)
                {
                    _incoming.Add(item);
                    room -= item.HealCharge;
                    if (room <= 0) break;
                }
            }
        }

        private void PruneIncoming()
        {
            for (int i = _incoming.Count - 1; i >= 0; i--)
            {
                MolimoCollectableHP item = _incoming[i];
                if (item == null || !item.IsAttracted)
                    _incoming.RemoveAt(i);
            }
        }

        private int IncomingCharge()
        {
            int sum = 0;
            for (int i = 0; i < _incoming.Count; i++)
                sum += _incoming[i].HealCharge;
            return sum;
        }

        #endregion

        #region Energy Tier API

        public EnergyTier GetCurrentTier()
        {
            if (currentHP >= powerfulEnergyThreshold) return EnergyTier.Powerful;
            if (currentHP >= mediumEnergyThreshold) return EnergyTier.Medium;
            return EnergyTier.Low;
        }

        private void FireTierRaisedIfNeeded(EnergyTier before)
        {
            EnergyTier after = GetCurrentTier();
            if (after <= before) return;

            if (before < EnergyTier.Medium && after >= EnergyTier.Medium)
                OnEnergyTierRaised?.Invoke(EnergyTier.Medium);

            if (before < EnergyTier.Powerful && after >= EnergyTier.Powerful)
                OnEnergyTierRaised?.Invoke(EnergyTier.Powerful);
        }

        #endregion

        #region Basket API

        public bool TryAddHP(int amount)
        {
            if (amount <= 0) return false;
            if (IsFull) return false;

            EnergyTier tierBefore = GetCurrentTier();

            int before = currentHP;
            currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
            int added = currentHP - before;

            if (added > 0)
            {
                OnMolimoHPChanged?.Invoke(currentHP, maxHP);
                OnItemCollected?.Invoke(added);
                FireTierRaisedIfNeeded(tierBefore);

                if (latchAttractionUntilEmpty && currentHP >= maxHP)
                    _attractionLatched = true;
            }

            return added > 0;
        }

        public bool TryConsumeHP(int amount)
        {
            if (amount <= 0) return false;
            if (currentHP < amount) return false;

            currentHP -= amount;
            if (currentHP <= 0) _spentFraction = 0f;
            OnMolimoHPChanged?.Invoke(currentHP, maxHP);

            return true;
        }

        public float ConsumeEnergy(float amount)
        {
            if (amount <= 0f || currentHP <= 0) return 0f;

            float exact = currentHP - _spentFraction;
            float consumed = Mathf.Min(amount, exact);
            if (consumed <= 0f) return 0f;

            float newExact = exact - consumed;

            int newHP = Mathf.Max(0, Mathf.CeilToInt(newExact - Epsilon));
            _spentFraction = newHP > 0 ? Mathf.Clamp01(newHP - newExact) : 0f;

            if (newHP != currentHP)
            {
                currentHP = newHP;
                OnMolimoHPChanged?.Invoke(currentHP, maxHP);
            }

            return consumed;
        }

        #endregion

        #region Helpers

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position + centerOffset;
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(center, attractionRadius);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.1f);
            Gizmos.DrawSphere(center, attractionRadius);
        }

        #endregion

        #region Static Reset

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            OnMolimoHPChanged = null;
            OnEnergyTierRaised = null;
        }

        #endregion
    }
}
