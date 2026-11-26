using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]

    public class PlayerDamageFlash : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("if empty, tries buscar automatically in the same GameObject (or parent/child).")]
        [SerializeField] private PlayerHealth health;

        [Header("Flash Settings")]
        [Tooltip("color of the flash when receiving damage.")]
        [SerializeField] private Color flashColor = Color.white;

        [Tooltip("duration total of the effect of flash in seconds.")]
        [SerializeField] private float flashDuration = 0.3f;

        [Tooltip("Quantos pulsos of flash during the duration total.")]
        [Range(1, 10)]
        [SerializeField] private int flashCount = 2;

        [Tooltip("if true, resets the flash caso tome damage novamente while already is piscando.")]
        [SerializeField] private bool refreshOnNewDamage = true;

        [Header("Renderer Exclusions")]
        [Tooltip("Renderers that never will be afetados by Damage Flash.")]
        [SerializeField] private Renderer[] ignoredRenderers;

        private struct MaterialTarget
        {
            public Renderer renderer;
            public int materialIndex;
            public int colorPropertyID;
            public Color originalColor;
        }

        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorID = Shader.PropertyToID("_Color");

        private readonly List<MaterialTarget> targets = new List<MaterialTarget>();
        private MaterialPropertyBlock propertyBlock;
        private Coroutine flashRoutine;

        private void Awake()
        {
            if (health == null) health = GetComponent<PlayerHealth>();
            if (health == null) health = GetComponentInParent<PlayerHealth>();
            if (health == null) health = GetComponentInChildren<PlayerHealth>();

            propertyBlock = new MaterialPropertyBlock();
            CollectTargets();
        }

        private void OnEnable()
        {
            if (health != null) health.OnDamaged += HandleHealthDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDamaged -= HandleHealthDamaged;

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            ResetAll();
        }

        private void CollectTargets()
        {
            targets.Clear();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                if (IsRendererIgnored(r)) continue;

                if (r is TrailRenderer || r is LineRenderer || r is ParticleSystemRenderer)
                    continue;

                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;

                    int propID = -1;
                    if (m.HasProperty(BaseColorID)) propID = BaseColorID;
                    else if (m.HasProperty(ColorID)) propID = ColorID;

                    if (propID == -1) continue;

                    targets.Add(new MaterialTarget
                    {
                        renderer = r,
                        materialIndex = i,
                        colorPropertyID = propID,
                        originalColor = m.GetColor(propID)
                    });
                }
            }

            if (targets.Count == 0)
            {
                Debug.LogWarning("[DamageFlash] none material compatible found is piscar.");
            }
        }

        private bool IsRendererIgnored(Renderer renderer)
        {
            if (renderer == null || ignoredRenderers == null)
                return false;

            for (int i = 0; i < ignoredRenderers.Length; i++)
            {
                if (ignoredRenderers[i] == renderer)
                    return true;
            }

            return false;
        }

        public void IgnoreRenderer(Renderer renderer)
        {
            if (renderer == null || IsRendererIgnored(renderer))
                return;

            int oldLength = ignoredRenderers != null ? ignoredRenderers.Length : 0;
            Renderer[] updated = new Renderer[oldLength + 1];

            if (oldLength > 0)
                System.Array.Copy(ignoredRenderers, updated, oldLength);

            updated[oldLength] = renderer;
            ignoredRenderers = updated;

            RestoreRenderer(renderer);
            targets.RemoveAll(target => target.renderer == renderer);
        }

        public void AllowRenderer(Renderer renderer)
        {
            if (renderer == null || ignoredRenderers == null)
                return;

            int index = System.Array.IndexOf(ignoredRenderers, renderer);
            if (index < 0)
                return;

            Renderer[] updated = new Renderer[ignoredRenderers.Length - 1];

            if (index > 0)
                System.Array.Copy(ignoredRenderers, 0, updated, 0, index);

            if (index < ignoredRenderers.Length - 1)
            {
                System.Array.Copy(
                    ignoredRenderers,
                    index + 1,
                    updated,
                    index,
                    ignoredRenderers.Length - index - 1);
            }

            ignoredRenderers = updated;
            CollectTargets();
        }

        private void RestoreRenderer(Renderer renderer)
        {
            if (renderer == null || propertyBlock == null)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                MaterialTarget target = targets[i];
                if (target.renderer != renderer)
                    continue;

                renderer.GetPropertyBlock(propertyBlock, target.materialIndex);
                propertyBlock.SetColor(target.colorPropertyID, target.originalColor);
                renderer.SetPropertyBlock(propertyBlock, target.materialIndex);
            }
        }

        private void HandleHealthDamaged(float amount)
        {
            TriggerFlash();
        }

        public void TriggerFlash()
        {
            if (!isActiveAndEnabled) return;
            if (targets.Count == 0) return;

            if (flashRoutine != null)
            {
                if (!refreshOnNewDamage) return;
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            float pulseTime = flashDuration / (flashCount * 2f);

            for (int p = 0; p < flashCount; p++)
            {
                ApplyFlash(1f);
                yield return new WaitForSeconds(pulseTime);

                ApplyFlash(0f);
                yield return new WaitForSeconds(pulseTime);
            }

            ResetAll();
            flashRoutine = null;
        }

        private void ApplyFlash(float t)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                MaterialTarget target = targets[i];
                if (target.renderer == null) continue;

                target.renderer.GetPropertyBlock(propertyBlock, target.materialIndex);
                Color c = Color.Lerp(target.originalColor, flashColor, t);
                propertyBlock.SetColor(target.colorPropertyID, c);
                target.renderer.SetPropertyBlock(propertyBlock, target.materialIndex);
            }
        }

        private void ResetAll()
        {
            for (int i = 0; i < targets.Count; i++)
            {
                MaterialTarget target = targets[i];
                if (target.renderer == null) continue;

                target.renderer.GetPropertyBlock(propertyBlock, target.materialIndex);
                propertyBlock.SetColor(target.colorPropertyID, target.originalColor);
                target.renderer.SetPropertyBlock(propertyBlock, target.materialIndex);
            }
        }
    }
}
