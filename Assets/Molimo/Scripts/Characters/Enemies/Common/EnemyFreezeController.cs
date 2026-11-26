using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    public static class EnemyFreezeController
    {
        #region Internal Data

        private struct FrozenEnemyData
        {
            public EnemySpiderAI enemy;
            public bool wasEnabled;

            public Animator animator;
            public float animatorSpeed;

            public Rigidbody rigidbody;
            public bool wasKinematic;
        }

        private static readonly List<FrozenEnemyData> _frozenEnemies = new List<FrozenEnemyData>();
        private static bool _isFrozen;

        #endregion

        #region Public API

        public static bool IsFrozen => _isFrozen;

        public static void SetAllEnemiesFrozen(bool isFrozen)
        {
            if (_isFrozen == isFrozen) return;

            _isFrozen = isFrozen;

            if (isFrozen) FreezeAll();
            else UnfreezeAll();

            Debug.Log($"[EnemyFreezeController] Enemies {(isFrozen ? "frozen" : "unfrozen")} " +
                      $"({_frozenEnemies.Count} affected).");
        }

        #endregion

        #region Freeze

        private static void FreezeAll()
        {
            _frozenEnemies.Clear();

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");

            foreach (var go in enemies)
            {
                if (go == null) continue;

                var box = go.GetComponentInChildren<EnemySpiderAI>();
                if (box == null) continue;

                var data = new FrozenEnemyData
                {
                    enemy = box,
                    wasEnabled = box.enabled,
                    animator = go.GetComponentInChildren<Animator>(),
                    rigidbody = go.GetComponent<Rigidbody>()
                };

                if (data.animator != null)
                    data.animatorSpeed = data.animator.speed;

                if (data.rigidbody != null)
                    data.wasKinematic = data.rigidbody.isKinematic;

                box.enabled = false;

                if (data.animator != null)
                    data.animator.speed = 0f;

                if (data.rigidbody != null)
                {
                    data.rigidbody.linearVelocity = Vector3.zero;
                    data.rigidbody.angularVelocity = Vector3.zero;
                    data.rigidbody.isKinematic = true;
                }

                _frozenEnemies.Add(data);
            }
        }

        private static void UnfreezeAll()
        {
            foreach (var data in _frozenEnemies)
            {
                if (data.enemy == null) continue;

                data.enemy.enabled = data.wasEnabled;

                if (data.animator != null)
                    data.animator.speed = data.animatorSpeed;

                if (data.rigidbody != null)
                    data.rigidbody.isKinematic = data.wasKinematic;
            }

            _frozenEnemies.Clear();
        }

        #endregion

        #region Static Reset

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _isFrozen = false;
            _frozenEnemies.Clear();
        }

        #endregion
    }
}
