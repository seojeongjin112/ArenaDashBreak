using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaBreak.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        private const string ActionMapName = "Player";
        private const string MoveActionName = "Move";
        private const string LookActionName = "Look";
        private const string JumpActionName = "Jump";

        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Camera")]
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private float _lookSensitivity = 0.1f;
        [SerializeField] private float _maxPitch = 80f;
        // 커서 잠금 첫 프레임에 delta가 크게 튀므로 프레임당 상한을 둔다
        [SerializeField] private float _maxLookDeltaPerFrame = 50f;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _jumpHeight = 1.5f;
        [SerializeField] private float _gravity = -20f;
        // 0으로 두면 isGrounded가 프레임마다 흔들리므로 바닥에 살짝 눌러 붙인다
        [SerializeField] private float _groundedStickVelocity = -2f;

        [Header("Cursor")]
        [SerializeField] private bool _lockCursor = true;

        private CharacterController _controller;
        private InputActionMap _actionMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;

        private float _pitch;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            _actionMap = _inputActions.FindActionMap(ActionMapName, true);
            _moveAction = _actionMap.FindAction(MoveActionName, true);
            _lookAction = _actionMap.FindAction(LookActionName, true);
            _jumpAction = _actionMap.FindAction(JumpActionName, true);
        }

        private void OnEnable()
        {
            _actionMap.Enable();

            if (_lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            _actionMap.Disable();

            if (_lockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            HandleLook();
            HandleMovement();
        }

        private void HandleLook()
        {
            Vector2 look = Vector2.ClampMagnitude(_lookAction.ReadValue<Vector2>(), _maxLookDeltaPerFrame);

            transform.Rotate(0f, look.x * _lookSensitivity, 0f);

            _pitch = Mathf.Clamp(_pitch - look.y * _lookSensitivity, -_maxPitch, _maxPitch);
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            Vector2 input = Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f);
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * _moveSpeed;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = _groundedStickVelocity;
            }

            if (_controller.isGrounded && _jumpAction.WasPressedThisFrame())
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }

            _verticalVelocity += _gravity * Time.deltaTime;

            Vector3 velocity = horizontal + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
