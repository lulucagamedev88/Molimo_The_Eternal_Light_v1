using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]

    public class DarkZoneTrigger : MonoBehaviour
    {
        [Tooltip("drag the object that has the DayNightLightingController here.")]
        [SerializeField] private DayNightLightingController dayNightController;

        private void Awake()
        {

            GetComponent<Collider>().isTrigger = true;

            if (dayNightController == null)
                dayNightController = FindAnyObjectByType<DayNightLightingController>();
        }

        private void OnTriggerEnter(Collider other)
        {

            if (other.CompareTag("Player"))
            {
                dayNightController.SetDarkZone(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {

            if (other.CompareTag("Player"))
            {
                dayNightController.SetDarkZone(false);
            }
        }
    }
}
