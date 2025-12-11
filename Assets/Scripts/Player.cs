using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.6f;
    public float rotationSpeed = 10f;

    [Tooltip("Add a yaw offset (degrees) if the model's forward axis doesn't match Transform.forward.")]
    public float yawOffset = 0f;

    [Header("Jump / Gravity")]
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("References")]
    public Transform cameraTransform;

    private CharacterController _controller;
    private Vector2 _moveInput;
    private bool _sprintPressed;
    private float _verticalVelocity;

    private void Awake()
    {
        Cursor.visible = false;
        _controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        // Build movement vector relative to camera
        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        Vector3 move = Vector3.zero;
        if (inputDir.sqrMagnitude > 0f)
        {
            Vector3 camForward = cameraTransform ? cameraTransform.forward : Vector3.forward;
            Vector3 camRight = cameraTransform ? cameraTransform.right : Vector3.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            move = camForward * inputDir.z + camRight * inputDir.x;
            move.Normalize();
        }

        float speed = moveSpeed * (_sprintPressed ? sprintMultiplier : 1f);
        Vector3 horizontalVelocity = move * speed;

        // Gravity
        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f; // small downward force to keep grounded

        _verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = horizontalVelocity + Vector3.up * _verticalVelocity;

        _controller.Move(velocity * Time.deltaTime);

        // Rotate to face movement
        if (move.sqrMagnitude > 0.001f)
        {
            // Flatten the move vector and compute a yaw angle (degrees).
            Vector3 flatMove = Vector3.ProjectOnPlane(move, Vector3.up);
            if (flatMove.sqrMagnitude > 0.0001f)
            {
                // Note: Atan2(x, z) used so 0 degrees = forward (0,0,1).
                float targetAngle = Mathf.Atan2(flatMove.x, flatMove.z) * Mathf.Rad2Deg;
                targetAngle += yawOffset;
                Quaternion targetRot = Quaternion.Euler(0f, targetAngle, 0f);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }



    public void OnMove(InputValue value)
    {
        Vector2 v = value.Get<Vector2>();
        _moveInput = v;
    }

    // Support for direct subscription or using generated callbacks
    public void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    public void OnSprint(InputValue value)
    {
        _sprintPressed = value.isPressed;
    }

    public void OnSprint(InputAction.CallbackContext ctx)
    {
        _sprintPressed = ctx.ReadValueAsButton();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
            TryJump();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            TryJump();
    }

    private void TryJump()
    {
        if (_controller.isGrounded)
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
}
