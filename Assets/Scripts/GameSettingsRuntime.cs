using System;
using RobotArena.Session;
using UnityEngine;
using ArenaPlayerSettings = RobotArena.Session.PlayerSettings;

public sealed class GameSettingsRuntime : MonoBehaviour
{
    public static GameSettingsRuntime Instance { get; private set; }

    public ArenaPlayerSettings Current { get; private set; }

    public event Action<ArenaPlayerSettings> SettingsChanged;

    private IPlayerSettingsStore store;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallAtStartup()
    {
        GetOrCreate();
    }

    public static GameSettingsRuntime GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject settingsObject = new GameObject("GameSettingsRuntime");
        return settingsObject.AddComponent<GameSettingsRuntime>();
    }

    public void SetLanguage(GameLanguage language)
    {
        UpdateSettings(Current.WithLanguage(language));
    }

    public void SetMasterVolume(float volume)
    {
        UpdateSettings(Current.WithMasterVolume(volume));
    }

    public void SetMusicVolume(float volume)
    {
        UpdateSettings(Current.WithMusicVolume(volume));
    }

    public void SetSfxVolume(float volume)
    {
        UpdateSettings(Current.WithSfxVolume(volume));
    }

    public void SetMuted(bool isMuted)
    {
        UpdateSettings(Current.WithMuted(isMuted));
    }

    public void SetGraphicsProfile(GraphicsQualityProfile profile)
    {
        UpdateSettings(Current.WithGraphicsProfile(profile));
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

        store = new PlayerPrefsPlayerSettingsStore();
        Current = store.Load();
        Apply(Current);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void UpdateSettings(ArenaPlayerSettings settings)
    {
        if (settings.Equals(Current))
        {
            return;
        }

        Current = settings;
        store.Save(Current);
        Apply(Current);
        SettingsChanged?.Invoke(Current);
    }

    private static void Apply(ArenaPlayerSettings settings)
    {
        ApplyAudio(settings);
        ApplyGraphics(settings.GraphicsProfile);
        RobotArenaAudioSource.ApplyAll(settings);
    }

    private static void ApplyAudio(ArenaPlayerSettings settings)
    {
        AudioListener.volume = settings.IsMuted ? 0f : settings.MasterVolume;
    }

    private static void ApplyGraphics(GraphicsQualityProfile profile)
    {
        string[] qualityNames = QualitySettings.names;
        if (qualityNames == null || qualityNames.Length == 0)
        {
            return;
        }

        string requestedName = GraphicsQualityProfileCatalog.GetQualityLevelName(profile);
        int qualityIndex = Array.IndexOf(qualityNames, requestedName);
        if (qualityIndex < 0)
        {
            int preferredIndex = GraphicsQualityProfileCatalog.GetFallbackQualityLevelIndex(profile);
            qualityIndex = Mathf.Clamp(preferredIndex, 0, qualityNames.Length - 1);
        }

        if (QualitySettings.GetQualityLevel() != qualityIndex)
        {
            QualitySettings.SetQualityLevel(qualityIndex, true);
        }
    }
}
