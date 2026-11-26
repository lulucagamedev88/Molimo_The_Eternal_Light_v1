using System;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public enum AmbushState
    {
        Hidden,
        Running,
        Finished
    }

    public class BushEnemy : MonoBehaviour, IDamageable
    {
        #region Fields & Inspector Properties

        [Header("Movement & Direction Settings")]
        [Tooltip("speed of the corrida of the is B.")]
        [SerializeField] private float runSpeed = 7f;

        [Tooltip("distance total percorrida in the axis X before if hide.")]
        [SerializeField] private float runDistance = 8f;

        [Tooltip("direction of the movement in the axis X: -1 is left (-X), 1 is right (+X).")]
        [SerializeField] private float directionX = -1f;

        [Tooltip("if true, chooses the direction X automatically is correr in the direction of the player.")]
        [SerializeField] private bool autoFacePlayerOnTrigger = false;

        [Header("Player Detection & Trigger")]
        [Tooltip("Transform of the player. Auto-detected via the 'Player' tag if null.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("distance horizontal/radius is enable the emboscada when the player if approach.")]
        [SerializeField] private float triggerRadius = 4f;

        [Header("Damage Output Settings")]
        [Tooltip("percentage of the health maximum of the player tirada when the colidir (0.15 = 15%).")]
        [SerializeField] private float damagePercentage = 0.15f;

        [Tooltip("if true, the enemy desaparece imediatamente after acertar the Player.")]
        [SerializeField] private bool hideOnDamagePlayer = true;

        [Header("Visuals, FX & Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualTransform;
        [SerializeField] private GameObject puffVFXPrefab;
        [SerializeField] private Transform vfxContainer;

        [Header("Audio Settings (SoundData)")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("sound tocado in the momento exact in that it exits of the arbusto.")]
        [SerializeField] private SoundData[] ambushSounds;
        [Tooltip("sounds of passos/corrida.")]
        [SerializeField] private SoundData[] runSounds;
        [SerializeField] private float stepSoundInterval = 0.2f;

        [Header("Animator Parameters")]
        [SerializeField] private string moveBoolParameter = "Move";
        [SerializeField] private string ambushTriggerParameter = "Ambush";

        [Header("State Monitor")]
        [SerializeField] private AmbushState currentState = AmbushState.Hidden;

        #endregion

        #region Private Variables

        private Vector3 startPosition;
        private float distanceTraveled = 0f;
        private float stepSoundTimer = 0f;
        private Collider enemyCollider;
        private bool hasDealtDamage = false;

        #endregion

        #region IDamageable Properties

        public float CurrentHealth => currentState == AmbushState.Finished ? 0f : 1f;
        public float MaxHealth => 1f;
        public bool IsAlive => currentState != AmbushState.Finished;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            FindPlayerReference();
            startPosition = transform.position;

            directionX = Mathf.Sign(directionX);
            if (directionX == 0) directionX = -1f;

            UpdateVisualOrientation(directionX);
        }

        private void Update()
        {
            switch (currentState)
            {
                case AmbushState.Hidden:
                    CheckPlayerTrigger();
                    break;

                case AmbushState.Running:
                    ExecuteRun();
                    HandleRunAudio();
                    break;

                case AmbushState.Finished:

                    break;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (currentState != AmbushState.Running || hasDealtDamage) return;

            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerHealth>() != null)
            {
                TryDamagePlayer(other.gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (currentState != AmbushState.Running || hasDealtDamage) return;

            if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponentInParent<PlayerHealth>() != null)
            {
                TryDamagePlayer(collision.gameObject);
            }
        }

        #endregion

        #region Initialization Helpers

        private void InitializeComponents()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (visualTransform == null && transform.childCount > 0)
            {
                visualTransform = transform.GetChild(0);
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 1.0f;
                }
            }

            enemyCollider = GetComponent<Collider>();
        }

        private void FindPlayerReference()
        {
            if (playerTransform == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    playerTransform = playerObj.transform;
                }
            }
        }

        #endregion

        #region Ambush & PlayerMovement Logic

        private void CheckPlayerTrigger()
        {
            if (playerTransform == null) return;

            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            if (distanceToPlayer <= triggerRadius)
            {
                StartAmbush();
            }
        }

        private void StartAmbush()
        {
            currentState = AmbushState.Running;

            if (autoFacePlayerOnTrigger && playerTransform != null)
            {
                float dirToPlayer = playerTransform.position.x - transform.position.x;
                directionX = Mathf.Sign(dirToPlayer);
                if (directionX == 0) directionX = -1f;
            }

            UpdateVisualOrientation(directionX);

            SetAnimatorTrigger(ambushTriggerParameter);
            SetAnimatorBool(moveBoolParameter, true);

            PlayRandomSound(ambushSounds);
        }

        private void ExecuteRun()
        {
            float moveStep = runSpeed * Time.deltaTime;
            Vector3 movement = new Vector3(directionX * moveStep, 0f, 0f);

            transform.position += movement;
            distanceTraveled += moveStep;

            if (distanceTraveled >= runDistance)
            {
                FinishAmbushAndHide();
            }
        }

        private void TryDamagePlayer(GameObject target)
        {
            PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
            {
                playerHealth = target.GetComponent<PlayerHealth>();
            }

            if (playerHealth != null)
            {
                float damageAmount = playerHealth.MaxHealth * damagePercentage;
                playerHealth.TakeDamage(damageAmount);
                hasDealtDamage = true;

                if (hideOnDamagePlayer)
                {
                    FinishAmbushAndHide();
                }
            }
        }

        public void TakeDamage(float amount, float knockbackDistance = 0f)
        {
            if (currentState == AmbushState.Finished) return;
            FinishAmbushAndHide();
        }

        private void FinishAmbushAndHide()
        {
            currentState = AmbushState.Finished;

            SetAnimatorBool(moveBoolParameter, false);

            if (puffVFXPrefab != null)
            {
                Instantiate(puffVFXPrefab, transform.position, Quaternion.identity, vfxContainer);
            }

            if (visualTransform != null)
            {
                visualTransform.gameObject.SetActive(false);
            }

            if (enemyCollider != null)
            {
                enemyCollider.enabled = false;
            }

            gameObject.SetActive(false);
        }

        #endregion

        #region Audio System

        private void HandleRunAudio()
        {
            stepSoundTimer += Time.deltaTime;

            if (stepSoundTimer >= stepSoundInterval)
            {
                stepSoundTimer = 0f;
                PlayRandomSound(runSounds);
            }
        }

        private void PlayRandomSound(SoundData[] sounds)
        {
            if (sounds == null || sounds.Length == 0) return;

            SoundData selectedSound = sounds[UnityEngine.Random.Range(0, sounds.Length)];
            if (selectedSound == null || selectedSound.clip == null) return;

            if (audioSource != null)
            {
                audioSource.pitch = selectedSound.GetPitch();
                audioSource.minDistance = selectedSound.minDistance;
                audioSource.maxDistance = selectedSound.maxDistance;
                audioSource.PlayOneShot(selectedSound.clip, selectedSound.volume);
            }
        }

        #endregion

        #region Helpers & Orientation

        private void UpdateVisualOrientation(float dirX)
        {
            if (visualTransform != null && Mathf.Abs(dirX) > 0.01f)
            {
                Vector3 currentScale = visualTransform.localScale;
                currentScale.x = (dirX > 0f) ? -Mathf.Abs(currentScale.x) : Mathf.Abs(currentScale.x);
                visualTransform.localScale = currentScale;
            }
        }

        private void SetAnimatorBool(string paramName, bool value)
        {
            if (animator != null && !string.IsNullOrEmpty(paramName))
            {
                animator.SetBool(paramName, value);
            }
        }

        private void SetAnimatorTrigger(string paramName)
        {
            if (animator != null && !string.IsNullOrEmpty(paramName))
            {
                animator.SetTrigger(paramName);
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, triggerRadius);

            Gizmos.color = Color.red;
            Vector3 startPos = Application.isPlaying ? startPosition : transform.position;
            Vector3 endPos = startPos + new Vector3(directionX * runDistance, 0f, 0f);

            Gizmos.DrawLine(startPos, endPos);
            Gizmos.DrawWireSphere(endPos, 0.3f);
        }

        #endregion
    }
}
