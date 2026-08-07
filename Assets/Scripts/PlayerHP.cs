

using UnityEngine;
using System;

public class PlayerHP : MonoBehaviour, IDamageable
{
    public float maxHealth = 250f;
    private float currentHealth;

    public event Action<float, float> OnHealthChanged;

    public DeathScreen deathScreen;

    void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"Игрок получил урон: {damage}. Осталось здоровья: {currentHealth}");

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void Die()
    {
        Debug.Log("💀 Игрок уничтожен!");
        deathScreen.ShowDeathScreen();
    }
}
