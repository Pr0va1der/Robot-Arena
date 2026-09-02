using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement Settings")]
    public float Speed = 10f;
    public float BrakeForce = 8f;
    public float JumpForce = 150f;

    [Header("Camera")]
    public Transform cameraTransform;

    private bool _isGrounded;
    private float groundedTimer = 0f;
    private Rigidbody _rb;

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
        if (PauseMenu.GameIsPaused || PauseMenu.PointerLockGestureConsumed)
        {
            return;
        }

        groundedTimer -= Time.fixedDeltaTime;
        _isGrounded = groundedTimer > 0f;

        MovementLogic();
        JumpLogic();
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

    private void JumpLogic()
    {
        if (GameplayInputActions.Current.JumpHeld && _isGrounded)
        {
            _rb.AddForce(Vector3.up * JumpForce);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            groundedTimer = 0.1f;
    }
}
