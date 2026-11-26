using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Conta decrescente de morte (10 → 00) exibida no HUD quando o jogador morre,
    /// seguida da tela final de derrota. Não toca vinhetas/áudio: isso já é feito
    /// pelo PlayerHealthVignetteController — aqui apenas sincronizamos o pulso do
    /// texto com a mesma cadência de BPM da morte (30 BPM por padrão).
    /// </summary>
    public class PlayerDeathCountdownUI : MonoBehaviour
    {
        private enum CountdownState { Idle, Counting, Defeat }

        [Header("References")]
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private CanvasGroup defeatPanel;
        [SerializeField] private TextMeshProUGUI defeatText;
        [Tooltip("Opcional: inicia a contagem automaticamente quando esta vida chega a zero.")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Countdown")]
        [Tooltip("Valor inicial da contagem (exibido como dois dígitos, ex.: 10).")]
        [SerializeField, Min(1)] private int countdownStartValue = 10;
        [Tooltip("Cadência cardíaca do pulso do texto. Na morte a vignette roda a 30 BPM.")]
        [SerializeField, Range(20f, 240f)] private float beatsPerMinute = 30f;
        [Tooltip("Intensidade do pulso de escala (0.18 = escala vai de 1.0 até 1.18).")]
        [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.18f;

        [Header("Defeat Screen")]
        [SerializeField, Min(0.01f)] private float defeatFadeDuration = 1f;
        [Tooltip("Se ativo, o painel preto aparece instantaneamente (sem fade).")]
        [SerializeField] private bool snapDefeatInstantly = false;
        [Tooltip("Se ativo, mostra 'GAME OVER' em vez de 'PERDEU O JOGO!'.")]
        [SerializeField] private bool useGameOverText = false;

        [Header("Revive Input (optional)")]
        [Tooltip("Ativa uma tecla do New Input System para cancelar a contagem/reviver.")]
        [SerializeField] private bool enableReviveKey = false;
        [SerializeField] private Key reviveKey = Key.None;

        [Header("Events")]
        public UnityEvent onCountdownFinished;
        public UnityEvent onRevived;

        private const string DefeatMessage = "PERDEU O JOGO!";
        private const string GameOverMessage = "GAME OVER";

        private static readonly string[] TwoDigitText = BuildTwoDigitCache();

        private CountdownState state = CountdownState.Idle;
        private float remainingSeconds;
        private int lastDisplayedSecond = -1;
        private float pulsePhase;
        private float defeatFadeProgress;
        private bool defeatFullyShown;

        private void OnEnable()
        {
            ResetVisualState();
            state = CountdownState.Idle;

            if (playerHealth != null)
                playerHealth.OnDeath += HandlePlayerDied;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.OnDeath -= HandlePlayerDied;

            ResetVisualState();
            state = CountdownState.Idle;
        }

        private void Update()
        {
            // Sem custo por frame enquanto ociosa.
            if (state == CountdownState.Idle) return;

            HandleReviveKey();

            if (state == CountdownState.Counting)
            {
                TickCountdown();
                TickPulse();
            }
            else if (state == CountdownState.Defeat)
            {
                TickDefeatFade();
            }
        }

        // ---------------- Public API ----------------

        /// <summary>Inicia a contagem decrescente (também chamada no OnDeath do PlayerHealth).</summary>
        public void StartCountdown()
        {
            if (countdownText == null)
            {
                Debug.LogError("[PlayerDeathCountdownUI] Countdown Text não atribuído!", this);
                return;
            }

            if (state == CountdownState.Counting) return;

            ResetVisualState();
            state = CountdownState.Counting;
            countdownText.enabled = true;
            ShowSecond(countdownStartValue);
        }

        /// <summary>
        /// Cancela a contagem (ou fecha a tela de derrota), esconde o texto e
        /// reseta todo o estado sem tocar na vinheta/áudio existentes.
        /// </summary>
        public void CancelCountdownAndRevive()
        {
            if (state == CountdownState.Idle) return;

            ResetVisualState();
            state = CountdownState.Idle;
            onRevived?.Invoke();
        }

        /// <summary>Permite sincronizar o BPM do pulso com outro script (ex.: a vignette).</summary>
        public void SetHeartbeatBpm(float bpm)
        {
            beatsPerMinute = Mathf.Clamp(bpm, 20f, 240f);
        }

        // ---------------- Internals ----------------

        private void HandlePlayerDied() => StartCountdown();

        private void HandleReviveKey()
        {
            if (!enableReviveKey || reviveKey == Key.None) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[reviveKey].wasPressedThisFrame)
                CancelCountdownAndRevive();
        }

        private void TickCountdown()
        {
            remainingSeconds -= Time.unscaledDeltaTime;

            // Ceil garante: 10 no início, ..., 1 quando falta menos de 1s.
            int displayed = Mathf.Clamp(Mathf.CeilToInt(remainingSeconds), 0, countdownStartValue);
            if (displayed != lastDisplayedSecond)
                ShowSecond(displayed);

            if (remainingSeconds <= 0f)
                EnterDefeat();
        }

        private void TickPulse()
        {
            // Um ciclo completo por batida: sobe suavemente até 1 + pulseAmount e desce de volta a 1.
            pulsePhase += Time.unscaledDeltaTime * (beatsPerMinute / 60f);
            float beat = Mathf.Sin((pulsePhase % 1f) * Mathf.PI);
            countdownText.rectTransform.localScale = Vector3.one * (1f + pulseAmount * beat);
        }

        private void TickDefeatFade()
        {
            if (defeatPanel == null || defeatFullyShown) return;

            defeatFadeProgress += Time.unscaledDeltaTime / defeatFadeDuration;
            defeatPanel.alpha = Mathf.Clamp01(defeatFadeProgress);

            if (defeatFadeProgress >= 1f)
                SetDefeatPanelInteractable(true);
        }

        private void EnterDefeat()
        {
            state = CountdownState.Defeat;

            // Esconde e reseta o texto da contagem.
            if (countdownText != null)
            {
                countdownText.enabled = false;
                countdownText.rectTransform.localScale = Vector3.one;
            }

            if (defeatText != null)
                defeatText.text = useGameOverText ? GameOverMessage : DefeatMessage;

            if (defeatPanel != null)
            {
                if (snapDefeatInstantly)
                {
                    defeatPanel.alpha = 1f;
                    SetDefeatPanelInteractable(true);
                }
                else
                {
                    defeatFadeProgress = 0f;
                    defeatPanel.alpha = 0f;
                    SetDefeatPanelInteractable(false);
                }
            }

            onCountdownFinished?.Invoke();
        }

        private void SetDefeatPanelInteractable(bool value)
        {
            if (defeatPanel == null) return;

            defeatPanel.blocksRaycasts = value;
            defeatPanel.interactable = value;
            defeatFullyShown = value;
        }

        private void ShowSecond(int value)
        {
            if (countdownText == null) return;

            countdownText.text = TwoDigitText[Mathf.Clamp(value, 0, 99)];
            lastDisplayedSecond = value;
        }

        private void ResetVisualState()
        {
            remainingSeconds = countdownStartValue;
            lastDisplayedSecond = -1;
            pulsePhase = 0f;
            defeatFadeProgress = 0f;
            defeatFullyShown = false;

            if (countdownText != null)
            {
                countdownText.enabled = false;
                countdownText.rectTransform.localScale = Vector3.one;
            }

            if (defeatPanel != null)
            {
                defeatPanel.alpha = 0f;
                defeatPanel.blocksRaycasts = false;
                defeatPanel.interactable = false;
            }
        }

        private static string[] BuildTwoDigitCache()
        {
            var cache = new string[100];
            for (int i = 0; i < 100; i++) cache[i] = i.ToString("00");
            return cache;
        }
    }
}
