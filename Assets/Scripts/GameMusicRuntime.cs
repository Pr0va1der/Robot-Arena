using RobotArena.Session;
using UnityEngine;
using UnityEngine.SceneManagement;
using ArenaPlayerSettings = RobotArena.Session.PlayerSettings;

[DefaultExecutionOrder(-150)]
public sealed class GameMusicRuntime : MonoBehaviour
{
    public const float CrossfadeDuration = 0.5f;

    private const double DeathScheduleLeadSeconds = 0.05;
    private const float ResumeSourceStallToleranceSeconds = 0.05f;
    private const float SourceEndObservationToleranceSeconds = 0.05f;

    public static GameMusicRuntime Instance { get; private set; }
    private static int activeOwnerCount;

    private MusicStateMachine stateMachine;
    private MusicLibrary library;
    private MusicPlaybackGroup[] groups;
    private AudioSource deathSource;
    private GameSettingsRuntime settingsRuntime;
    private BotCombatRuntime combatRuntime;
    private BotSpawnManager sessionManager;
    private PlayerHP playerHealth;
    private bool applicationFocusLost;
    private bool applicationPaused;
    private bool musicWasPaused;
    private bool discardNextUnpausedDelta;
    private bool ownerRegistered;
    private bool deathPlaying;
    private float deathGain;
    private float deathFadeStart;
    private float deathFadeTarget;
    private float deathFadeElapsed;
    private float deathFadeDuration;
    private float deathRemainingSeconds;
    private bool calmIntroPlaying;
    private int activeGroup = -1;
    private MusicCue activeCue = MusicCue.None;
    private bool missingLibraryWarningShown;
    private MusicRuntimeDiagnostics lastDiagnostics;
    private bool diagnosticsInitialized;

    public MusicMode CurrentMode => stateMachine == null ? MusicMode.Silent : stateMachine.Mode;
    public bool AudioPermissionGranted => stateMachine != null && stateMachine.AudioPermissionGranted;
    public MusicRuntimeDiagnostics Diagnostics => CreateDiagnostics();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallAtStartup()
    {
        GetOrCreate();
    }

    public static GameMusicRuntime GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        // A scene-authored runtime may be discovered before its Awake callback
        // has populated Instance. Reuse it instead of creating a second owner.
        GameMusicRuntime existing = FindObjectOfType<GameMusicRuntime>();
        if (existing != null)
        {
            return existing;
        }

