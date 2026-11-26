using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class MolimoCombat : MonoBehaviour
    {
        #region Enums

        private enum AttackPhase { None, Dive, Return, Cooldown }

        #endregion

        #region Inspector Fields

        [Header("detection of enemies")]
        [Tooltip("radius of detection of enemies. the center is the PLAYER.")]
        [SerializeField] private float detectionRadius = 8f;

        [Tooltip("distance maximum between the enemy and the PLAYER is the Molimo atacar.")]
        [SerializeField] private float maxAttackDistanceFromPlayer = 6f;

        [SerializeField] private LayerMask enemyLayer = ~0;
        [SerializeField] private string enemyTag = "Enemy";

        [Header("attack")]
        [SerializeField] private float attackSpeed = 15f;
        [SerializeField] private float returnSpeed = 12f;
        [SerializeField] private float attackCooldown = 3f;
        [SerializeField] private float damagePerHit = 40f;
        [SerializeField] private float attackKnockback = 0f;
        [SerializeField] private float hitDistance = 0.9f;
        [SerializeField] private float targetAimHeight = 0.5f;
        [SerializeField] private float returnArrivalDistance = 0.3f;

        #endregion

        #region Internal State

        private Transform _playerTransform;
        private Vector3 _followOffset;
        private MolimoVFXManager _vfx;

        private Transform _currentTarget;
        private AttackPhase _phase = AttackPhase.None;
        private float _phaseTimer;
        private Vector3 _diveTarget;

        public Transform CurrentTarget => _currentTarget;
        public bool HasTarget => _currentTarget != null;

        #endregion

        #region Initialization

        public void Initialize(Transform player, Vector3 followOffset)
        {
            _playerTransform = player;
            _followOffset = followOffset;

            if (_vfx == null) _vfx = GetComponent<MolimoVFXManager>();
        }

        #endregion

        #region Public API

        public void BeginCombat()
        {
            _phase = AttackPhase.None;
            _currentTarget = null;
        }

        public void Cancel()
        {
            _currentTarget = null;
            _phase = AttackPhase.None;
        }

        public bool HasAnyTargetsInRange()
        {
            return FindNearestEnemyInRange() != null;
        }

        public bool Tick(Vector3 bobOffset)
        {
            if (_currentTarget != null &&
                Vector3.Distance(_currentTarget.position, _playerTransform.position)
                    > maxAttackDistanceFromPlayer)
            {
                Cancel();
                return false;
            }

            if (_currentTarget == null || !IsTargetAlive(_currentTarget))
            {
                Transform next = FindNearestEnemyInRange();
                if (next == null)
                {
                    Cancel();
                    return false;
                }

                _currentTarget = next;
                StartDive();
            }

            switch (_phase)
            {
                case AttackPhase.None: StartDive(); break;
                case AttackPhase.Dive: UpdateDive(); break;
                case AttackPhase.Return: UpdateReturn(bobOffset); break;
                case AttackPhase.Cooldown: UpdateCooldown(bobOffset); break;
            }

            return true;
        }

        #endregion

        #region Attack Phases

        private void StartDive()
        {
            _phase = AttackPhase.Dive;
            _phaseTimer = 0f;
            _diveTarget = _currentTarget.position + Vector3.up * targetAimHeight;
        }

        private void UpdateDive()
        {
            if (_currentTarget == null || !IsTargetAlive(_currentTarget))
            {
                _phase = AttackPhase.None;
                return;
            }

            Vector3 toTarget = _diveTarget - transform.position;
            float dist = toTarget.magnitude;

            if (dist <= hitDistance)
            {
                ApplyDamage(_currentTarget);
                _phase = AttackPhase.Return;
                _phaseTimer = 0f;
                return;
            }

            transform.position = Vector3.MoveTowards(
                transform.position, _diveTarget, attackSpeed * Time.deltaTime);
        }

        private void UpdateReturn(Vector3 bobOffset)
        {
            Vector3 followPos = GetFollowPosition() + bobOffset;
            transform.position = Vector3.MoveTowards(
                transform.position, followPos, returnSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, followPos) <= returnArrivalDistance)
            {
                _phase = AttackPhase.Cooldown;
                _phaseTimer = 0f;
            }
        }

        private void UpdateCooldown(Vector3 bobOffset)
        {
            Vector3 followPos = GetFollowPosition() + bobOffset;
            transform.position = Vector3.Lerp(
                transform.position, followPos, Time.deltaTime * 5f);

            _phaseTimer += Time.deltaTime;
            if (_phaseTimer < attackCooldown) return;

            if (_currentTarget != null && IsTargetAlive(_currentTarget))
                StartDive();
        }

        #endregion

        #region Enemy Detection

        private Transform FindNearestEnemyInRange()
        {
            if (_playerTransform == null) return null;

            Collider[] hits = Physics.OverlapSphere(
                _playerTransform.position, detectionRadius, enemyLayer,
                QueryTriggerInteraction.Collide);

            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit == null) continue;

                Transform candidate = ResolveEnemyRoot(hit.transform);
                if (candidate == null) continue;
                if (!MatchesTag(candidate, enemyTag)) continue;
                if (!IsTargetAlive(candidate)) continue;

                float d = Vector3.Distance(candidate.position, _playerTransform.position);
                if (d > maxAttackDistanceFromPlayer) continue;

                if (d < bestDist)
                {
                    bestDist = d;
                    best = candidate;
                }
            }

            return best;
        }

        private Transform ResolveEnemyRoot(Transform hitTransform)
        {
            if (hitTransform == null) return null;

            var dmg = hitTransform.GetComponentInParent<IDamageable>();
            if (dmg is Component c) return c.transform;

            return hitTransform.root;
        }

        private bool MatchesTag(Transform t, string tag)
        {
            if (t == null || string.IsNullOrEmpty(tag)) return false;
            if (t.CompareTag(tag)) return true;
            if (t.root != t && t.root.CompareTag(tag)) return true;
            return false;
        }

        private bool IsTargetAlive(Transform target)
        {
            if (target == null) return false;
            if (!target.gameObject.activeInHierarchy) return false;

            IDamageable dmg = target.GetComponentInParent<IDamageable>();
            if (dmg == null) dmg = target.GetComponentInChildren<IDamageable>();
            if (dmg == null) return false;

            return dmg.IsAlive;
        }

        private void ApplyDamage(Transform target)
        {
            if (target == null) return;

            IDamageable dmg = target.GetComponentInParent<IDamageable>();
            if (dmg == null) dmg = target.GetComponentInChildren<IDamageable>();

            if (dmg == null)
            {
                _currentTarget = null;
                return;
            }

            dmg.TakeDamage(damagePerHit, attackKnockback);

            if (_vfx != null)
                _vfx.SpawnImpactVFX(transform.position, transform.rotation);
        }

        #endregion

        #region Helpers

        private Vector3 GetFollowPosition()
        {
            if (_playerTransform == null) return transform.position;

            return _playerTransform.position
                   + _playerTransform.right * _followOffset.x
                   + Vector3.up * _followOffset.y
                   + _playerTransform.forward * _followOffset.z;
        }

        private void OnDrawGizmosSelected()
        {
            if (_playerTransform != null)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
                Gizmos.DrawWireSphere(_playerTransform.position, detectionRadius);

                Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f);
                Gizmos.DrawWireSphere(_playerTransform.position, maxAttackDistanceFromPlayer);
            }

            if (_currentTarget != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, _currentTarget.position);
                Gizmos.DrawWireSphere(_currentTarget.position, hitDistance);
            }
        }

        #endregion
    }
}
