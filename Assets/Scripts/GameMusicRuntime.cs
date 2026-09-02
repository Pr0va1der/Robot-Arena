using RobotArena.Session;
using UnityEngine;
using UnityEngine.SceneManagement;
using ArenaPlayerSettings = RobotArena.Session.PlayerSettings;

[DefaultExecutionOrder(-150)]
public sealed class GameMusicRuntime : MonoBehaviour
{
    public const float CrossfadeDuration = 0.5f;

    private const double ScheduleLeadSeconds = 0.05;

    public static GameMusicRuntime Instance { get; private set; }

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
    private bool deathPlaying;
    private float deathGain;
    private float deathFadeStart;
    private float deathFadeTarget;
    private float deathFadeElapsed;
    private float deathFadeDuration;
    private float deathRemainingSeconds;
    private bool calmIntroPlaying;
    private float calmIntroRemainingSeconds;
    private int activeGroup = -1;
    private bool missingLibraryWarningShown;

    public MusicMode CurrentMode => stateMachine == null ? MusicMode.Silent : stateMachine.Mode;
    public bool AudioPermissionGranted => stateMachine != null && stateMachine.AudioPermissionGranted;

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
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
    }

    private void Update()
    {
        if (GameplayInputActions.Current.UserGesturePressed)
        {
            stateMachine.RegisterAudioGesture();
        }

        bool isPaused = IsMusicPaused();
        if (!isPaused)
        {
            stateMachine.Advance(Time.unscaledDeltaTime, false);
            AdvanceCalmIntro(Time.unscaledDeltaTime);
            UpdateFades(Time.unscaledDeltaTime);
        }

        if (!isPaused)
        {
            UpdateDeathPlayback(Time.unscaledDeltaTime);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        applicationFocusLost = !hasFocus;
        ApplyFallbackListenerPause();
    }

    private void OnApplicationPause(bool isPaused)
    {
        applicationPaused = isPaused;
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
        switch (cue)
        {
            case MusicCue.CalmIntro:
                StartMode(library?.CalmIntro, library?.CalmLoop);
                calmIntroPlaying = true;
                calmIntroRemainingSeconds = library?.CalmIntro == null
                    ? 0f
                    : library.CalmIntro.length + (float)ScheduleLeadSeconds;
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

        if (activeGroup >= 0)
        {
            groups[activeGroup].FadeTo(0f, CrossfadeDuration);
        }

        group.Schedule(intro, loop, AudioSettings.dspTime + ScheduleLeadSeconds);
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
        double startTime = AudioSettings.dspTime + ScheduleLeadSeconds;
        deathRemainingSeconds = library.Death.length + (float)ScheduleLeadSeconds;
        deathSource.PlayScheduled(startTime);
        deathPlaying = true;
    }

    private void FadeToSilence()
    {
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].FadeTo(0f, CrossfadeDuration);
        }

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

    private void AdvanceCalmIntro(float elapsedSeconds)
    {
        if (!calmIntroPlaying)
        {
            return;
        }

        calmIntroRemainingSeconds -= elapsedSeconds;
        if (calmIntroRemainingSeconds > 0f)
        {
            return;
        }

        calmIntroPlaying = false;
        calmIntroRemainingSeconds = 0f;
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
        calmIntroRemainingSeconds = 0f;
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
    }

    private sealed class MusicPlaybackGroup
    {
        private readonly AudioSource introSource;
        private readonly AudioSource loopSource;
        private float fadeStart;
        private float fadeTarget;
        private float fadeElapsed;
        private float fadeDuration;

        public MusicPlaybackGroup(AudioSource introSource)
        {
            this.introSource = introSource;
            loopSource = introSource.gameObject.AddComponent<AudioSource>();
            GameMusicRuntime.ConfigureAudioSource(loopSource);
            loopSource.volume = 0f;
        }

        public bool Active { get; private set; }
        public float Gain { get; private set; }

        public void Schedule(AudioClip intro, AudioClip loop, double startTime)
        {
            introSource.Stop();
            loopSource.Stop();
            introSource.clip = intro;
            introSource.loop = false;
            loopSource.clip = loop;
            loopSource.loop = true;
            introSource.PlayScheduled(startTime);
            loopSource.PlayScheduled(startTime + intro.length);
            Gain = 0f;
            fadeStart = 0f;
            fadeTarget = 0f;
            fadeElapsed = 0f;
            fadeDuration = 0f;
            Active = true;
        }

        public void FadeTo(float target, float duration)
        {
            fadeStart = Gain;
            fadeTarget = Mathf.Clamp01(target);
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
            introSource.Stop();
            loopSource.Stop();
            introSource.clip = null;
            loopSource.clip = null;
            introSource.volume = 0f;
            loopSource.volume = 0f;
            Gain = 0f;
            Active = false;
        }
    }
}
