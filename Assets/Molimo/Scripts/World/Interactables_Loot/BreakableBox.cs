using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(LootDropper))]

    public class BreakableBox : MonoBehaviour, IDamageable
    {
        [Header("health / Durabilidade")]
        [Tooltip("health total of the box before quebrar (ex.: 35)")]
        [SerializeField] private float maxHealth = 100f;

        [Header("references")]
        [SerializeField] private GameObject intactBox;
        [SerializeField] private GameObject destroyedBox;

        [Header("Cinemachine Impulse")]
        [Tooltip("component Cinemachine Impulse Source anexado when the object")]
        [SerializeField] private CinemachineImpulseSource impulseSource;

        [Header("audio — Quebra")]
        [SerializeField] private SoundData[] breakSoundDataList;

        [Header("audio — impact")]
        [SerializeField] private SoundData[] impactSoundDataList;

        [Header("settings of explosion")]
        [SerializeField] private float explosionForce = 10f;
        [SerializeField] private float explosionRadius = 2f;
        [SerializeField] private float upwardModifier = 1f;

        [Header("settings of time")]
        [SerializeField] private float physicsDelay = 3f;
        [SerializeField] private float destroyDelay = 5f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private const float SfxMinDistance = 1f;
        private const float SfxMaxDistance = 20f;

        private float _currentHealth;
        private bool _isBroken;
        private Collider _boxCollider;
        private LootDropper _lootDropper;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => !_isBroken && _currentHealth > 0f;

        private void Awake()
        {
            _currentHealth = maxHealth;

            if (destroyedBox != null) destroyedBox.SetActive(false);
            if (intactBox != null) intactBox.SetActive(true);

            _boxCollider = GetComponent<Collider>();
            if (_boxCollider != null) _boxCollider.isTrigger = false;

            _lootDropper = GetComponent<LootDropper>();

            if (impulseSource == null)
                impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        void IDamageable.TakeDamage(float amount, float knockbackDistance)
        {

            TakeDamage(amount, transform.position);
        }

        public void TakeDamage(float damageAmount)
            => TakeDamage(damageAmount, transform.position);

        public void TakeDamage(float damageAmount, Vector3 impactPoint)
        {
            if (_isBroken) return;

            _currentHealth -= damageAmount;

            if (impulseSource != null) impulseSource.GenerateImpulse();

            float displayHealth = Mathf.Max(_currentHealth, 0f);
            FloatingDamageText.Create(impactPoint, -displayHealth);

            if (debugLogs)
                Debug.Log($"[BreakableBox] Recebeu {damageAmount} of damage. health remaining: {_currentHealth}");

            if (_currentHealth <= 0f)
            {

                BreakBox(impactPoint);
            }
            else
            {
                PlayRandomSound(impactSoundDataList, impactPoint);
            }
        }

        private void BreakBox(Vector3 impactPoint)
        {
            if (_isBroken) return;
            _isBroken = true;

            if (impulseSource != null) impulseSource.GenerateImpulse();

            if (_lootDropper != null) _lootDropper.DropLoot();

            PlayRandomSound(breakSoundDataList, impactPoint);

            if (_boxCollider != null) _boxCollider.enabled = false;
            if (intactBox != null) intactBox.SetActive(false);

            if (destroyedBox != null)
            {
                destroyedBox.SetActive(true);
                ActivateFragments(impactPoint);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void PlayRandomSound(SoundData[] list, Vector3 position)
        {
            SoundData soundData = PickValidSoundData(list);
            if (soundData == null) return;

            SpawnOneShot(soundData, position);
        }

        private static SoundData PickValidSoundData(SoundData[] list)
        {
            if (list == null || list.Length == 0) return null;

            int start = Random.Range(0, list.Length);
            for (int i = 0; i < list.Length; i++)
            {
                SoundData sd = list[(start + i) % list.Length];
                if (sd != null && sd.GetClip() != null) return sd;
            }
            return null;
        }

        private void SpawnOneShot(SoundData soundData, Vector3 position)
        {
            GameObject sfx = new GameObject($"SFX_{soundData.soundName}");
            sfx.transform.position = position;

            AudioSource src = sfx.AddComponent<AudioSource>();
            src.clip = soundData.GetClip();
            src.volume = soundData.volume;
            src.pitch = soundData.GetPitch();
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = SfxMinDistance;
            src.maxDistance = SfxMaxDistance;

            src.Play();

            float pitch = Mathf.Max(0.01f, Mathf.Abs(src.pitch));
            Destroy(sfx, src.clip.length / pitch);
        }

        private void ActivateFragments(Vector3 impactPoint)
        {
            Rigidbody[] fragments = destroyedBox.GetComponentsInChildren<Rigidbody>(true);
            MeshCollider[] meshColliders = destroyedBox.GetComponentsInChildren<MeshCollider>(true);

            foreach (Rigidbody rb in fragments)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.AddExplosionForce(explosionForce, impactPoint, explosionRadius, upwardModifier, ForceMode.Impulse);
            }

            foreach (MeshCollider mc in meshColliders)
            {
                mc.enabled = true;
                mc.convex = true;
            }

            StartCoroutine(DisablePhysicsRoutine(fragments, meshColliders, physicsDelay));
            Destroy(gameObject, destroyDelay);
        }

        private IEnumerator DisablePhysicsRoutine(Rigidbody[] fragments, MeshCollider[] colliders, float delay)
        {
            yield return new WaitForSeconds(delay);

            foreach (Rigidbody rb in fragments)
            {
                if (rb == null) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            foreach (MeshCollider mc in colliders)
            {
                if (mc != null) mc.enabled = false;
            }
        }

        public void Break() => BreakBox(transform.position);

        public void BreakAt(Vector3 impactPoint) => BreakBox(impactPoint);

        public bool IsBroken() => _isBroken;
    }
}
