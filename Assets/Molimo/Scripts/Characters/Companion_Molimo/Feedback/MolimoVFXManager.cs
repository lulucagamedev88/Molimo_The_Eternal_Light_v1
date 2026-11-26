using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class MolimoVFXManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("VFX of impact")]
        [Tooltip("ParticleSystem local (child of Molimo). will be cloned in the impact.")]
        [SerializeField] private ParticleSystem localImpactParticle;

        [Tooltip("time extra after the VFX end before destroy the clone.")]
        [SerializeField, Range(0f, 2f)] private float vfxDestroyPadding = 0.5f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (localImpactParticle == null)
                localImpactParticle = GetComponentInChildren<ParticleSystem>(true);

            if (localImpactParticle != null)
                localImpactParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        #endregion

        #region Impact VFX

        public void SpawnImpactVFX(Vector3 position, Quaternion rotation)
        {
            if (localImpactParticle == null) return;

            GameObject clone = Instantiate(
                localImpactParticle.gameObject,
                position,
                rotation);

            clone.name = "ImpactVFX_Temp";
            clone.SetActive(true);

            ParticleSystem clonePS = clone.GetComponent<ParticleSystem>();
            if (clonePS != null)
            {
                clonePS.Play(true);

                float life = clonePS.main.duration
                             + clonePS.main.startLifetime.constantMax
                             + vfxDestroyPadding;

                Destroy(clone, life);
            }
            else
            {
                Destroy(clone, vfxDestroyPadding);
            }
        }

        #endregion
    }
}
