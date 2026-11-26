using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class PlayerWeaponEquipment : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Animation Overrides")]
        [SerializeField] private AnimatorOverrideController unarmedOverride;
        [SerializeField] private AnimatorOverrideController combatOverride;

        [Header("Weapon Sockets (Sword)")]
        [Tooltip("object 3D of the sword in the hand of the player.")]
        [SerializeField] private GameObject handSword;

        [Tooltip("object 3D of the sword in the cintura of the player (during climbing).")]
        [SerializeField] private GameObject hipSword;

        [Tooltip("point of origem is spawnar the sword when the dropar.")]
        [SerializeField] private Transform weaponDropPoint;

        [Header("Shield Sockets")]
        [Tooltip("object 3D of the escudo in the hand left.")]
        [SerializeField] private GameObject handShield;

        [Tooltip("object 3D of the escudo in the costas (during climbing).")]
        [SerializeField] private GameObject backShield;

        [Tooltip("point of origem is spawnar the escudo when the dropar.")]
        [SerializeField] private Transform shieldDropPoint;

        [Header("Drop Prefabs (only fallback)")]
        [Tooltip("only is instanciado if not houver the WeaponPickup apanhado is reaproveitar " +
                 "(ex.: Player that starts with the sword already equipada).")]
        [SerializeField] private GameObject swordPickupPrefab;
        [Tooltip("only is instanciado if not houver the WeaponPickup apanhado is reaproveitar.")]
        [SerializeField] private GameObject shieldPickupPrefab;
        [SerializeField] private float dropForce = 4f;

        [Header("Drop Rules")]
        [Tooltip("if true, largar the sword larga also the escudo.")]
        [SerializeField] private bool dropShieldWithSword = true;

        [Header("Equip Sounds")]
        [Tooltip("sounds tocados when the equipar the sword. the is sorteado aleatoriamente.")]
        [SerializeField] private SoundData[] swordEquipSounds;

        [Tooltip("sounds tocados when the equipar the escudo. the is sorteado aleatoriamente.")]
        [SerializeField] private SoundData[] shieldEquipSounds;

        #endregion

        #region Internal State

        private Animator animator;
        private PlayerBrain brain;

        private bool isWeaponEquipped = false;
        private bool isShieldEquipped = false;
        private bool isClimbing = false;

        private WeaponPickup heldSwordPickup;
        private WeaponPickup heldShieldPickup;

        #endregion

        #region Public Properties

        public bool IsWeaponEquipped => isWeaponEquipped;
        public bool IsShieldEquipped => isShieldEquipped;
        public bool IsClimbing => isClimbing;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            brain = GetComponent<PlayerBrain>();
            if (brain == null) brain = GetComponentInParent<PlayerBrain>();
        }

        private void Start()
        {
            UnequipWeapon();
            UnequipShield();
        }

        #endregion

        #region Held Pickups API

        public void RegisterHeldPickup(WeaponPickup.PickupType type, WeaponPickup pickup)
        {
            if (pickup == null) return;

            if (type == WeaponPickup.PickupType.Shield)
                ReplaceHeldPickup(ref heldShieldPickup, pickup);
            else
                ReplaceHeldPickup(ref heldSwordPickup, pickup);
        }

        private static void ReplaceHeldPickup(ref WeaponPickup slot, WeaponPickup incoming)
        {

            if (slot != null && slot != incoming)
                Destroy(slot.gameObject);

            slot = incoming;
        }

        #endregion

        #region Sword API

        public void EquipWeapon()
        {
            isWeaponEquipped = true;

            if (combatOverride != null && animator != null)
                animator.runtimeAnimatorController = combatOverride;

            UpdateWeaponVisuals();

            if (brain != null) brain.OnWeaponEquipped();

            PlayEquipSound(swordEquipSounds);
        }

        public void UnequipWeapon()
        {
            isWeaponEquipped = false;

            if (unarmedOverride != null && animator != null)
                animator.runtimeAnimatorController = unarmedOverride;

            UpdateWeaponVisuals();

            if (brain != null) brain.OnWeaponUnequipped();
        }

        public void DropWeapon()
        {
            if (isWeaponEquipped)
            {
                Transform origin = weaponDropPoint != null
                    ? weaponDropPoint
                    : (isClimbing && hipSword != null
                        ? hipSword.transform
                        : (handSword != null ? handSword.transform : transform));

                SpawnDrop(swordPickupPrefab, ref heldSwordPickup, origin, "sword");
                UnequipWeapon();
            }

            if (dropShieldWithSword && isShieldEquipped)
                DropShield();
        }

        #endregion

        #region Shield API

        public void EquipShield()
        {
            isShieldEquipped = true;
            UpdateWeaponVisuals();

            PlayEquipSound(shieldEquipSounds);

            if (brain != null) brain.OnShieldEquipped();
        }

        public void UnequipShield()
        {
            isShieldEquipped = false;
            UpdateWeaponVisuals();

            if (brain != null) brain.OnShieldUnequipped();

        }

        public void DropShield()
        {
            if (!isShieldEquipped) return;

            Transform origin = shieldDropPoint != null
                ? shieldDropPoint
                : (isClimbing && backShield != null
                    ? backShield.transform
                    : (handShield != null ? handShield.transform : transform));

            SpawnDrop(shieldPickupPrefab, ref heldShieldPickup, origin, "escudo");
            UnequipShield();
        }

        #endregion

        #region Climbing

        public void SetClimbing(bool climbing)
        {
            isClimbing = climbing;
            UpdateWeaponVisuals();
        }

        #endregion

        #region Visuals

        private void UpdateWeaponVisuals()
        {

            if (!isWeaponEquipped)
            {
                if (handSword != null) handSword.SetActive(false);
                if (hipSword != null) hipSword.SetActive(false);
            }
            else if (isClimbing)
            {
                if (handSword != null) handSword.SetActive(false);
                if (hipSword != null) hipSword.SetActive(true);
            }
            else
            {
                if (handSword != null) handSword.SetActive(true);
                if (hipSword != null) hipSword.SetActive(false);
            }

            if (!isShieldEquipped)
            {
                if (handShield != null) handShield.SetActive(false);
                if (backShield != null) backShield.SetActive(false);
            }
            else if (isClimbing)
            {
                if (handShield != null) handShield.SetActive(false);
                if (backShield != null) backShield.SetActive(true);
            }
            else
            {
                if (handShield != null) handShield.SetActive(true);
                if (backShield != null) backShield.SetActive(false);
            }
        }

        #endregion

        #region Drop Helper

        private void SpawnDrop(GameObject prefab, ref WeaponPickup heldPickup, Transform origin, string label)
        {
            Vector3 spawnPosition = origin.position;
            Quaternion spawnRotation = origin.rotation;

            Vector3 dropDirection = (Vector3.up * 1.5f)
                                    + (-transform.forward * 1f)
                                    + (transform.right * Random.Range(-0.5f, 0.5f));
            Vector3 impulse = dropDirection * dropForce;

            if (heldPickup != null)
            {
                WeaponPickup reused = heldPickup;
                heldPickup = null;

                reused.ReleaseFromPlayer(spawnPosition, spawnRotation, impulse, ForceMode.Impulse);

                return;
            }

            if (prefab == null)
            {
                return;
            }

            GameObject dropped = Instantiate(prefab, spawnPosition, spawnRotation);

            if (dropped.TryGetComponent<WeaponPickup>(out var pickup))
            {
                pickup.ThrowWithPhysics(impulse, ForceMode.Impulse);
            }
            else if (dropped.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.AddForce(impulse, ForceMode.Impulse);
            }

        }

        #endregion

        #region Sound

        private void PlayEquipSound(SoundData[] sounds)
        {
            if (sounds == null || sounds.Length == 0)
            {
                return;
            }

            SoundData data = sounds[Random.Range(0, sounds.Length)];
            if (data == null || data.GetClip() == null)
            {
                return;
            }

            GameObject sfx = new GameObject($"SFX_Equip_{data.soundName}");
            sfx.transform.position = transform.position;

            AudioSource src = sfx.AddComponent<AudioSource>();
            src.clip = data.GetClip();
            src.volume = data.volume;
            src.pitch = data.GetPitch();
            src.priority = data.priority;
            src.outputAudioMixerGroup = data.mixerGroup;
            src.spatialBlend = 1f;
            src.minDistance = data.minDistance;
            src.maxDistance = data.maxDistance;
            src.Play();

            float life = data.GetClip().length / Mathf.Max(0.1f, Mathf.Abs(src.pitch));
            Destroy(sfx, life + 0.1f);
        }

        #endregion
    }
}
