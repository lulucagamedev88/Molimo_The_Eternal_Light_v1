using UnityEngine;

namespace MolimoByLulucaGameDev
{

    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class ShieldSoundController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("PlayerCombat consultado para saber se o Player está em Block. if empty, is searched automaticamente.")]
        [SerializeField] private PlayerCombat combat;

        [Tooltip("Container of the sounds (Player_SFX). if empty, is localizado/created in the root shared com o PlayerSoundController.")]
        [SerializeField] private Transform sfxContainer;

        [Header("Shield Impact (elemento +)")]
        [Tooltip("Variações de som de impacto no escudo. Só toca quando o Player está em Block.")]
        [SerializeField] private SoundData[] shieldImpactSounds;

        [Header("Filtros")]
        [Tooltip("Layers que podem tocar o som de impacto (inimigos). vazio = qualquer collider que tocar no escudo.")]
        [SerializeField] private LayerMask enemyLayerMask;

        [Header("Regras de Reprodução")]
        [Tooltip("Só toca quando o Player está em Block (combat.IsBlocking). Desligue para tocar sempre que algo tocar no escudo.")]
        [SerializeField] private bool onlyWhenBlocking = true;

        [Tooltip("Intervalo mínimo entre impactos (segundos). Evita spam quando o inimigo desliza contra o escudo.")]
        [SerializeField, Min(0f)] private float minIntervalBetweenSounds = 0.08f;

        [Header("Settings Gerais")]
        [Tooltip("Carrega os clips in memory in the Start (elimina hitch of the 1.º Play).")]
        [SerializeField] private bool preloadClipsOnStart = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private SFXPool _sfxPool;
        private float _lastPlayTime = -Mathf.Infinity;

        private void Awake()
        {
            if (combat == null) combat = GetComponentInParent<PlayerCombat>();
            if (combat == null) combat = GetComponentInChildren<PlayerCombat>();
        }

        private void Start()
        {
            if (!preloadClipsOnStart) return;

            if (shieldImpactSounds == null) return;

            for (int i = 0; i < shieldImpactSounds.Length; i++)
                shieldImpactSounds[i]?.GetClip();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || collision.collider == null) return;

            Vector3 point = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;

            ProcessContact(collision.collider, point);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            Vector3 point;
            try { point = other.ClosestPoint(transform.position); }
            catch { point = transform.position; }

            ProcessContact(other, point);
        }

        private void ProcessContact(Collider other, Vector3 impactPoint)
        {
            if (shieldImpactSounds == null || shieldImpactSounds.Length == 0) return;

            if (onlyWhenBlocking && (combat == null || !combat.IsBlocking))
                return;

            if (!IsAllowedLayer(other.gameObject.layer))
                return;

            if (Time.time - _lastPlayTime < minIntervalBetweenSounds)
                return;

            _lastPlayTime = Time.time;

            if (debugLogs)
                Debug.Log($"[ShieldSoundController] ShieldImpact → {other.name} (Blocking={combat != null && combat.IsBlocking})", this);

            PlayRandomSound(impactPoint);
        }

        private bool IsAllowedLayer(int layer)
        {
            return enemyLayerMask.value == 0 || (enemyLayerMask.value & (1 << layer)) != 0;
        }

        private void PlayRandomSound(Vector3 spawnPosition)
        {
            SoundData soundData = shieldImpactSounds[Random.Range(0, shieldImpactSounds.Length)];
            if (soundData == null || soundData.GetClip() == null) return;

            EnsureSFXPool();
            if (_sfxPool == null) return;

            _sfxPool.Play(soundData, spawnPosition);
        }

        private void EnsureSFXPool()
        {
            if (_sfxPool != null) return;
            EnsureSFXContainer();
            if (sfxContainer == null) return;
            _sfxPool = SFXPool.GetOrCreate(sfxContainer);
        }

        private void EnsureSFXContainer()
        {
            if (sfxContainer != null) return;

            Transform root = transform.root;
            Transform found = root.Find("Player_SFX");

            if (found != null)
            {
                sfxContainer = found;
            }
            else if (root != transform)
            {
                var go = new GameObject("Player_SFX");
                go.transform.SetParent(root, false);
                sfxContainer = go.transform;
            }
        }
    }
}
