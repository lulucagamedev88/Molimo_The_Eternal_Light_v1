using System;
using UnityEngine;
using Unity.Cinemachine;

namespace MolimoByLulucaGameDev
{
    [ExecuteAlways]
    [AddComponentMenu("Molimo/Cinemachine Custom LookAt")]

    public class CinemachineCustomLookAt : CinemachineExtension
    {
        [Header("reference of the target")]
        [Tooltip("drag the Transform of the player here is the camera mirar nele automatically")]
        public Transform playerTarget;

        [Header("Ajustes of Mira")]
        [Tooltip("Offset is ajustar the point of mira (ex: Y = 1.5 is look is the chest/head)")]
        public Vector3 targetOffset = new Vector3(0, 1.5f, 0);

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage == CinemachineCore.Stage.Aim)
            {

                Transform target = playerTarget != null ? playerTarget : vcam.LookAt;

                if (target != null)
                {

                    Vector3 targetPosition = target.position + targetOffset;

                    Vector3 dirToTarget = targetPosition - state.RawPosition;

                    if (dirToTarget != Vector3.zero)
                    {

                        state.RawOrientation = Quaternion.LookRotation(dirToTarget, Vector3.up);
                    }
                }
            }
        }
    }
}
