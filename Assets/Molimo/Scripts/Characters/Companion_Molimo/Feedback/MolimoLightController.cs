using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]

    public class MolimoLightController : MonoBehaviour
    {
        [Header("references")]
        [Tooltip("drag the Light that is child of Molimo.")]
        [SerializeField] private Light molimoLight;

        [Tooltip("drag the DayNightLightingController (if deixar empty, it searches automatically).")]
        [SerializeField] private DayNightLightingController dayNightController;

        [Header("Rules of time (light acende)")]
        [Tooltip("hour in that the light acende (17:00 / 5 of the tarde).")]
        [SerializeField, Range(0f, 24f)] private float lightsOnHour = 17f;

        [Tooltip("hour in that the light turns off (07:00 / 7 of the morning).")]
        [SerializeField, Range(0f, 24f)] private float lightsOffHour = 7f;

        private void Awake()
        {
            if (molimoLight == null)
                molimoLight = GetComponentInChildren<Light>();

            if (dayNightController == null)
                dayNightController = FindAnyObjectByType<DayNightLightingController>();
        }

        private void Update()
        {
            if (molimoLight == null || dayNightController == null) return;

            float hour = dayNightController.CurrentTimeOfDay;
            bool isInDarkZone = dayNightController.IsInDarkZone;

            bool isTimeToBeOn;
            if (lightsOnHour > lightsOffHour)
                isTimeToBeOn = hour >= lightsOnHour || hour < lightsOffHour;
            else
                isTimeToBeOn = hour >= lightsOnHour && hour < lightsOffHour;

            bool shouldBeOn = isTimeToBeOn || isInDarkZone;

            if (molimoLight.enabled != shouldBeOn)
            {
                molimoLight.enabled = shouldBeOn;
            }
        }
    }
}
