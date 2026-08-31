using System.Collections.Generic;
using RobotArena.Session;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class BotSpawnManager : MonoBehaviour, ISessionBotFactory, ISessionBotRegistry, IPlayerRecovery
{
    [Header("Waves")]
    public List<WaveConfiguration> waveConfigurations = WaveConfiguration.CreateDefaults();
    public float intermissionDuration = 5f;
    [Range(0f, 1f)] public float intermissionHealthRestoreFraction = 0.20f;

    [Header("Bots")]
    public GameObject botPrefab;
    public Transform spawnPointsRoot;

    [Header("UI")]
    public TextMeshProUGUI botsCounterText;
    [FormerlySerializedAs("waveTimerText")]
    public TextMeshProUGUI spawnTimerText;
    public GameObject winScreen;

    private readonly List<Transform> spawnPoints = new List<Transform>();
    private SessionOrchestrator session;
    private PlayerHP playerHealth;
    private PauseMenu pauseMenu;

    public SessionState State => session?.State ?? SessionState.NotStarted;
    public SessionResult? Result => session?.Result;
    public float? BestTime => session?.BestTime;
    public int LiveBotCount => session?.LiveBotCount ?? 0;
    public int CurrentWaveNumber => session?.CurrentWaveNumber ?? 0;
    public int TotalWaves => session?.TotalWaves ?? waveConfigurations?.Count ?? 0;
    public float SpawnTimeRemaining => session?.SpawnTimeRemaining ?? 0f;
    public float IntermissionTimeRemaining => session?.IntermissionTimeRemaining ?? 0f;

    public event System.Action<SessionState> SessionStateChanged;

    private void Start()
    {
        CollectSpawnPoints();
        playerHealth = FindObjectOfType<PlayerHP>();
        pauseMenu = FindObjectOfType<PauseMenu>();
        session = new SessionOrchestrator(
            CreateSessionPlan(),
            this,
            this,
            new PlayerPrefsSessionBestTimeStore(),
            pauseMenu?.PauseCoordinator);
        session.StateChanged += OnSessionStateChanged;

        SessionBotRegistration[] placedBots = FindObjectsOfType<SessionBotRegistration>();
        var initialBots = new List<BotId>(placedBots.Length);
        foreach (SessionBotRegistration bot in placedBots)
        {
            bot.Connect(this);
            initialBots.Add(bot.Id);
        }

        session.StartSession(initialBots);
        if (playerHealth != null)
        {
            playerHealth.Died += OnPlayerDied;
        }

        if (spawnTimerText != null)
        {
            spawnTimerText.gameObject.SetActive(true);
        }
    }

    private void Update()
    {
        session?.Advance(Time.deltaTime);

        if (botsCounterText != null)
        {
            botsCounterText.text = $"Волна {CurrentWaveNumber}/{TotalWaves} · Ботов осталось: {LiveBotCount}";
        }

        if (spawnTimerText != null)
        {
            UpdateStateText();
        }
    }

    private void OnDestroy()
    {
        if (session != null)
        {
            session.StateChanged -= OnSessionStateChanged;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerDied;
        }
    }

    public bool RegisterBot(SessionBotRegistration bot)
    {
        return bot != null && session != null && session.RegisterBot(bot.Id);
    }

    public bool UnregisterBot(SessionBotRegistration bot)
    {
        return bot != null && session != null && session.RemoveBot(bot.Id);
    }

    public bool TryCreateBot(WaveSchedule wave, out BotId botId)
    {
        botId = default;
        if (botPrefab == null || spawnPoints.Count == 0)
        {
            return false;
        }

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Count)];
        GameObject bot = Instantiate(botPrefab, point.position, point.rotation);
        SessionBotRegistration registration = bot.GetComponentInChildren<SessionBotRegistration>();
        if (registration == null)
        {
            registration = bot.AddComponent<SessionBotRegistration>();
        }

        registration.Connect(this);
        botId = registration.Id;

        Animator animator = bot.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            ApplyShootingDifficulty(animator, wave);
            animator.SetBool("isPlayerVisible", true);
        }

        BotCombatReporter.Ensure(bot).EnterCombat();

        return true;
    }

    public void RestoreHealthFraction(float fraction)
    {
        if (playerHealth != null)
        {
            playerHealth.Heal(playerHealth.maxHealth * fraction);
        }
    }

    public void OnPlayerDied()
    {
        session?.DefeatPlayer();
    }

    private void CollectSpawnPoints()
    {
        spawnPoints.Clear();
        if (spawnPointsRoot == null)
        {
            Debug.LogError("SpawnPointsRoot is not assigned.");
            return;
        }

        foreach (Transform child in spawnPointsRoot)
        {
            spawnPoints.Add(child);
        }
    }

    private SessionPlan CreateSessionPlan()
    {
        if (waveConfigurations == null ||
            waveConfigurations.Count != 4 ||
            waveConfigurations.Exists(configuration => configuration == null))
        {
            Debug.LogWarning("A session requires exactly four wave configurations. Defaults were restored.");
            waveConfigurations = WaveConfiguration.CreateDefaults();
        }

        var schedules = new List<WaveSchedule>(waveConfigurations.Count);
        foreach (WaveConfiguration configuration in waveConfigurations)
        {
            schedules.Add(configuration.ToSchedule());
        }

        return new SessionPlan(
            schedules,
            intermissionDuration,
            intermissionHealthRestoreFraction);
    }

    private static void ApplyShootingDifficulty(Animator animator, WaveSchedule wave)
    {
        gunsUpBehaviour[] shootingBehaviours = animator.GetBehaviours<gunsUpBehaviour>();
        foreach (gunsUpBehaviour behaviour in shootingBehaviours)
        {
            behaviour.fireRate *= wave.FireIntervalMultiplier;
            behaviour.aimConeAngle *= wave.AimConeMultiplier;
        }
    }

    private void UpdateStateText()
    {
        if (State == SessionState.Spawning)
        {
            spawnTimerText.text = $"Появление: {Mathf.CeilToInt(session.SpawnTimeRemaining)}";
        }
        else if (State == SessionState.Clearing)
        {
            spawnTimerText.text = "Зачистите оставшихся ботов";
        }
        else if (State == SessionState.Intermission)
        {
            spawnTimerText.text = $"Следующая волна через: {Mathf.CeilToInt(session.IntermissionTimeRemaining)}";
        }
    }

    private void OnSessionStateChanged(SessionState state)
    {
        SessionStateChanged?.Invoke(state);

        if ((state == SessionState.Won || state == SessionState.Lost) && spawnTimerText != null)
        {
            spawnTimerText.gameObject.SetActive(false);
        }

        if (DesktopArenaUi.Instance != null)
        {
            return;
        }

        if (state == SessionState.Won)
        {
            ShowVictoryScreen();
        }
        else if (state == SessionState.Lost && playerHealth != null && playerHealth.deathScreen != null)
        {
            if (Result.HasValue)
            {
                playerHealth.deathScreen.SetResult(Result.Value, BestTime);
            }

            playerHealth.deathScreen.ShowDeathScreen();
        }
    }

    private void ShowVictoryScreen()
    {
        if (winScreen != null)
        {
            WinScreen resultScreen = winScreen.GetComponentInParent<WinScreen>(true);
            if (resultScreen != null && Result.HasValue)
            {
                resultScreen.SetResult(Result.Value, BestTime);
            }

            winScreen.SetActive(true);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
    }
}
