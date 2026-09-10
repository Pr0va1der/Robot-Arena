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
    // WebGL can report a natural AudioSource stop after its cursor has fallen
    // several frames behind the WebAudio node, so the timer and cursor need a
    // small backend-specific completion window.
    private const float NaturalIntroStopTimingToleranceSeconds = 0.5f;
    private const float IntroEndObservationToleranceSeconds = 0.75f;
    private const float NaturalIntroStopTimingToleranceRatio = 0.1f;

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
    private PauseMenu pauseMenu;
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
            MarkLifecyclePauseBoundary();
        }

        ApplyFallbackListenerPause();
    }

    private void OnApplicationPause(bool isPaused)
    {
        applicationPaused = isPaused;
        if (isPaused)
        {
            MarkLifecyclePauseBoundary();
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
        pauseMenu = FindObjectOfType<PauseMenu>();

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
                StartMode(library?.CalmIntro, library?.CalmLoop, MusicMode.Calm);
                calmIntroPlaying = true;
                break;
            case MusicCue.CombatIntro:
                StopCalmIntroPlayback();
                StartMode(library?.CombatIntro, library?.CombatLoop, MusicMode.Combat);
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
        MusicMode semanticMode = activeCue == MusicCue.CombatIntro
            ? MusicMode.Combat
            : MusicMode.Calm;
        StartMode(intro, loop, semanticMode);
    }

    private void StartMode(AudioClip intro, AudioClip loop, MusicMode semanticMode)
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

        group.StartSequence(intro, loop, semanticMode);
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
        int fadingSemanticModeMask = 0;
        int fadingGroupIndex = -1;
        bool intentionalCrossfade = false;
        MusicMode targetSemanticMode = deathPlaying
            ? MusicMode.Death
            : activeGroup >= 0
                ? groups[activeGroup].SemanticMode
                : MusicMode.Silent;

        if (groups != null)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                int groupAudibleSourceCount = groups[i].GetAudibleSourceCount(isAudioPaused);
                audibleSourceCount += groupAudibleSourceCount;
                if (groups[i].IsFadingOut)
                {
                    fadingGroupMask |= 1 << i;
                    if (groups[i].SemanticMode != MusicMode.Silent)
                    {
                        fadingSemanticModeMask |= 1 << (int)groups[i].SemanticMode;
                    }
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
                         groups[i].IsFadingOut &&
                         groups[i].SemanticMode != MusicMode.Silent &&
                         groups[i].SemanticMode != targetSemanticMode)
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
            fadingSemanticModeMask,
            audibleSourceCount,
            activeSequenceAudibleSourceCount,
            intentionalCrossfade,
            isAudioPaused,
            GetActivePauseSources());
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

    private PauseSource GetActivePauseSources()
    {
        if (pauseMenu == null)
        {
            pauseMenu = FindObjectOfType<PauseMenu>();
        }

        PauseSource sources = PauseMenu.CurrentActivePauseSources;
        if (pauseMenu != null)
        {
            sources |= pauseMenu.ActivePauseSources;
        }
        if (applicationFocusLost)
        {
            sources |= PauseSource.Focus;
        }

        if (applicationPaused)
        {
            sources |= PauseSource.Platform;
        }

        return sources;
    }

    private void MarkLifecyclePauseBoundary()
    {
        discardNextUnpausedDelta = true;
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
        private float maxObservedIntroPositionSeconds;
        private int maxObservedIntroTimeSamples;
        private bool introPlaybackObserved;
        private bool introCompletionObserved;
        private MusicMode semanticMode = MusicMode.Silent;

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
        public MusicMode SemanticMode => semanticMode;
        public bool IsFadingOut => Active && fadeTarget <= 0f && Gain > 0f;

        public void StartSequence(AudioClip intro, AudioClip loop, MusicMode semanticMode)
        {
            introSource.Stop();
            loopSource.Stop();
            introSource.clip = intro;
            introSource.loop = false;
            loopSource.clip = loop;
            loopSource.loop = true;
            this.semanticMode = semanticMode;
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
            maxObservedIntroPositionSeconds = 0f;
            maxObservedIntroTimeSamples = 0;
            introPlaybackObserved = false;
            introCompletionObserved = false;
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
                if (isPaused ||
                    !introSource.isPlaying ||
                    !sequence.MarkIntroStarted(GetIntroPositionSeconds()))
                {
                    return;
                }

                introPlaybackObserved = true;
                ObserveIntroPosition();
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
            ObserveIntroPosition();
            if (!introSource.isPlaying &&
                !introCompletionObserved &&
                sequence.IntroRemainingSeconds > GetNaturalIntroStopTimingToleranceSeconds())
            {
                // A browser lifecycle stop can arrive before OnApplicationFocus.
                // Keep the sequence in Intro and wait for the source to resume;
                // a source that was stopped permanently must never be promoted
                // to the loop by the elapsed-time fallback.
                needsIntroResumeConfirmation = true;
                return;
            }

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
            if (introSource.clip == null ||
                !introPlaybackObserved)
            {
                return false;
            }

            // The timer only bounds how much active time remains. WebGL can
            // keep isPlaying true for one frame at the exact clip boundary, so
            // an observed last sample is also authoritative. An intro stopped
            // early remains blocked above until the source is resumed.
            return introCompletionObserved ||
                (!introSource.isPlaying &&
                 sequence.IntroRemainingSeconds <= GetNaturalIntroStopTimingToleranceSeconds());
        }

        private void ObserveIntroPosition()
        {
            if (introSource.clip == null)
            {
                return;
            }

            float positionSeconds = GetIntroPositionSeconds();
            maxObservedIntroPositionSeconds = Mathf.Max(
                maxObservedIntroPositionSeconds,
                positionSeconds);
            maxObservedIntroTimeSamples = Mathf.Max(
                maxObservedIntroTimeSamples,
                Mathf.Max(0, introSource.timeSamples));
            if (HasReachedIntroEndPosition())
            {
                introCompletionObserved = true;
            }
        }

        private float GetIntroPositionSeconds()
        {
            return introSource.clip == null
                ? 0f
                : Mathf.Clamp(introSource.time, 0f, introSource.clip.length);
        }

        private bool HasReachedIntroEndPosition()
        {
            if (introSource.clip == null)
            {
                return false;
            }

            int lastSample = Mathf.Max(0, introSource.clip.samples - 1);
            if (maxObservedIntroTimeSamples >= lastSample)
            {
                return true;
            }

            float oneSampleSeconds = introSource.clip.frequency > 0
                ? 1f / introSource.clip.frequency
                : 0f;
            float observationTolerance = Mathf.Max(
                oneSampleSeconds,
                Mathf.Min(
                    IntroEndObservationToleranceSeconds,
                    introSource.clip.length * NaturalIntroStopTimingToleranceRatio));
            return maxObservedIntroPositionSeconds >= introSource.clip.length - observationTolerance;
        }

        private float GetNaturalIntroStopTimingToleranceSeconds()
        {
            if (introSource.clip == null)
            {
                return 0f;
            }

            return Mathf.Min(
                NaturalIntroStopTimingToleranceSeconds,
                introSource.clip.length * NaturalIntroStopTimingToleranceRatio);
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
            maxObservedIntroPositionSeconds = 0f;
            maxObservedIntroTimeSamples = 0;
            introPlaybackObserved = false;
            introCompletionObserved = false;
            semanticMode = MusicMode.Silent;
            Active = false;
        }

        private static bool IsAudible(AudioSource source)
        {
            return source != null && source.isPlaying && source.volume > 0f;
        }
    }
}
