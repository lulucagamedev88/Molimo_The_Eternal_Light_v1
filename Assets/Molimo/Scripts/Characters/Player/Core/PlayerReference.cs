using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Molimo By Luluca/Player Reference")]

    public sealed class PlayerReference : MonoBehaviour
    {

        public static PlayerReference Instance { get; private set; }

        [Tooltip("Transform root of the player (can be this own). " +
                 "leave empty is use this GameObject.")]
        [SerializeField] private Transform playerRoot;

        public Transform Root => playerRoot != null ? playerRoot : transform;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"[{nameof(PlayerReference)}] already existe the Player registado. " +
                    $"Destruindo duplicado in '{name}'.", this);
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
