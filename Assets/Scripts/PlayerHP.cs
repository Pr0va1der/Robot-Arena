

using UnityEngine;
using System;

public class PlayerHP : MonoBehaviour, IDamageable
{
    public float maxHealth = 250f;
    private float currentHealth;

    public event Action<float, float> OnHealthChanged;
    public event Action Died;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private bool isDead;

    void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
        {
            return;
        }

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
        isDead = true;
        Debug.Log("💀 Игрок уничтожен!");
        Died?.Invoke();
    }
}
