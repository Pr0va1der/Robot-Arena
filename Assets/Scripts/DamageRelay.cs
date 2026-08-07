using UnityEngine;

public class DamageRelay : MonoBehaviour, IDamageable
{
    [Tooltip("Основной компонент, который действительно принимает урон")]
    public MonoBehaviour damageReceiver; // скрипт, реализующий IDamageable

    private IDamageable target;

    void Awake()
    {
        if (damageReceiver != null)
            target = damageReceiver as IDamageable;
        else
            target = GetComponentInParent<IDamageable>(); // ищем наверх по иерархии
    }

    public void TakeDamage(float damage)
    {
        if (target != null && target != this)
            target.TakeDamage(damage); // ✅ передаём урон другому объекту
        else
            Debug.LogWarning($"{name}: нет получателя урона!");
    }
}
