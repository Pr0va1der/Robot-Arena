using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BotSpawnManager : MonoBehaviour
{
    [Header("Initial bots (already on map)")]
    public int initialBotsOnMap = 11;

    [Header("Max bots on map")]
    public int maxBotsOnMap = 5;

    [Header("Bots")]
    public GameObject botPrefab;

    [Header("Spawn Points Root")]
    public Transform spawnPointsRoot;

    [Header("Wave Settings")]
    public float waveDuration = 20f;
    public float spawnInterval = 2f;

    [Header("UI")]
    public TextMeshProUGUI botsCounterText;
    public TextMeshProUGUI waveTimerText;

    private List<Transform> spawnPoints = new List<Transform>();
    private int currentBotsOnMap;
    private bool waveActive = false;
    private bool waveCompleted = false;

    private Coroutine waveTimerCoroutine;
    private Coroutine spawnCoroutine;

    public GameObject winScreen;

    void Start()
    {
        currentBotsOnMap = initialBotsOnMap;
        Debug.Log($"🧮 Initial bots on map: {currentBotsOnMap}");

        if (waveTimerText != null)
            waveTimerText.gameObject.SetActive(false);

        CollectSpawnPoints();
    }

    void CollectSpawnPoints()
    {
        spawnPoints.Clear();

        if (spawnPointsRoot == null)
        {
            Debug.LogError("❌ SpawnPointsRoot is NOT assigned!");
            return;
        }

        foreach (Transform child in spawnPointsRoot)
        {
            spawnPoints.Add(child);
        }

        Debug.Log($"📍 Spawn points collected: {spawnPoints.Count}");
    }

    public void OnBotDied()
    {
        currentBotsOnMap = Mathf.Max(0, currentBotsOnMap - 1);
        Debug.Log($"💀 Bot died. Current bots on map: {currentBotsOnMap}");

        if (currentBotsOnMap == 0 && !waveActive && !waveCompleted)
        {
            StartWave();
        }
    }

    void StartWave()
    {
        if (waveActive)
            return;

        waveActive = true;
        Debug.Log("🌊 WAVE STARTED");

        waveTimerCoroutine = StartCoroutine(WaveTimer());
        spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    IEnumerator WaveTimer()
    {
        float timeLeft = waveDuration;

        if (waveTimerText != null)
            waveTimerText.gameObject.SetActive(true);

        while (timeLeft > 0f)
        {
            int seconds = Mathf.CeilToInt(timeLeft);

            if (waveTimerText != null)
                waveTimerText.text = $"До конца волны: {seconds}";

            Debug.Log($"⏳ Wave time left: {seconds} sec");

            yield return new WaitForSeconds(1f);
            timeLeft -= 1f;
        }

        waveActive = false;
        waveCompleted = true;

        if (waveTimerText != null)
            waveTimerText.gameObject.SetActive(false);

        Debug.Log("⏱️ WAVE ENDED — no more spawns");
        ShowVictoryScreen();
    }

    void ShowVictoryScreen()
    {
        Debug.Log("🏆 VICTORY!");

        if (winScreen != null)
            winScreen.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
    }


    IEnumerator SpawnLoop()
    {

        while (waveActive)
        {
            TrySpawnBot();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void TrySpawnBot()
    {

        if (!waveActive)
            return;

        if (waveCompleted)
            return;

        if (currentBotsOnMap >= maxBotsOnMap)
        {
            Debug.Log("🚫 Spawn skipped — max bots on map reached");
            return;
        }

        if (botPrefab == null || spawnPoints.Count == 0)
            return;

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Count)];
        GameObject bot = Instantiate(botPrefab, point.position, point.rotation);

        currentBotsOnMap++;
        Debug.Log($"🤖 Bot spawned at {point.name}. Current bots: {currentBotsOnMap}");

        Animator animator = bot.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetBool("isPlayerVisible", true);
            Debug.Log("🔫 Bot switched to gunsUp state");
        }
        else
        {
            Debug.LogWarning("⚠️ Animator not found on spawned bot");
        }
    }

    private void FixedUpdate()
    {
        UpdateBotsCounter();
    }

    void UpdateBotsCounter()
    {
        if (botsCounterText != null)
            botsCounterText.text = $"Ботов осталось: {currentBotsOnMap}";
    }
}