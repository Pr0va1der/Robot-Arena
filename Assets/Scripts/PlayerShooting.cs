using System.Collections;
using RobotArena.PlayerWeapon;
using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(200)]
public class PlayerShooting : MonoBehaviour
{
    private const string ShootStateName = "Shoot";
    private const string UltimateStateName = "Ulta";
    private const string IdleStateName = "Idle";
    private const float UltimateAnimationWaitTimeout = 5f;

    [Header("Shooting Settings")]
    public GameObject bulletPrefab;
    public Transform firePointLeft;
    public Transform firePointRight;
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
    private bool volleyConfigurationErrorLogged;

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

    private void LateUpdate()
    {
        // CinemachineBrain updates the final camera in LateUpdate. Running this
        // after GunRotation (100) and LaserPointer (110) keeps a shot on the
        // current frame's central camera ray instead of the previous turret pose.
        if (PauseMenu.GameIsPaused || PauseMenu.PointerLockGestureConsumed)
        {
            return;
        }

        if (weaponController == null)
        {
            return;
        }

        IGameplayInputActions inputActions = GameplayInputActions.Current;
        bool fireVolleyAvailable = true;
        if (inputActions.FireHeld)
        {
            fireVolleyAvailable = TryGetVolleyDirection(out _);
            if (!fireVolleyAvailable)
            {
                LogVolleyConfigurationErrorOnce();
            }
        }

        PlayerWeaponCommand command = weaponController.Tick(
            Time.time,
            inputActions.FireHeld,
            inputActions.UltimatePressed,
            IsUltimateReady,
            fireVolleyAvailable);

        switch (command)
        {
            case PlayerWeaponCommand.FireVolley:
                TryFireVolley();
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

    public bool TryGetVolleyDirection(out Vector3 direction)
    {
        direction = default;
        if (bulletPrefab == null || firePointLeft == null || firePointRight == null)
        {
            return false;
        }

        if (laserPointer == null || !laserPointer.TryGetAimDirection(out direction))
        {
            direction = default;
            return false;
        }

        return direction.sqrMagnitude > 0.0001f;
    }

    public bool TryFireVolley()
    {
        if (!TryGetVolleyDirection(out Vector3 direction))
        {
            LogVolleyConfigurationErrorOnce();
            return false;
        }

        PlayShootAnimation();
        FireBullets(direction);
        return true;
    }

    private void FireBullets(Vector3 direction)
    {
        FireBullet(firePointLeft, direction);
        FireBullet(firePointRight, direction);
    }

    private void FireBullet(Transform barrel, Vector3 direction)
    {
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

    private void LogVolleyConfigurationErrorOnce()
    {
        if (volleyConfigurationErrorLogged)
        {
            return;
        }

        volleyConfigurationErrorLogged = true;
        Debug.LogError("PlayerShooting requires a bullet, left and right barrels, and a valid laser direction for a normal volley.");
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
