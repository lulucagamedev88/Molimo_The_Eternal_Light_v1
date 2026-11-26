using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace MolimoByLulucaGameDev
{

    public class HarmItem : MonoBehaviour
    {
        [Header("Damage Settings")]
        [Tooltip("percentage of health the be tirada of the player/enemy (0.15 = 15% of the health maximum)")]
        [SerializeField] private float damagePercentage = 0.15f;

        [Header("Layer Detection")]
        [Tooltip("select the Layers of the player and of the enemies that must sofrer the damage of the explosion")]
        [SerializeField] private LayerMask targetLayers;

        [Header("Explosion Settings")]
        [Tooltip("time in seconds after the item appear is it explodir")]
        [SerializeField] private float fuseDuration = 1.0f;

        [Tooltip("radius of alcance of the damage of the explosion")]
        [SerializeField] private float explosionRadius = 3f;

        [Tooltip("distance of the knockback (knockback) applied to the enemies when the explodir")]
        [SerializeField] private float explosionRecoilDistance = 1.2f;

        [Tooltip("Prefab of the effect visual of the explosion (optional)")]
        [SerializeField] private GameObject explosionVfxPrefab;

        [Header("gradual Pulse & Rotation Settings")]
        [SerializeField] private float minPulseAmount = 0.02f;
        [SerializeField] private float maxPulseAmount = 0.35f;
        [SerializeField] private float minPulseSpeed = 5f;
        [SerializeField] private float maxPulseSpeed = 30f;
        [SerializeField] private float maxBaseSwell = 1.25f;

        [Tooltip("speed initial of rotation in the axis Y (graus/second)")]
        [SerializeField] private float minRotationSpeed = 90f;

        [Tooltip("speed maximum of rotation in the axis Y in the final of the count (graus/second)")]
        [SerializeField] private float maxRotationSpeed = 720f;

        [Header("Audio Settings (SoundData)")]
        [SerializeField] private SoundData[] fuseSoundDataList;
        [SerializeField] private SoundData[] explosionSoundDataList;

        private bool _alreadyExploded = false;
        private AudioSource _activeAudioSource;
        private GameObject _activeSfxObject;
        private CinemachineImpulseSource _impulseSource;

        private static readonly Collider[] hitBuffer = new Collider[30];

        private void Awake()
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        private void Start()
        {
            PlayFuseSound();
            StartCoroutine(FuseRoutine());
        }

        public void PlayFuseSound()
        {
            if (fuseSoundDataList == null || fuseSoundDataList.Length == 0) return;

            int randomIndex = Random.Range(0, fuseSoundDataList.Length);
            SoundData soundData = fuseSoundDataList[randomIndex];

            if (soundData == null || soundData.GetClip() == null) return;

            _activeSfxObject = new GameObject($"SFX_{soundData.soundName}");
            _activeSfxObject.transform.position = transform.position;

            _activeAudioSource = _activeSfxObject.AddComponent<AudioSource>();
            _activeAudioSource.clip = soundData.GetClip();
            _activeAudioSource.volume = soundData.volume;
            _activeAudioSource.pitch = soundData.GetPitch();
            _activeAudioSource.spatialBlend = 1.0f;
            _activeAudioSource.minDistance = soundData.minDistance;
            _activeAudioSource.maxDistance = soundData.maxDistance;

            _activeAudioSource.Play();
        }

        private IEnumerator FuseRoutine()
        {
            Vector3 originalScale = transform.localScale;
            float timer = 0f;

            while (timer < fuseDuration)
            {
                timer += Time.deltaTime;

                float progress = Mathf.Clamp01(timer / fuseDuration);

                float currentSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, progress);
                float currentAmount = Mathf.Lerp(minPulseAmount, maxPulseAmount, progress);
                float currentBaseSwell = Mathf.Lerp(1f, maxBaseSwell, progress);

                float pulseWave = Mathf.Sin(timer * currentSpeed) * currentAmount;
                transform.localScale = originalScale * (currentBaseSwell + pulseWave);

                float currentRotationSpeed = Mathf.Lerp(minRotationSpeed, maxRotationSpeed, progress);
                transform.Rotate(Vector3.up, currentRotationSpeed * Time.deltaTime, Space.Self);

                yield return null;
            }

            Explode();
        }

        private void Explode()
        {
            if (_alreadyExploded) return;
            _alreadyExploded = true;

            if (_activeSfxObject != null)
            {
                Destroy(_activeSfxObject);
            }

            PlayExplosionSound();

            if (_impulseSource != null)
            {
                _impulseSource.GenerateImpulse();
            }

            if (explosionVfxPrefab != null)
            {
                GameObject vfx = Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
                Destroy(vfx, 0.9f);
            }

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, hitBuffer, targetLayers);

            HashSet<GameObject> processedRoots = new HashSet<GameObject>();

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = hitBuffer[i];
                if (hit == null) continue;

                GameObject rootObj = hit.transform.root.gameObject;
                if (processedRoots.Contains(rootObj)) continue;

                bool appliedDamage = false;

                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null) damageable = hit.GetComponent<IDamageable>();

                if (damageable != null && damageable.CurrentHealth > 0f)
                {
                    float enemyDamage = damageable.MaxHealth * damagePercentage;
                    damageable.TakeDamage(enemyDamage, explosionRecoilDistance);
                    appliedDamage = true;
                }

                PlayerHealth targetHealth = hit.GetComponentInParent<PlayerHealth>();
                if (targetHealth == null) targetHealth = hit.GetComponent<PlayerHealth>();

                if (targetHealth != null)
                {
                    float playerDamage = targetHealth.MaxHealth * damagePercentage;
                    targetHealth.TakeDamage(playerDamage);
                    appliedDamage = true;
                }

                PlayerBrain targetBrain = hit.GetComponentInParent<PlayerBrain>();
                if (targetBrain == null) targetBrain = hit.GetComponent<PlayerBrain>();

                if (targetBrain != null)
                {
                    targetBrain.TriggerKnockdown();
                }

                if (appliedDamage)
                {
                    processedRoots.Add(rootObj);
                }
            }

            Destroy(gameObject);
        }

        private void PlayExplosionSound()
        {
            if (explosionSoundDataList == null || explosionSoundDataList.Length == 0) return;

            int randomIndex = Random.Range(0, explosionSoundDataList.Length);
            SoundData soundData = explosionSoundDataList[randomIndex];

            if (soundData == null || soundData.GetClip() == null) return;

            GameObject sfxObject = new GameObject($"SFX_Explosion_{soundData.soundName}");
            sfxObject.transform.position = transform.position;

            AudioSource audioSource = sfxObject.AddComponent<AudioSource>();
            audioSource.clip = soundData.GetClip();
            audioSource.volume = soundData.volume;
            audioSource.pitch = soundData.GetPitch();
            audioSource.spatialBlend = 1.0f;

            audioSource.Play();
            Destroy(sfxObject, soundData.GetClip().length / Mathf.Max(0.1f, Mathf.Abs(audioSource.pitch)));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
