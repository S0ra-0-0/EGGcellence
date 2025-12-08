using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCam : MonoBehaviour
{
    public Transform orientation;
    public Transform player;
    public Transform playerObj;
    public Rigidbody rb;

    public float rotationSpeed;

    private Vector2 _moveInput;
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // rotate orientation
        Vector3 viewDir = player.position - new Vector3(transform.position.x, player.position.y, transform.position.z);
        orientation.forward = viewDir.normalized;

        // rotate player object

        //if (inputDir != Vector3.zero)
          //  playerObj.forward = Vector3.Slerp(playerObj.forward, inputDir.normalized, Time.deltaTime * rotationSpeed);

    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
            _moveInput = ctx.ReadValue<Vector2>();
       }

    private Vector2 _moveInput;
    private void HandleMovement()
    {
        Vector3 inputDir = new Vector3(_moveInput.x, 0, _moveInput.y);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        if (inputDir != Vector3.zero)
        {
            Vector3 camForward = _cameraTransform.forward;
            Vector3 camRight = _cameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 move = camForward * inputDir.z + camRight * inputDir.x;
            _controller.Move(move * PlayerSpeed * Time.deltaTime);

            _animator.SetFloat("Speed", 4f);

            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
        else
        {
            _animator.SetFloat("Speed", 2f);
        }
    }
}
