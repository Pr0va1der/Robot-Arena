using System.Collections.Generic;
using RobotArena.Session;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class BotSpawnManager : MonoBehaviour, ISessionBotFactory, ISessionBotRegistry
{
    [Header("Spawn Settings")]
    [FormerlySerializedAs("waveDuration")]
    public float spawnDuration = 20f;
    public float spawnInterval = 2f;
    public int maxBotsOnMap = 5;

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

    public SessionState State => session?.State ?? SessionState.NotStarted;
    public int LiveBotCount => session?.LiveBotCount ?? 0;

    private void Start()
    {
        CollectSpawnPoints();
        session = new SessionOrchestrator(
            new WaveSchedule(spawnDuration, spawnInterval, maxBotsOnMap),
            this);
        session.StateChanged += OnSessionStateChanged;

        SessionBotRegistration[] placedBots = FindObjectsOfType<SessionBotRegistration>();
        var initialBots = new List<BotId>(placedBots.Length);
        foreach (SessionBotRegistration bot in placedBots)
        {
            bot.Connect(this);
            initialBots.Add(bot.Id);
        }

        session.StartSession(initialBots);
        playerHealth = FindObjectOfType<PlayerHP>();
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
            botsCounterText.text = $"Ботов осталось: {LiveBotCount}";
        }

        if (spawnTimerText != null && State == SessionState.Spawning)
        {
            spawnTimerText.text = $"До конца появления: {Mathf.CeilToInt(session.SpawnTimeRemaining)}";
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

    public bool TryCreateBot(out BotId botId)
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
            animator.SetBool("isPlayerVisible", true);
        }

        return true;
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

    private void OnSessionStateChanged(SessionState state)
    {
        if (state != SessionState.Spawning && spawnTimerText != null)
        {
            spawnTimerText.gameObject.SetActive(false);
        }

        if (state == SessionState.Won)
        {
            ShowVictoryScreen();
        }
        else if (state == SessionState.Lost && playerHealth != null && playerHealth.deathScreen != null)
        {
            playerHealth.deathScreen.ShowDeathScreen();
        }
    }

    private void ShowVictoryScreen()
    {
        if (winScreen != null)
        {
            winScreen.SetActive(true);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
    }
}
