using RobotArena.PlayerWeapon;
using UnityEngine;

public sealed class PlayerWeaponAudioPlayback : IPlayerWeaponAudio
{
    private const int SampleRate = 44100;
    private const string AudioObjectName = "PlayerWeaponAudio";

    private static AudioClip defaultVolleyClip;
    private static AudioClip defaultUltimateClip;

    private readonly AudioSource audioSource;
    private readonly AudioClip volleyClip;
    private readonly AudioClip ultimateClip;

    public PlayerWeaponAudioPlayback(
        Transform owner,
        AudioClip configuredVolleyClip,
        AudioClip configuredUltimateClip)
    {
        audioSource = CreateAudioSource(owner);
        volleyClip = configuredVolleyClip != null
            ? configuredVolleyClip
            : GetOrCreateDefaultVolleyClip();
        ultimateClip = configuredUltimateClip != null
            ? configuredUltimateClip
            : GetOrCreateDefaultUltimateClip();
    }

    public void Play(PlayerWeaponAudioCue cue)
    {
        AudioClip clip = cue == PlayerWeaponAudioCue.Volley
            ? volleyClip
            : ultimateClip;
        PlayClip(clip);
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource == null || clip == null || !HasAudioPermission())
        {
            return;
        }

        audioSource.PlayOneShot(clip);
    }

    private static bool HasAudioPermission()
    {
        return GameMusicRuntime.Instance == null ||
               GameMusicRuntime.Instance.AudioPermissionGranted;
    }

    private static AudioSource CreateAudioSource(Transform owner)
    {
        Transform audioTransform = owner.Find(AudioObjectName);
        GameObject audioObject = audioTransform != null
            ? audioTransform.gameObject
            : new GameObject(AudioObjectName);

        if (audioTransform == null)
        {
            audioObject.transform.SetParent(owner, false);
        }

        AudioSource source = audioObject.GetComponent<AudioSource>();
        if (source == null)
        {
            source = audioObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.ignoreListenerPause = false;

        RobotArenaAudioSource sfxChannel = audioObject.GetComponent<RobotArenaAudioSource>();
        if (sfxChannel == null)
        {
            sfxChannel = audioObject.AddComponent<RobotArenaAudioSource>();
        }

        if (GameSettingsRuntime.Instance != null)
        {
            sfxChannel.Apply(GameSettingsRuntime.Instance.Current);
        }

        return source;
    }

    private static AudioClip GetOrCreateDefaultVolleyClip()
    {
        if (defaultVolleyClip == null)
        {
            defaultVolleyClip = CreateSweepClip(
                "RobotArena_DefaultVolleySfx",
                0.12f,
                980f,
                220f,
                1.5f,
                0.32f);
        }

        return defaultVolleyClip;
    }

    private static AudioClip GetOrCreateDefaultUltimateClip()
    {
        if (defaultUltimateClip == null)
        {
            defaultUltimateClip = CreateSweepClip(
                "RobotArena_DefaultUltimateSfx",
                0.42f,
                150f,
                620f,
                2f,
                0.38f);
        }

        return defaultUltimateClip;
    }

    private static AudioClip CreateSweepClip(
        string name,
        float duration,
        float startFrequency,
        float endFrequency,
        float harmonicMultiplier,
        float amplitude)
    {
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
        float[] samples = new float[sampleCount];
        double phase = 0d;
        double harmonicPhase = 0d;

        for (int i = 0; i < sampleCount; i++)
        {
            float normalized = sampleCount == 1
                ? 1f
                : (float)i / (sampleCount - 1);
            float frequency = Mathf.Lerp(startFrequency, endFrequency, normalized);
            phase += 2d * Mathf.PI * frequency / SampleRate;
            harmonicPhase += 2d * Mathf.PI * frequency * harmonicMultiplier / SampleRate;

            float attack = Mathf.Clamp01(normalized / 0.02f);
            float release = Mathf.Pow(1f - normalized, 1.6f);
            float envelope = attack * release;
            float tone = Mathf.Sin((float)phase) + 0.24f * Mathf.Sin((float)harmonicPhase);
            samples[i] = tone * envelope * amplitude;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
