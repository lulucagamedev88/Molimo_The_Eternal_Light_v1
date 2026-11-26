using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MolimoByLulucaGameDev
{

    [DefaultExecutionOrder(-10000)]
    public class GlobalAnimationFPS : MonoBehaviour
    {

        public static GlobalAnimationFPS Instance { get; private set; }

        [Header("configuration retro")]
        public bool enableRetroMode = true;

        [Range(1, 60)]
        public int animationFPS = 12;

        [Header("Exclusoes automatic")]
        [Tooltip("Layers cujos Animators will be ignorados (ex: camera, UI).")]
        public LayerMask ignoredLayers;

        [Tooltip("Tags cujos Animators will be ignorados (ex: MainCamera).")]
        public string[] ignoredTags = new string[] { "MainCamera" };

        [Tooltip("Automatically detects Cinemachine components (Brain, camera, ImpulseListener) and ignores them.")]
        public bool ignoreCinemachine = true;

        [Header("Auto Scan (objects spawnados in runtime)")]
        [Tooltip("if true, search periodicamente Animators novos.")]
        public bool autoScanForNewAnimators = true;

        [Tooltip("interval (seconds) between escaneamentos. 0.5s is the bom compromisso.")]
        [Range(0.1f, 5f)]
        public float autoScanInterval = 0.5f;

        [Tooltip("Scan extra logo after the arranque (is apanhar Animators criados " +
                 "in the first frame by spawners/Awake).")]
        [Range(0f, 2f)]
        public float startupExtraScanDelay = 0.1f;

        [Header("Enforcement")]
        [Tooltip("if some script reabilitar the Animator gerenciado, voltamos the disable it-lo " +
                 "is that only this system the atualize.")]
        public bool enforceRetroModeOnManagedAnimators = true;

        [Tooltip("checks the state 'enabled' only the each N frames (reduces overhead). 1 = all the frames.")]
        [Range(1, 30)]
        public int enforceCheckEveryNthFrame = 3;

        [Header("Debug")]
        public bool logManagedCount = false;
        public bool logRegistrations = false;

        private readonly List<Animator> _managedAnimators = new List<Animator>(256);
        private readonly HashSet<Animator> _managedSet = new HashSet<Animator>();

        private float _timer;
        private float _step;
        private float _scanTimer;
        private int _enforceFrameCounter;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _step = 1f / Mathf.Max(1, animationFPS);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RefreshAll();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Start()
        {

            if (startupExtraScanDelay > 0f)
                Invoke(nameof(ScanForNewAnimators), startupExtraScanDelay);
            else
                ScanForNewAnimators();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshAll();

        public void RegisterAnimator(Animator animator)
        {
            if (animator == null) return;
            if (ShouldIgnore(animator)) return;
            if (!AddToManaged(animator)) return;

            ApplyRetroMode(animator);

            if (logRegistrations || logManagedCount)
                Debug.Log($"[GlobalAnimationFPS] + {animator.name} (total: {_managedAnimators.Count})");
        }

        public void UnregisterAnimator(Animator animator)
        {
            if (animator == null) return;
            if (!_managedSet.Remove(animator)) return;

            _managedAnimators.Remove(animator);

            if (animator != null) animator.enabled = true;

            if (logRegistrations || logManagedCount)
                Debug.Log($"[GlobalAnimationFPS] - {animator.name} (total: {_managedAnimators.Count})");
        }

        public bool IsManaged(Animator animator)
        {
            if (animator == null) return false;
            return _managedSet.Contains(animator);
        }

        public void RefreshAll()
        {
            _managedAnimators.Clear();
            _managedSet.Clear();

            ScanForNewAnimators();
            ApplyRetroModeToAll();

            if (logManagedCount)
                Debug.Log($"[GlobalAnimationFPS] RefreshAll — a gerir {_managedAnimators.Count} Animators.");
        }

        private void ScanForNewAnimators()
        {
            Animator[] found;

#if UNITY_2022_1_OR_NEWER
            found = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include);
#else
            found = Object.FindObjectsOfType<Animator>(true);
#endif

            int added = 0;

            for (int i = 0; i < found.Length; i++)
            {
                Animator the = found[i];
                if (the == null) continue;
                if (ShouldIgnore(the)) continue;
                if (!AddToManaged(the)) continue;

                ApplyRetroMode(the);
                added++;

                if (logRegistrations)
                    Debug.Log($"[GlobalAnimationFPS] scan + {the.name} (total: {_managedAnimators.Count})");
            }

            if (logManagedCount && added > 0)
                Debug.Log($"[GlobalAnimationFPS] Scan adicionou {added} novos Animators " +
                          $"(total: {_managedAnimators.Count}).");
        }

        private bool AddToManaged(Animator the)
        {
            if (the == null) return false;
            if (_managedSet.Contains(the)) return false;

            _managedSet.Add(the);
            _managedAnimators.Add(the);
            return true;
        }

        private bool ShouldIgnore(Animator animator)
        {
            if (animator == null) return true;

            GameObject go = animator.gameObject;
            if (go == null) return true;

            if (((1 << go.layer) & ignoredLayers) != 0)
                return true;

            if (ignoredTags != null && ignoredTags.Length > 0)
            {
                string t = go.tag;
                if (!string.IsNullOrEmpty(t))
                {
                    for (int i = 0; i < ignoredTags.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(ignoredTags[i]) && t == ignoredTags[i])
                            return true;
                    }
                }
            }

            if (ignoreCinemachine && HasCinemachine(animator.transform))
                return true;

            return false;
        }

        private bool HasCinemachine(Transform t)
        {
            while (t != null)
            {
                MonoBehaviour[] comps = t.GetComponents<MonoBehaviour>();
                for (int i = 0; i < comps.Length; i++)
                {
                    MonoBehaviour c = comps[i];
                    if (c == null) continue;

                    string n = c.GetType().Name;
                    if (n.StartsWith("Cinemachine"))
                        return true;
                }
                t = t.parent;
            }
            return false;
        }

        private void ApplyRetroModeToAll()
        {
            for (int i = 0; i < _managedAnimators.Count; i++)
            {
                Animator the = _managedAnimators[i];
                if (the != null) ApplyRetroMode(the);
            }
        }

        private void ApplyRetroMode(Animator animator)
        {
            if (animator == null) return;

#if UNITY_2022_1_OR_NEWER
            animator.keepAnimatorStateOnDisable = true;
#else
            animator.keepAnimatorControllerStateOnDisable = true;
#endif

            SkinnedMeshRenderer[] skinned = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
            {
                if (skinned[i] != null)
                    skinned[i].updateWhenOffscreen = true;
            }

            if (enableRetroMode)
                animator.enabled = false;
        }

        private void Update()
        {

            if (autoScanForNewAnimators)
            {
                _scanTimer += Time.deltaTime;
                if (_scanTimer >= autoScanInterval)
                {
                    _scanTimer = 0f;
                    ScanForNewAnimators();
                }
            }

            if (!enableRetroMode)
            {
                for (int i = 0; i < _managedAnimators.Count; i++)
                {
                    Animator the = _managedAnimators[i];
                    if (the != null && !the.enabled) the.enabled = true;
                }
                return;
            }

            if (enforceRetroModeOnManagedAnimators)
            {
                _enforceFrameCounter++;
                if (_enforceFrameCounter >= Mathf.Max(1, enforceCheckEveryNthFrame))
                {
                    _enforceFrameCounter = 0;
                    for (int i = 0; i < _managedAnimators.Count; i++)
                    {
                        Animator the = _managedAnimators[i];
                        if (the != null && the.enabled) the.enabled = false;
                    }
                }
            }

            _step = 1f / Mathf.Max(1, animationFPS);
            _timer += Time.deltaTime;

            int steps = 0;
            while (_timer >= _step && steps < 8)
            {
                _timer -= _step;
                steps++;
            }

            if (steps == 0) return;

            float dt = steps * _step;

            for (int i = _managedAnimators.Count - 1; i >= 0; i--)
            {
                Animator the = _managedAnimators[i];

                if (the == null)
                {
                    _managedAnimators.RemoveAt(i);
                    continue;
                }

                if (!the.enabled)
                    the.Update(dt);
            }
        }
    }
}
