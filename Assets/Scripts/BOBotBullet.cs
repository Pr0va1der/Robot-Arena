using UnityEngine;

public class BOBotBullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float speed = 200f;
    public float lifetime = 5f;
    public float damage = 25f;
    public GameObject owner;

    private Rigidbody rb;
    private bool hasHit = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.isKinematic = false;

        // 🚀 Стартовый импульс
        rb.AddForce(transform.forward * speed, ForceMode.Impulse);

        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        hasHit = true;

        // 🚫 Не наносим урон себе
        if (collision.gameObject == owner)
            return;

        // 🎯 Попадание в объект, который может получать урон
        IDamageable damageable = collision.gameObject.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }

        // ❌ Убираем передачу импульса, но не ломаем физику игрока
        Rigidbody hitRb = collision.rigidbody;
        if (hitRb != null && hitRb != rb)
        {
            // Игнорируем добавление сил от физики коллизии
            Physics.IgnoreCollision(collision.collider, GetComponent<Collider>());
        }

        // 🧍‍♂️ Пуля теряет скорость и падает
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = true;

        // 💥 Можно добавить эффект попадания
        // Instantiate(hitEffectPrefab, collision.contacts[0].point, Quaternion.identity);

        Destroy(gameObject, 0.1f);
    }
}
