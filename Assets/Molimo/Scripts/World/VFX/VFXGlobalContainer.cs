using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public class VFXGlobalContainer : MonoBehaviour
    {

        public static Transform Instance { get; private set; }

        [Tooltip("if true, this is the container default used by effects that not specify the.")]
        [SerializeField] private bool isDefault = true;

        private void Awake()
        {
            if (isDefault)
            {
                Instance = transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == transform) Instance = null;
        }
    }
}
