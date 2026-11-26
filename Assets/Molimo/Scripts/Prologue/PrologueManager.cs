using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace MolimoByLulucaGameDev
{

    public class PrologueManager : MonoBehaviour
    {

        public static PrologueManager Instance { get; private set; }

        [Header("Cameras (Cinemachine 3.x)")]
        [Tooltip("CinemachineCamera inside of PrologueCamera (PrologueFreeLookCamera).")]
        [SerializeField] private CinemachineCamera prologueCamera;

        [Tooltip("the GameObject root/parent that contains the camera of the prologue. if left in blank, the script usara the parent of the prologueCamera automatically.")]
        [SerializeField] private GameObject prologueCameraRoot;

        [Tooltip("CinemachineCamera inside of PlayerCamera (FreeLookCamera).")]
        [SerializeField] private CinemachineCamera mainCamera;

        [Header("Priorities")]
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 10;

        [Header("settings of transition")]
        [Tooltip("time in seconds is wait before disable/destroy the camera of the prologue.")]
        [SerializeField] private float prologueCameraCleanupDelay = 1.5f;

        [Tooltip("if checked, the camera of the prologue and its parent will be destroyed. if unchecked, only desativados.")]
        [SerializeField] private bool destroyInsteadOfDisable = false;

        [Header("state")]
        [SerializeField] private bool prologueCompleted = false;

        public bool PrologueCompleted => prologueCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SwitchToPrologue();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SwitchToPrologue()
        {
            if (prologueCamera == null || mainCamera == null) return;

            prologueCamera.Priority = activePriority;
            mainCamera.Priority = inactivePriority;
            prologueCompleted = false;
        }

        public void SwitchToMain()
        {
            if (mainCamera == null) return;

            mainCamera.Priority = activePriority;
            prologueCompleted = true;

            if (prologueCamera != null || prologueCameraRoot != null)
            {
                if (prologueCameraCleanupDelay > 0f)
                {
                    StartCoroutine(CleanupPrologueCameraRoutine());
                }
                else
                {
                    CleanupPrologueCamera();
                }
            }
        }

        private IEnumerator CleanupPrologueCameraRoutine()
        {
            yield return new WaitForSeconds(prologueCameraCleanupDelay);
            CleanupPrologueCamera();
        }

        private void CleanupPrologueCamera()
        {
            GameObject targetToDestroy = prologueCameraRoot;

            if (targetToDestroy == null && prologueCamera != null)
            {
                targetToDestroy = prologueCamera.transform.parent != null
                    ? prologueCamera.transform.parent.gameObject
                    : prologueCamera.gameObject;
            }

            if (targetToDestroy == null) return;

            if (destroyInsteadOfDisable)
            {
                Destroy(targetToDestroy);
                prologueCamera = null;
                prologueCameraRoot = null;
            }
            else
            {
                targetToDestroy.SetActive(false);
            }
        }

        public void BlendToMainCamera() => SwitchToMain();
    }
}
