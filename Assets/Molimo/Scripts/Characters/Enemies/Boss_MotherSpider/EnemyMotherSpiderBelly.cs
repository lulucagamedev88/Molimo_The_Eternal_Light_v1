using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class EnemyMotherSpiderBelly : MonoBehaviour, IDamageable
    {
        [Header("references")]
        [Tooltip("spider mother. if empty, searches the parents.")]
        [SerializeField] private EnemyMotherSpider boss;

        [Tooltip("Collider trigger of the belly. if empty, uses the deste GameObject.")]
        [SerializeField] private Collider bellyCollider;

        [Header("damage")]
        [Tooltip("Multiplicador of the damage in the belly (1 = damage normal, 1.5 = +50%).")]
        [SerializeField] private float damageMultiplier = 1f;

        [Header("who conta the 'inside'")]
        [Tooltip("if true, the Molimo (MolimoAI) also enables the point fraco, not only the Player.")]
        [SerializeField] private bool acceptMolimo = true;

        [Tooltip("optional: tag alternative is identificar the Molimo. " +
                 "Deixa empty if confiares only in the component MolimoAI.")]
        [SerializeField] private string molimoTag = "";

        [Tooltip("optional: restringe the detection the estas layers. 0 = accepts tudo.")]
        [SerializeField] private LayerMask targetLayers = ~0;

        [Header("Gizmo")]
        [SerializeField] private Color gizmoColor = new Color(0.9f, 0.2f, 0.8f, 0.6f);

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private readonly HashSet<Collider> _inside = new HashSet<Collider>();
        private readonly List<Collider> _toRemove = new List<Collider>();
        private bool _exposed = true;

        public float DamageMultiplier => damageMultiplier;

        public float CurrentHealth => boss != null ? boss.CurrentHealth : 0f;
        public float MaxHealth => boss != null ? boss.MaxHealth : 0f;
        public bool IsAlive => boss != null && boss.IsAlive;

        public void TakeDamage(float amount, float knockbackDistance = -1f)
        {
            if (boss == null) return;

            if (!_exposed)
            {
                if (debugLogs)
                    Debug.Log("[EnemyMotherBelly] damage ignored: belly protegida.");
                return;
            }

            boss.ApplyDamageFromWeakPoint(amount, knockbackDistance, damageMultiplier);

            if (debugLogs)
                Debug.Log($"[EnemyMotherBelly] damage fraco applied: {amount} x{damageMultiplier}.");
        }

        public bool IsPlayerInside
        {
            get
            {
                if (!_exposed) return false;

                _toRemove.Clear();
                foreach (Collider c in _inside)
                {
                    if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                        _toRemove.Add(c);
                }
                for (int i = 0; i < _toRemove.Count; i++)
                    _inside.Remove(_toRemove[i]);

                return _inside.Count > 0;
            }
        }

        public bool IsTargetInside => IsPlayerInside;

        private void Awake()
        {
            if (boss == null) boss = GetComponentInParent<EnemyMotherSpider>();
            if (bellyCollider == null) bellyCollider = GetComponent<Collider>();

            if (bellyCollider == null)
            {
                Debug.LogError($"[EnemyMotherBelly] '{name}' does not have Collider.");
                return;
            }

            if (!bellyCollider.isTrigger)
            {
                Debug.LogWarning($"[EnemyMotherBelly] the collider of '{name}' not era Trigger. the marcar Is Trigger automatically.");
                bellyCollider.isTrigger = true;
            }

            if (GetComponent<Rigidbody>() != null)
            {
                Debug.LogWarning($"[EnemyMotherBelly] '{name}' has Rigidbody own. removes-the: " +
                                 $"the belly must use the Rigidbody of the spider.");
            }

            if (boss == null)
            {
                Debug.LogWarning($"[EnemyMotherBelly] none EnemyMotherSpider found nos parents of '{name}'.");
            }
        }

        private void OnDisable()
        {
            _inside.Clear();
        }

        public void SetExposed(bool exposed)
        {
            _exposed = exposed;

            if (bellyCollider != null)
                bellyCollider.enabled = exposed;

            if (!exposed)
                _inside.Clear();

            if (debugLogs)
                Debug.Log($"[EnemyMotherBelly] belly {(exposed ? "EXPOSTA" : "protegida")}.");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsValidTarget(other)) return;

            _inside.Add(other);

            if (debugLogs)
                Debug.Log($"[EnemyMotherBelly] Entrou: {other.name}");
        }

        private void OnTriggerExit(Collider other)
        {
            if (_inside.Remove(other) && debugLogs)
                Debug.Log($"[EnemyMotherBelly] Saiu: {other.name}");
        }

        private bool IsValidTarget(Collider other)
        {
            if (other == null) return false;
            if (!other.enabled || !other.gameObject.activeInHierarchy) return false;

            if (targetLayers.value != 0 &&
                (targetLayers.value & (1 << other.gameObject.layer)) == 0)
                return false;

            if (other.GetComponentInParent<PlayerBrain>() != null) return true;

            if (acceptMolimo)
            {
                if (other.GetComponentInParent<MolimoAI>() != null) return true;

                if (!string.IsNullOrEmpty(molimoTag))
                {
                    Transform t = other.transform;
                    if (t.CompareTag(molimoTag)) return true;
                    if (t.root != t && t.root.CompareTag(molimoTag)) return true;

                    Transform walker = t.parent;
                    while (walker != null)
                    {
                        if (walker.CompareTag(molimoTag)) return true;
                        walker = walker.parent;
                    }
                }
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            Collider c = bellyCollider != null ? bellyCollider : GetComponent<Collider>();
            if (c == null) return;

            Gizmos.color = gizmoColor;
            Gizmos.matrix = c.transform.localToWorldMatrix;

            if (c is BoxCollider box)
                Gizmos.DrawWireCube(box.center, box.size);
            else if (c is SphereCollider sphere)
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
    }
}
