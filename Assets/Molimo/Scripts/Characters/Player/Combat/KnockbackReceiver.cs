using UnityEngine;

namespace MolimoByLulucaGameDev
{

    [DisallowMultipleComponent]
    public class KnockbackReceiver : MonoBehaviour, IKnockbackReceiver
    {
        [Header("references (auto-detetadas if vazias)")]
        [SerializeField] private Rigidbody rb;

        [Header("parameters")]
        [Tooltip("duration default of the knockback (s). 0.15–0.3 is the interval habitual.")]
        [SerializeField] private float defaultDuration = 0.2f;

        [Tooltip("limit of safety of the distance of knockback (m).")]
        [SerializeField] private float maxDistance = 3f;

        [Tooltip("resistance. 1 = normal | 2 = recua metade | 0 = imune (boss).")]
        [SerializeField] private float weight = 1f;

        [Tooltip("time extra stopped after the knockback acabar (hit stun) (s).")]
        [SerializeField] private float extraStun = 0.05f;

        [Header("direction")]
        [Tooltip("if ON, ignores the direction passada by who bate and calculates the direction the partir of the position " +
                 "of the player in the momento of the hit — the knockback empurra the enemy always is far of the player.")]
        [SerializeField] private bool directionFromPlayer = true;

        [Tooltip("(optional) reference direct when the Player. if empty, is auto-resolved (Brain → tag 'Player').")]
        [SerializeField] private Transform playerTarget;

        private Transform _player;
        private Vector3 dir;
        private float initialSpeed;
        private float duration;
        private float elapsed;
        private float stunEndTime;
        private bool active;

        public bool IsStunned => active || Time.time < stunEndTime;

        private void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
        }

        public void ApplyKnockback(Vector3 direction, float distance, float knockDuration)
        {
            if (weight <= 0f) return;

            if (directionFromPlayer)
            {
                ResolvePlayer();
                if (_player != null)
                    direction = transform.position - _player.position;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            float d = Mathf.Min(distance / weight, maxDistance);
            if (d <= 0f) return;

            dir = direction.normalized;
            duration = knockDuration > 0f ? knockDuration : defaultDuration;
            elapsed = 0f;

            initialSpeed = 2f * d / duration;

            active = true;
            stunEndTime = Time.time + duration + extraStun;

            if (rb != null && !rb.isKinematic)
                rb.linearVelocity = Vector3.zero;
        }

        private void FixedUpdate()
        {
            if (!active) return;
            if (rb == null || rb.isKinematic) return;

            float dt = Time.fixedDeltaTime;
            Vector3 v = CurrentVelocity();
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);

            elapsed += dt;
            if (elapsed >= duration) EndKnockback();
        }

        private Vector3 CurrentVelocity()
        {
            float t = Mathf.Clamp01(elapsed / duration);
            return dir * (initialSpeed * (1f - t));
        }

        private void ResolvePlayer()
        {
            if (playerTarget != null) { _player = playerTarget; return; }
            if (_player != null) return;

            var brain = Object.FindAnyObjectByType<PlayerBrain>();
            if (brain != null) { _player = brain.transform; return; }

            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) _player = go.transform;
        }

        private void EndKnockback()
        {
            active = false;
            if (rb != null && !rb.isKinematic)
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }
}
