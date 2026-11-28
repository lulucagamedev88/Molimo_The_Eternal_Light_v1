using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{

    [DisallowMultipleComponent]
    public class PlayerVFX : MonoBehaviour
    {
        [Header("global Settings")]
        [SerializeField] private bool vfxEnabled = true;

        #region Component References
        [Header("References")]
        [SerializeField] private PlayerBrain brain;
        [SerializeField] private Rigidbody entityRigidbody;
        #endregion

        #region Dust & PlayerMovement Settings
        [System.Serializable]

        public struct MovementDustData
        {
            public ParticleSystem runDustPS;
            public ParticleSystem jumpDustPS;
            public ParticleSystem landDustPS;
            public float minimumSpeedToEmit;
            public LayerMask groundLayerMask;
            public float groundCheckDistance;
        }

        [Header("Dust / Footsteps VFX")]
        [SerializeField]
        private MovementDustData dustData = new MovementDustData
        {
            minimumSpeedToEmit = 4.0f,
            groundCheckDistance = 0.4f
        };
        #endregion

        #region Action VFX Systems
        [Header("Action VFX (modules of Expans�o)")]
        [SerializeField] private ParticleSystem dashTrailPS;
        [SerializeField] private ParticleSystem hurtImpactPS;
        [SerializeField] private ParticleSystem healBurstPS;
        [SerializeField] private ParticleSystem levelUpPS;
        [SerializeField] private ParticleSystem blockImpactPS;
        [SerializeField] private ParticleSystem blockAuraPS;

        [Header("Dash Attack VFX")]
        [Tooltip("ParticleSystems disparados sempre que o DashAttack é ativado. VFX filhos do Player seguem a rotação do dash; VFX externos são reposicionados no Player e rodados para a direção do dash.")]
        [SerializeField] private List<ParticleSystem> dashAttackVFX = new List<ParticleSystem>();
        #endregion

        #region PlayerClimb VFX Settings
        [Header("Climb VFX Settings")]
        [Tooltip("ParticleSystem tocado while the Player is escalando.")]
        [SerializeField] private ParticleSystem climbPS;

        [Tooltip("if enabled, the system of Climb is dirigido by Climb.cs via SetClimbVFXState(). " +
                 "Desligue is depurar without effects.")]
        [SerializeField] private bool enableClimbVFX = true;

        [Tooltip("if enabled, imprime in the Console each change of state of the Climb VFX.")]
        [SerializeField] private bool debugClimbVFX = false;
        #endregion

        #region Charge VFX Settings
        [Header("Charge VFX Settings")]
        [Tooltip("ParticleSystem of carga, ativado during the transfer of energy of Molimo.")]
        [SerializeField] private ParticleSystem chargePS;

        [Tooltip("if true, SetChargeVFXState(true) only funciona after AuthorizeChargeVFX(). " +
                 "prevents that any script external ligue the Charge by acidente.")]
        [SerializeField] private bool requireAuthorizationForCharge = true;

        [Tooltip("duration of the ramp of subida of the emission when the turn on.")]
        [SerializeField, Range(0.1f, 3f)] private float chargeRampInDuration = 0.8f;

        [Tooltip("Curva of the ramp-in. 1 = linear, 2 = devagar→fast, 4 = very devagar→very fast.")]
        [SerializeField, Range(0.5f, 4f)] private float chargeRampInPower = 2f;

        [Tooltip("duration of the ramp of descida of the emission when the turn off.")]
        [SerializeField, Range(0.1f, 3f)] private float chargeRampOutDuration = 0.5f;

        [Tooltip("Curva of the ramp-out. 1 = linear, 0.5 = fast→devagar, 2 = devagar→fast.")]
        [SerializeField, Range(0.2f, 4f)] private float chargeRampOutPower = 0.5f;
        #endregion

        #region Internal State
        private bool isCurrentlyEmittingDust;
        private bool isCurrentlyBlocking;
        private bool isCurrentlyCharging;
        private bool isCurrentlyClimbing;

        private Coroutine _chargeRoutine;
        private float _chargeMaxRate = 1f;
        private bool _chargeMaxRateCaptured;
        private bool _chargeAuthorized;
        #endregion

        #region Properties
        public bool VFXEnabled
        {
            get => vfxEnabled;
            set
            {
                vfxEnabled = value;
                if (!vfxEnabled) StopAllContinuousVFX();
            }
        }

        public bool IsEmittingDust => isCurrentlyEmittingDust;
        public bool IsBlocking => isCurrentlyBlocking;
        public bool IsCharging => isCurrentlyCharging;
        public bool IsChargeAuthorized => _chargeAuthorized;
        public bool IsClimbing => isCurrentlyClimbing;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            InitializeReferences();
        }

        private void Update()
        {
            if (!vfxEnabled) return;
            UpdateMovementDust();
        }

        private void OnDisable()
        {
            StopAllContinuousVFX();
        }
        #endregion

        #region Initialization
        private void InitializeReferences()
        {
            if (brain == null)
                brain = GetComponentInParent<PlayerBrain>();

            if (entityRigidbody == null && brain != null)
                entityRigidbody = brain.PlayerRigidbody;

            if (entityRigidbody == null)
                entityRigidbody = GetComponentInParent<Rigidbody>();

            if (chargePS != null && !_chargeMaxRateCaptured)
            {
                _chargeMaxRate = chargePS.emission.rateOverTimeMultiplier;
                _chargeMaxRateCaptured = true;

                var emission = chargePS.emission;
                emission.rateOverTimeMultiplier = 0f;
                chargePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (climbPS != null)
                climbPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        #endregion

        #region Dust Logic
        private void UpdateMovementDust()
        {
            if (dustData.runDustPS == null) return;

            bool isGrounded = CheckIsGrounded();
            float currentSpeed = GetEntitySpeed();

            bool shouldEmit = isGrounded && (currentSpeed >= dustData.minimumSpeedToEmit);

            if (shouldEmit != isCurrentlyEmittingDust)
            {
                isCurrentlyEmittingDust = shouldEmit;

                if (shouldEmit)
                    dustData.runDustPS.Play(true);
                else
                    dustData.runDustPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private bool CheckIsGrounded()
        {
            if (brain != null) return brain.IsGrounded;
            return Physics.Raycast(transform.position, Vector3.down,
                                   dustData.groundCheckDistance, dustData.groundLayerMask);
        }

        private float GetEntitySpeed()
        {
            if (brain != null && brain.GetMovementState() != null)
                return brain.GetMovementState().CurrentPhysicalSpeed;

            if (entityRigidbody != null)
                return entityRigidbody.linearVelocity.magnitude;

            return 0f;
        }
        #endregion

        #region Block VFX Logic

        public void PlayBlockImpactVFX()
        {
            if (!vfxEnabled) return;
            PlayParticleSystem(blockImpactPS);
        }

        public void SetBlockVFXState(bool isBlocking)
        {
            if (!vfxEnabled)
            {
                if (blockAuraPS != null) blockAuraPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                isCurrentlyBlocking = false;
                return;
            }

            if (isBlocking == isCurrentlyBlocking) return;

            isCurrentlyBlocking = isBlocking;

            if (blockAuraPS == null) return;

            if (isBlocking) blockAuraPS.Play(true);
            else blockAuraPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        #endregion

        #region PlayerClimb VFX Logic

        public void SetClimbVFXState(bool isClimbing)
        {
            if (!enableClimbVFX || !vfxEnabled)
            {
                StopClimbVFXInternal();
                return;
            }

            if (isClimbing == isCurrentlyClimbing) return;
            isCurrentlyClimbing = isClimbing;

            if (debugClimbVFX)
            {
                Debug.Log($"[PlayerVFX] Climb VFX = {isClimbing} | " +
                          $"PS={(climbPS != null ? "OK" : "NULL")}");
            }

            if (climbPS == null) return;

            if (isClimbing)
            {
                climbPS.Play(true);
            }
            else
            {
                climbPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public void StopClimbVFX()
        {
            StopClimbVFXInternal();
        }

        private void StopClimbVFXInternal()
        {
            if (climbPS != null)
                climbPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            isCurrentlyClimbing = false;
        }
        #endregion

        #region Charge VFX Logic

        public void AuthorizeChargeVFX()
        {
            _chargeAuthorized = true;
        }

        public void SetChargeVFXState(bool isCharging)
        {
            if (chargePS == null) return;

            if (!vfxEnabled)
            {
                ForceStopCharge();
                return;
            }

            if (isCharging)
            {

                if (requireAuthorizationForCharge && !_chargeAuthorized)
                {
                    Debug.LogWarning("[PlayerVFX] attempt of turn on the Charge VFX without authorization. " +
                                     "Chame AuthorizeChargeVFX() first.");
                    return;
                }

                _chargeAuthorized = false;
            }

            if (isCharging == isCurrentlyCharging) return;
            isCurrentlyCharging = isCharging;

            if (_chargeRoutine != null)
                StopCoroutine(_chargeRoutine);

            _chargeRoutine = isCharging
                ? StartCoroutine(ChargeRampInRoutine())
                : StartCoroutine(ChargeRampOutRoutine());
        }

        public void PlayChargeVFX()
        {
            AuthorizeChargeVFX();
            SetChargeVFXState(true);
        }

        private IEnumerator ChargeRampInRoutine()
        {
            var emission = chargePS.emission;
            emission.rateOverTimeMultiplier = 0f;

            chargePS.Play(true);

            float t = 0f;
            while (t < chargeRampInDuration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / chargeRampInDuration);
                float factor = Mathf.Pow(norm, chargeRampInPower);

                emission.rateOverTimeMultiplier = _chargeMaxRate * factor;
                yield return null;
            }

            emission.rateOverTimeMultiplier = _chargeMaxRate;
            _chargeRoutine = null;
        }

        private IEnumerator ChargeRampOutRoutine()
        {
            var emission = chargePS.emission;
            float startRate = emission.rateOverTimeMultiplier;

            float t = 0f;
            while (t < chargeRampOutDuration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / chargeRampOutDuration);
                float factor = 1f - Mathf.Pow(norm, chargeRampOutPower);

                emission.rateOverTimeMultiplier = startRate * factor;
                yield return null;
            }

            emission.rateOverTimeMultiplier = 0f;
            chargePS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _chargeRoutine = null;
        }

        private void ForceStopCharge()
        {
            if (_chargeRoutine != null)
            {
                StopCoroutine(_chargeRoutine);
                _chargeRoutine = null;
            }

            if (chargePS != null)
            {
                var emission = chargePS.emission;
                emission.rateOverTimeMultiplier = 0f;
                chargePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            isCurrentlyCharging = false;
            _chargeAuthorized = false;
        }
        #endregion

        #region One-Shot Action VFX (Gatilhos)

        public void PlayJumpVFX() { if (vfxEnabled) PlayParticleSystem(dustData.jumpDustPS); }

        public void PlayLandVFX() { if (vfxEnabled) PlayParticleSystem(dustData.landDustPS); }

        public void PlayDashVFX() { if (vfxEnabled) PlayParticleSystem(dashTrailPS); }

        // Dispara todos os VFX do DashAttack. Slots vazios (None) são ignorados com
        // segurança; o Stop→Clear→Play reinicia emissões pendentes de dashes anteriores.
        public void PlayDashAttackVFX(Vector3 dashDirection)
        {
            if (!vfxEnabled || dashAttackVFX == null || dashAttackVFX.Count == 0)
                return;

            Quaternion dashRotation = Quaternion.LookRotation(dashDirection);

            for (int i = 0; i < dashAttackVFX.Count; i++)
            {
                ParticleSystem vfx = dashAttackVFX[i];

                if (vfx == null)
                    continue;

                // VFX externos (não filhos do Player) são movidos para a origem do dash
                // e alinhados com a direção; VFX filhos já seguem a rotação do corpo.
                if (!vfx.transform.IsChildOf(transform))
                    vfx.transform.SetPositionAndRotation(transform.position, dashRotation);

                PlayParticleSystem(vfx);
            }
        }

        public void PlayHurtVFX() { if (vfxEnabled) PlayParticleSystem(hurtImpactPS); }

        public void PlayHealVFX() { if (vfxEnabled) PlayParticleSystem(healBurstPS); }

        public void PlayLevelUpVFX() { if (vfxEnabled) PlayParticleSystem(levelUpPS); }

        private void PlayParticleSystem(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }
        #endregion

        #region Utility Methods

        public void StopAllContinuousVFX()
        {
            isCurrentlyEmittingDust = false;
            isCurrentlyBlocking = false;

            if (dustData.runDustPS != null)
                dustData.runDustPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (dashTrailPS != null)
                dashTrailPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (blockAuraPS != null)
                blockAuraPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            StopClimbVFXInternal();

            ForceStopCharge();
        }
        #endregion

        #region Gizmos & Debug
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 pos = transform.position;
            Gizmos.DrawLine(pos, pos + Vector3.down * dustData.groundCheckDistance);
            Gizmos.DrawWireSphere(pos + Vector3.down * dustData.groundCheckDistance, 0.05f);
        }
        #endregion
    }
}
