using UnityEngine;

namespace MolimoByLulucaGameDev
{

    [RequireComponent(typeof(Camera))]

    public class PlayerOverlayCameraSync : MonoBehaviour
    {
        [SerializeField] private Camera sourceCamera;

        private Camera overlayCamera;

        private void Awake()
        {
            overlayCamera = GetComponent<Camera>();

            if (sourceCamera == null)
            {
                Debug.LogError("[PlayerOverlayCameraSync] sourceCamera not assigned the!");
            }
        }

        private void LateUpdate()
        {
            if (sourceCamera == null || overlayCamera == null) return;

            overlayCamera.orthographic = sourceCamera.orthographic;
            overlayCamera.orthographicSize = sourceCamera.orthographicSize;
            overlayCamera.fieldOfView = sourceCamera.fieldOfView;
            overlayCamera.nearClipPlane = sourceCamera.nearClipPlane;
            overlayCamera.farClipPlane = sourceCamera.farClipPlane;
        }
    }
}
