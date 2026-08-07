using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting Settings")]
    public GameObject bulletPrefab;       // Префаб пули
    public Transform[] barrels;           // Список дул (может быть одно или два)
    public float shootForce = 50f;        // Сила выстрела
    public float fireRate = 0.2f;         // Задержка между выстрелами

    [Header("Ulti Settings")]
    public float ultimateForce = 15f;
    public Transform cameraTransform;
    public GameObject physicsBody;

    [Header("Ulti Effect")]
    public GameObject ultimateEffectPrefab;   
    public Transform ultiSpawnPoint;         
    public float ultiEffectLifetime = 2f;   

    private Animator animator;
    private Rigidbody rb;
    private bool isPlayingAnimation = false;
    private float nextFireTime = 0f;

    void Start()
    {
        animator = GetComponent<Animator>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (physicsBody != null)
        {
            rb = physicsBody.GetComponent<Rigidbody>();
            if (rb == null)
                Debug.LogError($"{physicsBody.name} не содержит Rigidbody!");
        }
        else
        {
            Debug.LogError("PhysicsBody не назначен в инспекторе!");
        }
    }

    void Update()
    {
        if (isPlayingAnimation) return;

        // --- Обычный выстрел ---
        if (Input.GetMouseButtonDown(0) && Time.time >= nextFireTime)
        {
            PlayAnimation("Shoot");
            FireBullets();
            nextFireTime = Time.time + fireRate;
        }

        // --- Ульта ---
        if (Input.GetKeyDown(KeyCode.Q))
        {
            PlayAnimation("Ultimate");
            ApplyUltimateForce();
            SpawnUltimateEffect(); 
        }
    }

    void PlayAnimation(string triggerName)
    {
        animator.SetTrigger(triggerName);
        isPlayingAnimation = true;
        StartCoroutine(ResetAfterAnimation());
    }

    IEnumerator ResetAfterAnimation()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        yield return new WaitForSeconds(stateInfo.length);
        isPlayingAnimation = false;
    }

    void FireBullets()
    {
        if (bulletPrefab == null || barrels.Length == 0) return;

        foreach (Transform barrel in barrels)
        {
            GameObject bullet = Instantiate(bulletPrefab, barrel.position, barrel.rotation);
            Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();

            if (bulletRb != null)
            {
                bulletRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                bulletRb.useGravity = true;
                bulletRb.AddForce(barrel.forward * shootForce, ForceMode.Impulse);
            }

            Destroy(bullet, 5f);
        }
    }

    void ApplyUltimateForce()
    {
        if (rb == null || cameraTransform == null) return;

        Vector3 direction = cameraTransform.forward;
        direction.y = 0;
        direction.Normalize();

        rb.AddForce(-direction * ultimateForce, ForceMode.Impulse);
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
