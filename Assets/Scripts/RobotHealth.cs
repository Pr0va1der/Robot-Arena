using UnityEngine;

public class RobotHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Destroy Target")]
    [Tooltip("Объект, который будет уничтожен после смерти. Если не указать — удалится этот объект.")]
    public GameObject objectToDestroy;

    [Header("Ulti Effect")]
    public GameObject ultimateEffectPrefab;  
    public Transform ultiSpawnPoint;          
    public float ultiEffectLifetime = 2f;

    private Animator animator;



    void Start()
    {
        currentHealth = maxHealth;
        
        animator = GetComponentInParent<Animator>();
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} получил урон: {damage}. Осталось здоровья: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }

        if (animator != null && currentHealth > 0)
        {
            animator.SetBool("isPlayerVisible", true);
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} уничтожен!");

        BotSpawnManager manager = FindObjectOfType<BotSpawnManager>();
        if (manager != null)
            manager.OnBotDied();

        Destroy(objectToDestroy);
        SpawnUltimateEffect();
    }

    void SpawnUltimateEffect()
    {
        if (ultimateEffectPrefab == null)
        {
            Debug.LogWarning("❗ Не назначен префаб ульты!");
            return;
        }

        Vector3 spawnPos = ultiSpawnPoint != null ? ultiSpawnPoint.position : transform.position;
        Quaternion spawnRot = ultiSpawnPoint != null ? ultiSpawnPoint.rotation : Quaternion.identity;

        GameObject effect = Instantiate(ultimateEffectPrefab, spawnPos, spawnRot);
        Destroy(effect, ultiEffectLifetime);
    }
}
