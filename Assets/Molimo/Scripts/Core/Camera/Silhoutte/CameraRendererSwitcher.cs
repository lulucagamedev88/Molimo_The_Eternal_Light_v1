using UnityEngine;

namespace MolimoByLulucaGameDev
{
    public class CameraRendererSwitcher : MonoBehaviour
    {
        [Header("references")]
        [SerializeField] private Camera silhouetteCamera;

        [Header("Delay")]
        [SerializeField] private float deactivationDelay = 0.2f;

        private bool isSilhouetteActive = false;
        private float deactivationTimer = 0f;

        private void Awake()
        {
            if (silhouetteCamera == null)
            {
                Debug.LogError("[CameraRendererSwitcher] Silhouette Camera not assigned!", this);
                enabled = false;
                return;
            }

            silhouetteCamera.enabled = false;
        }

        private void Update()
        {
            // Visibilidade partilhada: a câmera fica ativa enquanto QUALQUER
            // Silhouette (Player ou Enemy) estiver visível.
            bool shouldActivate = Silhouette.AnySilhouetteVisible;

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
                deactivationTimer += Time.unscaledDeltaTime;

                if (isSilhouetteActive && deactivationTimer >= deactivationDelay)
                {
                    isSilhouetteActive = false;
                    silhouetteCamera.enabled = false;
                }
            }
        }

        private void OnDisable()
        {
            // Nunca deixar a câmera de silhueta ligada se este switcher for desativado.
            if (silhouetteCamera != null)
            {
                isSilhouetteActive = false;
                silhouetteCamera.enabled = false;
            }
        }
    }
}