        GameObject runtimeObject = new GameObject("GameMusicRuntime");
        return runtimeObject.AddComponent<GameMusicRuntime>();
    }

    public void BeginFreshCalm()
    {
        stateMachine?.StartCalm();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            activeOwnerCount++;
            ownerRegistered = true;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        activeOwnerCount++;
        ownerRegistered = true;
        DontDestroyOnLoad(gameObject);

        stateMachine = new MusicStateMachine();
        stateMachine.CueRequested += OnCueRequested;

        library = Resources.Load<MusicLibrary>("RobotArenaMusic");
        groups = new[]
        {
            new MusicPlaybackGroup(CreateAudioSource("CalmOrCombatA")),
            new MusicPlaybackGroup(CreateAudioSource("CalmOrCombatB"))
        };
        deathSource = CreateAudioSource("Death");

        settingsRuntime = GameSettingsRuntime.GetOrCreate();
        settingsRuntime.SettingsChanged += OnSettingsChanged;

        combatRuntime = BotCombatRuntime.GetOrCreate();
        combatRuntime.Tracker.CombatPresenceChanged += OnCombatPresenceChanged;

        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyVolumes(settingsRuntime.Current);
        stateMachine.StartCalm();
        stateMachine.SetCombatPresence(combatRuntime.Tracker.AnyBotInCombat);
        BindSceneSystems();
        PublishDiagnostics();
    }

    private void Update()
    {
        if (GameplayInputActions.Current.UserGesturePressed)
        {
            stateMachine.RegisterAudioGesture();
        }

        bool isPaused = IsMusicPaused();
        bool resumedThisFrame = !isPaused && (musicWasPaused || discardNextUnpausedDelta);
        float elapsedSeconds = resumedThisFrame ? 0f : Time.unscaledDeltaTime;
        if (!isPaused)
        {
            stateMachine.Advance(elapsedSeconds, false);
            UpdateFades(elapsedSeconds);
        }

        AdvanceMusicPlayback(elapsedSeconds, isPaused);

        if (!isPaused)
        {
            AdvanceCalmIntro();
            UpdateDeathPlayback(elapsedSeconds);
            discardNextUnpausedDelta = false;
        }

        musicWasPaused = isPaused;
        PublishDiagnostics();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        applicationFocusLost = !hasFocus;
        if (!hasFocus)
        {
            discardNextUnpausedDelta = true;
        }

        ApplyFallbackListenerPause();
    }

    private void OnApplicationPause(bool isPaused)
    {
        applicationPaused = isPaused;
        if (isPaused)
        {
            discardNextUnpausedDelta = true;
        }

        ApplyFallbackListenerPause();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindSceneSystems();
        ApplyFallbackListenerPause();
    }

    private void BindSceneSystems()
    {
        if (sessionManager != null)
        {
            sessionManager.SessionStateChanged -= OnSessionStateChanged;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerDied;
        }

        sessionManager = FindObjectOfType<BotSpawnManager>();
        playerHealth = FindObjectOfType<PlayerHP>();

        if (sessionManager != null)
        {
            sessionManager.SessionStateChanged += OnSessionStateChanged;
            OnSessionStateChanged(sessionManager.State);
        }

        if (playerHealth != null)
        {
            playerHealth.Died += OnPlayerDied;
        }
    }

    private void OnCombatPresenceChanged(bool isPresent)
    {
        stateMachine.SetCombatPresence(isPresent);
    }

    private void OnSessionStateChanged(SessionState state)
    {
        if (state == SessionState.Won)
        {
            stateMachine.CompleteVictory();
        }
        else if (state == SessionState.Lost)
        {
            stateMachine.CompleteDeath();
        }
    }

    private void OnPlayerDied()
    {
        stateMachine.CompleteDeath();
    }

    private void OnCueRequested(MusicCue cue)
    {
        activeCue = cue;
        switch (cue)
        {
            case MusicCue.CalmIntro:
                StartMode(library?.CalmIntro, library?.CalmLoop);
                calmIntroPlaying = true;
                break;
            case MusicCue.CombatIntro:
                StopCalmIntroPlayback();
                StartMode(library?.CombatIntro, library?.CombatLoop);
                break;
            case MusicCue.Death:
                StopCalmIntroPlayback();
                StartDeath();
                break;
            case MusicCue.Silent:
                StopCalmIntroPlayback();
                FadeToSilence();
                break;
        }
    }

    private void StartMode(AudioClip intro, AudioClip loop)
    {
        if (intro == null || loop == null)
        {
            WarnMissingLibrary();
            FadeToSilence();
            return;
        }

        StopDeath();
        int nextGroup = activeGroup < 0 ? 0 : 1 - activeGroup;
        MusicPlaybackGroup group = groups[nextGroup];
        if (group.Active)
        {
            group.Stop();
        }

        if (activeGroup >= 0 && groups[activeGroup].Active)
        {
            groups[activeGroup].FadeTo(0f, CrossfadeDuration);
        }

        group.StartSequence(intro, loop);
        group.FadeTo(1f, CrossfadeDuration);
        activeGroup = nextGroup;
    }

    private void StartDeath()
    {
        if (library == null || library.Death == null)
        {
            WarnMissingLibrary();
            FadeToSilence();
            stateMachine.CompleteDeathPlayback();
            return;
        }

        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].FadeTo(0f, CrossfadeDuration);
        }

        deathSource.Stop();
        deathSource.clip = library.Death;
        deathSource.loop = false;
        deathSource.volume = 0f;
        deathGain = 0f;
        deathFadeStart = 0f;
        deathFadeTarget = 1f;
        deathFadeElapsed = 0f;
        deathFadeDuration = CrossfadeDuration;
        activeGroup = -1;
        double startTime = AudioSettings.dspTime + DeathScheduleLeadSeconds;
        deathRemainingSeconds = library.Death.length + (float)DeathScheduleLeadSeconds;
        deathSource.PlayScheduled(startTime);
        deathPlaying = true;
    }

    private void FadeToSilence()
    {
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].FadeTo(0f, CrossfadeDuration);
        }

        activeGroup = -1;
        StopDeath();
    }

    private void UpdateFades(float elapsedSeconds)
    {
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].AdvanceFade(elapsedSeconds);
            groups[i].ApplyVolume(GetMusicVolume());
        }

        if (deathPlaying)
        {
            deathFadeElapsed = Mathf.Min(deathFadeDuration, deathFadeElapsed + elapsedSeconds);
            deathGain = EvaluateFade(deathFadeStart, deathFadeTarget, deathFadeElapsed, deathFadeDuration);
            deathSource.volume = GetMusicVolume() * deathGain;
        }
    }

    private void AdvanceMusicPlayback(float elapsedSeconds, bool isPaused)
    {
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].AdvancePlayback(elapsedSeconds, isPaused);
        }
    }

    private MusicRuntimeDiagnostics CreateDiagnostics()
    {
        bool isAudioPaused = IsMusicPaused();
        int audibleSourceCount = 0;
        int activeSequenceAudibleSourceCount = 0;
        MusicPlaybackPhase playbackPhase = MusicPlaybackPhase.Silent;
        int fadingGroupMask = 0;
        int fadingGroupIndex = -1;
        bool intentionalCrossfade = false;

        if (groups != null)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                int groupAudibleSourceCount = groups[i].GetAudibleSourceCount(isAudioPaused);
                audibleSourceCount += groupAudibleSourceCount;
                if (groups[i].IsFadingOut)
                {
                    fadingGroupMask |= 1 << i;
                    if (fadingGroupIndex < 0)
                    {
                        fadingGroupIndex = i;
                    }
                }
                if (i == activeGroup)
                {
                    activeSequenceAudibleSourceCount = groupAudibleSourceCount;
                    playbackPhase = groups[i].PlaybackPhase;
                }
                else if ((activeGroup >= 0 || deathPlaying) &&
                         groupAudibleSourceCount > 0 &&
                         groups[i].IsFadingOut)
                {
                    intentionalCrossfade = true;
                }
            }
        }

        if (!isAudioPaused &&
            deathPlaying &&
            deathSource != null &&
            deathSource.isPlaying &&
            deathSource.volume > 0f)
        {
            audibleSourceCount++;
        }

        return new MusicRuntimeDiagnostics(
            activeOwnerCount,
            CurrentMode,
            activeCue,
            playbackPhase,
            activeGroup,
            fadingGroupIndex,
            fadingGroupMask,
            audibleSourceCount,
            activeSequenceAudibleSourceCount,
            intentionalCrossfade,
            isAudioPaused);
    }

    private void PublishDiagnostics()
    {
        MusicRuntimeDiagnostics diagnostics = Diagnostics;
        if (diagnosticsInitialized && diagnostics.Equals(lastDiagnostics))
        {
            return;
        }

        lastDiagnostics = diagnostics;
        diagnosticsInitialized = true;
        Debug.Log("[RobotArena.Music] " + diagnostics);
        if (diagnostics.ActiveSequenceAudibleSourceCount > 1)
        {
            Debug.LogError("[RobotArena.Music] Same-sequence audible source overlap detected: " + diagnostics);
        }
        if (diagnostics.OwnerCount > 1)
        {
            Debug.LogError("[RobotArena.Music] Multiple runtime owners detected: " + diagnostics);
        }
    }

    private void UpdateDeathPlayback(float elapsedSeconds)
    {
        if (!deathPlaying)
        {
            return;
        }

        deathRemainingSeconds -= elapsedSeconds;
        if (deathRemainingSeconds <= 0f)
        {
            StopDeath();
            stateMachine.CompleteDeathPlayback();
        }
    }

    private void AdvanceCalmIntro()
    {
        if (!calmIntroPlaying)
        {
            return;
        }

        if (activeGroup >= 0 && groups[activeGroup].Active)
        {
            if (groups[activeGroup].PlaybackPhase != MusicPlaybackPhase.Loop)
            {
                return;
            }
        }

        calmIntroPlaying = false;
        stateMachine.CompleteCalmIntro();
    }

    private void StopDeath()
    {
        deathPlaying = false;
        deathGain = 0f;
        deathRemainingSeconds = 0f;
        if (deathSource == null)
        {
            return;
        }

        deathSource.Stop();
        deathSource.clip = null;
        deathSource.volume = 0f;
    }

    private void StopCalmIntroPlayback()
    {
        calmIntroPlaying = false;
    }

    private void OnSettingsChanged(ArenaPlayerSettings settings)
    {
        ApplyVolumes(settings);
    }

    private void ApplyVolumes(ArenaPlayerSettings settings)
    {
        float volume = settings.IsMuted ? 0f : settings.MusicVolume;
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].ApplyVolume(volume);
        }

        if (deathSource != null)
        {
            deathSource.volume = volume * deathGain;
        }
    }

    private float GetMusicVolume()
    {
        if (settingsRuntime == null)
        {
            return 1f;
        }

        ArenaPlayerSettings settings = settingsRuntime.Current;
        return settings.IsMuted ? 0f : settings.MusicVolume;
    }

    private bool IsMusicPaused()
    {
        return applicationFocusLost ||
               applicationPaused ||
               PauseMenu.AudioIsPaused;
    }

    private void ApplyFallbackListenerPause()
    {
        if (FindObjectOfType<PauseMenu>() == null)
        {
            AudioListener.pause = applicationFocusLost || applicationPaused;
        }
    }

    private AudioSource CreateAudioSource(string name)
    {
        GameObject sourceObject = new GameObject(name);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        ConfigureAudioSource(source);
        source.volume = 0f;
        return source;
    }

    private static void ConfigureAudioSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.ignoreListenerPause = false;
    }

    private void WarnMissingLibrary()
    {
        if (missingLibraryWarningShown)
        {
            return;
        }

        missingLibraryWarningShown = true;
        Debug.LogError("Robot Arena music library is missing one or more clips.");
    }

    private static float EvaluateFade(float start, float target, float elapsed, float duration)
    {
        if (duration <= 0f)
        {
            return target;
        }

        return Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
    }

    private void OnDestroy()
    {
        if (stateMachine != null)
        {
            stateMachine.CueRequested -= OnCueRequested;
        }

        if (settingsRuntime != null)
        {
            settingsRuntime.SettingsChanged -= OnSettingsChanged;
        }

        if (combatRuntime != null)
        {
            combatRuntime.Tracker.CombatPresenceChanged -= OnCombatPresenceChanged;
        }

        if (sessionManager != null)
        {
            sessionManager.SessionStateChanged -= OnSessionStateChanged;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerDied;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopDeath();
        if (groups != null)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].Stop();
            }
        }

        if (Instance == this)
        {
            Instance = null;
        }
        if (ownerRegistered)
        {
            activeOwnerCount = Mathf.Max(0, activeOwnerCount - 1);
            ownerRegistered = false;
        }
    }

    private sealed class MusicPlaybackGroup
    {
        private readonly AudioSource introSource;
        private readonly AudioSource loopSource;
        private readonly MusicPlaybackSequence sequence = new MusicPlaybackSequence();
        private float fadeStart;
        private float fadeTarget;
        private float fadeElapsed;
        private float fadeDuration;
        private bool needsIntroResumeConfirmation;

        public MusicPlaybackGroup(AudioSource introSource)
        {
            this.introSource = introSource;
            loopSource = introSource.gameObject.AddComponent<AudioSource>();
            GameMusicRuntime.ConfigureAudioSource(loopSource);
            loopSource.volume = 0f;
        }

        public bool Active { get; private set; }
        public float Gain { get; private set; }
        public MusicPlaybackPhase PlaybackPhase => sequence.Phase;
        public bool IsFadingOut => Active && fadeTarget <= 0f && Gain > 0f;

        public void StartSequence(AudioClip intro, AudioClip loop)
        {
            introSource.Stop();
            loopSource.Stop();
            introSource.clip = intro;
            introSource.loop = false;
            loopSource.clip = loop;
            loopSource.loop = true;
            sequence.QueueIntro(intro.length);
            if (sequence.Phase == MusicPlaybackPhase.Loop)
            {
                loopSource.Play();
            }
            else
            {
                introSource.Play();
            }

            Gain = 0f;
            fadeStart = 0f;
            fadeTarget = 0f;
            fadeElapsed = 0f;
            fadeDuration = 0f;
            needsIntroResumeConfirmation = false;
            Active = true;
        }

        public void AdvancePlayback(float elapsedSeconds, bool isPaused)
        {
            if (!Active)
            {
                return;
            }

            if (sequence.Phase == MusicPlaybackPhase.WaitingForIntro)
            {
                if (!introSource.isPlaying || !sequence.MarkIntroStarted())
                {
                    return;
                }

                return;
            }

            if (isPaused)
            {
                if (sequence.Phase == MusicPlaybackPhase.Intro)
                {
                    needsIntroResumeConfirmation = true;
                }

                return;
            }

            if (needsIntroResumeConfirmation &&
                !introSource.isPlaying &&
                sequence.IntroRemainingSeconds > ResumeSourceStallToleranceSeconds)
            {
                return;
            }

            needsIntroResumeConfirmation = false;
            bool actualIntroComplete = IsIntroActuallyComplete();
            if (!sequence.Advance(elapsedSeconds, false, actualIntroComplete))
            {
                return;
            }

            introSource.Stop();
            loopSource.Play();
        }

        private bool IsIntroActuallyComplete()
        {
            if (introSource.clip == null)
            {
                return false;
            }

            // Unity WebGL can keep the source marked as playing at the exact
            // clip boundary, so position evidence uses a small observation
            // tolerance. A stopped source is accepted only after the
            // countdown anchored to its acknowledged start has elapsed.
            return (!introSource.isPlaying && sequence.IntroRemainingSeconds <= 0f) ||
                introSource.time >= introSource.clip.length - SourceEndObservationToleranceSeconds;
        }

        public int GetAudibleSourceCount(bool isAudioPaused)
        {
            if (isAudioPaused)
            {
                return 0;
            }

            int count = 0;
            if (IsAudible(introSource))
            {
                count++;
            }

            if (IsAudible(loopSource))
            {
                count++;
            }

            return count;
        }

        public void FadeTo(float target, float duration)
        {
            fadeStart = Gain;
            fadeTarget = Mathf.Clamp01(target);
            if (fadeTarget <= 0f)
            {
                sequence.Stop();
            }

            fadeElapsed = 0f;
            fadeDuration = duration;
            if (duration <= 0f)
            {
                Gain = fadeTarget;
            }
        }

        public void AdvanceFade(float elapsedSeconds)
        {
            if (!Active)
            {
                return;
            }

            fadeElapsed = Mathf.Min(fadeDuration, fadeElapsed + elapsedSeconds);
            Gain = GameMusicRuntime.EvaluateFade(fadeStart, fadeTarget, fadeElapsed, fadeDuration);
            if (fadeTarget <= 0f && Gain <= 0f && fadeElapsed >= fadeDuration)
            {
                Stop();
            }
        }

        public void ApplyVolume(float musicVolume)
        {
            float volume = musicVolume * Gain;
            introSource.volume = volume;
            loopSource.volume = volume;
        }

        public void Stop()
        {
            sequence.Stop();
            introSource.Stop();
            loopSource.Stop();
            introSource.clip = null;
            loopSource.clip = null;
            introSource.volume = 0f;
            loopSource.volume = 0f;
            Gain = 0f;
            needsIntroResumeConfirmation = false;
            Active = false;
        }

        private static bool IsAudible(AudioSource source)
        {
            return source != null && source.isPlaying && source.volume > 0f;
        }
    }
}
