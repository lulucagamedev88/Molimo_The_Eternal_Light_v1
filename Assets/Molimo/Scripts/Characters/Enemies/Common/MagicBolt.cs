using UnityEngine;

using Unity.Cinemachine;

namespace MolimoByLulucaGameDev
{

    [DisallowMultipleComponent]
    public class MagicBolt : MonoBehaviour
    {
        [Header("movement")]
        [SerializeField] private float speed = 8f;
        [Tooltip("distance maximum percorrida before if auto-explodir (without damage).")]
        [SerializeField] private float maxDistance = 25f;

        [Header("Timer Anti-accumulation")]
        [Tooltip("time maximum of health in seconds.")]
        [SerializeField] private float maxLifetime = 5f;

        [Header("damage")]
        [SerializeField] private float damage = 15f;

        [Header("collision by Layer")]
        [Tooltip("the bola only explode when the play in colliders cujo GameObject (or some parent) esteja nestas Layers. ~0 = Tudo.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("VFX of explosion")]
        [SerializeField] private GameObject impactVfxPrefab;
        [SerializeField] private float impactVfxLifetime = 3f;

        [Header("sound continuous (hum in flight)")]
        [Tooltip("Clip of sound that toca in loop while the bola voa. " +
                 "Idealmente the clip curto (0.5–2s) that faca loop without 'click'.")]
        [SerializeField] private AudioClip continuousSound;
        [Range(0f, 1f)]
        [SerializeField] private float continuousVolume = 0.6f;
        [Tooltip("Pitch base of the hum.")]
        [SerializeField] private float continuousPitch = 1f;
        [Tooltip("variation random of pitch (min, max) is dar variedade between bolas. " +
                 "Ex: (0.9, 1.1). sets (1,1) is disable.")]
        [SerializeField] private Vector2 continuousPitchVariation = new Vector2(0.9f, 1.1f);
        [Tooltip("0 = sound 2D (always audible, bom is retro), 1 = sound 3D (posicional).")]
        [Range(0f, 1f)]
        [SerializeField] private float continuousSpatialBlend = 1f;

        [Header("sound of explosion")]
        [SerializeField] private AudioClip impactSound;
        [Range(0f, 1f)]
        [SerializeField] private float impactVolume = 1f;

        [Header("camera Shake")]
        [SerializeField] private bool shakeOnImpact = true;
        [SerializeField] private bool shakeOnlyOnCollision = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private Rigidbody rb;
        private Vector3 direction;
        private Vector3 startPosition;
        private float lifeTimer;
        private bool initialized;
        private bool destroyed;
        private bool useVelocityMode;

        private CinemachineImpulseSource impulseSource;
        private AudioSource continuousAudioSource;

        public bool IsInitialized => initialized;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError("[MagicBolt] Preciso of the Rigidbody (kinematic or dynamic).", this);
                enabled = false;
                return;
            }

            impulseSource = GetComponent<CinemachineImpulseSource>();

            continuousAudioSource = GetComponent<AudioSource>();
            if (continuousAudioSource == null)
            {
                continuousAudioSource = gameObject.AddComponent<AudioSource>();
            }

            continuousAudioSource.playOnAwake = false;
            continuousAudioSource.loop = true;
            continuousAudioSource.spatialBlend = continuousSpatialBlend;
            continuousAudioSource.volume = continuousVolume;

            continuousAudioSource.rolloffMode = AudioRolloffMode.Linear;
            continuousAudioSource.minDistance = 1f;
            continuousAudioSource.maxDistance = 25f;
        }

        public void Initialize(Vector3 dir, float speedOverride = -1f, float damageOverride = -1f)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            direction = dir.normalized;

            if (speedOverride > 0f) speed = speedOverride;
            if (damageOverride > 0f) damage = damageOverride;

            startPosition = transform.position;
            lifeTimer = 0f;
            initialized = true;
            destroyed = false;

            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            if (rb != null)
            {
                if (rb.isKinematic)
                {
                    useVelocityMode = false;
                    if (debugLogs) Debug.Log("[MagicBolt] Modo: Kinematic (MovePosition).", this);
                }
                else
                {
                    useVelocityMode = true;
                    rb.linearVelocity = direction * speed;
                    rb.angularVelocity = Vector3.zero;
                    if (debugLogs)
                        Debug.Log($"[MagicBolt] Modo: Dynamic (velocity={rb.linearVelocity}).", this);
                }
            }

