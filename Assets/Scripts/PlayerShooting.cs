using System.Collections;
using RobotArena.PlayerWeapon;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerShooting : MonoBehaviour
{
    private const string ShootStateName = "Shoot";
    private const string UltimateStateName = "Ulta";
    private const string IdleStateName = "Idle";
    private const float UltimateAnimationWaitTimeout = 5f;

    [Header("Shooting Settings")]
    public GameObject bulletPrefab;
    public Transform[] barrels;
    public float shootForce = 50f;
    [FormerlySerializedAs("fireRate")]
    [Min(0.01f)] public float recoilCycleDuration = 0.25f;
    [Min(0.01f)] public float shootAnimationDurationAtDefaultSpeed = 0.8125f;
    [Min(0f)] public float shootAnimationTransitionDuration = 0.03f;

    [Header("Aim")]
    public LaserPointer laserPointer;

    [Header("Ulti Settings")]
    public float ultimateForce = 15f;
    [Min(0f)] public float ultimateCooldown = 8f;
    public Transform cameraTransform;
    public GameObject physicsBody;

    [Header("Ulti Effect")]
    public GameObject ultimateEffectPrefab;
    public Transform ultiSpawnPoint;
    public float ultiEffectLifetime = 2f;

    private Animator animator;
    private Rigidbody rb;
    private PlayerWeaponController weaponController;
    private Coroutine ultimateAnimationRoutine;
    private float nextUltimateTime;
    private float defaultAnimatorSpeed = 1f;
    private bool shootAnimationActive;

    public float UltimateCooldownRemaining => Mathf.Max(0f, nextUltimateTime - Time.time);
    public bool IsUltimateReady => UltimateCooldownRemaining <= 0f;

    private void Start()
    {
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            defaultAnimatorSpeed = animator.speed;
        }

        weaponController = new PlayerWeaponController(Mathf.Max(0.01f, recoilCycleDuration));

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (laserPointer == null)
        {
            laserPointer = GetComponentInChildren<LaserPointer>(true);
        }

        if (physicsBody != null)
        {
            rb = physicsBody.GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError($"{physicsBody.name} не содержит Rigidbody!");
            }
        }
        else
        {
            Debug.LogError("PhysicsBody не назначен в инспекторе!");
        }
    }

    private void Update()
    {
        if (PauseMenu.GameIsPaused || PauseMenu.PointerLockGestureConsumed)
        {
            return;
        }

        if (weaponController == null)
        {
            return;
        }

        IGameplayInputActions inputActions = GameplayInputActions.Current;
        PlayerWeaponCommand command = weaponController.Tick(
            Time.time,
            inputActions.FireHeld,
            inputActions.UltimatePressed,
            IsUltimateReady);

        switch (command)
        {
            case PlayerWeaponCommand.FireVolley:
                PlayShootAnimation();
                FireBullets();
                break;
            case PlayerWeaponCommand.StartUltimate:
                StartUltimate();
                break;
        }

        SyncAnimationState();
    }

    private void PlayShootAnimation()
    {
        if (animator == null)
        {
            return;
        }

        animator.speed = GetShootAnimationSpeed();
        animator.CrossFadeInFixedTime(
            ShootStateName,
            shootAnimationTransitionDuration,
            0,
            0f);
        shootAnimationActive = true;
    }

    private void SyncAnimationState()
    {
        if (animator == null || weaponController.IsUltimateActive || weaponController.IsRecoilActive)
        {
            return;
        }

        if (!shootAnimationActive)
        {
            return;
        }

        animator.speed = defaultAnimatorSpeed;
        animator.CrossFadeInFixedTime(
            IdleStateName,
            shootAnimationTransitionDuration,
            0,
            0f);
        shootAnimationActive = false;
    }

    private float GetShootAnimationSpeed()
    {
        float duration = Mathf.Max(0.01f, recoilCycleDuration);
        return defaultAnimatorSpeed * shootAnimationDurationAtDefaultSpeed / duration;
    }

    private void StartUltimate()
    {
        FinishShootAnimation();
        ApplyUltimateForce();
        SpawnUltimateEffect();
        nextUltimateTime = Time.time + ultimateCooldown;

        if (animator == null)
        {
            weaponController.CompleteUltimate();
            return;
        }

        animator.speed = defaultAnimatorSpeed;
        animator.CrossFadeInFixedTime(
            UltimateStateName,
            shootAnimationTransitionDuration,
            0,
            0f);

        if (ultimateAnimationRoutine != null)
        {
            StopCoroutine(ultimateAnimationRoutine);
        }

        ultimateAnimationRoutine = StartCoroutine(CompleteUltimateAfterAnimation());
    }

    private IEnumerator CompleteUltimateAfterAnimation()
    {
        yield return null;
        float timeoutAt = Time.time + UltimateAnimationWaitTimeout;

        while (animator != null &&
               Time.time < timeoutAt &&
               !animator.GetCurrentAnimatorStateInfo(0).IsName(UltimateStateName))
        {
            yield return null;
        }

        while (animator != null && Time.time < timeoutAt)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(UltimateStateName) && stateInfo.normalizedTime >= 1f)
            {
                break;
            }

            yield return null;
        }

        weaponController.CompleteUltimate();
        ultimateAnimationRoutine = null;
    }

    private void FinishShootAnimation()
    {
        if (animator == null || !shootAnimationActive)
        {
            return;
        }

        animator.speed = defaultAnimatorSpeed;
        animator.CrossFadeInFixedTime(
            IdleStateName,
            shootAnimationTransitionDuration,
            0,
            0f);
        shootAnimationActive = false;
    }

    private void FireBullets()
    {
        if (bulletPrefab == null || barrels == null || barrels.Length == 0)
        {
            return;
        }

        Vector3 aimTarget = GetAimTarget();
        foreach (Transform barrel in barrels)
        {
            if (barrel == null)
            {
                continue;
            }

            Vector3 direction = PlayerWeaponAim.DirectionToTarget(
                barrel.position,
                aimTarget,
                barrel.forward);
            GameObject bullet = Instantiate(
                bulletPrefab,
                barrel.position,
                Quaternion.LookRotation(direction));
            Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();

            if (bulletRb != null)
            {
                bulletRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                bulletRb.useGravity = false;
                bulletRb.AddForce(direction * shootForce, ForceMode.Impulse);
            }

            Bullet bulletComponent = bullet.GetComponent<Bullet>();
            if (bulletComponent != null)
            {
                bulletComponent.owner = gameObject;
                bulletComponent.useGravity = false;
            }

            Destroy(bullet, 5f);
        }
    }

    private Vector3 GetAimTarget()
    {
        if (laserPointer != null && laserPointer.TryGetAimTarget(out Vector3 target))
        {
            return target;
        }

        foreach (Transform barrel in barrels)
        {
            if (barrel != null)
            {
                return barrel.position + barrel.forward * 100f;
            }
        }

        return transform.position + transform.forward * 100f;
    }

    private void ApplyUltimateForce()
    {
        if (rb == null || cameraTransform == null)
        {
            return;
        }

        Vector3 direction = cameraTransform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        rb.AddForce(-direction.normalized * ultimateForce, ForceMode.Impulse);
    }

    private void SpawnUltimateEffect()
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
