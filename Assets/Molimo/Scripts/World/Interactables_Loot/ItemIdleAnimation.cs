using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [ExecuteAlways]

    public class ItemIdleAnimation : MonoBehaviour
    {
        [Header("Editor (Viewport)")]
        [Tooltip("Ative is ver the animation in the Scene. Desative is can mover the object by mapa.")]
        [SerializeField] private bool animateInEditor = false;

        [Header("rotation")]
        [SerializeField] private float rotationSpeed = 90f;

        [Header("Flutuacathe")]
        [SerializeField] private float floatSpeed = 2f;
        [SerializeField] private float floatAmplitude = 0.2f;

        [Header("Travar position")]
        [Tooltip("locks X and Z in the position of spawn is prevent sliding by force of spawn")]
        [SerializeField] private bool lockPositionXZ = true;

        private Vector3 _startPosition;

        private void Start()
        {

            if (Application.isPlaying)
            {
                _startPosition = transform.position;

                if (TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.isKinematic = true;
                }
            }
        }

        private void Update()
        {

            if (!Application.isPlaying && !animateInEditor)
            {
                _startPosition = transform.position;
                return;
            }

            float timeToUse = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            float deltaTime = Application.isPlaying ? Time.deltaTime : 0.016f;

            transform.Rotate(
                Vector3.up,
                rotationSpeed * deltaTime,
                Space.World
            );

            float newY =
                _startPosition.y +
                (Mathf.Sin(timeToUse * floatSpeed) * floatAmplitude);

            if (lockPositionXZ)
            {

                transform.position = new Vector3(
                    _startPosition.x,
                    newY,
                    _startPosition.z
                );
            }
            else
            {

                transform.position = new Vector3(
                    transform.position.x,
                    newY,
                    transform.position.z
                );
            }
        }
    }
}
