namespace MolimoByLulucaGameDev
{
    using System.Collections.Generic;
    using UnityEngine;

    [DisallowMultipleComponent]

    public class CloudLayer : MonoBehaviour
    {
        [Header("Nuvens (use the button + is add)")]
        [Tooltip("drag each sprite of nuvem is this list.")]
        [SerializeField] private List<Transform> clouds = new List<Transform>();

        [Header("movement (axis X →)")]
        [Tooltip("speed in units/second. Positivo = right (+X).")]
        [SerializeField] private float speed = 1f;

        [Header("limits of the Loop")]
        [Tooltip("Calcula the limits automatically by camera orthographic.")]
        [SerializeField] private bool autoBounds = true;
        [Tooltip("X where the nuvem reaparece (enters by left).")]
        [SerializeField] private float leftBound = -12f;
        [Tooltip("X where the nuvem 'morre' and returns pro start (exits by right).")]
        [SerializeField] private float rightBound = 12f;
        [Tooltip("margin extra is ensure that the nuvem saia totalmente of the tela.")]
        [SerializeField] private float margin = 2f;

        [Header("variation (optional)")]
        [Tooltip("when the dar loop, sorteia the novo Y inside the interval.")]
        [SerializeField] private bool randomizeYOnLoop = false;
        [SerializeField] private Vector2 yRange = new Vector2(-1f, 1f);

        private Camera cam;

        private void Awake()
        {
            cam = Camera.main;

            if (autoBounds && cam != null && cam.orthographic)
            {
                float halfWidth = cam.orthographicSize * cam.aspect;
                leftBound = cam.transform.position.x - halfWidth - margin;
                rightBound = cam.transform.position.x + halfWidth + margin;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < clouds.Count; i++)
            {
                Transform c = clouds[i];
                if (c == null) continue;

                Vector3 p = c.position;
                p.x += speed * dt;

                if (speed > 0f && p.x > rightBound)
                {
                    p.x = leftBound;
                    if (randomizeYOnLoop)
                        p.y = Random.Range(yRange.x, yRange.y);
                }

                else if (speed < 0f && p.x < leftBound)
                {
                    p.x = rightBound;
                    if (randomizeYOnLoop)
                        p.y = Random.Range(yRange.x, yRange.y);
                }

                c.position = p;
            }
        }
    }
}
