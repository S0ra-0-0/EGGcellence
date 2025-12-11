using System.Collections;
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

    [Header("Attack")]
    public float attackDamage = 10f;
    public float attackRange = 2f;
    public float attackRate = 1f; // cooldown (seconds) between new combos
    [Tooltip("Optional Animator. Triggers: Attack1, Attack2, Attack3")]
    public Animator attackAnimator;
    [Tooltip("Number of attacks in the combo")]
    public int comboMax = 3;
    [Tooltip("Seconds after last attack to break the combo")]
    public float comboTimeout = 0.6f;
    [Tooltip("Delay from animation start to when the hit is applied")]
    public float hitDelay = 0.15f;
    [Tooltip("Estimated total animation duration (used to wait before allowing reset)")]
    public float attackAnimDuration = 0.45f;
    [Tooltip("Layers that can be hit by attacks")]
    public LayerMask attackLayer = ~0;

    [Header("References")]
    public Transform cameraTransform;

    [Header("Interaction")]
    [Tooltip("Maximum distance to interactable objects.")]
    public float interactRange = 3f;
    [Tooltip("Layers that contain interactable objects.")]
    public LayerMask interactLayer = ~0;
    private Interactable _focusedInteractable;

    [Header("Interaction Debug")]
    [Tooltip("Draw the interaction area and hit info in Scene view / Game view (Gizmos must be enabled).")]
    public bool visualizeInteractionRay = true;
    public Color debugRayHitColor = Color.green;
    public Color debugRayMissColor = Color.red;
    public Color debugFocusedColor = Color.yellow;
    public float debugRayDuration = 0f; // 0 means draw for a single frame

    // cached last overlap result for gizmos
    private Collider[] _lastOverlaps;
    private bool _lastOverlapHadHit;

    private CharacterController _controller;
    private Vector2 _moveInput;
    private bool _sprintPressed;
    private bool _interactPressed;

    private float _verticalVelocity;

    // Programmatic subscription
    private PlayerInput _playerInput;
    private InputAction _interactAction;
    private InputAction _attackAction;

    // Combo state
    private int _comboIndex = 0; // 0..comboMax-1
    private float _lastAttackTime = -999f; // time when last attack started
    private float _lastComboTime = -999f;  // time when last combo/attack happened

    private void Awake()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // try to find PlayerInput on this object or parent (supports different setups)
        _playerInput = GetComponent<PlayerInput>() ?? GetComponentInParent<PlayerInput>();
    }

    private void OnEnable()
    {
        if (_playerInput == null)
            _playerInput = GetComponent<PlayerInput>() ?? GetComponentInParent<PlayerInput>();

        if (_playerInput != null)
        {
            // find action named exactly "Interact" in the action asset
            _interactAction = _playerInput.actions.FindAction("Interact", true);
            if (_interactAction != null)
                _interactAction.performed += OnInteractAction;
            else
                Debug.LogWarning("PlayerInput found but action named 'Interact' not present in Actions asset.");

            // optional: find action named "Attack"
            _attack_action_setup();
        }
    }

    private void _attack_action_setup()
    {
        _attackAction = _playerInput.actions.FindAction("Attack", false);
        if (_attackAction != null)
        {
            _attackAction.performed += OnAttackAction;
        }
        // It's OK if Attack is not found; OnAttack(InputValue) public method still supports SendMessage wiring or old input.
    }

    private void OnDisable()
    {
        if (_interactAction != null)
            _interactAction.performed -= OnInteractAction;
        _interactAction = null;

        if (_attackAction != null)
            _attackAction.performed -= OnAttackAction;
        _attackAction = null;
    }

    private void Update()
    {
        HandleMovement();
        UpdateFocusedInteractable();

        // Optional: automatically reset combo if timed out
        if (Time.time - _lastComboTime > comboTimeout)
            _comboIndex = 0;
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
            Vector3 flatMove = Vector3.ProjectOnPlane(move, Vector3.up);
            if (flatMove.sqrMagnitude > 0.0001f)
            {
                float targetAngle = Mathf.Atan2(flatMove.x, flatMove.z) * Mathf.Rad2Deg;
                targetAngle += yawOffset;
                Quaternion targetRot = Quaternion.Euler(0f, targetAngle, 0f);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }

    private void UpdateFocusedInteractable()
    {
        // Use a sphere around the player (centered slightly above feet) to find nearby interactables.
        Vector3 origin = transform.position + Vector3.up * 1.0f;

        _lastOverlaps = Physics.OverlapSphere(origin, interactRange, interactLayer);
        _lastOverlapHadHit = _lastOverlaps != null && _lastOverlaps.Length > 0;

        // Find the nearest Interactable among overlaps (if any)
        Interactable nearest = null;
        float nearestDistSq = float.MaxValue;

        if (_lastOverlapHadHit)
        {
            for (int i = 0; i < _lastOverlaps.Length; i++)
            {
                Collider c = _lastOverlaps[i];
                if (c == null)
                    continue;

                Interactable it = c.GetComponentInParent<Interactable>();
                if (it == null)
                    continue;

                float dSq = (it.transform.position - origin).sqrMagnitude;
                if (dSq < nearestDistSq)
                {
                    nearestDistSq = dSq;
                    nearest = it;
                }
            }
        }

        if (nearest != _focusedInteractable)
        {
            if (_focusedInteractable != null)
                _focusedInteractable.OnDefocus();

            _focusedInteractable = nearest;

            if (_focusedInteractable != null)
                _focusedInteractable.OnFocus();
        }

        // Debug draw the sphere in Scene/Game view
        if (visualizeInteractionRay)
        {
            Color sphereColor = _lastOverlapHadHit ? debugRayHitColor : debugRayMissColor;
            DebugExtensions.DrawWireSphere(origin, interactRange, sphereColor, debugRayDuration);
        }
    }

    private void TryInteract()
    {
        Debug.Log("Player trying to interact");
        if (_focusedInteractable != null)
        {
            Debug.Log("Interacting with focused interactable");
            _focusedInteractable.Interact(this);
            return;
        }

        // Fallback: check sphere now and interact with nearest if found
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Collider[] overlaps = Physics.OverlapSphere(origin, interactRange, interactLayer);
        Interactable nearest = null;
        float nearestDistSq = float.MaxValue;

        if (overlaps != null && overlaps.Length > 0)
        {
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider c = overlaps[i];
                if (c == null)
                    continue;

                Interactable it = c.GetComponentInParent<Interactable>();
                if (it == null)
                    continue;

                float dSq = (it.transform.position - origin).sqrMagnitude;
                if (dSq < nearestDistSq)
                {
                    nearestDistSq = dSq;
                    nearest = it;
                }
            }

            if (nearest != null)
                nearest.Interact(this);
        }
    }

    private void OnInteractAction(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            TryInteract();
    }

    // --- Input System callbacks (retain these for Send Messages / Invoke Events wiring) ---
    public void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();
    }

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

    public void OnInteract(InputValue value)
    {
        Debug.Log("OnInteract via InputValue");
        _interactPressed = value.isPressed;
        if (_interactPressed)
            TryInteract();
    }

    public void OnInteract(InputAction.CallbackContext ctx)
    {
        Debug.Log("OnInteract via ctx");

        _interactPressed = ctx.ReadValueAsButton();
        if (ctx.performed)
            TryInteract();
    }

    // --- Attack input handlers (supports InputValue, CallbackContext and PlayerInput action named "Attack") ---
    public void OnAttack(InputValue value)
    {
        if (value.isPressed)
            HandleAttackInput();
    }

    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            HandleAttackInput();
    }

    private void OnAttackAction(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            HandleAttackInput();
    }

    private void HandleAttackInput()
    {
        // If last attack happened long enough ago, start a new combo
        if (Time.time - _lastComboTime > comboTimeout)
        {
            // enforce cooldown between new combos
            if (Time.time - _lastAttackTime < attackRate)
                return;

            _comboIndex = 0;
            StartCoroutine(AttackRoutine(_comboIndex));
        }
        else
        {
            // within combo window, advance if possible
            if (_comboIndex < comboMax - 1)
            {
                _comboIndex++;
                StartCoroutine(AttackRoutine(_comboIndex));
            }
        }

        _lastAttackTime = Time.time;
        _lastComboTime = Time.time;
    }

    private IEnumerator AttackRoutine(int attackIndex)
    {
        // trigger animation if available
        if (attackAnimator != null)
        {
            string triggerName = $"Attack{attackIndex + 1}";
            attackAnimator.SetTrigger(triggerName);
        }

        // wait for hit timing
        if (hitDelay > 0f)
            yield return new WaitForSeconds(hitDelay);

        // perform hit: sphere in front of player
        Vector3 origin = transform.position + Vector3.up * 1.0f + transform.forward * (attackRange * 0.5f);
        Collider[] hits = Physics.OverlapSphere(origin, attackRange, attackLayer);
        if (hits != null && hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i];
                if (c == null)
                    continue;

                // Try common damage receiver patterns: SendMessage so it's non-breaking if component missing.
                c.gameObject.SendMessage("ApplyDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
                Interactable it = c.GetComponentInParent<Interactable>();
                if (it != null)
                {
                    it.Interact(this);
                }
            }
        }

        // wait remaining animation time before allowing next inputs to be spaced visually
        float remaining = Mathf.Max(0f, attackAnimDuration - hitDelay);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // if finished combo (last attack), reset combo index so next press starts new combo after timeout
        if (attackIndex >= comboMax - 1)
        {
            _comboIndex = 0;
            _lastComboTime = -999f;
        }
        else
        {
            // keep comboIndex as-is so next input increments it
            _lastComboTime = Time.time;
        }
    }

    // Draw extra visual helpers in Scene view (and Game view when Gizmos enabled)
    private void OnDrawGizmos()
    {
        if (!visualizeInteractionRay)
            return;

        Vector3 origin = (transform != null) ? transform.position + Vector3.up * 1.0f : Vector3.zero;

        // sphere area
        Gizmos.color = _lastOverlapHadHit ? debugRayHitColor : debugRayMissColor;
        Gizmos.DrawWireSphere(origin, interactRange);

        // mark overlapped colliders centers
        if (_lastOverlaps != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < _lastOverlaps.Length; i++)
            {
                Collider c = _lastOverlaps[i];
                if (c == null)
                    continue;
                Gizmos.DrawSphere(c.bounds.center, 0.05f);
            }
        }

        // highlight focused interactable
        if (_focusedInteractable != null)
        {
            Gizmos.color = debugFocusedColor;
            Gizmos.DrawWireSphere(_focusedInteractable.transform.position, 0.25f);
        }

        // draw attack range preview in front of player
        Gizmos.color = Color.magenta;
        Vector3 aOrigin = (transform != null) ? transform.position + Vector3.up * 1.0f + transform.forward * (attackRange * 0.5f) : Vector3.zero;
        Gizmos.DrawWireSphere(aOrigin, attackRange);
    }
}

static class DebugExtensions
{
    public static void DrawWireSphere(Vector3 center, float radius, Color color, float duration = 0f)
    {
        // draw a few circle segments in world space using Debug.DrawLine for a quick runtime visualization
        int segments = 24;
        Vector3 lastPoint = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Debug.DrawLine(lastPoint, nextPoint, color, duration);
            lastPoint = nextPoint;
        }
        // vertical circle
        lastPoint = center + new Vector3(0f, radius, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(0f, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            Debug.DrawLine(lastPoint, nextPoint, color, duration);
            lastPoint = nextPoint;
        }
    }
}