using RobotArena.Session;
using UnityEngine;
using ArenaPlayerSettings = RobotArena.Session.PlayerSettings;

public enum RobotArenaAudioChannel
{
    Music,
    Sfx
}

[RequireComponent(typeof(AudioSource))]
public sealed class RobotArenaAudioSource : MonoBehaviour
{
    [SerializeField]
    private RobotArenaAudioChannel channel = RobotArenaAudioChannel.Sfx;

    private AudioSource audioSource;
    private float baseVolume;

    public RobotArenaAudioChannel Channel
    {
        get { return channel; }
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        baseVolume = audioSource.volume;
    }

    private void Start()
    {
        if (GameSettingsRuntime.Instance != null)
        {
            Apply(GameSettingsRuntime.Instance.Current);
        }
    }

    public void Apply(ArenaPlayerSettings settings)
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            baseVolume = audioSource.volume;
        }

        float channelVolume = channel == RobotArenaAudioChannel.Music
            ? settings.MusicVolume
            : settings.SfxVolume;
        audioSource.volume = baseVolume * channelVolume;
    }

    public static void ApplyAll(ArenaPlayerSettings settings)
    {
        RobotArenaAudioSource[] sources = FindObjectsOfType<RobotArenaAudioSource>();
        foreach (RobotArenaAudioSource source in sources)
        {
            source.Apply(settings);
        }

        AudioSource[] unboundSources = FindObjectsOfType<AudioSource>();
        foreach (AudioSource source in unboundSources)
        {
            if (source.GetComponent<RobotArenaAudioSource>() != null ||
                source.GetComponentInParent<GameMusicRuntime>() != null)
            {
                continue;
            }

            RobotArenaAudioSource sfxSource = source.gameObject.AddComponent<RobotArenaAudioSource>();
            sfxSource.Apply(settings);
        }
    }
}
