using UnityEngine;

public class FireballDamage : MonoBehaviour
{
    public float damage = 150f;

    void OnTriggerEnter(Collider other)
    {
        // Проверяем, есть ли у объекта компонент, который может получать урон
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }
    }
}
