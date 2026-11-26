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
        [Tooltip("Opcional: cérebro do jogador; usado para o devolver ao estado vivo no revive.")]
        [SerializeField] private PlayerBrain playerBrain;

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

        [Header("BGM Tape-Stop (on death)")]
        [Tooltip("AudioSource da música de fundo ativa na cena. Na morte sofre tape-stop (pitch down) + fade de volume.")]
        [SerializeField] private AudioSource backgroundMusic;
        [Tooltip("Pitch final do efeito tape-stop.")]
        [SerializeField, Range(0.05f, 1f)] private float bgmTargetPitch = 0.25f;
        [Tooltip("Duração da descida de pitch.")]
        [SerializeField, Min(0.01f)] private float bgmPitchDropDuration = 2.5f;
        [Tooltip("Duração do fade de volume da BGM até 0.")]
        [SerializeField, Min(0.01f)] private float bgmVolumeFadeDuration = 2.5f;
        [Tooltip("Duração da restauração de pitch/volume no revive.")]
        [SerializeField, Min(0.01f)] private float bgmRestoreDuration = 0.5f;

        [Header("Heartbeat (via Vignette)")]
        [Tooltip("Controlador da vinheta que toca o batimento; silenciado no game over.")]
        [SerializeField] private PlayerHealthVignetteController vignetteController;
        [Tooltip("Duração do fade out do batimento ao chegar em 00.")]
        [SerializeField, Min(0.01f)] private float heartbeatFadeOutDuration = 1f;

        [Header("Ambient")]
        [Tooltip("AudioSource do som ambiente da cena (fade out no game over, restaurado no revive).")]
        [SerializeField] private AudioSource ambientAudio;
        [SerializeField, Min(0.01f)] private float ambientFadeOutDuration = 2f;
        [SerializeField, Min(0.01f)] private float ambientRestoreDuration = 0.5f;

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

        private bool bgmTapeStopActive;
        private bool bgmRestoring;
        private bool bgmPaused;
        private bool bgmWasPlaying;
        private float bgmOriginalPitch = 1f;
        private float bgmOriginalVolume = 1f;
        private float bgmSavedTime;
        private float bgmTimer;

        private bool ambientFading;
        private bool ambientRestoring;
        private bool ambientMuted;
        private bool ambientWasPlaying;
        private float ambientOriginalVolume = 1f;
        private float ambientTimer;

        private void OnEnable()
        {
            ResetVisualState();
            state = CountdownState.Idle;

            if (playerHealth != null)
                playerHealth.OnDeath += HandlePlayerDied;

            if (playerBrain == null && Application.isPlaying)
                playerBrain = FindAnyObjectByType<PlayerBrain>();

            if (playerBrain == null)
                Debug.LogWarning("[PlayerDeathCountdownUI] PlayerBrain não encontrado: o revive não vai restaurar o estado do jogador.", this);
        }

        private void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.OnDeath -= HandlePlayerDied;

            SnapAudioToDefaults();
            ResetVisualState();
            state = CountdownState.Idle;
        }

        private void Update()
        {
            // Sem custo por frame enquanto ociosa e sem transições de áudio pendentes.
            if (state == CountdownState.Idle &&
                !bgmTapeStopActive && !bgmRestoring &&
                !ambientFading && !ambientRestoring) return;

            if (state != CountdownState.Idle)
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

            TickAudio();
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

            BeginBgmTapeStop();
        }

        /// <summary>
        /// Cancela a contagem (ou fecha a tela de derrota), esconde o texto, reseta todo
        /// o estado visual e restaura BGM/ambiente/batimento aos valores originais.
        /// </summary>
        public void CancelCountdownAndRevive()
        {
            if (state == CountdownState.Idle) return;

            ResetVisualState();
            state = CountdownState.Idle;

            // Devolve o jogador ao estado vivo (saúde, física, animação de levantar,
            // controlo de movimento e sistemas) — o próprio Brain ignora se já estiver vivo.
            if (playerBrain != null)
                playerBrain.Revive();

            // Limpa imediatamente a vinheta de morte em vez de esperar pelo fade de cura.
            if (vignetteController != null)
                vignetteController.ResetVignetteToAlive();

            BeginAudioRestore();
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

            // Silencia o batimento e o ambiente para a tela de derrota em silêncio total.
            if (vignetteController != null)
                vignetteController.FadeOutAndMuteHeartbeat(heartbeatFadeOutDuration);

            BeginAmbientFadeOut();

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

        // ---------------- Audio flow (BGM / heartbeat / ambient) ----------------

        private void TickAudio()
        {
            if (bgmTapeStopActive) TickBgmTapeStop();
            else if (bgmRestoring) TickBgmRestore();

            if (ambientFading) TickAmbientFadeOut();
            else if (ambientRestoring) TickAmbientRestore();
        }

        /// <summary>Tape-stop: pitch desce até bgmTargetPitch e volume faz fade até 0 (unscaled), depois para limpo.</summary>
        private void BeginBgmTapeStop()
        {
            if (backgroundMusic == null) return;

            bgmOriginalPitch = backgroundMusic.pitch;
            bgmOriginalVolume = backgroundMusic.volume;
            bgmWasPlaying = backgroundMusic.isPlaying;
            bgmSavedTime = backgroundMusic.time;
            bgmTapeStopActive = true;
            bgmPaused = false;
            bgmRestoring = false;
            bgmTimer = 0f;
        }

        private void TickBgmTapeStop()
        {
            bgmTimer += Time.unscaledDeltaTime;

            float pitchT = Mathf.Clamp01(bgmTimer / bgmPitchDropDuration);
            float volumeT = Mathf.Clamp01(bgmTimer / bgmVolumeFadeDuration);

            backgroundMusic.pitch = Mathf.Lerp(bgmOriginalPitch, bgmTargetPitch, pitchT);
            backgroundMusic.volume = Mathf.Lerp(bgmOriginalVolume, 0f, volumeT);

            if (pitchT >= 1f && volumeT >= 1f)
            {
                // Pause em vez de Stop: preserva o clip/estado e evita perder a posição;
                // o verdadeiro ponto de retoma é o timestamp guardado no início da morte.
                bgmTapeStopActive = false;
                bgmPaused = true;
                backgroundMusic.Pause();
                backgroundMusic.volume = 0f;
            }
        }

        private void TickBgmRestore()
        {
            bgmTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(bgmTimer / bgmRestoreDuration);

            // Pitch sobe suavemente até ao valor original e o volume faz fade in do silêncio.
            backgroundMusic.pitch = Mathf.Lerp(backgroundMusic.pitch, bgmOriginalPitch, t);
            backgroundMusic.volume = Mathf.Lerp(backgroundMusic.volume, bgmOriginalVolume, t);

            if (t >= 1f)
            {
                bgmRestoring = false;
                bgmPaused = false;
                backgroundMusic.pitch = bgmOriginalPitch;
                backgroundMusic.volume = bgmOriginalVolume;
            }
        }

        private void BeginAmbientFadeOut()
        {
            if (ambientAudio == null) return;

            ambientOriginalVolume = ambientAudio.volume;
            ambientWasPlaying = ambientAudio.isPlaying;
            ambientFading = true;
            ambientRestoring = false;
            ambientMuted = false;
            ambientTimer = 0f;
        }

        private void TickAmbientFadeOut()
        {
            ambientTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(ambientTimer / ambientFadeOutDuration);

            ambientAudio.volume = Mathf.Lerp(ambientOriginalVolume, 0f, t);

            if (t >= 1f)
            {
                ambientFading = false;
                ambientMuted = true;
                ambientAudio.Stop();
            }
        }

        private void TickAmbientRestore()
        {
            ambientTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(ambientTimer / ambientRestoreDuration);

            ambientAudio.volume = Mathf.Lerp(ambientAudio.volume, ambientOriginalVolume, t);

            if (t >= 1f)
            {
                ambientRestoring = false;
                ambientMuted = false;
                ambientAudio.volume = ambientOriginalVolume;

                if (ambientWasPlaying && !ambientAudio.isPlaying)
                    ambientAudio.Play();
            }
        }

        /// <summary>Devolve BGM, ambiente e batimento aos valores originais (revive/reset).</summary>
        private void BeginAudioRestore()
        {
            if (backgroundMusic != null && (bgmTapeStopActive || bgmPaused))
            {
                bgmTapeStopActive = false;
                bgmRestoring = true;
                bgmTimer = 0f;

                // Retoma a partir do timestamp guardado quando a morte começou (a música
                // abrandada que tocou durante a contagem é descartada).
                if (bgmWasPlaying)
                {
                    backgroundMusic.time = bgmSavedTime;
                    backgroundMusic.volume = 0f;

                    if (!backgroundMusic.isPlaying)
                        backgroundMusic.Play();
                }
            }

            if (ambientAudio != null && (ambientFading || ambientMuted))
            {
                ambientFading = false;
                ambientRestoring = true;
                ambientTimer = 0f;
            }

            if (vignetteController != null)
                vignetteController.RestoreHeartbeat();
        }

        /// <summary>
        /// Restaura imediatamente BGM/ambiente/batimento sem fades (usado no OnDisable
        /// para nunca deixar uma fonte com pitch/volume alterados entre runs).
        /// </summary>
        private void SnapAudioToDefaults()
        {
            bool bgmAffected = bgmTapeStopActive || bgmRestoring || bgmPaused;
            bool ambientAffected = ambientFading || ambientRestoring || ambientMuted;

            if (backgroundMusic != null && bgmAffected)
            {
                bgmTapeStopActive = false;
                bgmRestoring = false;
                bgmPaused = false;
                backgroundMusic.pitch = bgmOriginalPitch;
                backgroundMusic.volume = bgmOriginalVolume;

                if (bgmWasPlaying && !backgroundMusic.isPlaying)
                {
                    backgroundMusic.time = bgmSavedTime;
                    backgroundMusic.Play();
                }
            }

            if (ambientAudio != null && ambientAffected)
            {
                ambientFading = false;
                ambientRestoring = false;
                ambientMuted = false;
                ambientAudio.volume = ambientOriginalVolume;

                if (ambientWasPlaying && !ambientAudio.isPlaying)
                    ambientAudio.Play();
            }

            if (bgmAffected || ambientAffected)
            {
                if (vignetteController != null)
                    vignetteController.RestoreHeartbeat();
            }
        }
    }
}
