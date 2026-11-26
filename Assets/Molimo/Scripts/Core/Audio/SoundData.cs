using UnityEngine;
using UnityEngine.Audio;

namespace MolimoByLulucaGameDev
{
    [CreateAssetMenu(fileName = "NewSound", menuName = "Molimo/Sound Data")]

    public class SoundData : ScriptableObject
    {
        [Header("identification")]
        public string soundName;

        [Header("Clip")]
        public AudioClip clip;

        [Header("Volume & Pitch")]
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Range(0f, 0.5f)] public float pitchRandomness = 0.1f;

        [Header("playback")]
        public bool loop = false;
        [Range(0, 256)] public int priority = 128;

        [Header("Mixer")]
        public AudioMixerGroup mixerGroup;

        [Header("spatialization")]
        [Range(0f, 1f)] public float spatialBlend = 0f;
        [Min(0f)] public float minDistance = 1f;
        [Min(0f)] public float maxDistance = 50f;

        [Header("Pitch Descend (optional)")]
        public bool enablePitchDescend = false;
        public float descendStartPitch = 1.8f;
        public float descendEndPitch = 0.15f;
        public float descendDuration = 3.5f;
        public AnimationCurve descendPitchCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public float descendFadeOutDuration = 0.8f;
        public bool descendUseLowPass = true;
        public float descendLowPassStart = 18000f;
        public float descendLowPassEnd = 450f;

        public AudioClip GetClip() => clip;

        public float GetVolume() => volume;

        public float GetPitch()
        {
            float p = pitch + Random.Range(-pitchRandomness, pitchRandomness);
            return Mathf.Max(0.01f, p);
        }

        private void OnValidate()
        {
            if (maxDistance < minDistance) maxDistance = minDistance;
            if (string.IsNullOrEmpty(soundName)) soundName = name;
        }
    }
}
