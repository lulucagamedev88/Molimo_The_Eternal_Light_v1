using System.Collections;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Molimo By Luluca/Abyss Hazard Trigger")]

    public sealed class AbyssHazardTrigger : MonoBehaviour
    {
        #region Inspector
        [Header("detection of the player")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool filterByTag = true;

        [Header("recovery — weapons / Escudos")]
        [SerializeField] private string[] recoverTags = new string[] { "Weapon", "Shield" };
        [SerializeField] private LayerMask recoverLayers = 0;

        [Header("destruction — Pieces / Enemy")]
        [SerializeField] private string[] destroyTags = new string[] { "Pieces", "Enemy" };
        [SerializeField] private LayerMask destroyLayers = 0;
        [SerializeField] private bool checkParents = true;
        [SerializeField] private GameObject destroyEffectPrefab;

        [Header("death in the Abismo")]
        [Tooltip("if true, the Player morre (Brain.Die) and is revivido in the respawn point. " +
                 "if false, uses the behavior old (preserves state).")]
        [SerializeField] private bool treatFallAsDeath = true;

        [Header("Respawn — Escolha of the Origem (Player only)")]
        [Tooltip("if true, ignores the RespawnCheckpointTracker and uses always fallbackSpawnPoint. " +
                 "weapons not are afectadas by isto — each pickup has the its own respawnPoint.")]
        [SerializeField] private bool preferFallbackSpawnPoint = true;

        [SerializeField] private float backUpDistance = 1.5f;
        [SerializeField] private Transform fallbackSpawnPoint;

        [Header("transition of Tela (GridScreenTransition)")]
        [SerializeField] private GridScreenTransition screenTransition;
        [SerializeField] private bool useScreenTransition = true;
        [SerializeField] private float postCoverDelay = 0.2f;
        [SerializeField] private float holdDurationOverride = -1f;
        [SerializeField] private float fallbackCoverDuration = 0.5f;

        [Header("Tempos (seconds reais) — Fallback")]
        [SerializeField] private float waitAfterTeleport = 0.15f;
        [SerializeField] private float overrideFadeDuration = -1f;

        [Header("Debounce (Player only)")]
        [SerializeField] private float minTimeBetweenActivations = 1f;

        [Header("diagnostics")]
        [SerializeField] private bool warnAboutDeathZone = true;
        #endregion

        #region state
        private float _lastActivationTime = -100f;
        private bool _respawnInProgress = false;
        private GridScreenTransition _cachedTransition;
        #endregion

        #region Ciclo of health
        private void Awake()
        {
            if (!warnAboutDeathZone) return;
            Component deathZone = GetComponent("DeathZone");
            if (deathZone != null)
                Debug.LogError("[AbyssHazardTrigger] 'DeathZone' in the same GameObject — remova-the.", this);
        }
        #endregion

        #region detection
        private void OnTriggerEnter(Collider other) => TryProcessHazardEntry(other.gameObject);
        private void OnTriggerEnter2D(Collider2D other) => TryProcessHazardEntry(other.gameObject);

        private void OnTriggerStay(Collider other)
        {
            RetryRecoverWeaponFromTrigger(other != null ? other.gameObject : null);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            RetryRecoverWeaponFromTrigger(other != null ? other.gameObject : null);
        }

        private void OnTriggerExit(Collider other)
        {
            RetryRecoverWeaponFromTrigger(other != null ? other.gameObject : null);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            RetryRecoverWeaponFromTrigger(other != null ? other.gameObject : null);
        }

        private void RetryRecoverWeaponFromTrigger(GameObject other)
        {
            if (other == null) return;
            if (IsPlayerHierarchy(other)) return;
            RetryRecoverWeapon(other);
        }

        private void TryProcessHazardEntry(GameObject other)
        {
            if (other == null) return;

            if (IsPlayerHierarchy(other)) { TryPlayerRespawn(other); return; }

            GameObject recoverTarget = FindAncestorMatching(other.transform, recoverTags, recoverLayers);
            if (recoverTarget != null) { TryRecoverWeaponPickup(recoverTarget); return; }

            GameObject destroyTarget = FindAncestorMatching(other.transform, destroyTags, destroyLayers);
            if (destroyTarget != null) { TryDestroyHazardObject(destroyTarget); return; }
        }

        private void RetryRecoverWeapon(GameObject other)
        {
            GameObject recoverTarget = FindAncestorMatching(other.transform, recoverTags, recoverLayers);
            if (recoverTarget != null)
                TryRecoverWeaponPickup(recoverTarget);
        }
        #endregion

        #region detection of Player
        private bool IsPlayerHierarchy(GameObject go)
        {
            if (go == null) return false;
            if (filterByTag && MatchesTagRecursive(go.transform, playerTag)) return true;

            PlayerReference playerRef = PlayerReference.Instance;
            if (playerRef != null && playerRef.Root != null)
            {
                Transform t = go.transform, root = playerRef.Root;
                if (t == root || t.IsChildOf(root) || root.IsChildOf(t)) return true;
            }
            return false;
        }

        private bool MatchesTagRecursive(Transform t, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            while (t != null) { if (t.CompareTag(tag)) return true; t = t.parent; }
            return false;
        }
        #endregion

        #region Respawn of the player (death + transition)
        private void TryPlayerRespawn(GameObject other)
        {
            if (_respawnInProgress) return;
            if (Time.unscaledTime - _lastActivationTime < minTimeBetweenActivations) return;

            PlayerBrain brain = ResolveBrain(other);
            if (brain == null)
            {
                Debug.LogWarning("[AbyssHazardTrigger] Brain not found.", this);
                return;
            }
            if (!brain.IsAlive) return;

            if (!TryGetRespawnPoint(brain, out Vector3 pos, out Quaternion rot))
            {
                Debug.LogWarning("[AbyssHazardTrigger] without checkpoint/fallback — Player not was dead.", this);
                return;
            }

            _lastActivationTime = Time.unscaledTime;
            StartCoroutine(RespawnRoutine(brain, pos, rot));
        }

        private PlayerBrain ResolveBrain(GameObject other)
        {
            PlayerReference playerRef = PlayerReference.Instance;
            if (playerRef != null && playerRef.Root != null)
            {
                PlayerBrain b = playerRef.Root.GetComponentInChildren<PlayerBrain>()
                       ?? playerRef.Root.GetComponentInParent<PlayerBrain>();
                if (b != null) return b;
            }
            return other.GetComponentInParent<PlayerBrain>()
                ?? Object.FindAnyObjectByType<PlayerBrain>();
        }

        private IEnumerator RespawnRoutine(PlayerBrain brain, Vector3 position, Quaternion rotation)
        {
            _respawnInProgress = true;
            GridScreenTransition transition = ResolveTransition();
            bool useTransition = transition != null && useScreenTransition;
            bool revealed = false;

            try
            {

                if (treatFallAsDeath && brain != null && brain.IsAlive)
                {
                    Debug.Log("[AbyssHazardTrigger] Player caiu in the abismo — Die().");
                    brain.Die();
                }

                if (useTransition)
                    yield return transition.Cover();
                else if (overrideFadeDuration > 0f)
                    yield return new WaitForSecondsRealtime(overrideFadeDuration);
                else if (fallbackCoverDuration > 0f)
                    yield return new WaitForSecondsRealtime(fallbackCoverDuration);

                if (postCoverDelay > 0f)
                    yield return new WaitForSecondsRealtime(postCoverDelay);

                if (brain == null)
                {
                    Debug.LogWarning("[AbyssHazardTrigger] Brain invalid after cover. Abortando.", this);
                    yield break;
                }

                if (treatFallAsDeath) brain.ReviveAt(position, rotation);
                else brain.RespawnFromAbyss(position, rotation);

                if (waitAfterTeleport > 0f)
                    yield return new WaitForSecondsRealtime(waitAfterTeleport);

                WeaponPickup.NotifyAllOfPlayerReset();

                if (useTransition)
                {
                    float hold = holdDurationOverride >= 0f ? holdDurationOverride : transition.HoldDuration;
                    if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

                    yield return transition.Reveal();
                    revealed = true;
                }
                else
                {
                    if (overrideFadeDuration > 0f)
                        yield return new WaitForSecondsRealtime(overrideFadeDuration);
                    revealed = true;
                }
            }
            finally
            {
                if (useTransition && !revealed)
                {
                    try { StartCoroutine(transition.Reveal()); }
                    catch {  }
                }
                _respawnInProgress = false;
            }
        }

        private bool TryGetRespawnPoint(PlayerBrain brain, out Vector3 position, out Quaternion rotation)
        {
            if (preferFallbackSpawnPoint && fallbackSpawnPoint != null)
            {
                position = fallbackSpawnPoint.position;
                rotation = fallbackSpawnPoint.rotation;
                return true;
            }

            PlayerRespawnCheckpointTracker tracker = brain.GetComponentInChildren<PlayerRespawnCheckpointTracker>();
            if (tracker != null && tracker.HasValidCheckpoint)
            {
                tracker.GetRespawnPoint(backUpDistance, out position, out rotation);
                return true;
            }

            if (fallbackSpawnPoint != null)
            {
                position = fallbackSpawnPoint.position;
                rotation = fallbackSpawnPoint.rotation;
                return true;
            }

            position = default; rotation = default;
            return false;
        }
        #endregion

        #region transition
        private GridScreenTransition ResolveTransition()
        {
            if (screenTransition != null) return screenTransition;
            if (_cachedTransition == null)
                _cachedTransition = Object.FindAnyObjectByType<GridScreenTransition>();
            return _cachedTransition;
        }
        #endregion

        #region recovery of weapons (delega TUDO when the WeaponPickup)
        private void TryRecoverWeaponPickup(GameObject target)
        {
            if (target == null || IsPlayerHierarchy(target)) return;

            WeaponPickup pickup = target.GetComponent<WeaponPickup>()
                               ?? target.GetComponentInParent<WeaponPickup>()
                               ?? target.GetComponentInChildren<WeaponPickup>();

            if (pickup == null)
            {
                Debug.LogWarning(
                    $"[AbyssHazardTrigger] '{target.name}' without WeaponPickup (nem parent/children). Ignorando — not destroy.",
                    target);
                return;
            }

            if (pickup.IsImmuneToAbyss) return;

            pickup.RecoverFromAbyss();
        }
        #endregion

        #region destruction (Pieces / Enemy)
        private void TryDestroyHazardObject(GameObject target)
        {
            if (target == null || IsPlayerHierarchy(target)) return;

            if (destroyEffectPrefab != null)
                Instantiate(destroyEffectPrefab, target.transform.position, target.transform.rotation);

            Destroy(target);
        }
        #endregion

        #region Filtros
        private GameObject FindAncestorMatching(Transform start, string[] tags, LayerMask layers)
        {
            if (start == null) return null;
            if (MatchesFilter(start.gameObject, tags, layers)) return start.gameObject;
            if (!checkParents) return null;

            Transform t = start.parent;
            while (t != null)
            {
                if (MatchesFilter(t.gameObject, tags, layers)) return t.gameObject;
                t = t.parent;
            }
            return null;
        }

        private bool MatchesFilter(GameObject go, string[] tags, LayerMask layers)
        {
            if (go == null) return false;

            if (tags != null && tags.Length > 0)
            {
                string goTag = go.tag;
                for (int i = 0; i < tags.Length; i++)
                    if (!string.IsNullOrEmpty(tags[i]) && goTag == tags[i]) return true;
            }

            if (layers.value != 0)
            {
                int layerBit = 1 << go.layer;
                if ((layers.value & layerBit) != 0) return true;
            }
            return false;
        }
        #endregion

        #region Gizmos
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.9f);
            if (TryGetComponent(out Collider col3D)) Gizmos.DrawWireCube(col3D.bounds.center, col3D.bounds.size);
            if (TryGetComponent(out Collider2D col2D)) Gizmos.DrawWireCube(col2D.bounds.center, col2D.bounds.size);

            if (fallbackSpawnPoint != null)
            {
                Gizmos.color = new Color(0.20f, 1f, 0.40f, 0.9f);
                Gizmos.DrawWireSphere(fallbackSpawnPoint.position, 0.5f);
                Gizmos.DrawLine(transform.position, fallbackSpawnPoint.position);
            }
        }
        #endregion
    }
}
