using RobotArena.Session;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement Settings")]
    public float Speed = 10f;
    public float BrakeForce = 8f;
    [FormerlySerializedAs("JumpForce")]
    [Min(0f)]
    public float JumpSpeed = 8f;

    [Header("Camera")]
    public Transform cameraTransform;

    private bool _isGrounded;
    private float groundedTimer = 0f;
    private Rigidbody _rb;
    private readonly JumpInputGate jumpInputGate = new JumpInputGate();

    [Range(0f, 1f)]
    public float GroundedNormalThreshold = 0.5f;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void FixedUpdate()
    {
        bool isGameplayPaused = PauseMenu.GameIsPaused || PauseMenu.PointerLockGestureConsumed;
        bool jumpPressed = jumpInputGate.Consume(
            GameplayInputActions.Current.JumpHeld,
            isGameplayPaused);

        if (isGameplayPaused)
        {
            return;
        }

        groundedTimer -= Time.fixedDeltaTime;
        _isGrounded = groundedTimer > 0f;

        MovementLogic();
        if (jumpPressed)
        {
            TryJump();
        }
    }

    private void MovementLogic()
    {
        IGameplayInputActions inputActions = GameplayInputActions.Current;
        Vector2 movementInput = inputActions.Movement;
        float moveHorizontal = movementInput.x;
        float moveVertical = movementInput.y;

        if (cameraTransform == null) return;

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 movement = (cameraForward * moveVertical + cameraRight * moveHorizontal).normalized;

        if (inputActions.BrakeHeld)
        {
            _rb.velocity = Vector3.Lerp(_rb.velocity, Vector3.zero, Time.fixedDeltaTime * BrakeForce);
            _rb.angularVelocity = Vector3.zero;
        }
        else if (movement.sqrMagnitude > 0.001f)
        {
            if (_rb.velocity.magnitude < Speed)
                _rb.AddForce(movement * Speed, ForceMode.Acceleration);

            Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
            _rb.rotation = Quaternion.Slerp(_rb.rotation, targetRotation, 10f * Time.fixedDeltaTime);
        }
    }

    public bool TryJump()
    {
        if (!_isGrounded || JumpSpeed <= 0f)
        {
            return false;
        }

        if (_rb == null)
        {
            _rb = GetComponent<Rigidbody>();
        }

        Vector3 velocity = _rb.velocity;
        if (velocity.y >= JumpSpeed)
        {
            return false;
        }

        velocity.y = JumpSpeed;
        _rb.velocity = velocity;
        groundedTimer = 0f;
        _isGrounded = false;
        return true;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ground"))
        {
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Vector3.Dot(collision.GetContact(i).normal, Vector3.up) >= GroundedNormalThreshold)
            {
                groundedTimer = 0.1f;
                return;
            }
        }
    }
}
