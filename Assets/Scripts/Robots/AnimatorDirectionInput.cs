using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
#endif

namespace CowBoya.Robots
{
    /// <summary>
    /// Updates animator parameters from player movement input, supporting both input backends.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimatorDirectionInput : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string directionParameter = "Direction";
        [SerializeField] private string walkBoolParameter = "IsWalking";
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string verticalDirectionParameter = "VerticalDirection";
        [SerializeField] private string jumpBoolParameter = "IsJumping";
        [SerializeField] private string crouchBoolParameter = "IsCrouching";
#if ENABLE_LEGACY_INPUT_MANAGER
        [SerializeField] private bool useRawLegacyAxis = true;
#endif
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private InputActionReference horizontalAction;
        [SerializeField] private InputActionReference verticalAction;
        private InputAction cachedHorizontalAction;
        private InputAction cachedVerticalAction;
        private enum AxisComponent
        {
            X,
            Y,
            Z
        }
#endif

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            TryInitializeActions();
            cachedHorizontalAction?.Enable();
            cachedVerticalAction?.Enable();
#endif
            ResetAnimatorMovement();
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            cachedHorizontalAction?.Disable();
            cachedVerticalAction?.Disable();
#endif
            ResetAnimatorMovement();
        }
#if ENABLE_INPUT_SYSTEM

        private void TryInitializeActions()
        {
            cachedHorizontalAction = ResolveAction(horizontalAction);
            cachedVerticalAction = ResolveAction(verticalAction);
        }

        private static InputAction ResolveAction(InputActionReference actionReference)
        {
            if (actionReference == null)
            {
                return null;
            }

            InputAction action = actionReference.action;
            if (action == null && actionReference.asset != null)
            {
                action = actionReference.asset.FindAction(actionReference.name, throwIfNotFound: false);
            }

            return action;
        }
#endif

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            int horizontalDirection = GetHorizontalDigitalDirection();
            int verticalDirection = GetVerticalDigitalDirection();
            bool isWalking = horizontalDirection != 0;

            ApplyHorizontalAnimatorValues(horizontalDirection, isWalking);
            ApplyVerticalAnimatorValues(verticalDirection);

            if (!string.IsNullOrEmpty(speedParameter))
            {
                animator.SetFloat(speedParameter, isWalking ? 1f : 0f);
            }
        }

        private void ApplyHorizontalAnimatorValues(float direction, bool isWalking)
        {
            if (!string.IsNullOrEmpty(walkBoolParameter))
            {
                animator.SetBool(walkBoolParameter, isWalking);
            }

            if (!string.IsNullOrEmpty(directionParameter))
            {
                animator.SetFloat(directionParameter, direction);
            }
        }

        private void ApplyVerticalAnimatorValues(int verticalDirection)
        {
            if (!string.IsNullOrEmpty(verticalDirectionParameter))
            {
                animator.SetFloat(verticalDirectionParameter, verticalDirection);
            }

            if (!string.IsNullOrEmpty(jumpBoolParameter))
            {
                animator.SetBool(jumpBoolParameter, verticalDirection > 0);
            }

            if (!string.IsNullOrEmpty(crouchBoolParameter))
            {
                animator.SetBool(crouchBoolParameter, verticalDirection < 0);
            }
        }

        private void ResetAnimatorMovement()
        {
            ApplyHorizontalAnimatorValues(0f, false);
            ApplyVerticalAnimatorValues(0);

            if (!string.IsNullOrEmpty(speedParameter))
            {
                animator.SetFloat(speedParameter, 0f);
            }
        }

        private int GetHorizontalDigitalDirection()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                bool leftPressed = Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed;
                bool rightPressed = Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed;

                if (leftPressed != rightPressed)
                {
                    return rightPressed ? 1 : -1;
                }
            }

            if (cachedHorizontalAction == null && horizontalAction != null)
            {
                TryInitializeActions();
                cachedHorizontalAction?.Enable();
            }

            if (cachedHorizontalAction != null)
            {
                if (cachedHorizontalAction.phase == InputActionPhase.Waiting || cachedHorizontalAction.activeControl == null)
                {
                    return 0;
                }

                if (TryReadActionValue(cachedHorizontalAction, AxisComponent.X, out float actionValue))
                {
                    if (Mathf.Abs(actionValue) > 0.5f)
                    {
                        return actionValue > 0f ? 1 : -1;
                    }
                }
            }
#endif

