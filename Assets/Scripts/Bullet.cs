using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 200f;
    public float lifetime = 5f;
    public float damage = 25f;

    [HideInInspector] public GameObject owner; // Кто выстрелил — не получит урон от своей пули

    private Rigidbody rb;
    private bool hasHit = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.isKinematic = false;

        // Добавляем импульс сразу при выстреле
        rb.AddForce(transform.forward * speed, ForceMode.Impulse);

        // Уничтожаем через время (если не столкнулась)
        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        hasHit = true;

        // 🧠 Игнорируем столкновение с владельцем (или его дочерними объектами)
        if (owner != null && (collision.gameObject == owner || collision.transform.IsChildOf(owner.transform)))
        {
            Physics.IgnoreCollision(collision.collider, GetComponent<Collider>());
            hasHit = false;
            return;
        }

        // Проверяем, может ли объект получать урон
        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }

        // Останавливаем пулю
        rb.velocity = Vector3.zero;
        rb.useGravity = true;
        rb.isKinematic = true;

        // Уничтожаем пулю чуть позже (чтобы не исчезала мгновенно)
        Destroy(gameObject, 0.2f);
    }
}
