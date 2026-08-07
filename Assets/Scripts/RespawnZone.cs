


using UnityEngine;

public class RespawnZone : MonoBehaviour
{
    // Точка, куда респавнить игрока
    public Transform respawnPoint;

    // Сколько здоровья отнять при столкновении
    public float damageOnRespawn = 50f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // проверяем, что столкнулся именно игрок
        {
            // Перемещаем игрока в точку респавна
            other.transform.position = respawnPoint.position;

            // Сбрасываем скорость, если есть Rigidbody
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // 🔥 Отнимаем здоровье
            PlayerHP playerHP = other.GetComponent<PlayerHP>();
            if (playerHP != null)
            {
                playerHP.TakeDamage(damageOnRespawn);
            }

            Debug.Log("Игрок респавнен и получил урон!");
        }
    }
}
