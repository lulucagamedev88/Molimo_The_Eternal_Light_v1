using System.Collections;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]

    public class MolimoCollectableHP : MonoBehaviour
    {
        #region Enums

        public enum OscillationMode
        {
            None,
            Wasp,
            RandomFloat
        }

        public enum ItemState
        {
            Idle,
            Attracted,
            Collected
        }

        #endregion

        #region Inspector Fields

        [Header("healing")]
        [SerializeField, Range(1, 100)] private int healCharge = 1;

        [Header("movement")]
        [SerializeField] private float defaultPullSpeed = 8f;
        [SerializeField] private float arrivalDistance = 0.4f;
        [SerializeField] private float minHeightOffset = 0.15f;
        [SerializeField] private OscillationMode oscillationMode = OscillationMode.Wasp;

        [Header("oscillation - Wasp")]
        [SerializeField] private float waspFrequency = 12f;
        [SerializeField] private float waspAmplitude = 0.4f;

        [Header("oscillation - RandomFloat")]
        [SerializeField] private float randomMagnitude = 0.8f;
        [SerializeField] private float randomFrequency = 2f;

        [Header("rotation")]
        [SerializeField] private bool addRotation = true;
        [SerializeField] private float rotationSpeed = 180f;

        [Header("sound")]
        [SerializeField] private SoundData[] attractSoundDataList;
        [SerializeField, Range(0.05f, 2f)] private float fadeDuration = 0.3f;

        #endregion

        #region Public Properties

        public ItemState State { get; private set; } = ItemState.Idle;
        public int HealCharge => healCharge;
        public bool IsAttracted => State == ItemState.Attracted;

        #endregion

        #region Internal State

        private Transform _target;
        private MolimoHealthContainer _targetContainer;
        private float _pullSpeed;
        private float _seed;

        private GameObject _activeSfxObject;
        private AudioSource _activeAudioSource;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            if (State != ItemState.Attracted) return;
            if (_target == null)
            {
                State = ItemState.Idle;
                return;
            }

            MoveTowardsTarget();
            CheckArrival();
        }

        private void OnDestroy()
        {
            if (_activeSfxObject != null)
                Destroy(_activeSfxObject);
        }

        #endregion

        #region Public API

        public void AttractTo(Transform target, float speed = -1f)
        {
            if (State != ItemState.Idle) return;
            if (target == null) return;

            State = ItemState.Attracted;
            _target = target;
            _pullSpeed = speed > 0f ? speed : defaultPullSpeed;

            _targetContainer = target.GetComponentInParent<MolimoHealthContainer>();

            if (TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
            if (TryGetComponent(out Collider col)) col.isTrigger = true;

            if (TryGetComponent(out ItemIdleAnimation idleAnim))
                Destroy(idleAnim);

            PlayAttractSound();
        }

        #endregion

        #region PlayerMovement

        private void MoveTowardsTarget()
        {
            Vector3 currentPos = transform.position;
            Vector3 targetPos = _target.position;

            Vector3 nextPos = Vector3.MoveTowards(
                currentPos, targetPos, _pullSpeed * Time.deltaTime);

            nextPos += ComputeOscillation(currentPos, targetPos);

            float minY = targetPos.y + minHeightOffset;
            if (nextPos.y < minY) nextPos.y = minY;

            transform.position = nextPos;

            if (addRotation)
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }

        private Vector3 ComputeOscillation(Vector3 currentPos, Vector3 targetPos)
        {
            switch (oscillationMode)
            {
                case OscillationMode.Wasp:
                    Vector3 dir = (targetPos - currentPos).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                    if (side == Vector3.zero) side = Vector3.right;

                    float tW = Time.time * waspFrequency + _seed;
                    float sideVel = Mathf.Cos(tW) * waspFrequency * waspAmplitude * Time.deltaTime;
                    float upVel = Mathf.Cos(tW * 1.3f) * (waspFrequency * 1.3f) * waspAmplitude * Time.deltaTime;
                    return (side * sideVel) + (Vector3.up * upVel);

                case OscillationMode.RandomFloat:
                    float tF = Time.time * randomFrequency + _seed;
                    float fx = Mathf.Cos(tF) * randomFrequency * randomMagnitude * Time.deltaTime;
                    float fy = Mathf.Cos(tF * 1.3f + 50f) * (randomFrequency * 1.3f) * randomMagnitude * Time.deltaTime;
                    float fz = Mathf.Cos(tF * 0.7f + 100f) * (randomFrequency * 0.7f) * randomMagnitude * Time.deltaTime;
                    return new Vector3(fx, fy, fz);

                case OscillationMode.None:
                default:
                    return Vector3.zero;
            }
        }

        private void CheckArrival()
        {
            if (Vector3.Distance(transform.position, _target.position) > arrivalDistance) return;
            DepositAndDestroy();
        }

        #endregion

        #region Collection

        private void DepositAndDestroy()
        {
            if (State == ItemState.Collected) return;
            State = ItemState.Collected;

            if (_targetContainer != null)
            {
                _targetContainer.TryAddHP(healCharge);
            }
            else
            {
                var container = _target != null
                    ? _target.GetComponentInParent<MolimoHealthContainer>()
                    : null;

                if (container != null)
                    container.TryAddHP(healCharge);
            }

            StopAttractSound();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        #endregion

        #region Audio

        private void PlayAttractSound()
        {
            if (attractSoundDataList == null || attractSoundDataList.Length == 0) return;

            int index = Random.Range(0, attractSoundDataList.Length);
            SoundData data = attractSoundDataList[index];
            if (data == null || data.GetClip() == null) return;

            _activeSfxObject = new GameObject($"SFX_{data.soundName}");
            _activeSfxObject.transform.position = transform.position;

            _activeAudioSource = _activeSfxObject.AddComponent<AudioSource>();
            _activeAudioSource.clip = data.GetClip();
            _activeAudioSource.volume = data.volume;
            _activeAudioSource.pitch = data.GetPitch();
            _activeAudioSource.spatialBlend = 1.0f;
            _activeAudioSource.minDistance = data.minDistance;
            _activeAudioSource.maxDistance = data.maxDistance;
            _activeAudioSource.Play();
        }

        private void StopAttractSound()
        {
            if (_activeAudioSource == null || _activeSfxObject == null) return;

            var cleaner = _activeSfxObject.AddComponent<AudioSourceAutoCleaner>();
            cleaner.FadeAndDestroy(_activeAudioSource, fadeDuration);

            _activeSfxObject = null;
            _activeAudioSource = null;
        }

        #endregion
    }

    internal class AudioSourceAutoCleaner : MonoBehaviour
    {

        public void FadeAndDestroy(AudioSource source, float duration)
        {
            StartCoroutine(FadeRoutine(source, duration));
        }

        private IEnumerator FadeRoutine(AudioSource source, float duration)
        {
            if (source == null)
            {
                Destroy(gameObject);
                yield break;
            }

            float startVolume = source.volume;
            float timer = 0f;

            while (timer < duration && source != null)
            {
                timer += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, timer / duration);
                yield return null;
            }

            if (source != null) source.Stop();
            Destroy(gameObject);
        }
    }
}
