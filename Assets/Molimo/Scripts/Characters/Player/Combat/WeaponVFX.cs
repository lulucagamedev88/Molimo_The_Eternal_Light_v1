using UnityEngine;
using UnityEngine.Serialization;

namespace MolimoByLulucaGameDev
{

    public class WeaponVFX : MonoBehaviour
    {

        public enum VFXOrientation
        {
            Horizontal,
            Vertical
        }

        [Header("SwordTrail")]
        [FormerlySerializedAs("particleSystem")]
        [SerializeField] private ParticleSystem swordTrail;

        [Header("Impact")]
        [SerializeField] private ParticleSystem impact;

        [Header("Orientation")]
        [SerializeField] private VFXOrientation defaultOrientation = VFXOrientation.Horizontal;

        private Transform swordTrailTransform;
        private ParticleSystemRenderer swordTrailRenderer;
        private Quaternion baseLocalRotation;
        private Vector3 baseRendererFlip;

        private void Awake()
        {
            if (swordTrail == null)
            {
                Debug.LogWarning("[WeaponVFX] SwordTrail not assigned.", this);
                return;
            }

            swordTrailTransform = swordTrail.transform;
            swordTrailRenderer = swordTrail.GetComponent<ParticleSystemRenderer>();

            if (swordTrailRenderer == null)
            {
                Debug.LogWarning("[WeaponVFX] ParticleSystemRenderer of the SwordTrail not found.", this);
                return;
            }

            baseLocalRotation = swordTrailTransform.localRotation;
            baseRendererFlip = swordTrailRenderer.flip;

            swordTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable()
        {
            DisableSwordTrail();
            StopImpact();
        }

        public void EnableSwordTrail()
        {
            EnableSwordTrail(defaultOrientation, false);
        }

        public void EnableSwordTrail(VFXOrientation orientation, bool reverse)
        {
            if (swordTrail == null || swordTrailRenderer == null)
            {
                Debug.LogWarning("[WeaponVFX] SwordTrail or ParticleSystemRenderer not configured.", this);
                return;
            }

            ApplyOrientation(orientation);
            ApplyReverse(orientation, reverse);

            swordTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            swordTrail.Play(true);
        }

        public void DisableSwordTrail()
        {
            if (swordTrail == null) return;

            swordTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void PlayImpact()
        {
            if (impact == null)
            {
                Debug.LogWarning("[WeaponVFX] Impact not assigned.", this);
                return;
            }

            impact.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            impact.Play(true);
        }

        public void StopImpact()
        {
            if (impact == null) return;

            impact.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void ApplyOrientation(VFXOrientation orientation)
        {
            float angle = orientation == VFXOrientation.Vertical ? 90f : 0f;

            swordTrailTransform.localRotation =
                Quaternion.Euler(0f, 0f, angle) * baseLocalRotation;
        }

        private void ApplyReverse(VFXOrientation orientation, bool reverse)
        {
            Vector3 flip = baseRendererFlip;

            if (reverse)
            {
                if (orientation == VFXOrientation.Horizontal)
                    flip.x = baseRendererFlip.x >= 0.5f ? 0f : 1f;
                else
                    flip.y = baseRendererFlip.y >= 0.5f ? 0f : 1f;
            }

            swordTrailRenderer.flip = flip;
        }

        [ContextMenu("Test Enable SwordTrail")]
        private void TestEnableSwordTrail()
        {
            if (Application.isPlaying)
                EnableSwordTrail();
        }

        [ContextMenu("Test Disable SwordTrail")]
        private void TestDisableSwordTrail()
        {
            if (Application.isPlaying)
                DisableSwordTrail();
        }

        [ContextMenu("Test Play Impact")]
        private void TestPlayImpact()
        {
            if (Application.isPlaying)
                PlayImpact();
        }

        [ContextMenu("Test Stop Impact")]
        private void TestStopImpact()
        {
            if (Application.isPlaying)
                StopImpact();
        }
    }
}
