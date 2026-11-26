using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]

    public class ThrowableItem : MonoBehaviour
    {
        [Header("settings of damage")]
        [SerializeField] private float impactDamage = 10f;
        [SerializeField] private float impactForce = 5f;

        [Header("settings of audio")]
        [Tooltip("sound tocado when the acertar the target.")]
        [SerializeField] private SoundData impactSound;

        [Header("Ciclo of health")]
        [SerializeField] private float lifetime = 5f;

        private bool _canDamage = false;
        private float _lifetimeTimer;

        private void OnEnable()
        {
            _canDamage = false;
            _lifetimeTimer = lifetime;
        }

        private void Update()
        {
            if (!_canDamage) return;

            _lifetimeTimer -= Time.deltaTime;
            if (_lifetimeTimer <= 0f)
            {
                Destroy(gameObject);
            }
        }

        public void ActivateDamage() => _canDamage = true;

        public void DeactivateDamage() => _canDamage = false;

        private void OnCollisionEnter(Collision collision)
        {
            if (!_canDamage) return;

            if (collision.gameObject.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                damageable.TakeDamage(impactDamage);

                if (collision.rigidbody != null)
                {
                    Vector3 pushDir = collision.contacts[0].normal * -1f;
                    collision.rigidbody.AddForce(pushDir * impactForce, ForceMode.Impulse);
                }

                if (impactSound != null && impactSound.clip != null)
                {
                    GameObject soundGO = new GameObject("TempImpactSFX");
                    soundGO.transform.position = transform.position;

                    AudioSource source = soundGO.AddComponent<AudioSource>();
                    source.clip = impactSound.clip;
                    source.volume = impactSound.volume;
                    source.pitch = impactSound.GetPitch();
                    source.spatialBlend = 1f;
                    source.minDistance = impactSound.minDistance;
                    source.maxDistance = impactSound.maxDistance;

                    source.Play();

                    Destroy(soundGO, impactSound.clip.length);
                }

                Destroy(gameObject);
            }
        }
    }
}
