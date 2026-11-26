using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(Rigidbody))]

    public class PrologueMolimoController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;

        [Header("Bound (area of movement)")]
        [Tooltip("CapsuleCollider that sets the area where the PrologueMolimo if can mover. " +
                 "Tipicamente inside of the GameObject own (ex.: 'PrologueBound').")]
        [SerializeField] private CapsuleCollider bound;

        [Tooltip("Padding adicional (meters) between the PrologueMolimo and the wall of the bound. " +
                 "if <= 0, is calculado automatically the partir of the own Collider of the PrologueMolimo " +
                 "(uses the larger extent horizontal).")]
        [SerializeField] private float boundPadding = 0f;

        [Tooltip("if true, the GameObject of the bound is destroyed when this PrologueMolimo is destroyed.")]
        [SerializeField] private bool destroyBoundOnDestroy = true;

        [Header("Player")]
        [Tooltip("reference when the Brain of the player.")]
        [SerializeField] private PlayerBrain playerBrain;

        [Header("MolimoAI")]
        [Tooltip("GameObject of the MolimoAI (must start desativado).")]
        [SerializeField] private GameObject molimoAI;

        [Tooltip("time (seconds) until the PrologueMolimo be destroyed after enable the MolimoAI.")]
        [SerializeField] private float destroyDelay = 0.1f;

        [Header("HUDs")]
        [Tooltip("Canvas of the player HUD. must start DESATIVADO (Unchecked). " +
                 "is ativado (Checked) when the PrologueMolimo passes the controle is the Player.")]
        [SerializeField] private GameObject playerHUDCanva;

        [Tooltip("Canvas of Molimo HUD. must start DESATIVADO (Unchecked). " +
                 "is ativado (Checked) when the PrologueMolimo passes the controle is the Player.")]
        [SerializeField] private GameObject molimoHUD;

        [Header("PrologueMolimo visual (light Pulsante)")]
        [Tooltip("MeshRenderer of the PrologueMolimo (the 'light' voadora). " +
                 "if remain empty, is searched automatically in the children.")]
        [SerializeField] private MeshRenderer prologueMolimoMesh;

        [Tooltip("Transform that will be scaled by pulse. " +
                 "recommended: the child dedicado (ex.: 'visual'), is not interferir with " +
                 "physics, HUDs or other scripts that mexam in the root. if empty, uses this GameObject.")]
        [SerializeField] private Transform pulseScaleTarget;

        [Tooltip("light optional associada when the PrologueMolimo. the intensidade also pulsa.")]
        [SerializeField] private Light prologueMolimoLight;

        [Tooltip("if the intensidade of the light in the Awake is 0, uses this value the base. " +
                 "useful when the light is configured in runtime by other script/animator.")]
        [SerializeField] private float fallbackLightIntensity = 3f;

        [Tooltip("uses Time.unscaledTime — the pulse continues same with Time.timeScale = 0.")]
        [SerializeField] private bool useUnscaledTime = true;

        [Tooltip("if true, also pulsa the emission of the material (creates instance of the material).")]
        [SerializeField] private bool pulseEmission = false;

        [Tooltip("Multiplicador maximum of the emission by up of the value base.")]
        [SerializeField] private float emissionPulseMultiplier = 3f;

        [Header("pulse — Ramp of Intensidade")]
        [Tooltip("speed minimum of pulse (primeiros seconds).")]
        [SerializeField] private float minPulseSpeed = 0.8f;

        [Tooltip("speed maximum of pulse (after the ramp completo).")]
        [SerializeField] private float maxPulseSpeed = 6f;

        [Tooltip("time (seconds) until the pulse atingir the intensidade maximum.")]
        [SerializeField] private float pulseRampDuration = 12f;

        [Tooltip("Amplitude minimum (variation of scale) of the pulse.")]
        [SerializeField] private float minPulseAmplitude = 0.10f;

        [Tooltip("Amplitude maximum (variation of scale) of the pulse.")]
        [SerializeField] private float maxPulseAmplitude = 0.35f;

        [Tooltip("Multiplicador of the amplitude applied to the intensidade of the light.")]
        [SerializeField] private float lightPulseMultiplier = 2.5f;

        [Header("pulse — state Calmo (near of the player)")]
        [Tooltip("radius the partir of the which the pulse acalma. normally " +
                 "DetectionRadius + PlayerRadius (2.0 + 0.5 = 2.5).")]
        [SerializeField] private float calmRadius = 2.5f;

        [Tooltip("Amplitude of the pulse when the Player is next.")]
        [SerializeField] private float calmPulseAmplitude = 0.06f;

        [Tooltip("speed of the pulse when the Player is next.")]
        [SerializeField] private float calmPulseSpeed = 0.7f;

        [Tooltip("how fast the transition is the state calmo acontece.")]
        [SerializeField] private float calmBlendSpeed = 2f;

        public static Vector3 LastProloguePosition { get; private set; }

        public static Quaternion LastPrologueRotation { get; private set; }

        public static bool HasValidProloguePosition { get; private set; }

        private const float DetectionRadius = 2.0f;
        private const float PlayerRadius = 0.5f;

        private Rigidbody rb;
        private InputSystem_Actions inputActions;
        private Vector2 moveInput;

        private bool playerDetected = false;
        private bool hasInteracted = false;

        private float _resolvedPadding = 0f;

        private Vector3 _initialScale;
        private float _initialLightIntensity;
        private float _pulseStartTime;
        private float _pulsePhase;
        private float _calmBlend;
        private Material _pulsedMaterial;
        private Color _baseEmissionColor;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.useGravity = false;

            inputActions = new InputSystem_Actions();

            if (playerBrain != null)
                playerBrain.enabled = false;

            if (molimoAI != null)
                molimoAI.SetActive(false);

            if (playerHUDCanva != null)
                playerHUDCanva.SetActive(false);

            if (molimoHUD != null)
                molimoHUD.SetActive(false);

            _resolvedPadding = ResolvePadding();

            if (prologueMolimoMesh == null)
                prologueMolimoMesh = GetComponentInChildren<MeshRenderer>();

            Transform scaleTarget = pulseScaleTarget != null ? pulseScaleTarget : transform;
            _initialScale = scaleTarget.localScale;

            if (prologueMolimoLight != null)
            {
                _initialLightIntensity = prologueMolimoLight.intensity;
                if (_initialLightIntensity <= 0.001f)
                    _initialLightIntensity = fallbackLightIntensity;
            }

            if (pulseEmission && prologueMolimoMesh != null)
            {
                _pulsedMaterial = prologueMolimoMesh.material;
                if (_pulsedMaterial != null && _pulsedMaterial.HasProperty("_EmissionColor"))
                {
                    _baseEmissionColor = _pulsedMaterial.GetColor("_EmissionColor");
                    _pulsedMaterial.EnableKeyword("_EMISSION");
                }
            }

            _pulseStartTime = Time.time;
            _pulsePhase = Random.Range(0f, 1000f);
        }

        private void OnEnable() => inputActions.Player.Enable();
        private void OnDisable() => inputActions.Player.Disable();

        private void OnDestroy()
        {
            inputActions?.Dispose();

            if (destroyBoundOnDestroy && bound != null && bound.gameObject != gameObject)
            {
                Destroy(bound.gameObject);
            }
        }

        private void Update()
        {
            UpdatePulse();

            if (hasInteracted) return;

            moveInput = inputActions.Player.Move.ReadValue<Vector2>();
            CheckPlayerProximity();
            HandleInteraction();
        }

        private void FixedUpdate()
        {
            if (hasInteracted) return;

            Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y);
            Vector3 desiredVel = dir * moveSpeed;
            desiredVel.y = 0f;

            if (bound != null)
            {
                Vector3 nextPos = rb.position + desiredVel * Time.fixedDeltaTime;
                Vector3 clampedPos = ClampInsideBound(nextPos);

                if ((clampedPos - nextPos).sqrMagnitude > 0.000001f)
                {
                    Vector3 allowedDelta = clampedPos - rb.position;
                    desiredVel = allowedDelta / Time.fixedDeltaTime;
                }
            }

            Vector3 vel = rb.linearVelocity;
            vel.x = desiredVel.x;
            vel.z = desiredVel.z;
            vel.y = 0f;
            rb.linearVelocity = vel;
        }

        private void UpdatePulse()
        {
            if (prologueMolimoMesh == null && prologueMolimoLight == null) return;

            float now = useUnscaledTime ? Time.unscaledTime : Time.time;
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            float rampT = Mathf.Clamp01((now - _pulseStartTime) / Mathf.Max(0.01f, pulseRampDuration));
            float smoothRamp = rampT * rampT * (3f - 2f * rampT);

            float rampSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, smoothRamp);
            float rampAmp = Mathf.Lerp(minPulseAmplitude, maxPulseAmplitude, smoothRamp);

            float targetCalm = 0f;
            if (playerBrain != null)
            {
                float d = Vector3.Distance(transform.position, playerBrain.transform.position);
                targetCalm = d <= calmRadius ? 1f : 0f;
            }
            _calmBlend = Mathf.MoveTowards(_calmBlend, targetCalm, calmBlendSpeed * dt);

            float speed = Mathf.Lerp(rampSpeed, calmPulseSpeed, _calmBlend);
            float amp = Mathf.Lerp(rampAmp, calmPulseAmplitude, _calmBlend);

            float t = now * speed + _pulsePhase;

            float noiseA = Mathf.PerlinNoise(t * 0.5f, 0f);
            float noiseB = Mathf.PerlinNoise(0f, t * 0.83f + 13.7f);
            float noise = ((noiseA + noiseB) * 0.5f) * 2f - 1f;

            float wave = Mathf.Sin(t) * 0.6f
                       + Mathf.Sin(t * 2.13f + 1.7f) * 0.3f;

            float pulse = Mathf.Clamp01((wave * 0.65f + noise * 0.35f + 1f) * 0.5f);

            if (prologueMolimoMesh != null)
            {
                Transform target = pulseScaleTarget != null ? pulseScaleTarget : transform;
                float scaleFactor = 1f + pulse * amp;
                target.localScale = _initialScale * scaleFactor;
            }

            if (prologueMolimoLight != null)
            {
                prologueMolimoLight.intensity = _initialLightIntensity
                                              * (1f + pulse * amp * lightPulseMultiplier);
            }

            if (_pulsedMaterial != null && _pulsedMaterial.HasProperty("_EmissionColor"))
            {
                float emissionMultiplier = 1f + pulse * amp * emissionPulseMultiplier;
                _pulsedMaterial.SetColor("_EmissionColor", _baseEmissionColor * emissionMultiplier);
            }
        }

        private float ResolvePadding()
        {
            if (boundPadding > 0f) return boundPadding;

            Collider own = GetComponent<Collider>();
            if (own != null)
            {
                Vector3 and = own.bounds.extents;
                return Mathf.Max(and.x, and.z);
            }

            return 0f;
        }

        private Vector3 ClampInsideBound(Vector3 worldPos)
        {
            if (bound == null) return worldPos;

            Transform bt = bound.transform;
            Vector3 local = bt.InverseTransformPoint(worldPos) - bound.center;

            int axis = (int)bound.direction;
            int a1 = (axis + 1) % 3;
            int a2 = (axis + 2) % 3;

            float halfHeight = Mathf.Max(0f, bound.height * 0.5f - bound.radius);
            local[axis] = Mathf.Clamp(local[axis], -halfHeight, halfHeight);

            float allowedRadius = Mathf.Max(0f, bound.radius - _resolvedPadding);

            Vector2 perp = new Vector2(local[a1], local[a2]);
            if (perp.sqrMagnitude > allowedRadius * allowedRadius)
            {
                perp = allowedRadius > 0f
                    ? perp.normalized * allowedRadius
                    : Vector2.zero;

                local[a1] = perp.x;
                local[a2] = perp.y;
            }

            return bt.TransformPoint(local + bound.center);
        }

        private void CheckPlayerProximity()
        {
            if (playerBrain == null) return;

            float distance = Vector3.Distance(transform.position, playerBrain.transform.position);
            bool overlapping = distance <= (DetectionRadius + PlayerRadius);

            if (overlapping && !playerDetected)
            {
                playerDetected = true;
            }
            else if (!overlapping && playerDetected)
            {
                playerDetected = false;
            }
        }

        private void HandleInteraction()
        {
            if (!playerDetected) return;

            if (inputActions.Player.Interact.WasPressedThisFrame())
                TriggerInteraction();
        }

        private void TriggerInteraction()
        {
            hasInteracted = true;

            LastProloguePosition = transform.position;
            LastPrologueRotation = transform.rotation;
            HasValidProloguePosition = true;

            if (PrologueManager.Instance != null)
                PrologueManager.Instance.BlendToMainCamera();

            if (molimoAI != null)
            {
                molimoAI.SetActive(true);
            }

            if (playerBrain != null)
            {
                playerBrain.PrepareWakeUpHandoff();
                playerBrain.enabled = true;
                playerBrain.BeginWakeUpSequence();
            }

            if (playerHUDCanva != null)
            {
                playerHUDCanva.SetActive(true);
            }

            if (molimoHUD != null)
            {
                molimoHUD.SetActive(true);
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            StartCoroutine(DestroyAfterDelay());
        }

        private IEnumerator DestroyAfterDelay()
        {
            yield return new WaitForSeconds(destroyDelay);
            Destroy(gameObject);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.25f);
            Gizmos.DrawSphere(transform.position, DetectionRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, DetectionRadius);

            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, calmRadius);

            if (playerBrain != null)
            {
                Gizmos.color = new Color(0f, 0.8f, 1f, 0.25f);
                Gizmos.DrawSphere(playerBrain.transform.position, PlayerRadius);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(playerBrain.transform.position, PlayerRadius);

                Gizmos.color = playerDetected ? Color.green : Color.grey;
                Gizmos.DrawLine(transform.position, playerBrain.transform.position);
            }
        }
    }
}
