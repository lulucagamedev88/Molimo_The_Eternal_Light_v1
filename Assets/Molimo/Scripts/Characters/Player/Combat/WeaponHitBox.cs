using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    [System.Serializable]

    public class WeaponVFXData
    {
        [Tooltip("Nome of the effect used in the Animation Event (ex: Swoosh1, Slash2, HeavyTrail, Block)")]
        public string vfxName;

        [Tooltip("ParticleSystem anexado to the sword (cortes and rastros of blade)")]
        public ParticleSystem particleSystem;

        [Tooltip("Prefab of effect instanciado in the position of the sword (optional)")]
        public GameObject vfxPrefab;
    }

    [System.Serializable]

    public class SwordImpactSoundData
    {
        [Tooltip("Identificador used in the Animation Event SwordImpact(string) (ex: Flesh, Metal, Stone, Wood)")]
        public string impactName;

        [Tooltip("variations of SoundData is this tipo of impact")]
        public SoundData[] sounds;
    }

    [RequireComponent(typeof(Collider))]
    public class WeaponHitBox : MonoBehaviour
    {

        [Header("settings of the attack")]
        [SerializeField] private float defaultDamage = 20f;
        [SerializeField] private float defaultKnockbackDistance = 0.5f;

        [Tooltip("FAILSAFE: fecha the window of hit if the DisableHit() never chegar (attack cancelado the middle). " +
                 "0 = desativado.")]
        [SerializeField] private float hitWindowSafetyTimeout = 1f;

        [Header("Filtros")]
        [SerializeField] private LayerMask breakableLayerMask;
        [SerializeField] private LayerMask enemyLayerMask;

        [Header("VFX — list of effects")]
        [Tooltip("effects chamados by nome in the Animation Events (SlashVFX / BlockVFX).")]
        [SerializeField] private List<WeaponVFXData> vfxList = new List<WeaponVFXData>();

        [Header("VFX — impact")]
        [Tooltip("effect instanciado when the acertar targets (must conter the ParticleSystem).")]
        [SerializeField] private GameObject defaultImpactPrefab;

        [Tooltip("Layers that can disparar the VFX of impact. empty = never shows impact.")]
        [SerializeField] private LayerMask impactLayerMask;

        [Tooltip("point fixo of the impact (optional). if empty, uses the point more next of the target.")]
        [SerializeField] private Transform impactPointTransform;

        [Tooltip("parent of the VFX instanciados. empty = search 'VFX Container' in the scene, otherwise root of the scene.")]
        [SerializeField] private Transform vfxContainer;

        [Header("audio of the sword (SoundData)")]
        [SerializeField] private SoundData[] swingSounds;
        [SerializeField] private SoundData[] clashSounds;

        [Tooltip("sounds of impact of the sword callable by nome via Animation Event SwordImpact(string).")]
        [SerializeField] private List<SwordImpactSoundData> swordImpactSounds = new List<SwordImpactSoundData>();

        [Tooltip("Container of the sounds (Player_SFX). if empty, is localizado/created in the root shared with the PlayerSoundController.")]
        [SerializeField] private Transform sfxContainer;

        [Tooltip("if enabled, the sound of swing toca when the open the hitbox. DESLIGUE if use Animation Event 'WeaponSwing' in the Player.")]
        [SerializeField] private bool autoPlaySwingOnHitOpen = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private Collider _col;
        private SFXPool _sfxPool;

        private bool _isHitWindowActive;
        private float _hitWindowOpenTime;
        private float _currentDamage;
        private float _currentKnockbackDistance;

        private readonly HashSet<Object> _hitTargetsThisAttack = new HashSet<Object>();
        private readonly Dictionary<string, WeaponVFXData> _vfxLookup = new Dictionary<string, WeaponVFXData>();
        private readonly Dictionary<string, SoundData[]> _swordImpactLookup = new Dictionary<string, SoundData[]>();

        private void Awake()
        {
            _col = GetComponent<Collider>();
            if (_col != null)
            {
                _col.isTrigger = true;
                _col.enabled = false;
            }

            _currentDamage = defaultDamage;
            _currentKnockbackDistance = defaultKnockbackDistance;

            EnsureSFXContainer();
            EnsureVFXContainer();
            BuildVFXLookup();
            BuildSwordImpactLookup();
        }

        private void Start()
        {
            PreloadAndWarmupSounds();
        }

        private void Update()
        {
            if (!_isHitWindowActive || hitWindowSafetyTimeout <= 0f) return;
            if (Time.time - _hitWindowOpenTime <= hitWindowSafetyTimeout) return;

            if (debugLogs)
                Debug.LogWarning("[WeaponHitBox] window of hit closed by safety timeout (DisableHit not chegou).", this);

            DisableHit();
        }

        private void OnDisable()
        {

            if (_isHitWindowActive) DisableHit();
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

        private void EnsureSFXPool()
        {
            if (_sfxPool != null) return;
            EnsureSFXContainer();
            if (sfxContainer == null) return;
            _sfxPool = SFXPool.GetOrCreate(sfxContainer);
        }

        private void PreloadAndWarmupSounds()
        {
            List<SoundData> allSounds = new List<SoundData>();

            if (swingSounds != null) allSounds.AddRange(swingSounds);
            if (clashSounds != null) allSounds.AddRange(clashSounds);

            if (swordImpactSounds != null)
            {
                for (int i = 0; i < swordImpactSounds.Count; i++)
                {
                    SwordImpactSoundData entry = swordImpactSounds[i];
                    if (entry != null && entry.sounds != null)
                        allSounds.AddRange(entry.sounds);
                }
            }

            if (allSounds.Count == 0) return;

            SoundData[] soundArray = allSounds.ToArray();
            SFXPool.Preload(soundArray);

            EnsureSFXPool();
            _sfxPool?.Warmup(soundArray);
        }

        private void EnsureVFXContainer()
        {
            if (vfxContainer != null) return;

            GameObject found = GameObject.Find("VFX Container");
            if (found != null) vfxContainer = found.transform;
        }

        private static string NormalizeKey(string key)
        {
            return key.ToLowerInvariant().Replace(" ", "").Replace("_", "").Trim();
        }

        private void BuildVFXLookup()
        {
            _vfxLookup.Clear();
            if (vfxList == null) return;

            for (int i = 0; i < vfxList.Count; i++)
            {
                WeaponVFXData data = vfxList[i];
                if (data == null || string.IsNullOrEmpty(data.vfxName)) continue;

                string key = NormalizeKey(data.vfxName);
                if (!_vfxLookup.ContainsKey(key)) _vfxLookup.Add(key, data);
            }
        }

        private void BuildSwordImpactLookup()
        {
            _swordImpactLookup.Clear();
            if (swordImpactSounds == null) return;

            for (int i = 0; i < swordImpactSounds.Count; i++)
            {
                SwordImpactSoundData data = swordImpactSounds[i];
                if (data == null || string.IsNullOrEmpty(data.impactName) || data.sounds == null) continue;

                string key = NormalizeKey(data.impactName);
                if (!_swordImpactLookup.ContainsKey(key)) _swordImpactLookup.Add(key, data.sounds);
            }
        }

        private WeaponVFXData FindVFXData(string searchName)
        {
            if (string.IsNullOrEmpty(searchName)) return null;
            _vfxLookup.TryGetValue(NormalizeKey(searchName), out WeaponVFXData data);
            return data;
        }

        #region Hitbox Events

        public void SetCustomDamage(float damage) => _currentDamage = damage;

        public void SetKnockbackDistance(float dist) => _currentKnockbackDistance = dist;

        public void EnableHit() => OpenHitWindow();

        public void EnableHitWithKnockback(float knockbackDistance)
        {
            _currentKnockbackDistance = Mathf.Max(0f, knockbackDistance);
            OpenHitWindow();
        }

        private void OpenHitWindow()
        {
            _isHitWindowActive = true;
            _hitWindowOpenTime = Time.time;
            _hitTargetsThisAttack.Clear();
            if (_col != null) _col.enabled = true;

            if (autoPlaySwingOnHitOpen) PlaySwingSound();
        }

        public void DisableHit()
        {
            _isHitWindowActive = false;
            _hitTargetsThisAttack.Clear();
            if (_col != null) _col.enabled = false;
            _currentDamage = defaultDamage;
            _currentKnockbackDistance = defaultKnockbackDistance;
        }
        #endregion

        #region VFX Events

        public void SlashVFX(string vfxName)
        {
            if (string.IsNullOrEmpty(vfxName))
            {
                if (debugLogs) Debug.LogWarning("[WeaponHitBox] the campo String of the evento VFX is empty!", this);
                return;
            }

            WeaponVFXData data = FindVFXData(vfxName);
            if (data == null)
            {
                if (debugLogs) Debug.LogWarning($"[WeaponHitBox] none VFX with the nome '{vfxName}' in the list!", this);
                return;
            }

            if (data.particleSystem != null)
            {
                data.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                data.particleSystem.Play(true);
                if (debugLogs) Debug.Log($"[WeaponHitBox] ParticleSystem '{vfxName}' ATIVADO.", this);
            }

            if (data.vfxPrefab != null)
            {
                SpawnVFX(data.vfxPrefab, transform.position, transform.rotation);
                if (debugLogs) Debug.Log($"[WeaponHitBox] Prefab '{vfxName}' instanciado.", this);
            }
        }

        public void StopVFX(string vfxName)
        {
            WeaponVFXData data = FindVFXData(vfxName);
            if (data == null || data.particleSystem == null) return;

            data.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (debugLogs) Debug.Log($"[WeaponHitBox] ParticleSystem '{vfxName}' stopped.", this);
        }

        public void PlaySwoosh()
        {
            WeaponVFXData target = GetSwooshData();
            if (target != null) SlashVFX(target.vfxName);
        }

        public void StopSwoosh()
        {
            WeaponVFXData target = GetSwooshData();
            if (target != null) StopVFX(target.vfxName);
        }

        private WeaponVFXData GetSwooshData()
        {
            if (vfxList == null || vfxList.Count == 0) return null;

            WeaponVFXData target = FindVFXData("Swoosh") ?? vfxList[0];
            return (target != null && !string.IsNullOrEmpty(target.vfxName)) ? target : null;
        }

        public void BlockVFX(string vfxName) => SlashVFX(vfxName);

        public void StopBlockVFX(string vfxName) => StopVFX(vfxName);
        #endregion

        #region Impact VFX

        public void PlayImpact(Vector3 impactPoint, Quaternion rotation)
        {
            if (defaultImpactPrefab == null) return;

            SpawnVFX(defaultImpactPrefab, impactPoint, rotation);
            if (debugLogs) Debug.Log($"[WeaponHitBox] impact instanciado in {impactPoint}.", this);
        }

        public void PlayImpact(Vector3 impactPoint, Quaternion rotation, int targetLayer)
        {
            if (defaultImpactPrefab == null) return;

            if (!IsOnLayer(impactLayerMask, targetLayer))
            {
                if (debugLogs) Debug.Log($"[WeaponHitBox] impact ignored: layer {targetLayer} not is in impactLayerMask.", this);
                return;
            }

            PlayImpact(impactPoint, rotation);
        }

        private GameObject SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject spawned = Instantiate(prefab, position, rotation);

            if (vfxContainer != null)
                spawned.transform.SetParent(vfxContainer, true);

            ParticleSystem ps = spawned.GetComponentInChildren<ParticleSystem>();
            float destroyDelay = (ps != null) ? ps.main.duration + ps.main.startLifetime.constantMax : 2f;
            Destroy(spawned, destroyDelay);

            return spawned;
        }
        #endregion

        #region Sound API

        public void PlaySwingSound() => PlayRandomSound(swingSounds, transform.position);

        public void PlayClashSound() => PlayRandomSound(clashSounds, transform.position);

        public void SwordImpact(string impactName)
        {
            if (string.IsNullOrEmpty(impactName))
            {
                if (debugLogs) Debug.LogWarning("[WeaponHitBox] the campo String of SwordImpact is empty!", this);
                return;
            }

            if (!_swordImpactLookup.TryGetValue(NormalizeKey(impactName), out SoundData[] sounds) || sounds == null || sounds.Length == 0)
            {
                if (debugLogs) Debug.LogWarning($"[WeaponHitBox] none sound of SwordImpact found is '{impactName}'!", this);
                return;
            }

            Vector3 spawnPos = impactPointTransform != null ? impactPointTransform.position : transform.position;
            PlayRandomSound(sounds, spawnPos);
        }

        private void PlayRandomSound(SoundData[] soundArray, Vector3 spawnPosition)
        {
            if (soundArray == null || soundArray.Length == 0) return;

            SoundData soundData = soundArray[Random.Range(0, soundArray.Length)];
            if (soundData == null || soundData.GetClip() == null) return;

            EnsureSFXPool();
            if (_sfxPool == null) return;

            _sfxPool.Play(soundData, spawnPosition);
        }
        #endregion

        #region Hit Handling
        private void OnTriggerEnter(Collider other) => ProcessTrigger(other);
        private void OnTriggerStay(Collider other) => ProcessTrigger(other);

        private void ProcessTrigger(Collider other)
        {
            if (!_isHitWindowActive || other == null) return;
            int otherLayer = other.gameObject.layer;

            if (IsOnLayer(enemyLayerMask, otherLayer)) HandleDamageableHit(other);
            else if (IsOnLayer(breakableLayerMask, otherLayer)) HandleBreakableHit(other);
        }

        private void HandleDamageableHit(Collider other)
        {
            IDamageable target = other.GetComponentInParent<IDamageable>();

            Object hitKey = GetHitKey(target as Object, other);
            if (!TryHit(hitKey, other, out _)) return;

            target?.TakeDamage(_currentDamage, _currentKnockbackDistance);

            ApplyKnockbackTo(other);
        }

        private void ApplyKnockbackTo(Collider other)
        {
            if (_currentKnockbackDistance <= 0f) return;

            IKnockbackReceiver receiver = other.GetComponentInParent<IKnockbackReceiver>();
            if (receiver == null) return;

            Transform attacker = transform.root;
            Vector3 dir = ((Component)receiver).transform.position - attacker.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = attacker.forward;

            receiver.ApplyKnockback(dir, _currentKnockbackDistance, 0f);
        }

        private void HandleBreakableHit(Collider other)
        {
            BreakableBox box = other.GetComponentInParent<BreakableBox>();

            Object hitKey = GetHitKey(box, other);
            if (!TryHit(hitKey, other, out Vector3 spawnPoint)) return;

            if (box != null) box.TakeDamage(_currentDamage, spawnPoint);
            else Destroy(other.gameObject);
        }

        private bool TryHit(Object hitKey, Collider other, out Vector3 spawnPoint)
        {
            spawnPoint = Vector3.zero;
            if (hitKey == null || _hitTargetsThisAttack.Contains(hitKey)) return false;

            spawnPoint = impactPointTransform != null
                ? impactPointTransform.position
                : GetSafeImpactPoint(other);

            _hitTargetsThisAttack.Add(hitKey);

            PlayImpact(spawnPoint, Quaternion.identity, other.gameObject.layer);
            return true;
        }

        private static Object GetHitKey(Object preferred, Collider other)
        {
            if (preferred != null) return preferred;
            return other.attachedRigidbody != null ? (Object)other.attachedRigidbody : other.transform.root;
        }

        private Vector3 GetSafeImpactPoint(Collider other)
        {
            try { return other.ClosestPoint(transform.position); }
            catch { return other.transform.position; }
        }

        private static bool IsOnLayer(LayerMask mask, int layer) => (mask.value & (1 << layer)) != 0;
        #endregion
    }
}
