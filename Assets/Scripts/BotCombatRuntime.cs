using RobotArena.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-200)]
public sealed class BotCombatRuntime : MonoBehaviour
{
    public static BotCombatRuntime Instance { get; private set; }

    public BotCombatTracker Tracker { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallAtStartup()
    {
        GetOrCreate();
    }

    public static BotCombatRuntime GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject runtimeObject = new GameObject("BotCombatRuntime");
        return runtimeObject.AddComponent<BotCombatRuntime>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Tracker = new BotCombatTracker();
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Tracker.Clear();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