#if ENABLE_INPUT_SYSTEM
            ReadOnlyArray<Gamepad> gamepads = Gamepad.all;
            if (gamepads.Count > 0)
            {
                for (int i = 0; i < gamepads.Count; i++)
                {
                    Gamepad pad = gamepads[i];
                    if (pad == null)
                    {
                        continue;
                    }

                    float stick = pad.leftStick.ReadValue().x;
                    if (Mathf.Abs(stick) > 0.5f)
                    {
                        return stick > 0f ? 1 : -1;
                    }

                    float dpad = pad.dpad.ReadValue().x;
                    if (Mathf.Abs(dpad) > 0.5f)
                    {
                        return dpad > 0f ? 1 : -1;
                    }
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            bool legacyLeft = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
            bool legacyRight = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);

            if (legacyLeft != legacyRight)
            {
                return legacyRight ? 1 : -1;
            }

            float horizontal = useRawLegacyAxis ? Input.GetAxisRaw("Horizontal") : Input.GetAxis("Horizontal");
            if (Mathf.Abs(horizontal) > 0.5f)
            {
                return horizontal > 0f ? 1 : -1;
            }
#endif

            return 0;
        }

        private int GetVerticalDigitalDirection()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                bool upPressed = Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed || Keyboard.current.spaceKey.isPressed;
                bool downPressed = Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed || Keyboard.current.leftCtrlKey.isPressed;

                if (upPressed != downPressed)
                {
                    return upPressed ? 1 : -1;
                }
            }

            if (cachedVerticalAction == null && verticalAction != null)
            {
                TryInitializeActions();
                cachedVerticalAction?.Enable();
            }

            if (cachedVerticalAction != null)
            {
                if (cachedVerticalAction.phase == InputActionPhase.Waiting || cachedVerticalAction.activeControl == null)
                {
                    return 0;
                }

                if (TryReadActionValue(cachedVerticalAction, AxisComponent.Y, out float verticalValue))
                {
                    if (Mathf.Abs(verticalValue) > 0.5f)
                    {
                        return verticalValue > 0f ? 1 : -1;
                    }
                }
            }
#endif

#if ENABLE_INPUT_SYSTEM
            ReadOnlyArray<Gamepad> gamepads = Gamepad.all;
            if (gamepads.Count > 0)
            {
                for (int i = 0; i < gamepads.Count; i++)
                {
                    Gamepad pad = gamepads[i];
                    if (pad == null)
                    {
                        continue;
                    }

                    float stick = pad.leftStick.ReadValue().y;
                    if (Mathf.Abs(stick) > 0.5f)
                    {
                        return stick > 0f ? 1 : -1;
                    }

                    float dpad = pad.dpad.ReadValue().y;
                    if (Mathf.Abs(dpad) > 0.5f)
                    {
                        return dpad > 0f ? 1 : -1;
                    }
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            bool legacyUp = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Space);
            bool legacyDown = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.LeftControl);

            if (legacyUp != legacyDown)
            {
                return legacyUp ? 1 : -1;
            }

            float vertical = useRawLegacyAxis ? Input.GetAxisRaw("Vertical") : Input.GetAxis("Vertical");
            if (Mathf.Abs(vertical) > 0.5f)
            {
                return vertical > 0f ? 1 : -1;
            }
#endif

            return 0;
        }

#if ENABLE_INPUT_SYSTEM
        private bool TryReadActionValue(InputAction action, AxisComponent axis, out float value)
        {
            value = 0f;
            if (action == null)
            {
                return false;
            }

            try
            {
                value = action.ReadValue<float>();
                return true;
            }
            catch (InvalidOperationException)
            {
                // Ignore and try other paths.
            }

            try
            {
                Vector2 vectorValue = action.ReadValue<Vector2>();
                value = GetComponent(vectorValue, axis);
                return true;
            }
            catch (InvalidOperationException)
            {
                // Ignore and try object conversion.
            }

            try
            {
                Vector3 vector3Value = action.ReadValue<Vector3>();
                value = GetComponent(vector3Value, axis);
                return true;
            }
            catch (InvalidOperationException)
            {
                // Ignore and try raw conversion.
            }

            object rawValue = action.ReadValueAsObject();
            switch (rawValue)
            {
                case float floatValue:
                    value = floatValue;
                    return true;
                case Vector2 vector2Value:
                    value = GetComponent(vector2Value, axis);
                    return true;
                case Vector3 vector3Value:
                    value = GetComponent(vector3Value, axis);
                    return true;
                default:
                    if (rawValue != null && float.TryParse(rawValue.ToString(), out float parsed))
                    {
                        value = parsed;
                        return true;
                    }

                    return false;
            }
        }

        private static float GetComponent(Vector2 value, AxisComponent axis)
        {
            return axis == AxisComponent.Y ? value.y : value.x;
        }

        private static float GetComponent(Vector3 value, AxisComponent axis)
        {
            switch (axis)
            {
                case AxisComponent.Y:
                    return value.y;
                case AxisComponent.Z:
                    return value.z;
                default:
                    return value.x;
            }
        }
#endif
    }
}
