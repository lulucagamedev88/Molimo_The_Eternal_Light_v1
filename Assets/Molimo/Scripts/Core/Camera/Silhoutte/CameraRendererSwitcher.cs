using UnityEngine;

namespace MolimoByRodrigoLuluca
{

    public class CameraRendererSwitcher : MonoBehaviour
    {
        [Header("references")]
        [SerializeField] private Silhouette silhouetteScript;
        [SerializeField] private Camera silhouetteCamera;

        [Header("Delay")]
        [SerializeField] private float deactivationDelay = 0.2f;

        private bool isSilhouetteActive = false;
        private float deactivationTimer = 0f;

        private void Awake()
        {
            if (silhouetteCamera != null)
                silhouetteCamera.enabled = false;
        }

        private void Update()
        {
            bool shouldActivate = silhouetteScript.IsSilhouetteVisible;

            if (shouldActivate)
            {
                deactivationTimer = 0f;

                if (!isSilhouetteActive)
                {
                    isSilhouetteActive = true;
                    silhouetteCamera.enabled = true;
                }
            }
            else
            {
                deactivationTimer += Time.deltaTime;

                if (isSilhouetteActive && deactivationTimer >= deactivationDelay)
                {
                    isSilhouetteActive = false;
                    silhouetteCamera.enabled = false;
                }
            }
        }
    }
}
