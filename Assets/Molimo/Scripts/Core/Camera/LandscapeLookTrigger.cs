using UnityEngine;
using Unity.Cinemachine;

namespace MolimoByLulucaGameDev
{

    public class LandscapeLookTrigger : MonoBehaviour
    {
        [Header("camera of the landscape (Cinemachine)")]
        [Tooltip("CinemachineCamera that filma the landscape. not is the Main Camera.")]
        public CinemachineCamera landscapeCamera;

        [Header("priorities of transition")]
        [Tooltip("priority of the camera of landscape when the Player not is in the trigger.")]
        public int lowPriority = 0;

        [Tooltip("priority of the camera of landscape when the Player is in the trigger. " +
                 "must be larger that the priority of the FreeLook of the player.")]
        public int highPriority = 20;

        private int playerInsideCount = 0;

        private void Start()
        {
            if (landscapeCamera == null)
            {
                Debug.LogError("[MolimoByLulucaGameDev] landscapeCamera not was assigned in the Inspector!");
                return;
            }

            landscapeCamera.Priority = lowPriority;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            playerInsideCount++;
            if (playerInsideCount == 1) SetLandscapeActive(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            playerInsideCount = Mathf.Max(0, playerInsideCount - 1);
            if (playerInsideCount == 0) SetLandscapeActive(false);
        }

        private void OnDisable()
        {
            playerInsideCount = 0;
            SetLandscapeActive(false);
        }

        private void SetLandscapeActive(bool active)
        {
            if (landscapeCamera == null) return;

            landscapeCamera.Priority = active ? highPriority : lowPriority;
        }
    }
}
