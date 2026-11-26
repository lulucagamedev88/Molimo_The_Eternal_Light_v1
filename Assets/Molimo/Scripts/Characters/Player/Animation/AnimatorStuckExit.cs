using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    [RequireComponent(typeof(Animator))]
    public class AnimatorStuckExit : MonoBehaviour
    {

        [Header("Fallback")]
        [Tooltip("Nome exact of the state is where force the exit when travar.")]
        [SerializeField] private string fallbackStateName = "Locomotion";

        [Tooltip("duration of the CrossFade when the force. 0 = corte dry.")]
        [SerializeField] private float fallbackCrossFade = 0.08f;

        [Header("detection")]
        [Tooltip("Multiplicador about the duration of the clip. Ex: 1.5 = tolera 50% of delay before force.")]
        [SerializeField] private float timeoutMultiplier = 1.5f;

        [Tooltip("time adicional (s) somado when the timeout calculado. useful is clipes curtos.")]
        [SerializeField] private float timeoutExtraSeconds = 0.4f;

        [Tooltip("Timeout maximum absoluto (s). never wait more that, same if the clip is longo.")]
        [SerializeField] private float maxTimeoutSeconds = 8f;

        [Tooltip("Frames with normalizedTime congelado before force exit.")]
        [SerializeField] private int frozenFramesThreshold = 20;

        [Tooltip("ignore states in loop (Locomotion, Idle, etc.).")]
        [SerializeField] private bool ignoreLoopingStates = true;

        [Header("layer")]
        [Tooltip("Layer of the Animator the monitorar (0 = base).")]
        [SerializeField] private int animatorLayerIndex = 0;

        [Header("Debug")]
        [SerializeField] private bool logEvents = true;
        [SerializeField] private bool logEveryFrame = false;

        private Animator animator;

        private int lastStateHash;
        private float stateEnterTime;
        private float stateTimeoutSeconds;
        private float lastNormalizedTime;
        private int frozenFrames;
        private bool isMonitoring;
        private bool isLoopingCurrent;

        private readonly List<string> allBoolParams = new List<string>();
        private readonly List<string> allTriggerParams = new List<string>();

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (animator == null)
            {
                Debug.LogError("[AnimatorStuckExit] none Animator found.");
                enabled = false;
                return;
            }

            CacheAnimatorParameters();

            if (logEvents)
                Debug.Log($"[AnimatorStuckExit] ready. {allBoolParams.Count} bool(s) and " +
                          $"{allTriggerParams.Count} trigger(s) cacheados. Fallback='{fallbackStateName}'.");
        }

        private void CacheAnimatorParameters()
        {
            allBoolParams.Clear();
            allTriggerParams.Clear();

            foreach (var p in animator.parameters)
            {
                switch (p.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        allBoolParams.Add(p.name);
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        allTriggerParams.Add(p.name);
                        break;
                }
            }
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled) return;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(animatorLayerIndex);
            int hash = info.shortNameHash;

            if (hash != lastStateHash)
            {
                EnterState(info, hash);
                return;
            }

            if (!isMonitoring) return;

            CheckStuck(info);
        }

        private void EnterState(AnimatorStateInfo info, int hash)
        {
            lastStateHash = hash;
            stateEnterTime = Time.time;
            lastNormalizedTime = 0f;
            frozenFrames = 0;

            isLoopingCurrent = IsLoopingState(info);

            if (ignoreLoopingStates && isLoopingCurrent)
            {
                isMonitoring = false;
                return;
            }

            stateTimeoutSeconds = Mathf.Min(
                info.length * timeoutMultiplier + timeoutExtraSeconds,
                maxTimeoutSeconds);

            isMonitoring = true;

            if (logEvents)
                Debug.Log($"[AnimatorStuckExit] 👁 '{GetStateName(info)}' monitorando. " +
                          $"length={info.length:F2}s timeout={stateTimeoutSeconds:F2}s loop={isLoopingCurrent}.");
        }

        private void CheckStuck(AnimatorStateInfo info)
        {
            float elapsed = Time.time - stateEnterTime;
            float norm = info.normalizedTime;

            bool timedOut = elapsed >= stateTimeoutSeconds;

            bool frozen = false;
            if (elapsed > 0.1f)
            {
                if (Mathf.Abs(norm - lastNormalizedTime) <= 0.0001f)
                    frozenFrames++;
                else
                    frozenFrames = 0;

                frozen = frozenFrames >= frozenFramesThreshold;
            }
            lastNormalizedTime = norm;

            if (logEveryFrame)
                Debug.Log($"[AnimatorStuckExit] '{GetStateName(info)}' " +
                          $"elapsed={elapsed:F2}/{stateTimeoutSeconds:F2}s norm={norm:F3} " +
                          $"frozen={frozenFrames}/{frozenFramesThreshold} timedOut={timedOut} frozen={frozen}");

            if (timedOut || frozen)
            {
                string reason = timedOut
                    ? $"timeout ({elapsed:F2}s ≥ {stateTimeoutSeconds:F2}s)"
                    : $"normalizedTime congelado ({frozenFrames} frames)";

                ForceExit(GetStateName(info), reason);
            }
        }

        private bool IsLoopingState(AnimatorStateInfo info)
        {

            string name = GetStateName(info).ToLowerInvariant();
            return name.Contains("locomotion")
                || name.Contains("idle")
                || name.Contains("walk")
                || name.Contains("run")
                || name.Contains("run")
                || name.Contains("move");
        }

        private string GetStateName(AnimatorStateInfo info)
        {

            var clips = animator.GetCurrentAnimatorClipInfo(animatorLayerIndex);
            if (clips != null && clips.Length > 0 && clips[0].clip != null)
                return clips[0].clip.name;

            return $"hash:{info.shortNameHash}";
        }

        private void ForceExit(string stateName, string reason)
        {
            if (string.IsNullOrEmpty(fallbackStateName))
            {
                Debug.LogError("[AnimatorStuckExit] fallbackStateName empty. forcing exit impossible.");
                return;
            }

            Debug.LogWarning($"[AnimatorStuckExit] ⚠ STUCK in '{stateName}'. Motivo: {reason}. " +
                             $"forcing → '{fallbackStateName}'.");

            foreach (var b in allBoolParams)
                animator.SetBool(b, false);

            foreach (var t in allTriggerParams)
                animator.ResetTrigger(t);

            if (fallbackCrossFade > 0f)
                animator.CrossFadeInFixedTime(fallbackStateName, fallbackCrossFade, animatorLayerIndex, 0f);
            else
                animator.Play(fallbackStateName, animatorLayerIndex, 0f);

            isMonitoring = false;
            lastStateHash = 0;
            frozenFrames = 0;
            lastNormalizedTime = 0f;
        }

        public void ForceExitNow(string reason = "call manual")
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(animatorLayerIndex);
            ForceExit(GetStateName(info), reason);
        }

        public string GetCurrentStateName()
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(animatorLayerIndex);
            return GetStateName(info);
        }
    }
}