            StartContinuousSound();

            if (debugLogs)
                Debug.Log($"[MagicBolt] Init: dir={direction}, speed={speed}, dmg={damage}, " +
                          $"life={maxLifetime}s, maxDist={maxDistance}", this);
        }

        private void StartContinuousSound()
        {
            if (continuousAudioSource == null || continuousSound == null) return;

            continuousAudioSource.clip = continuousSound;
            continuousAudioSource.volume = continuousVolume;
            continuousAudioSource.pitch = continuousPitch *
                Random.Range(continuousPitchVariation.x, continuousPitchVariation.y);
            continuousAudioSource.Play();
        }

        private void StopContinuousSound()
        {
            if (continuousAudioSource != null && continuousAudioSource.isPlaying)
            {
                continuousAudioSource.Stop();
            }
        }

        private void FixedUpdate()
        {
            if (!initialized || destroyed || rb == null) return;

            lifeTimer += Time.fixedDeltaTime;
            if (lifeTimer >= maxLifetime)
            {
                if (debugLogs)
                    Debug.Log($"[MagicBolt] Expirou by time ({maxLifetime}s) — auto-destroyed.", this);

                Explode(realImpact: false);
                return;
            }

            if (useVelocityMode)
            {
                if ((rb.position - startPosition).sqrMagnitude >= maxDistance * maxDistance)
                {
                    Explode(realImpact: false);
                }
            }
            else
            {
                Vector3 nextPos = rb.position + direction * speed * Time.fixedDeltaTime;
                if ((nextPos - startPosition).sqrMagnitude >= maxDistance * maxDistance)
                {
                    Explode(realImpact: false);
                    return;
                }
                rb.MovePosition(nextPos);
            }
        }

        private void OnTriggerEnter(Collider other) => HandleHit(other);
        private void OnCollisionEnter(Collision collision) => HandleHit(collision.collider);

        private void HandleHit(Collider other)
        {
            if (!initialized || destroyed || other == null) return;

            if (other.GetComponentInParent<WitchEye>() != null) return;
            if (other.GetComponentInParent<MagicBolt>() != null) return;

            if (!IsInHitLayers(other))
            {
                if (debugLogs)
                    Debug.Log($"[MagicBolt] ignored: '{other.name}' (layer {other.gameObject.layer}) " +
                              $"outside of hitLayers.", this);
                return;
            }

            PlayerBrain playerBrain = other.GetComponentInParent<PlayerBrain>();
            if (playerBrain != null && playerBrain.IsAlive)
            {
                PlayerHealth h = playerBrain.GetHealthState();
                if (h != null)
                {
                    h.TakeDamage(damage);
                    if (debugLogs)
                        Debug.Log($"[MagicBolt] damage {damage} applied when the Player.", this);
                }
                else
                {
                    Debug.LogWarning("[MagicBolt] Player found but Health == null — damage not applied.", this);
                }
            }

            Explode(realImpact: true);
        }

        private bool IsInHitLayers(Collider other)
        {
            Transform t = other.transform;
            while (t != null)
            {
                if (((1 << t.gameObject.layer) & hitLayers) != 0)
                    return true;
                t = t.parent;
            }
            return false;
        }

        public void Explode(bool realImpact = true)
        {
            if (destroyed) return;
            destroyed = true;

            StopContinuousSound();

            bool shouldShake = shakeOnImpact
                            && impulseSource != null
                            && (realImpact || !shakeOnlyOnCollision);

            if (shouldShake) impulseSource.GenerateImpulse();

            if (impactVfxPrefab != null)
            {
                GameObject vfx = Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
                if (impactVfxLifetime > 0f) Destroy(vfx, impactVfxLifetime);
            }

            if (impactSound != null)
                AudioSource.PlayClipAtPoint(impactSound, transform.position, impactVolume);

            if (debugLogs)
                Debug.Log($"[MagicBolt] Explodiu (realImpact={realImpact}).", this);

            Destroy(gameObject);
        }
    }
}
