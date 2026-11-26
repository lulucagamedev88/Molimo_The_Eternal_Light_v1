using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]

    public class ShadowController : MonoBehaviour
    {
        [Header("references")]
        [Tooltip("leave in blank is use automatically the object parent imediato (Transform.parent).")]
        [SerializeField] private Transform targetParent;

        [Header("settings of height of the ground")]
        [Tooltip("if ativado, the height of the ground will be captured automatically in the position Y initial of the shadow.")]
        [SerializeField] private bool useInitialYAsGround = true;
        [Tooltip("height fixa of the ground (used only if the booleano above is false).")]
        [SerializeField] private float fixedGroundY = 0f;

        [Header("settings of scale (illusion of Salto)")]
        [Tooltip("height maximum that the parent can rise before of the shadow atingir the size minimum.")]
        [SerializeField] private float maxHeight = 5f;
        [Tooltip("size of the shadow when the parent is in the ground.")]
        [SerializeField] private float normalScale = 1f;
        [Tooltip("size minimum of the shadow when the parent is in the ar (ex: 0.25 = 25%).")]
        [SerializeField] private float minScale = 0.25f;

        [Header("smoothing")]
        [Tooltip("how fast the shadow changes of size (0 = instant).")]
        [SerializeField, Min(0f)] private float scaleSmoothness = 10f;

        private float _groundY;

        private void Awake()
        {

            if (targetParent == null)
            {
                targetParent = transform.parent;
            }

            if (targetParent == null)
            {
                Debug.LogError("[ShadowController] none object parent found! Coloque the shadow the child of the personagem or atribua the Target Parent.", this);
                enabled = false;
                return;
            }

            _groundY = useInitialYAsGround ? transform.position.y : fixedGroundY;
        }

        private void LateUpdate()
        {
            if (targetParent == null) return;

            transform.position = new Vector3(targetParent.position.x, _groundY, targetParent.position.z);

            float height = targetParent.position.y - _groundY;

            float heightRatio = Mathf.Clamp01(height / maxHeight);

            float targetScale = Mathf.Lerp(normalScale, minScale, heightRatio);

            float currentScale = transform.localScale.x;
            float finalScale = targetScale;

            if (scaleSmoothness > 0f)
            {
                finalScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * scaleSmoothness);
            }

            transform.localScale = new Vector3(finalScale, finalScale, finalScale);
        }
    }
}
