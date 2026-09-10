using System;
using RobotArena.Platform;
using UnityEngine;

[DefaultExecutionOrder(-110)]
public sealed class RobotArenaPlatformServices : MonoBehaviour
{
    private static RobotArenaPlatformServices instance;
    private PlatformServicesAdapter adapter;
    private IPlatformServicesBackend backend;

    public static RobotArenaPlatformServices Instance => instance;

    public PlatformServicesSnapshot Current => adapter == null ? null : adapter.Current;

    public event Action<PlatformServicesSnapshot> SnapshotChanged;

    public event Action<bool> PlatformPauseChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallAtStartup()
    {
        EnsureInstalled();
    }

    public static RobotArenaPlatformServices EnsureInstalled()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject host = new GameObject(nameof(RobotArenaPlatformServices));
        DontDestroyOnLoad(host);
        return host.AddComponent<RobotArenaPlatformServices>();
    }

    public static void MarkInteractiveReady()
    {
        if (instance != null && instance.adapter != null)
        {
            instance.adapter.MarkInteractiveReady();
        }
    }

    private void Update()
    {
        if (backend != null)
        {
            backend.Tick(Time.unscaledTime);
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

#if ROBOTARENA_PLUGINYG2
        backend = new RobotArenaPluginYG2Backend();
#else
        RobotArenaPlatformProbe probe = RobotArenaPlatformProbe.EnsureInstalled();
        backend = new RobotArenaPlatformProbeBackend(probe);
#endif
        adapter = new PlatformServicesAdapter(backend);
        adapter.SnapshotChanged += OnSnapshotChanged;
        adapter.PlatformPauseChanged += OnPlatformPauseChanged;
    }

    private void OnDestroy()
    {
        if (adapter != null)
        {
            adapter.SnapshotChanged -= OnSnapshotChanged;
            adapter.PlatformPauseChanged -= OnPlatformPauseChanged;
            adapter.Dispose();
            adapter = null;
        }

        if (backend != null)
        {
            backend.Dispose();
            backend = null;
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnSnapshotChanged(PlatformServicesSnapshot snapshot)
    {
        SnapshotChanged?.Invoke(snapshot);
    }

    private void OnPlatformPauseChanged(bool isPaused)
    {
        PlatformPauseChanged?.Invoke(isPaused);
    }
}
