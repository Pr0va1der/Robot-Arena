


using UnityEngine;

public class RespawnZone : MonoBehaviour
{
    // Точка, куда респавнить игрока
    public Transform respawnPoint;

    // Опорная точка камеры игрока. Синхронизируется сразу после респавна,
    // чтобы камера и турель не догоняли новый трансформ в следующем кадре.
    public StableCameraTarget cameraSupportPoint;

    // Сколько здоровья отнять при столкновении
    public float damageOnRespawn = 50f;

    private void OnTriggerEnter(Collider other)
    {
        TryRespawn(other);
    }

    /// <summary>
    /// Respawns a player collider and synchronizes all state that must be
    /// visible on the same rendered frame.
    /// </summary>
    public bool TryRespawn(Collider other)
    {
        if (other == null || !other.CompareTag("Player"))
        {
            return false;
        }

        if (respawnPoint == null)
        {
            return false;
        }

        Rigidbody rb = other.attachedRigidbody ?? other.GetComponent<Rigidbody>();
        Transform playerTransform = rb != null ? rb.transform : other.transform;

        // Перемещаем игрока в точку респавна
        if (rb != null)
        {
            rb.position = respawnPoint.position;
        }
        else
        {
            other.transform.position = respawnPoint.position;
        }

        // Сбрасываем скорость, если есть Rigidbody
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cameraSupportPoint != null && cameraSupportPoint.followTarget == playerTransform)
        {
            cameraSupportPoint.SnapToFollowTarget();
        }

        // 🔥 Отнимаем здоровье
        PlayerHP playerHP = other.GetComponent<PlayerHP>();
        if (playerHP != null)
        {
            playerHP.TakeDamage(damageOnRespawn);
        }

        Debug.Log("Игрок респавнен и получил урон!");
        return true;
    }
}
