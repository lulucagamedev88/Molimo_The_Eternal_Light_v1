using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(AudioSource))]

    public class PlayerSoundController : MonoBehaviour
    {
        [Header("organization of the hierarchy")]
        [Tooltip("drag the GameObject 'Player_SFX'. if empty, is localizado/created automatically.")]
        [SerializeField] private Transform sfxContainer;

        [Header("references")]
        [Tooltip("if empty, is searched automatically neste object, in the parents or in the children.")]
        [SerializeField] private PlayerHealth health;

        [Header("detection of surface")]
        [SerializeField] private LayerMask grassLayerMask;
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private float groundCheckRadius = 0.3f;

        [Header("locomotion")]
        [SerializeField] private SoundData[] stepSounds;
        [SerializeField] private SoundData[] stepSoundsGrass;
        [SerializeField] private SoundData[] jumpSounds;
        [SerializeField] private SoundData[] rollSounds;
        [SerializeField] private SoundData[] climbSounds;

        [Header("combat")]
        [SerializeField] private SoundData[] hurtSounds;

        [Tooltip("sounds specific of Kip Up (levantar after knockdown). if empty, cai automatically is hurtSounds.")]
        [SerializeField] private SoundData[] kipUpSounds;

        [SerializeField] private SoundData[] blockSounds;
        [SerializeField] private SoundData[] deathSounds;
        [SerializeField] private SoundData[] respawnSounds;

        [Tooltip("Tocado in the start of each healing: key Heal or rescue critical of Molimo (evento Health.OnHealingStarted).")]
        [SerializeField] private SoundData[] healSounds;

        [Header("settings Gerais")]
        [Tooltip("uses the pool 3D (recommended). turn off only if quiser PlayOneShot 2D.")]
        [SerializeField] private bool use3DOneShotObjects = true;

        [Tooltip("Carrega clips in memory + warmup in the Start (elimina hitch of the 1.º Play).")]
        [SerializeField] private bool preloadClipsOnStart = true;

        [Header("Debug")]
        [Tooltip("Logs each played category (useful for diagnosing AnimationEvents).")]
        [SerializeField] private bool logPlayedCategories = false;

        private AudioSource _audioSource;
        private SFXPool _sfxPool;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            FindHealthIfNeeded();
            EnsureSFXContainer();
            EnsureSFXPool();
        }

        private void OnEnable()
        {
            FindHealthIfNeeded();
            if (health != null)
            {

                health.OnHealingStarted -= HandleHealingStarted;
                health.OnHealingStarted += HandleHealingStarted;
            }
        }

        private void OnDisable()
        {
            if (health != null) health.OnHealingStarted -= HandleHealingStarted;
        }

        private void Start()
        {
            if (!preloadClipsOnStart) return;
            PreloadAllClips();
            WarmupAllClips();
        }

        private void FindHealthIfNeeded()
        {
            if (health != null) return;
            health = GetComponentInParent<PlayerHealth>();
            if (health == null) health = GetComponentInChildren<PlayerHealth>();
        }

        private void HandleHealingStarted()
        {
            if (logPlayedCategories)
                Debug.Log("[PlayerSoundController] Heal → start of healing.");

            PlayRandomSoundFromList(healSounds);
        }

        private void EnsureSFXContainer()
        {
            if (sfxContainer != null) return;
            Transform existing = transform.Find("Player_SFX");
            if (existing != null) sfxContainer = existing;
            else
            {
                var go = new GameObject("Player_SFX");
                go.transform.SetParent(transform, false);
                sfxContainer = go.transform;
            }
        }

        private void EnsureSFXPool()
        {
            if (_sfxPool != null) return;
            EnsureSFXContainer();
            _sfxPool = SFXPool.GetOrCreate(sfxContainer);
        }

        private void PreloadAllClips()
        {
            SFXPool.Preload(
                stepSounds, stepSoundsGrass, jumpSounds, rollSounds, climbSounds,
                hurtSounds, kipUpSounds,
                blockSounds, deathSounds, respawnSounds, healSounds);
        }

        private void WarmupAllClips()
        {
            EnsureSFXPool();
            if (_sfxPool == null) return;

            _sfxPool.Warmup(
                stepSounds, stepSoundsGrass, jumpSounds, rollSounds, climbSounds,
                hurtSounds, kipUpSounds,
                blockSounds, deathSounds, respawnSounds, healSounds);
        }

        public void PlaySoundCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return;
            string key = categoryName.ToLower().Trim();

            if (logPlayedCategories)
                Debug.Log($"[PlayerSoundController] PlaySoundCategory → '{key}'");

            switch (key)
            {

                case "step": case "stepsounds": HandleStepSound(); break;
                case "stepgrass":
                case "stepsoundsgrass":
                case "grassstep":
                    PlayRandomSoundFromList(stepSoundsGrass); break;
                case "jump": case "jumpsounds": PlayRandomSoundFromList(jumpSounds); break;
                case "roll": case "rollsounds": PlayRandomSoundFromList(rollSounds); break;
                case "climb": case "climbsounds": PlayRandomSoundFromList(climbSounds); break;

                case "hurt": case "hurtsounds": PlayRandomSoundFromList(hurtSounds); break;
                case "kipup": case "kipupsounds": HandleKipUpSound(); break;
                case "block": case "blocksounds": PlayRandomSoundFromList(blockSounds); break;
                case "death": case "deathsounds": PlayRandomSoundFromList(deathSounds); break;
                case "respawn": case "respawnsounds": PlayRandomSoundFromList(respawnSounds); break;
                case "heal": case "healsounds": PlayRandomSoundFromList(healSounds); break;

                default:
                    Debug.LogWarning($"[PlayerSoundController] category '{categoryName}' not encontrada! " +
                                     $"categories valid: step, stepgrass, jump, roll, climb, hurt, kipup, block, death, respawn, heal.");
                    break;
            }
        }

        #region logic Interna
        private void HandleStepSound()
        {
            if (IsOnGrass() && stepSoundsGrass != null && stepSoundsGrass.Length > 0)
                PlayRandomSoundFromList(stepSoundsGrass);
            else
                PlayRandomSoundFromList(stepSounds);
        }

        private void HandleKipUpSound()
        {

            if (kipUpSounds != null && kipUpSounds.Length > 0)
            {
                if (logPlayedCategories)
                    Debug.Log($"[PlayerSoundController] KipUp: using kipUpSounds ({kipUpSounds.Length} clips).");
                PlayRandomSoundFromList(kipUpSounds);
            }
            else
            {
                if (logPlayedCategories)
                    Debug.Log($"[PlayerSoundController] KipUp: kipUpSounds empty — fallback is hurtSounds ({hurtSounds?.Length ?? 0} clips).");
                PlayRandomSoundFromList(hurtSounds);
            }
        }

        private bool IsOnGrass()
        {
            Vector3 pos = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
            return Physics.CheckSphere(pos, groundCheckRadius, grassLayerMask);
        }

        private void PlayRandomSoundFromList(SoundData[] arr)
        {
            if (arr == null || arr.Length == 0)
            {
                if (logPlayedCategories)
                    Debug.LogWarning("[PlayerSoundController] PlayRandomSoundFromList: array is empty or null — no sound played.");
                return;
            }

            ExecuteSoundData(arr[Random.Range(0, arr.Length)]);
        }

        private void ExecuteSoundData(SoundData soundData)
        {
            if (soundData == null)
            {
                if (logPlayedCategories)
                    Debug.LogWarning("[PlayerSoundController] ExecuteSoundData: SoundData is null.");
                return;
            }

            if (soundData.GetClip() == null)
            {
                if (logPlayedCategories)
                    Debug.LogWarning($"[PlayerSoundController] ExecuteSoundData: '{soundData.name}' not has clip assigned.");
                return;
            }

            if (use3DOneShotObjects)
            {
                EnsureSFXPool();
                if (_sfxPool != null) _sfxPool.Play(soundData, transform.position);
            }
            else
            {
                _audioSource.pitch = soundData.GetPitch();
                _audioSource.PlayOneShot(soundData.GetClip(), soundData.volume);
            }
        }
        #endregion

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 pos = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
            Gizmos.DrawWireSphere(pos, groundCheckRadius);
        }
    }
}
